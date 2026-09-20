using System;
using System.Collections.Generic;
using System.Reflection;
using CoastalTemple.Mechanisms;
using Courtyard.Water;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class WaterPrefabChecks
    {
        public const string PrefabPath = "Assets/Prefabs/Water/WaterVolume.prefab";
        const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

        public static string CheckAsset()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (!root) throw new InvalidOperationException("Water prefab is missing.");
            var water = root.GetComponent<WaterVolume>();
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;
            if (!water || !mesh || !EditorUtility.IsPersistent(mesh))
                throw new InvalidOperationException("FAIL water prefab must carry a persistent visible surface mesh before Play.");
            var data = new List<Vector4>(); mesh.GetUVs(1, data);
            if (data.Count != mesh.vertexCount || data.FindIndex(v => v.x > .1f) < 0)
                throw new InvalidOperationException("FAIL water preview needs depth data for the refraction shader.");
            if (!water.surfaceMaterial || root.GetComponent<MeshRenderer>().sharedMaterial != water.surfaceMaterial)
                throw new InvalidOperationException("FAIL water prefab renderer must already use its water material.");
            if (root.GetComponent<Collider>() || root.GetComponent<Rigidbody>())
                throw new InvalidOperationException("FAIL water surface must not act as a solid floor.");
            if (water.bedTerrain || water.surfaceMotion)
                throw new InvalidOperationException("FAIL standalone pool prefab must not reference scene terrain or waves.");
            return "PASS prefab has persistent geometry, wet-depth data, material, no solid floor, no scene dependencies.";
        }

        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
            var results = new List<string>();
            void Check(bool ok, string label)
            {
                results.Add((ok ? "PASS " : "FAIL ") + label);
                if (!ok) throw new InvalidOperationException(string.Join("\n", results));
            }
            var scene = SceneManager.CreateScene("TemporaryWaterPrefabChecks", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var holder = new GameObject("Water prefab checks");
            SceneManager.MoveGameObjectToScene(holder, scene); holder.SetActive(false);
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            WaterVolume Basin(Vector3 position)
            {
                var clone = UnityEngine.Object.Instantiate(source, holder.transform);
                clone.transform.position = position;
                var water = clone.GetComponent<WaterVolume>();
                water.sizeX = water.sizeZ = 6; water.cellSize = 1; water.bottom = -4; water.initialLevel = 0;
                water.drainPosition = Vector2.zero; return water;
            }
            var a = Basin(new Vector3(9000, 0, 9000));
            var b = Basin(new Vector3(9010, 0, 9000));
            var actor = new GameObject("Unbound swimmer"); actor.transform.SetParent(holder.transform, false);
            actor.transform.position = a.transform.position - Vector3.up + Vector3.right * 2;
            var walker = actor.AddComponent<CourtyardWalker>();
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube); block.transform.SetParent(holder.transform, false);
            block.transform.position = a.transform.position - Vector3.up;
            var body = block.AddComponent<Rigidbody>(); body.mass = 400;
            var buoyancy = block.AddComponent<BuoyantBody>(); buoyancy.displacementSize = Vector3.one;
            try
            {
                holder.SetActive(true); walker.enabled = false; buoyancy.enabled = false;
                Check(walker.SampleWater(actor.transform.position, out _, out _, out _) == a,
                    "unbound player discovers a dragged water prefab");
                Check(a.Grid != b.Grid && a.GetComponent<MeshFilter>().sharedMesh != b.GetComponent<MeshFilter>().sharedMesh,
                    "copies own separate solvers and runtime meshes");
                var physics = scene.GetPhysicsScene();
                for (int i = 0; i < 300; i++)
                {
                    typeof(BuoyantBody).GetMethod("FixedUpdate", Hidden).Invoke(buoyancy, null);
                    physics.Simulate(.02f);
                }
                Check(body.position.y > -.1f && body.position.y < .3f && buoyancy.Submersion > .2f,
                    "unbound rigidbody floats at its mass/displacement equilibrium");
                var density = typeof(WaterVolume).GetField("density");
                Check(density != null, "water carries an editable density");
                density.SetValue(a, 200f);
                body.position = a.transform.position - Vector3.up; body.linearVelocity = Vector3.zero;
                for (int i = 0; i < 20; i++)
                {
                    typeof(BuoyantBody).GetMethod("FixedUpdate", Hidden).Invoke(buoyancy, null);
                    physics.Simulate(.02f);
                }
                Check(body.position.y < -.2f - 1, "lower water density makes the same object sink");
                density.SetValue(a, 1000f);
                double beforeB = b.Grid.Volume, beforeA = a.Grid.Volume;
                a.OpenDrain(); for (int i = 0; i < 100; i++) { a.Advance(.02f); b.Advance(.02f); }
                Check(a.Grid.Volume < beforeA - .1 && Math.Abs(b.Grid.Volume - beforeB) < .001,
                    "draining one prefab leaves its copy full");
                a.enabled = false;
                Check(!walker.SampleWater(actor.transform.position, out _, out _, out _),
                    "disabled water no longer catches the swimmer");
                a.enabled = true; a.ResetWater();
                Check(walker.SampleWater(actor.transform.position, out _, out _, out _) == a,
                    "re-enabled water becomes available again");
                Check(!WaterVolume.FindAt(actor.transform.position, SceneManager.GetActiveScene(), out _, out _, out _),
                    "automatic sampling cannot cross isolated physics scenes");

                var optics = new GameObject("Prefab refraction probe"); optics.SetActive(false); optics.transform.SetParent(holder.transform);
                optics.transform.position = a.transform.position;
                Transform Anchor(string name, Vector3 position)
                {
                    var obj = new GameObject(name); obj.transform.SetParent(optics.transform, false); obj.transform.localPosition = position;
                    return obj.transform;
                }
                var mirage = optics.AddComponent<WaterMirage>(); mirage.water = a;
                mirage.source = Anchor("Source", new Vector3(0, 2, -2)); mirage.source.localRotation = Quaternion.Euler(50, 0, 0);
                mirage.sampleObject = Anchor("Submerged sample", Vector3.down);
                mirage.projectionPlane = Anchor("Projection plane", new Vector3(0, -1, .5f));
                mirage.target = Anchor("Target", new Vector3(0, -2, .5f));
                optics.SetActive(true); mirage.EvaluateNow(); var normalDirection = mirage.RefractedDirection;
                a.refractiveIndex = 1.7f; mirage.EvaluateNow();
                Check(mirage.HasProjection && Vector3.Angle(normalDirection, mirage.RefractedDirection) > 5,
                    "changing water refractive index bends the existing optical mechanism");
                a.refractiveIndex = 1.333f;

                var frame = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/VerticalBuoyantPlatform.prefab"), holder.transform);
                var rail = frame.GetComponent<GuidedBuoyantPlatform>(); frame.transform.position = a.transform.position;
                rail.pathStart = Vector3.down * 3; rail.pathEnd = Vector3.up * 2; rail.initialTravel = .5f; rail.water = null;
                rail.ResetState(); rail.Simulate(.02f); float rise = rail.BuoyancySpeed;
                a.density = 200; rail.ResetState(); rail.Simulate(.02f);
                Check(rise > 0 && rail.BuoyancySpeed < 0, "guided platform also reads density from its automatically found water");
                a.density = 1000;
                return string.Join("\n", results) + "\n" + results.Count + " water prefab checks passed.";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(holder);
                SceneManager.UnloadSceneAsync(scene);
            }
        }

        public static string CheckEditing()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play first.");
            var existing = new HashSet<string>(AssetDatabase.FindAssets("t:Mesh", new[] { "Assets/Objects/Water/SurfacePreviews" }));
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            string generatedPath = null;
            try
            {
                var water = root.GetComponent<WaterVolume>(); var filter = root.GetComponent<MeshFilter>();
                var original = filter.sharedMesh; var originalBounds = original.bounds;
                WaterSurfacePreviewAssets.Refresh(water, true);
                var unchanged = filter.sharedMesh;
                WaterSurfacePreviewAssets.Refresh(water, true);
                if (filter.sharedMesh != unchanged) throw new InvalidOperationException("FAIL repeated preview refresh creates duplicate geometry assets.");
                water.sizeX = 8; water.sizeZ = 10;
                if (!WaterSurfacePreviewAssets.Refresh(water, true)) throw new InvalidOperationException("FAIL prefab contents cannot refresh their preview.");
                generatedPath = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (filter.sharedMesh == original || original.bounds != originalBounds || Mathf.Abs(filter.sharedMesh.bounds.size.x - 8) > .001f)
                    throw new InvalidOperationException("FAIL resized prefab copy modified another water's geometry.");
                return "PASS Prefab Mode preview, repeated refresh reuses assets, resized copies do not overwrite shared geometry.";
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
                if (generatedPath != null && !existing.Contains(AssetDatabase.AssetPathToGUID(generatedPath))) AssetDatabase.DeleteAsset(generatedPath);
            }
        }
    }
}
