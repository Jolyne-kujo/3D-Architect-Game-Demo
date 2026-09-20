#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastalTemple.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    /// <summary>Exercises the imported bot through the real movement and animation components.</summary>
    [AddComponentMenu("")]
    public sealed class RedBotPlayChecks : MonoBehaviour
    {
        const string Output = "Documentation/RedBotReplacement";
        const string SourcePath = "Assets/Animations/Character/X Bot@Slow Run.fbx";
        readonly List<string> lines = new List<string>();
        CoastalWalkthrough tour;
        CourtyardWalker walker;
        CoastalPlayerCamera rig;
        RiggedPlayerAnimation body;
        PlayerLantern lantern;
        GameObject floor;
        Camera observer;
        bool expectLift, cleaned, previousEnabled, previousPerspective, previousLight;

        public static string Begin(bool expectLift = false)
        {
            if (!Application.isPlaying) throw new System.InvalidOperationException("Enter Play first.");
            if (FindFirstObjectByType<RedBotPlayChecks>()) return "Red Bot checks are already running.";
            Directory.CreateDirectory(Output);
            var check = new GameObject("TemporaryRedBotPlayChecks").AddComponent<RedBotPlayChecks>();
            check.expectLift = expectLift;
            return "Red Bot idle, locomotion, jump, orbit, swimming and optional carrying checks running.";
        }

        void Check(bool valid, string label)
        {
            lines.Add((valid ? "PASS " : "FAIL ") + label);
            File.WriteAllLines(Output + "/PlayChecks.txt", lines);
        }

        bool Plays(string clipName, int layer = 0) => body.animator.GetCurrentAnimatorClipInfo(layer)
            .Any(c => c.clip && c.clip.name == clipName && c.weight > .95f);

        IEnumerator Move(float duration, Vector2 input, bool fast = false, float dive = 0)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                // Match full gameplay frames: a fractional final microstep can erase
                // CharacterController.isGrounded even though the actor is on the floor.
                float step = Mathf.Min(Time.deltaTime, .05f);
                walker.SimulateMovement(input, fast, false, dive, step);
                elapsed += step;
                yield return null;
            }
        }

        static Color BaseColor(Material material)
        {
            if (!material) return Color.clear;
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            return material.HasProperty("_Color") ? material.GetColor("_Color") : Color.clear;
        }

        void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Output + "/" + name + "-Game.png");
            var target = RenderTexture.GetTemporary(1280, 960, 24);
            var texture = new Texture2D(1280, 960, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                observer.transform.position = walker.transform.position + walker.transform.forward * 4.2f
                    + walker.transform.right * 2.6f + Vector3.up * 2.2f;
                observer.transform.LookAt(walker.transform.position + Vector3.up * .95f);
                observer.targetTexture = target;
                observer.aspect = 4f / 3;
                observer.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 960), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Output + "/" + name + "-Front.png", texture.EncodeToPNG());
            }
            finally
            {
                observer.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                Destroy(texture);
            }
        }

        IEnumerator Start()
        {
            tour = FindFirstObjectByType<CoastalWalkthrough>();
            if (!tour || !tour.walker || !tour.playerCamera)
            {
                Check(false, "active scene supplies the coastal player and camera");
                Destroy(gameObject);
                yield break;
            }
            walker = tour.walker;
            rig = tour.playerCamera;
            previousEnabled = walker.enabled;
            previousPerspective = rig.thirdPerson;
            lantern = walker.GetComponent<PlayerLantern>();
            previousLight = lantern && lantern.LanternOn;
            body = walker.GetComponentInChildren<RiggedPlayerAnimation>();
            if (!body || !body.animator)
            {
                Check(false, "player has its skeletal animation driver and Animator");
                Destroy(gameObject);
                yield break;
            }
            tour.SetOverview(false);
            walker.enabled = false;
            rig.SetThirdPerson(true);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "TemporaryRedBotFloor";
            floor.transform.position = new Vector3(-65, 149.9f, 204);
            floor.transform.localScale = new Vector3(80, .2f, 80);
            observer = new GameObject("TemporaryRedBotObserver").AddComponent<Camera>();
            observer.CopyFrom(tour.view);
            observer.enabled = false;
            observer.fieldOfView = 38;
            observer.nearClipPlane = .05f;
            observer.farClipPlane = 160;
            Physics.SyncTransforms();
            walker.RespawnAt(new Vector3(-65, 150.03f, 204), 0);
            rig.SnapToTarget();
            yield return Move(.9f, Vector2.zero);

            var renderers = body.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePath);
            var sourceMeshes = source ? source.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(r => r.sharedMesh).ToArray() : new Mesh[0];
            Check(renderers.Length == 2 && sourceMeshes.Length == 2
                && renderers.All(r => r.sharedMesh && sourceMeshes.Contains(r.sharedMesh)),
                "world body uses both meshes from the supplied skinned Slow Run FBX");
            Check(renderers.All(r => r.enabled && r.shadowCastingMode == ShadowCastingMode.On
                && r.sharedMaterials.Length > 0 && r.sharedMaterials.All(m => m && m.shader && m.shader.isSupported)),
                "both body parts have supported materials and visible world shadows");
            Check(renderers.SelectMany(r => r.sharedMaterials).Any(m =>
            {
                Color color = BaseColor(m);
                return color.r > .2f && color.r > color.g * 1.4f && color.r > color.b * 1.4f;
            }), "bot uses a visibly red material tint");
            Check(renderers.All(r => !AssetDatabase.GetAssetPath(r.sharedMesh).Contains("Quaternius")),
                "world body no longer contains the previous ivory mannequin mesh");
            Check(body.animator.avatar && body.animator.avatar.isValid && body.animator.avatar.isHuman,
                "replacement body has a valid humanoid avatar");
            Check(!body.animator.applyRootMotion, "source root motion cannot displace the movement controller");
            Check(Plays("Idle"), "standing still plays the supplied Idle clip");
            Check(body.animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Idle" && c.clip.isLooping),
                "supplied Idle clip is configured to loop");
            Capture("01-Idle");
            yield return null;

            Vector3 start = walker.transform.position;
            Quaternion heading = walker.transform.rotation;
            for (int i = 0; i < 12; i++)
            {
                walker.ApplyLook(new Vector2(30, i == 0 ? -20 : i == 11 ? 20 : 0));
                yield return null;
            }
            Check(Vector3.Distance(start, walker.transform.position) < .001f,
                "stationary camera orbit does not translate the actor");
            Check(Quaternion.Angle(heading, walker.transform.rotation) < .01f,
                "stationary orbit does not rotate the body or its world shadow");

            foreach (bool fast in new[] { false, true })
            {
                string expected = fast ? "Fast Run" : "Slow Run";
                start = walker.transform.position;
                yield return Move(1.1f, Vector2.up, fast);
                Check(Plays(expected), (fast ? "Shift sprint" : "normal movement") + " plays supplied " + expected);
                Check(Vector3.Distance(start, walker.transform.position) > (fast ? 4 : 2.4f),
                    expected + " accompanies real controller movement");
                var knee = body.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                Quaternion kneeBefore = knee.localRotation;
                float kneeAngle = 0, bodyError = 0;
                for (int i = 0; i < 12; i++)
                {
                    yield return Move(.04f, Vector2.up, fast);
                    kneeAngle = Mathf.Max(kneeAngle, Quaternion.Angle(kneeBefore, knee.localRotation));
                    bodyError = Mathf.Max(bodyError, Vector3.Distance(body.transform.position, walker.transform.position));
                }
                Check(kneeAngle > 10 && bodyError < .001f,
                    expected + " animates legs while body root stays on controller; knee=" + kneeAngle + ", drift=" + bodyError);
                Capture(fast ? "03-FastRun" : "02-SlowRun");
                yield return null;
            }

            yield return Move(.8f, Vector2.zero);
            Check(Plays("Idle"), "stopping returns to the supplied Idle loop");
            walker.SimulateMovement(Vector2.up, false, true, 0, Mathf.Min(Time.deltaTime, .05f));
            yield return Move(.12f, Vector2.zero);
            Check(!walker.Controller.isGrounded && walker.VerticalSpeed > 0,
                "space jump lifts the controller into the ascending airborne phase");
            Check(Plays("Jump"), "airborne state plays the supplied Jump clip");
            Capture("04-Jump");
            yield return Move(1.1f, Vector2.zero);
            Check(walker.Controller.isGrounded && Plays("Idle"), "landing returns to the supplied Idle loop");

            if (expectLift)
            {
                if (!lantern || !lantern.worldHand)
                    Check(false, "optional lifting has a world lantern hand binding");
                else
                {
                    walker.RespawnAt(new Vector3(-65, 150.03f, 204), 0);
                    rig.SnapToTarget();
                    lantern.GiveLantern();
                    lantern.SetLantern(true);
                    yield return Move(.8f, Vector2.zero);
                    int carry = body.animator.GetLayerIndex("Carried Lantern");
                    Check(carry >= 0 && body.animator.GetLayerWeight(carry) > .95f,
                        "picking up the lantern enables the upper-body lifting layer");
                    Check(lantern.carriedLantern && lantern.carriedLantern.activeInHierarchy && lantern.LanternOn
                        && lantern.carriedLantern.transform.parent == lantern.worldHand,
                        "working lantern remains attached to the replacement bot's world hand");
                    bool left = lantern.worldHand == body.animator.GetBoneTransform(HumanBodyBones.LeftHand)
                        || lantern.worldHand.IsChildOf(body.animator.GetBoneTransform(HumanBodyBones.LeftHand));
                    var shoulder = body.animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
                    var knee = body.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
                    float height = Mathf.Max(.5f, body.animator.GetBoneTransform(HumanBodyBones.Head).position.y
                        - walker.transform.position.y);
                    foreach (bool fast in new[] { false, true })
                    {
                        string expected = fast ? "Fast Run" : "Slow Run";
                        yield return Move(.8f, Vector2.up, fast);
                        Quaternion kneeBefore = knee.localRotation;
                        float maxKnee = 0, minHandRelativeHeight = float.PositiveInfinity;
                        for (int i = 0; i < 20; i++)
                        {
                            yield return Move(.06f, Vector2.up, fast);
                            maxKnee = Mathf.Max(maxKnee, Quaternion.Angle(kneeBefore, knee.localRotation));
                            minHandRelativeHeight = Mathf.Min(minHandRelativeHeight,
                                (lantern.worldHand.position.y - shoulder.position.y) / height);
                        }
                        Check(Plays(expected) && maxKnee > 10,
                            expected + " with lifting overlay preserves running legs; knee motion=" + maxKnee);
                        Check(carry >= 0 && body.animator.GetLayerWeight(carry) > .95f,
                            expected + " keeps the lifting overlay fully active");
                        Check(minHandRelativeHeight > -.12f,
                            expected + " keeps carrying hand raised throughout locomotion; minimum relative shoulder height=" + minHandRelativeHeight);
                        Capture(fast ? "05B-FastRunLift" : "05A-SlowRunLift");
                        yield return null;
                    }
                }
            }

            tour.ResetWater();
            walker.RespawnAt(tour.water.transform.position + new Vector3(0, -.8f, 0), 160);
            rig.SnapToTarget();
            yield return Move(3, Vector2.zero);
            walker.SampleWater(walker.transform.position + Vector3.up * .8f, out float surface, out _, out _);
            Check(walker.Swimming && body.animator.GetCurrentAnimatorStateInfo(0).IsName("Swimming"),
                "real courtyard pool still activates the swimming animation on the replacement body");
            Check(Plays("Treading Water"), "stationary swimming plays supplied Treading Water");
            Check(new[] { "Treading Water", "Swimming" }.All(name => AssetDatabase.LoadAllAssetsAtPath("Assets/Animations/Character/X Bot@" + name + ".fbx")
                    .OfType<AnimationClip>().Any(clip => clip.name == name && clip.humanMotion && clip.isLooping)),
                "both supplied water animations loop");
            Vector3 acrossShoulders = body.animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position
                - body.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            float swimFacing = Vector3.Dot(Vector3.Cross(acrossShoulders, Vector3.up).normalized,
                walker.transform.forward);
            Check(swimFacing > .75f,
                "idle swimming keeps the replacement bot facing the controller heading; forward dot=" + swimFacing);
            int swimmingCarry = body.animator.GetLayerIndex("Carried Lantern");
            Check(swimmingCarry < 0 || body.animator.GetLayerWeight(swimmingCarry) < .05f,
                "swimming fades out the lifting overlay so both arms can swim");
            Check(tour.view.transform.position.y > surface + .25f,
                "surface swimming keeps the third-person camera above the water");
            Capture("06-Swimming");
            yield return Move(1.4f, Vector2.up);
            Check(walker.Swimming && Plays("Swimming"), "moving in water plays supplied Swimming");
            Capture("07-ForwardSwimming");
            float feet = walker.transform.position.y;
            yield return Move(.8f, Vector2.zero, false, -1);
            Check(walker.transform.position.y < feet - .3f && rig.thirdPerson,
                "diving still moves the new bot underwater with the third-person camera");
            Cleanup();
            Destroy(gameObject);
        }

        void Cleanup()
        {
            if (cleaned) return;
            cleaned = true;
            if (walker)
            {
                walker.ResetPosition();
                walker.enabled = previousEnabled;
            }
            if (lantern) lantern.SetLantern(previousLight);
            if (rig)
            {
                rig.SetThirdPerson(previousPerspective);
                tour.SetOverview(false);
                rig.SnapToTarget();
            }
            if (floor) Destroy(floor);
            if (observer) Destroy(observer.gameObject);
        }

        void OnDestroy() => Cleanup();
    }
}
#endif
