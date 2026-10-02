using System.Collections.Generic;
using System.IO;
using CoastalTemple.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class ThirdPersonCameraChecks
    {
        public static string Run(string report = "Documentation/ThirdPersonPreview/CameraChecks.txt")
        {
            var lines = new List<string>();
            void Check(bool ok, string name) => lines.Add((ok ? "PASS " : "FAIL ") + name);
            var root = new GameObject("TemporaryThirdPersonCameraChecks");
            try
            {
                var player = new GameObject("Actor"); player.transform.SetParent(root.transform);
                player.transform.position = new Vector3(10000, 10000, 10000);
                var walker = player.AddComponent<CourtyardWalker>();
                var eye = new GameObject("Eye").transform; eye.SetParent(player.transform, false);
                eye.localPosition = Vector3.up * 1.6f; walker.eye = eye;
                var view = new GameObject("View").AddComponent<Camera>(); view.transform.SetParent(eye, false);
                var body = GameObject.CreatePrimitive(PrimitiveType.Capsule); body.transform.SetParent(player.transform, false);
                var avatar = body.AddComponent<CoastalPlayerAvatar>(); avatar.bodyRenderers = body.GetComponents<Renderer>(); avatar.keepShadowsWhenHidden = true;
                var hands = new GameObject("Hands").AddComponent<FirstPersonHands>(); hands.transform.SetParent(view.transform, false);
                hands.model = new GameObject("Arms").transform; hands.model.SetParent(hands.transform, false);
                hands.overlayCamera = new GameObject("Overlay").AddComponent<Camera>(); hands.overlayCamera.transform.SetParent(hands.transform, false);
                var rig = player.AddComponent<CoastalPlayerCamera>();
                rig.walker = walker; rig.view = view; rig.avatar = avatar; rig.hands = hands; rig.overviewRoot = root.transform;
                rig.SetThirdPerson(true);
                Check(rig.thirdPerson, "third-person mode can be selected");
                Check(Vector3.Distance(view.transform.position, eye.position) > 3, "camera follows behind the actor instead of remaining in the head");
                Check(avatar.bodyRenderers[0].enabled && avatar.bodyRenderers[0].shadowCastingMode == ShadowCastingMode.On, "complete body is rendered with a real world shadow");
                Check(!hands.overlayCamera.enabled && !hands.model.gameObject.activeSelf, "first-person arms and overlay are hidden");
                var position = player.transform.position; var rotation = player.transform.rotation;
                walker.ApplyLook(new Vector2(90, -30)); rig.SnapToTarget();
                Check(Vector3.Distance(position, player.transform.position) < .0001f, "orbit never translates the character");
                Check(Quaternion.Angle(rotation, player.transform.rotation) < .001f, "stationary camera orbit does not rotate the body or shadow");
                Check(Vector3.Dot(view.transform.forward, Vector3.right) > .5f, "camera yaw follows mouse look independently of actor heading");
                walker.ApplyLook(new Vector2(-90, 30)); rig.SnapToTarget();
                var pivot = player.transform.position + Vector3.up * rig.pivotHeight;
                var offset = view.transform.position - pivot;
                var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.SetParent(root.transform, false);
                wall.transform.position = pivot + offset.normalized * 2;
                wall.transform.rotation = Quaternion.LookRotation(offset.normalized);
                wall.transform.localScale = new Vector3(5, 5, .3f); Physics.SyncTransforms(); rig.SnapToTarget();
                Check(rig.CurrentDistance > .1f && rig.CurrentDistance < 1.8f, "solid wall retracts the camera before clipping");
                wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms(); rig.SnapToTarget();
                Check(rig.CurrentDistance > 3.9f, "trigger volumes and actor colliders do not obstruct camera");
                rig.SetOverview(true); rig.SetOverview(false);
                Check(rig.thirdPerson && !hands.overlayCamera.enabled && Vector3.Distance(view.transform.position, eye.position) > 3, "return from overview preserves third-person mode");
                rig.SetThirdPerson(false);
                Check(view.transform.parent == eye && view.transform.localPosition == Vector3.zero && !hands.overlayCamera.enabled, "explicit first-person fallback restores eye without hands");
                Check(avatar.bodyRenderers[0].shadowCastingMode == ShadowCastingMode.ShadowsOnly, "first-person fallback still casts the complete body shadow");
            }
            finally { Object.DestroyImmediate(root); }
            Directory.CreateDirectory(Path.GetDirectoryName(report)); File.WriteAllLines(report, lines);
            return string.Join("\n", lines);
        }
    }
}
