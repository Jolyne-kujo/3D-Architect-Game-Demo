using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class ReusableAssetChecks
    {

        public static string Run()
        {
            var report = new List<string>();
            void Check(bool ok, string name) { report.Add((ok ? "PASS " : "FAIL ") + name); }
            var avatar = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/QuaterniusMannequin.prefab");
            Check(avatar, "downloaded mannequin prefab exists");
            if (avatar)
            {
                var animator = avatar.GetComponentInChildren<Animator>();
                Check(animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman, "valid Humanoid skeleton and Animator");
                Check(animator && animator.runtimeAnimatorController, "authored animation controller attached");
                Check(avatar.GetComponentsInChildren<SkinnedMeshRenderer>().Any(r => r.bones.Length > 20), "real skinned mesh and imported bones");
                Check(avatar.GetComponentsInChildren<Collider>().Length == 0, "visual model does not obstruct player or camera");
            }
            foreach (string name in new[] { "RedStone_Permanent", "RedStone_WhileLit" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Light/" + name + ".prefab");
                Check(prefab, name + " reusable prefab exists");
                if (!prefab) continue;
                var c = prefab.GetComponent<RedStoneCurtain>();
                Check(c && c.PhysicalCollider && c.OpticalCollider && c.Visuals.Length > 0, name + " owns visuals and optical/physical shape");
                Check(c && c.Visuals.All(r => r && r.transform.IsChildOf(prefab.transform)), name + " has no external visual references");
            }
            var player = new GameObject("TemporaryCameraBindingCheck");
            var cameraObject = new GameObject("TemporaryCamera");
            try
            {
                player.transform.position = Vector3.one * 10000;
                var walker = player.AddComponent<CourtyardWalker>();
                walker.eye = new GameObject("Eye").transform;walker.eye.SetParent(player.transform,false);
                var camera = cameraObject.AddComponent<Camera>();
                var rig = player.AddComponent<CoastalPlayerCamera>(); rig.walker = walker; rig.view = camera;
                rig.SnapToTarget();
                Check(camera.transform.IsChildOf(player.transform), "main camera remains parented to character");
                Vector3 before = camera.transform.position; player.transform.position += Vector3.right * 3;
                Check(Vector3.Distance(camera.transform.position, before + Vector3.right * 3) < .001f, "moving character immediately moves attached camera");
                rig.SetOverview(true); rig.SetOverview(false);
                Check(camera.transform.IsChildOf(player.transform), "returning from overview rebinds camera");
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(player); }
            return string.Join("\n", report);
        }

        public static string RunCurtainPhysics()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play mode.");
            var report = new List<string>();
            void Check(bool ok, string name) { report.Add((ok ? "PASS " : "FAIL ") + name); }
            var root = new GameObject("TemporaryPrefabOpticsChecks");
            try
            {
                foreach (string name in new[] { "RedStone_Permanent", "RedStone_WhileLit" })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Light/" + name + ".prefab");
                    var a = UnityEngine.Object.Instantiate(prefab, root.transform).GetComponent<RedStoneCurtain>();
                    a.transform.SetPositionAndRotation(new Vector3(12000,100,12000), Quaternion.Euler(0,37,0));
                    a.transform.localScale = new Vector3(1.3f,.8f,1.7f);
                    var b = UnityEngine.Object.Instantiate(a.gameObject, root.transform).GetComponent<RedStoneCurtain>();
                    b.transform.position += Vector3.right * 30;
                    Check(a.Visuals.All(r=>r.transform.IsChildOf(a.transform)) && b.Visuals.All(r=>r.transform.IsChildOf(b.transform)), name + " duplication remaps all renderer references");
                    var lamp = new GameObject("ProbeEmitter"); lamp.transform.SetParent(root.transform);
                    lamp.transform.SetPositionAndRotation(a.transform.TransformPoint(new Vector3(0,1.5f,-5)), a.transform.rotation);
                    var source=lamp.AddComponent<LaserEmitter>(); source.RenderBeam=false;source.MaxDistance=20;
                    var obstruction=GameObject.CreatePrimitive(PrimitiveType.Cube); obstruction.transform.SetParent(root.transform);
                    obstruction.transform.position=a.transform.TransformPoint(new Vector3(0,1.5f,-2));obstruction.transform.localScale=Vector3.one*2;
                    Physics.SyncTransforms(); LightPuzzleWorld.Step(.5f);
                    Check(!a.IsOpen && a.PhysicalCollider.enabled, name + " opaque obstruction prevents activation");
                    UnityEngine.Object.DestroyImmediate(obstruction);Physics.SyncTransforms();LightPuzzleWorld.Step(.1f);
                    bool permanent=a.Mode==CurtainMode.Permanent;
                    Check(permanent?!a.IsOpen:a.IsOpen,name + " configured activation timing is honored");
                    LightPuzzleWorld.Step(.5f);
                    Check(a.IsOpen&&!a.PhysicalCollider.enabled&&a.Visuals.All(r=>!r.enabled),name + " real beam removes visuals and collision after rotation and scaling");
                    Check(!b.IsOpen&&b.PhysicalCollider.enabled&&b.Visuals.All(r=>r.enabled),name + " unlit duplicate stays independently solid");
                    source.Powered=false;
                    if(!permanent)
                    {
                        var body=GameObject.CreatePrimitive(PrimitiveType.Capsule);body.transform.SetParent(root.transform);
                        body.transform.position=a.PhysicalCollider.transform.TransformPoint(a.PhysicalCollider.center);var rb=body.AddComponent<Rigidbody>();rb.isKinematic=true;
                        Physics.SyncTransforms();LightPuzzleWorld.Step(.5f);
                        Check(a.IsOpen&&a.RestorePending&&!a.PhysicalCollider.enabled,"returning prefab waits while an occupant is inside");
                        UnityEngine.Object.DestroyImmediate(body);Physics.SyncTransforms();
                    }
                    LightPuzzleWorld.Step(.5f);
                    Check(permanent?a.IsOpen:!a.IsOpen,name + " darkness follows its own permanent or returning rule");
                    a.ResetState();
                    Check(!a.IsOpen&&a.PhysicalCollider.enabled&&a.Visuals.All(r=>r.enabled),name + " explicit reset restores model and collision");
                    UnityEngine.Object.DestroyImmediate(lamp);UnityEngine.Object.DestroyImmediate(a.gameObject);UnityEngine.Object.DestroyImmediate(b.gameObject);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            return string.Join("\n",report);
        }
    }
}
