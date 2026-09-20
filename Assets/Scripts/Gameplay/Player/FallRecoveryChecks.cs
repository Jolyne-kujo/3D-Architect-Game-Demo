#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    public static class FallRecoveryChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Fall recovery checks require Play mode for an isolated local physics scene. Enter Play mode, then call CoastalTemple.Player.FallRecoveryChecks.Run().");
            var report = new List<string>();
            GameObject root = null;
            Scene scene = default;
            void Check(bool passed, string label)
            {
                report.Add((passed ? "PASS " : "FAIL ") + label);
                if (!passed) throw new InvalidOperationException(string.Join("\n", report));
            }
            try
            {
                scene = SceneManager.CreateScene("TemporaryFallRecoveryChecks", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                root = new GameObject("TemporaryFallRecoveryChecks"); root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = new Vector3(14000, 500, 14000);
                GameObject Child(string name, Vector3 local)
                {
                    var child = new GameObject(name); child.transform.SetParent(root.transform, false); child.transform.localPosition = local; return child;
                }
                var player = Child("Player", Vector3.up * 3);
                player.transform.rotation = Quaternion.Euler(0, 23, 0);
                var controller = player.AddComponent<CharacterController>();
                controller.height = 1.8f; controller.center = Vector3.up * .9f; controller.radius = .28f; controller.stepOffset = .31f;
                var walker = player.AddComponent<CourtyardWalker>(); walker.active = true;
                var eye = new GameObject("Eye"); eye.transform.SetParent(player.transform, false); eye.transform.localPosition = Vector3.up * 1.5f; walker.eye = eye.transform;
                var safe = Child("SectionEntrance", new Vector3(30, 3, 0)).transform; safe.rotation = Quaternion.Euler(0, 137, 0);
                var regionObject = Child("RotatedFallRegion", new Vector3(20, -4, 8));
                regionObject.transform.localRotation = Quaternion.Euler(0, 90, 0); regionObject.transform.localScale = new Vector3(2, 1, .5f);
                var region = regionObject.AddComponent<FallRecoveryRegion>(); region.walker = walker; region.safePoint = safe;
                region.center = new Vector3(1, -2, 3); region.size = new Vector3(8, 4, 2);
                var formerPlatform = Child("FormerMovingSupport", Vector3.left * 20).AddComponent<Rigidbody>(); formerPlatform.isKinematic = true;
                root.SetActive(true);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                Check(walker.Controller == controller,
                    "Play-mode fixture runs the real walker Awake before movement");
                Vector3 originalSpawn = player.transform.position;
                walker.ApplyLook(new Vector2(37, 15)); walker.SimulateMovement(Vector2.right, true, false, 0, .04f);
                Check(walker.VerticalSpeed < 0 && walker.PlanarVelocity.magnitude > .1f && walker.LookPitch != 0,
                    "real walker establishes motion and looking state before recovery");
                // Inject retained contact/swim state so reset is checked even without a whole water scene.
                typeof(CourtyardWalker).GetField("groundBody", flags).SetValue(walker, formerPlatform);
                typeof(CourtyardWalker).GetField("groundPosition", flags).SetValue(walker, formerPlatform.position);
                typeof(CourtyardWalker).GetField("<Swimming>k__BackingField", flags).SetValue(walker, true);
                controller.stepOffset = 0;
                walker.RespawnAt(safe.position, 137);
                Check(Vector3.Distance(player.transform.position, safe.position) < .001f && Mathf.Abs(Mathf.DeltaAngle(walker.LookYaw, 137)) < .001f,
                    "explicit respawn uses the requested safe point and yaw");
                Check(walker.VerticalSpeed == 0 && walker.PlanarVelocity == Vector3.zero && !walker.Swimming
                    && walker.LookPitch == 0 && Quaternion.Angle(eye.transform.localRotation, Quaternion.identity) < .001f,
                    "respawn clears vertical, planar, swimming and look pitch state");
                Check(typeof(CourtyardWalker).GetField("groundBody", flags).GetValue(walker) == null
                    && (Vector3)typeof(CourtyardWalker).GetField("groundPosition", flags).GetValue(walker) == Vector3.zero
                    && controller.enabled && Mathf.Abs(controller.stepOffset - .31f) < .001f,
                    "respawn removes former support attachment and restores normal controller stepping");
                Vector3 fallPosition = region.transform.TransformPoint(region.center + new Vector3(3.8f, 0, .8f));
                walker.RespawnAt(fallPosition, 0);
                region.enabled = false;
                Check(!region.EvaluateNow() && region.RecoveryCount == 0 && player.transform.position == fallPosition,
                    "disabled region neither moves the player nor consumes a recovery");
                region.enabled = true; walker.enabled = false;
                Check(!region.EvaluateNow(), "disabled walker cannot be recovered by the region");
                walker.enabled = true; controller.enabled = false;
                Check(!region.EvaluateNow(), "disabled character controller cannot be recovered by the region");
                controller.enabled = true;
                Check(region.EvaluateNow() && region.RecoveryCount == 1 && Vector3.Distance(player.transform.position, safe.position) < .001f,
                    "rotated and scaled local fall box recovers to the authored section entrance");
                Check(!region.EvaluateNow() && region.RecoveryCount == 1, "safe point outside the region does not repeatedly recover");
                walker.RespawnAt(region.transform.TransformPoint(region.center + Vector3.forward * 1.2f), 0);
                Check(!region.EvaluateNow() && region.RecoveryCount == 1, "outside the rotated narrow side does not trigger recovery");
                walker.RespawnAt(fallPosition, 0);
                Check(region.EvaluateNow() && region.RecoveryCount == 2, "a later fall reuses the same region independently");
                region.EvaluateNow(); // Observe the safe point outside and rearm the entry policy.
                safe.position = region.transform.TransformPoint(region.center);
                walker.RespawnAt(fallPosition, 0);
                Check(region.EvaluateNow() && region.RecoveryCount == 3, "even a mistaken destination inside the region has only one recovery per entry");
                bool stable = true;
                for (int i = 0; i < 10; i++) stable &= !region.EvaluateNow();
                Check(stable && region.RecoveryCount == 3 && player.transform.position == safe.position,
                    "remaining inside a mistaken safe region cannot cause per-frame teleport jitter");
                walker.ResetPosition();
                Check(Vector3.Distance(player.transform.position, originalSpawn) < .001f && Mathf.Abs(Mathf.DeltaAngle(walker.LookYaw, 23)) < .001f,
                    "ResetPosition retains the original Awake spawn and yaw after multiple local recoveries");
                return string.Join("\n", report) + "\n" + report.Count + " fall recovery component checks passed in an isolated scene.";
            }
            finally
            {
                if (root) UnityEngine.Object.DestroyImmediate(root);
                // The temporary objects are removed now; the empty runtime scene unloads on the next player-loop update.
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
