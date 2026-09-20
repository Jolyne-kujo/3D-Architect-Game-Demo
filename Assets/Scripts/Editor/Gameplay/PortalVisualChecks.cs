#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using UnityEngine;
using UnityEditor;

namespace CoastalTemple.Portals.Editor
{
    public static class PortalVisualChecks
    {
        [Serializable] public class Result { public string name; public bool passed; public string detail; }
        [Serializable] public class Report { public int passed; public int failed; public List<Result> results = new List<Result>(); }
        static GameObject fixture;
        static Report report;
        static readonly Vector3 Offset = new Vector3(9400, 800, 9400);
        static readonly List<Material> temporaryMaterials = new List<Material>();
        public static string Run()
        {
            report = new Report();
            Test("mesh crossing creates only clipped renderer geometry", () => {
                Pair(out var a, out var b);
                var actor = Actor(); var source = actor.GetComponentInChildren<MeshRenderer>();
                var visual = actor.AddComponent<PortalTravellerVisual>(); visual.EvaluateNow();
                Check(visual.IsSlicing && visual.ActivePortal == a && visual.GhostRendererCount == 1, "no source/exit split");
                Check(visual.GhostRoot.GetComponentsInChildren<MonoBehaviour>().Length == 0, "ghost duplicated behaviour scripts");
                Check(visual.GhostRoot.GetComponentsInChildren<Collider>().Length == 0, "ghost duplicated colliders");
                Check(source.sharedMaterial.shader.name == "Coastal Temple/Portal Clipped Lit", "source was not clipped");
                var ghost = visual.GhostRoot.GetComponentInChildren<MeshRenderer>();
                Check(Vector3.Distance(ghost.bounds.center, a.MapPoint(source.bounds.center)) < .005f, "ghost geometry is not at mapped location");
                var block = new MaterialPropertyBlock(); source.GetPropertyBlock(block); Vector4 sourcePlane = block.GetVector("_PortalClipPlane");
                ghost.GetPropertyBlock(block); Vector4 ghostPlane = block.GetVector("_PortalClipPlane");
                Vector3 before = a.transform.position + a.PlaneNormal * .2f;
                Vector3 after = a.transform.position - a.PlaneNormal * .2f;
                Check(Evaluate(sourcePlane, before) > 0 && Evaluate(sourcePlane, after) < 0, "source halfspace reversed");
                Check(Evaluate(ghostPlane, a.MapPoint(before)) < 0 && Evaluate(ghostPlane, a.MapPoint(after)) > 0, "ghost does not show complementary half");
            });
            Test("leaving portal restores exact original material and property block", () => {
                Pair(out _, out _); var actor = Actor(); var source = actor.GetComponentInChildren<MeshRenderer>();
                Material original = source.sharedMaterial;
                var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", Color.green); source.SetPropertyBlock(block);
                var visual = actor.AddComponent<PortalTravellerVisual>(); visual.EvaluateNow();
                actor.transform.position += Vector3.forward * 4; visual.EvaluateNow();
                source.GetPropertyBlock(block);
                Check(!visual.IsSlicing && !visual.GhostRoot && visual.GhostRendererCount == 0, "ghost retained away from portal");
                Check(source.sharedMaterial == original && block.GetColor("_BaseColor") == Color.green, "original material state lost");
                Check(block.GetFloat("_PortalClipEnabled") == 0, "clip property leaked after leaving");
            });
            Test("crossing swaps body and ghost sides without extra controllers", () => {
                Pair(out var a, out var b); var actor = Actor();
                var visual = actor.AddComponent<PortalTravellerVisual>(); visual.EvaluateNow();
                actor.transform.position -= Vector3.forward * .3f;
                Check(actor.GetComponent<PortalTraveller>().WarpThrough(a), "traveller failed to transfer");
                visual.EvaluateNow();
                Check(visual.ActivePortal == b && visual.GhostRendererCount == 1, "body/ghost sides did not swap");
                Check(visual.GhostRoot.GetComponentsInChildren<PortalTraveller>().Length == 0, "ghost acquired a controller");
            });
            Test("actual RedBot skinned pose is baked and restored", () => {
                Pair(out var a, out _);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/RedBot.prefab");
                Check(prefab, "RedBot prefab missing");
                var actor = new GameObject("RedBot visual check actor"); actor.transform.SetParent(fixture.transform, false); actor.transform.localPosition = new Vector3(0, 0, .08f);
                actor.AddComponent<PortalTraveller>();
                var model = UnityEngine.Object.Instantiate(prefab, actor.transform); model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(); Check(skins.Length > 0, "RedBot has no skinned renderers");
                var materials = new Material[skins.Length]; for (int i = 0; i < skins.Length; i++) materials[i] = skins[i].sharedMaterial;
                var animator = model.GetComponentInChildren<Animator>();
                if (Application.isPlaying && animator) { animator.Rebind(); animator.Update(0); }
                var visual = actor.AddComponent<PortalTravellerVisual>(); visual.EvaluateNow();
                Check(visual.IsSlicing && visual.GhostRendererCount == skins.Length, "RedBot renderer set not fully represented");
                var ghosts = visual.GhostRoot.GetComponentsInChildren<MeshFilter>();
                int sourceVertices = 0, ghostVertices = 0;
                foreach (var skin in skins) sourceVertices += skin.sharedMesh.vertexCount;
                foreach (var ghost in ghosts) ghostVertices += ghost.sharedMesh.vertexCount;
                Check(sourceVertices > 0 && sourceVertices == ghostVertices, "baked RedBot pose lost vertices");
                Check(visual.GhostRoot.GetComponentsInChildren<Animator>().Length == 0, "duplicate animator drives ghost");
                visual.ClearVisuals();
                for (int i = 0; i < skins.Length; i++) Check(skins[i].sharedMaterial == materials[i], "RedBot material not restored");
            });
            Directory.CreateDirectory("Documentation/Mechanisms");
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText("Documentation/Mechanisms/PortalVisualChecks.json", json);
            return json;
        }
        static float Evaluate(Vector4 plane, Vector3 point) => Vector4.Dot(plane, new Vector4(point.x, point.y, point.z, 1));
        static GameObject Actor()
        {
            var actor = new GameObject("Visual check actor"); actor.transform.SetParent(fixture.transform, false); actor.transform.localPosition = new Vector3(0, 0, .15f);
            actor.AddComponent<PortalTraveller>();
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube); body.transform.SetParent(actor.transform, false);
            body.transform.localPosition = Vector3.up; body.transform.localScale = new Vector3(.5f, 1.8f, .6f);
            UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit")); material.SetColor("_BaseColor", Color.red); temporaryMaterials.Add(material);
            body.GetComponent<Renderer>().sharedMaterial = material;
            return actor;
        }
        static void Pair(out LightPortal a, out LightPortal b)
        {
            var left = new GameObject("Slice entrance"); left.transform.SetParent(fixture.transform, false); left.transform.localPosition = Vector3.up;
            var right = new GameObject("Slice exit"); right.transform.SetParent(fixture.transform, false); right.transform.localPosition = new Vector3(15, 1, 0);
            a = left.AddComponent<LightPortal>(); b = right.AddComponent<LightPortal>(); a.Paired = b; b.Paired = a;
            a.Aperture = b.Aperture = new Vector2(3, 4);
            left.AddComponent<PortalSurface>().SendMessage("OnEnable"); right.AddComponent<PortalSurface>().SendMessage("OnEnable");
        }
        static void Test(string name, Action run)
        {
            fixture = new GameObject("Portal visual checks") { hideFlags = HideFlags.DontSave }; fixture.transform.position = Offset;
            var result = new Result { name = name };
            try { run(); result.passed = true; result.detail = "passed"; report.passed++; }
            catch (Exception error) { result.detail = error.ToString(); report.failed++; }
            finally
            {
                foreach (var visual in fixture.GetComponentsInChildren<PortalTravellerVisual>()) visual.ClearVisuals();
                foreach (var surface in fixture.GetComponentsInChildren<PortalSurface>()) surface.SendMessage("OnDisable");
                UnityEngine.Object.DestroyImmediate(fixture);
                foreach (var material in temporaryMaterials) if (material) UnityEngine.Object.DestroyImmediate(material);
                temporaryMaterials.Clear();
            }
            report.results.Add(result);
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
#endif
