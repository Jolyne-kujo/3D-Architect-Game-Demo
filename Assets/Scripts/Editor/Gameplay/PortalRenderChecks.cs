#if UNITY_EDITOR
using System;
using System.IO;
using CoastalTemple.LightPuzzles;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CoastalTemple.Portals.Editor
{
    public static class PortalRenderChecks
    {
        [Serializable] public class Report
        {
            public bool passed;
            public string detail;
            public Color upper;
            public Color lower;
            public int recursiveRenders;
            public string screenshot;
        }
        [MenuItem("Coastal Temple/Tests/Portal Render (Play Mode)")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            var report = new Report();
            if (!Application.isPlaying) return JsonUtility.ToJson(new Report { detail = "Run in Play mode after the render pipeline has initialized." }, true);
            var root = new GameObject("Disposable portal render check") { hideFlags = HideFlags.DontSave };
            Vector3 offset = new Vector3(9000, 800, 9000);
            Material portalMaterial = null, red = null, blue = null;
            Texture2D pixels = null;
            RenderTexture observerTarget = null;
            RenderTexture oldActive = RenderTexture.active;
            try
            {
                var shader = Shader.Find("Coastal Temple/Portal Window");
                if (!shader || !shader.isSupported) throw new Exception("Portal Window shader missing or unsupported");
                portalMaterial = new Material(shader);
                red = new Material(Shader.Find("Universal Render Pipeline/Unlit")); red.SetColor("_BaseColor", Color.red);
                blue = new Material(Shader.Find("Universal Render Pipeline/Unlit")); blue.SetColor("_BaseColor", Color.blue);
                PortalSurface MakePortal(string name, Vector3 position)
                {
                    var go = new GameObject(name); go.transform.SetParent(root.transform); go.transform.position = offset + position;
                    var light = go.AddComponent<LightPortal>(); light.Aperture = new Vector2(4, 4);
                    var screen = GameObject.CreatePrimitive(PrimitiveType.Cube); screen.transform.SetParent(go.transform, false);
                    screen.transform.localScale = new Vector3(4, 4, .02f); UnityEngine.Object.DestroyImmediate(screen.GetComponent<Collider>());
                    screen.GetComponent<Renderer>().sharedMaterial = portalMaterial;
                    var surface = go.AddComponent<PortalSurface>(); surface.Screen = screen.GetComponent<Renderer>();
                    surface.MaxTextureSize = 256; surface.ResolutionScale = 1;
                    return surface;
                }
                var entrance = MakePortal("Check Entrance", Vector3.zero);
                var exit = MakePortal("Check Exit", new Vector3(20, 0, 0));
                entrance.Portal.Paired = exit.Portal; exit.Portal.Paired = entrance.Portal;
                var cameraObject = new GameObject("Check Observer"); cameraObject.transform.SetParent(root.transform);
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.SetPositionAndRotation(offset + new Vector3(0, 0, 4), Quaternion.Euler(0, 180, 0));
                camera.fieldOfView = 50; camera.aspect = 1; camera.nearClipPlane = .05f; camera.farClipPlane = 100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
                entrance.Observer = camera; exit.Observer = camera;
                GameObject Marker(string name, float height, Material material)
                {
                    var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name; value.transform.SetParent(root.transform);
                    value.transform.position = offset + new Vector3(20, height, 3);
                    value.transform.localScale = new Vector3(8, 3, .1f);
                    value.GetComponent<Renderer>().sharedMaterial = material;
                    UnityEngine.Object.DestroyImmediate(value.GetComponent<Collider>()); return value;
                }
                var upper = Marker("Upper red", 1.5f, red); var lower = Marker("Lower blue", -1.5f, blue);
                PortalSurface.RenderAllNow();
                if (!entrance.CurrentTexture || entrance.RenderCount == 0) throw new Exception("URP render request did not produce a texture");
                // Inspect the actual portal shader through the observer, not only the intermediate texture.
                observerTarget = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32); observerTarget.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = observerTarget };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = observerTarget;
                pixels = new Texture2D(observerTarget.width, observerTarget.height, TextureFormat.RGBA32, false);
                pixels.ReadPixels(new Rect(0, 0, pixels.width, pixels.height), 0, 0); pixels.Apply();
                report.upper = pixels.GetPixel(pixels.width / 2, pixels.height * 3 / 4);
                report.lower = pixels.GetPixel(pixels.width / 2, pixels.height / 4);
                if (report.upper.r < .4f || report.upper.b > .2f || report.lower.b < .4f || report.lower.r > .2f)
                    throw new Exception("Destination view is missing, upside down, or clipped incorrectly");
                Directory.CreateDirectory("Documentation/Mechanisms");
                report.screenshot = "Documentation/Mechanisms/PortalRenderProbe.png";
                File.WriteAllBytes(report.screenshot, pixels.EncodeToPNG());
                upper.SetActive(false); lower.SetActive(false);
                exit.transform.SetPositionAndRotation(offset + new Vector3(0, 0, 10), Quaternion.Euler(0, 180, 0));
                int before = entrance.RenderCount;
                PortalSurface.RenderAllNow();
                report.recursiveRenders = entrance.RenderCount - before;
                if (report.recursiveRenders != 2) throw new Exception("Expected exactly two render levels, got " + report.recursiveRenders);
                report.passed = true; report.detail = "URP destination colors, vertical orientation, clipping and bounded two-level recursion passed.";
            }
            catch (Exception exception) { report.detail = exception.ToString(); }
            finally
            {
                RenderTexture.active = oldActive;
                UnityEngine.Object.DestroyImmediate(root);
                if (portalMaterial) UnityEngine.Object.DestroyImmediate(portalMaterial);
                if (red) UnityEngine.Object.DestroyImmediate(red);
                if (blue) UnityEngine.Object.DestroyImmediate(blue);
                if (pixels) UnityEngine.Object.DestroyImmediate(pixels);
                if (observerTarget) { observerTarget.Release(); UnityEngine.Object.DestroyImmediate(observerTarget); }
            }
            Directory.CreateDirectory("Documentation/Mechanisms");
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText("Documentation/Mechanisms/PortalRenderChecks.json", json);
            return json;
        }
    }
}
#endif
