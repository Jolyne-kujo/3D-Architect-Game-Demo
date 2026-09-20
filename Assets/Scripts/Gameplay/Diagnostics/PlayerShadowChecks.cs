#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CoastalTemple.Player;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    // Tests world-space skinned geometry and actual rendered shadows, not just camera parenting.
    [AddComponentMenu("")]
    public sealed class PlayerShadowChecks : MonoBehaviour
    {
        string DirectoryPath = "Documentation/CoastalV14Verification";
        readonly List<string> records = new List<string>();
        readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();
        readonly Vector3 start = new Vector3(-65, 150.03f, 204);
        CourtyardWalker walker;
        CoastalWalkthrough tour;
        RiggedPlayerAnimation driver;
        SkinnedMeshRenderer skin;
        Animator animator;
        Camera observer;
        Light sun;
        Light[] oldLights;
        bool[] lightEnabled;
        bool cleaned, previousThirdPerson;
        float oldAnimatorSpeed;
        ShadowCastingMode oldShadowMode;

        public static string Begin(string outputDirectory = "Documentation/CoastalV14Verification")
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play first.");
            var checks = new GameObject("TemporaryPlayerShadowChecks").AddComponent<PlayerShadowChecks>();
            checks.DirectoryPath = outputDirectory;
            return "Checking actual body geometry, fixed-camera shadows, yaw/pitch and movement.";
        }

        void Check(bool ok, string detail) => records.Add((ok ? "PASS " : "FAIL ") + detail);
        T Track<T>(T value) where T : UnityEngine.Object { temporary.Add(value); return value; }

        Vector3[] Vertices()
        {
            var mesh = new Mesh();
            skin.BakeMesh(mesh);
            var points = mesh.vertices.Select(skin.transform.TransformPoint).ToArray();
            Destroy(mesh);
            return points;
        }

        Color32[] Capture(string filename)
        {
            var rt = RenderTexture.GetTemporary(1024, 768, 24);
            var texture = new Texture2D(1024, 768, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                observer.targetTexture = rt; observer.aspect = 4f / 3;
                observer.Render(); RenderTexture.active = rt;
                texture.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); texture.Apply();
                File.WriteAllBytes(DirectoryPath + "/" + filename + ".png", texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            finally { observer.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Destroy(texture); }
        }

        static double Difference(Color32[] a, Color32[] b)
        {
            long total = 0;
            for (int i = 0; i < a.Length; i++) total += Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b);
            return total / (a.Length * 3.0);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(DirectoryPath);
            tour = FindFirstObjectByType<CoastalWalkthrough>(); walker = tour.walker;
            previousThirdPerson=tour.playerCamera.thirdPerson;tour.playerCamera.SetThirdPerson(false);
            tour.SetOverview(false); walker.enabled = false;
            driver = walker.GetComponentInChildren<RiggedPlayerAnimation>(); animator = driver.animator;
            skin = driver.GetComponentInChildren<SkinnedMeshRenderer>(); oldShadowMode = skin.shadowCastingMode;
            oldAnimatorSpeed = animator.speed;
            var floor = Track(GameObject.CreatePrimitive(PrimitiveType.Cube)); floor.name = "TemporaryShadowFloor";
            floor.transform.position = start - new Vector3(0, .13f, 0); floor.transform.localScale = new Vector3(25, .2f, 25);
            var material = Track(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            material.SetColor("_BaseColor", new Color(.8f, .8f, .8f)); material.SetFloat("_Smoothness", 0);
            floor.GetComponent<Renderer>().sharedMaterial = material;
            Physics.SyncTransforms(); walker.RespawnAt(start, 0);
            for (int i = 0; i < 15; i++) { walker.SimulateMovement(Vector2.zero, false, false, 0, 1f/60); yield return null; }
            driver.enabled = false; animator.SetFloat("Speed", 0); animator.SetBool("Grounded", true); animator.SetBool("Swimming", false);
            animator.Play("Locomotion", 0, .25f); animator.Update(0); animator.speed = 0;
            yield return null;
            var origin = walker.transform.position; var eye = tour.view.transform.position; var initial = Vertices();
            var feet = (animator.GetBoneTransform(HumanBodyBones.LeftFoot).position + animator.GetBoneTransform(HumanBodyBones.RightFoot).position) * .5f;
            Check(new Vector2(feet.x-origin.x, feet.z-origin.z).magnitude < .18f, "idle feet centered within capsule; horizontal offset=" + new Vector2(feet.x-origin.x,feet.z-origin.z).magnitude);
            float soleGap = initial.Min(p => p.y) - (start.y - .03f);
            Check(soleGap >= -.04f && soleGap < .05f, "actual skinned sole to floor distance=" + soleGap);
            Check(!animator.applyRootMotion, "animation root motion disabled; controller owns translation");
            Check(tour.playerCamera.hands.GetComponentsInChildren<Renderer>(true).All(r => r.shadowCastingMode == ShadowCastingMode.Off), "all camera-space geometry including lantern casts no world shadow");

            oldLights = FindObjectsByType<Light>(FindObjectsSortMode.None); lightEnabled = oldLights.Select(l => l.enabled).ToArray();
            foreach (var light in oldLights) light.enabled = false;
            sun = Track(new GameObject("TemporaryFixedSun")).AddComponent<Light>(); sun.type = LightType.Directional;
            sun.intensity = 1.5f; sun.shadows = LightShadows.Hard; sun.shadowBias = .02f; sun.shadowNormalBias = .1f;
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            observer = Track(new GameObject("TemporaryFixedObserver")).AddComponent<Camera>(); observer.enabled = false;
            observer.transform.position = origin + new Vector3(0, 9, .4f); observer.transform.rotation = Quaternion.Euler(90, 0, 0);
            observer.orthographic = true; observer.orthographicSize = 3; observer.nearClipPlane = .1f; observer.farClipPlane = 20;
            observer.cullingMask = ~(1 << LayerMask.NameToLayer("FirstPersonHands")); observer.clearFlags = CameraClearFlags.SolidColor;
            observer.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            skin.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            // New cameras/lights register with URP on the next frame; an immediate first render
            // may contain only the clear color and is not a usable shadow baseline.
            yield return null;
            var referenceImage = Capture("01-FixedShadow-Level");
            Check(referenceImage.Any(p=>p.r>200) && referenceImage.Any(p=>p.r<160), "baseline contains both lit floor and rendered shadow");
            foreach (float pitch in new[] { -75f, 75f, 0f })
            {
                walker.ApplyLook(new Vector2(0, walker.LookPitch - pitch)); yield return null;
                var points = Vertices(); float max = points.Select((p, i) => Vector3.Distance(p, initial[i])).Max();
                Check(max < .0001f, "pitch " + pitch + " changes no world body vertices; max meters=" + max);
                Check(Vector3.Distance(eye, tour.view.transform.position) < .0001f, "pitch " + pitch + " rotates camera in place");
                double difference = Difference(referenceImage, Capture("02-FixedShadow-Pitch" + pitch));
                Check(difference < .1, "pitch " + pitch + " keeps actual fixed-view shadow unchanged; mean RGB delta=" + difference);
            }
            // Camera-only yaw isolates rendering/view changes from legitimate body rotation.
            tour.view.transform.localRotation = Quaternion.Euler(0, 120, 0); yield return null;
            Check(Difference(referenceImage, Capture("03-CameraOnlyYaw")) < .1, "camera-only yaw cannot move world shadow");
            tour.view.transform.localRotation = Quaternion.identity;
            for (int i = 1; i <= 4; i++)
            {
                walker.ApplyLook(new Vector2(90, 0)); yield return null;
                var points = Vertices(); var turn = Quaternion.Euler(0, 90 * i, 0);
                float max = points.Select((p, n) => Vector3.Distance(p, origin + turn * (initial[n]-origin))).Max();
                Check(max < .00015f, "yaw " + i*90 + " all skinned vertices rotate about controller pivot; error=" + max);
                Check(Vector3.Distance(driver.transform.position, origin) < .0001f && Vector3.Distance(eye, tour.view.transform.position) < .0001f,
                    "yaw " + i*90 + " world body pivot and camera do not translate");
                Capture("04-InPlaceYaw" + i*90);
            }
            sun.transform.rotation = Quaternion.Euler(48, 65, 0);
            Check(Difference(referenceImage, Capture("05-ChangedLightDirection")) > .1, "actual world shadow responds to changed light direction");
            sun.transform.rotation = Quaternion.Euler(48, -35, 0);
            // Show where the real body stands using the same fixed light and original world mesh.
            skin.shadowCastingMode = ShadowCastingMode.On;
            observer.orthographicSize = 2.2f; observer.transform.position = origin + new Vector3(3, 3.2f, -4);
            observer.transform.LookAt(origin + Vector3.up * .8f); Capture("06-BodyAndShadowAtFeet");
            skin.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            animator.speed = oldAnimatorSpeed; driver.enabled = true;
            for(int i=0;i<40;i++){walker.SimulateMovement(Vector2.right, false, false, 0, 1f/60);yield return null;}
            Check(Vector3.Distance(walker.transform.position, origin) > 1, "actual controller strafe moves player");
            Check(Vector3.Distance(driver.transform.position, walker.transform.position) < .0001f, "animated body follows controller translation without trailing offset");
            Check(Quaternion.Angle(driver.facingRoot.rotation, walker.transform.rotation * Quaternion.Euler(0, driver.modelYawOffset, 0)) < .01f,
                "strafing animation cannot turn body toward velocity");
            File.WriteAllText(DirectoryPath + "/ShadowChecks.txt", string.Join("\n", records));
            Cleanup(); Destroy(gameObject);
        }

        void Cleanup()
        {
            if (cleaned) return; cleaned = true;
            if(oldLights!=null)for(int i=0;i<oldLights.Length;i++)if(oldLights[i])oldLights[i].enabled=lightEnabled[i];
            if(animator)animator.speed=oldAnimatorSpeed;
            if(driver)driver.enabled=true;
            if(skin)skin.shadowCastingMode=oldShadowMode;
            foreach(var item in temporary)if(item)Destroy(item);
            if(walker){walker.enabled=true;walker.ResetPosition();tour.view.transform.localRotation=Quaternion.identity;tour.playerCamera.SetThirdPerson(previousThirdPerson);tour.SetOverview(false);}
        }
        void OnDestroy()=>Cleanup();
    }
}
#endif
