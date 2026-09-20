using System;
using System.Collections.Generic;

namespace CoastalTemple.Editor
{
    // This kernel has no Unity/native dependencies, so its area and interpolation rules run outside the editor too.
    public static class TutorialGapGeometry
    {
        public struct Vertex
        {
            public double X, Y, Z, A, B, C;
            public Vertex(double x, double y, double z, double a, double b, double c)
            { X = x; Y = y; Z = z; A = a; B = b; C = c; }
            public static Vertex Lerp(Vertex a, Vertex b, double t) => new Vertex(
                a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t,
                a.A + (b.A - a.A) * t, a.B + (b.B - a.B) * t, a.C + (b.C - a.C) * t);
        }
        public struct Box
        {
            public double MinX, MinY, MinZ, MaxX, MaxY, MaxZ;
            public Box(double x0, double y0, double z0, double x1, double y1, double z1)
            { MinX = x0; MinY = y0; MinZ = z0; MaxX = x1; MaxY = y1; MaxZ = z1; }
        }
        public struct Triangle
        {
            public Vertex A, B, C;
            public Triangle(Vertex a, Vertex b, Vertex c) { A = a; B = b; C = c; }
            public double Area
            {
                get
                {
                    double x1 = B.X - A.X, y1 = B.Y - A.Y, z1 = B.Z - A.Z;
                    double x2 = C.X - A.X, y2 = C.Y - A.Y, z2 = C.Z - A.Z;
                    double x = y1 * z2 - z1 * y2, y = z1 * x2 - x1 * z2, z = x1 * y2 - y1 * x2;
                    return Math.Sqrt(x * x + y * y + z * z) * .5;
                }
            }
        }

        public static List<Triangle> Subtract(Vertex a, Vertex b, Vertex c, Box box, out double removedArea)
        {
            var pending = new List<Vertex>(8) { a, b, c };
            var result = new List<Triangle>(12);
            for (int plane = 0; plane < 6 && pending.Count >= 3; plane++)
            {
                Split(pending, box, plane, out var inside, out var outside);
                Triangulate(outside, result);
                pending = inside;
            }
            removedArea = PolygonArea(pending);
            if (removedArea <= 1e-12)
            {
                removedArea = 0;
                result.Clear(); result.Add(new Triangle(a, b, c));
            }
            return result;
        }

        static double Distance(Vertex p, Box b, int plane)
        {
            switch (plane)
            {
                case 0: return b.MinX - p.X;
                case 1: return p.X - b.MaxX;
                case 2: return b.MinY - p.Y;
                case 3: return p.Y - b.MaxY;
                case 4: return b.MinZ - p.Z;
                default: return p.Z - b.MaxZ;
            }
        }

        static void Split(List<Vertex> polygon, Box box, int plane, out List<Vertex> inside, out List<Vertex> outside)
        {
            inside = new List<Vertex>(8); outside = new List<Vertex>(8);
            if (polygon.Count == 0) return;
            Vertex previous = polygon[polygon.Count - 1];
            double previousDistance = Distance(previous, box, plane);
            bool previousInside = previousDistance <= 0;
            foreach (var current in polygon)
            {
                double currentDistance = Distance(current, box, plane);
                bool currentInside = currentDistance <= 0;
                if (currentInside != previousInside)
                {
                    double t = previousDistance / (previousDistance - currentDistance);
                    var intersection = Vertex.Lerp(previous, current, t);
                    // The same point belongs to both polygon boundaries; coplanar faces are classified only once.
                    AddDistinct(inside, intersection); AddDistinct(outside, intersection);
                }
                AddDistinct(currentInside ? inside : outside, current);
                previous = current; previousDistance = currentDistance; previousInside = currentInside;
            }
            RemoveClosingDuplicate(inside); RemoveClosingDuplicate(outside);
        }

        static bool SamePosition(Vertex a, Vertex b)
        {
            double x = a.X - b.X, y = a.Y - b.Y, z = a.Z - b.Z;
            return x * x + y * y + z * z <= 1e-22;
        }
        static void AddDistinct(List<Vertex> values, Vertex value)
        { if (values.Count == 0 || !SamePosition(values[values.Count - 1], value)) values.Add(value); }
        static void RemoveClosingDuplicate(List<Vertex> values)
        { if (values.Count > 1 && SamePosition(values[0], values[values.Count - 1])) values.RemoveAt(values.Count - 1); }
        static void Triangulate(List<Vertex> polygon, List<Triangle> result)
        {
            for (int i = 1; i + 1 < polygon.Count; i++)
            {
                var triangle = new Triangle(polygon[0], polygon[i], polygon[i + 1]);
                if (triangle.Area > 1e-12) result.Add(triangle);
            }
        }
        static double PolygonArea(List<Vertex> polygon)
        {
            double area = 0;
            for (int i = 1; i + 1 < polygon.Count; i++) area += new Triangle(polygon[0], polygon[i], polygon[i + 1]).Area;
            return area;
        }
    }
}

#if UNITY_EDITOR
namespace CoastalTemple.Editor
{
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using UnityEditor;
    using UnityEditor.SceneManagement;
    using UnityEngine;
    using UnityEngine.Rendering;
    using Object = UnityEngine.Object;
    using G = TutorialGapGeometry;

    /// <summary>
    /// Editor-only surface excavation. Begin restores this folder's previous cuts; Cut subtracts a yaw-aligned box.
    /// Originals are never modified. Save the scene after cutting. To move cuts, Begin and apply the new boxes again.
    /// Restore(assetFolder) also works after editor reload. This removes original surfaces; it does not invent cut-wall caps.
    /// </summary>
    public static class TutorialGapExcavator
    {
        [Serializable] public sealed class CutReport
        {
            public string id;
            public int terrains, holeCells, meshes, colliderOnlyMeshes, unsupportedColliders;
            public bool unchangedRepeat;
            public double removedSurfaceArea, milliseconds;
            public override string ToString() => JsonUtility.ToJson(this);
        }
        [Serializable] sealed class AssetReference { public string path, guid, backup; public long localId; }
        [Serializable] sealed class ColliderRecord { public string id; public bool enabled; }
        [Serializable] sealed class MeshRecord
        {
            public string ownerId, filterId, sourceColliderId, outputPath, generatedColliderId;
            public AssetReference original;
            public List<ColliderRecord> colliders = new List<ColliderRecord>();
        }
        [Serializable] sealed class TerrainRecord
        {
            public string terrainId, colliderId, outputPath;
            public AssetReference original, originalCollider;
        }
        [Serializable] sealed class Manifest
        {
            public int version = 1;
            public string scenePath, geographyId;
            public List<MeshRecord> meshes = new List<MeshRecord>();
            public List<TerrainRecord> terrains = new List<TerrainRecord>();
            public List<string> cutIds = new List<string>();
            public List<string> cutSignatures = new List<string>();
        }
        static Transform geography;
        static string folder;
        static Manifest manifest;
        static readonly HashSet<string> ownedPaths = new HashSet<string>();
        const string ManifestName = "TutorialGapRestore.json";

        public static void Begin(string assetFolder, Transform geographyRoot)
        {
            RequireEditMode();
            if (!geographyRoot || geographyRoot.name != "10_Geography_EDIT_TERRAIN_HERE")
                throw new ArgumentException("Pass the authored 10_Geography_EDIT_TERRAIN_HERE root; other roots are not excavated.");
            if (string.IsNullOrEmpty(geographyRoot.gameObject.scene.path))
                throw new InvalidOperationException("Save the scene before excavation so the restoration manifest has persistent object identities.");
            assetFolder = NormalizeFolder(assetFolder);
            EnsureFolder(assetFolder); EnsureFolder(assetFolder + "/Originals");
            var previous = LoadManifest(assetFolder);
            ownedPaths.Clear();
            if (previous != null)
            {
                if (previous.scenePath != geographyRoot.gameObject.scene.path || previous.geographyId != Id(geographyRoot))
                    throw new InvalidOperationException("This excavation folder belongs to another scene/root. Choose a separate asset folder.");
                RememberOwned(previous);
                RestoreManifest(previous);
            }
            geography = geographyRoot; folder = assetFolder;
            manifest = new Manifest { scenePath = geographyRoot.gameObject.scene.path, geographyId = Id(geographyRoot) };
            SaveManifest();
        }

        public static CutReport Cut(Transform frame, Bounds localBounds, string id)
        {
            RequireEditMode();
            if (!geography || manifest == null) throw new InvalidOperationException("Call Begin(assetFolder, geographyRoot) before Cut.");
            if (!frame || Vector3.Distance(frame.lossyScale, Vector3.one) > .0001f || Vector3.Dot(frame.up, Vector3.up) < .99999f)
                throw new ArgumentException("Cut frame must have world unit scale and only yaw rotation.");
            if (localBounds.size.x <= 0 || localBounds.size.y <= 0 || localBounds.size.z <= 0 || string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Cut needs a non-empty id and three positive bounds dimensions.");
            string signature = Hash(frame.localToWorldMatrix.ToString("R") + localBounds.center.ToString("R") + localBounds.size.ToString("R"));
            int previousCut = manifest.cutIds.IndexOf(id);
            if (previousCut >= 0)
            {
                if (manifest.cutSignatures != null && previousCut < manifest.cutSignatures.Count && manifest.cutSignatures[previousCut] == signature)
                    return new CutReport { id = id, unchangedRepeat = true };
                throw new InvalidOperationException("Cut id already has different bounds. To move/rebuild cuts, call Begin again and replay the boxes from originals.");
            }
            var timer = Stopwatch.StartNew();
            var report = new CutReport { id = id };
            Matrix4x4 worldToCut = frame.worldToLocalMatrix;
            var cut = Box(localBounds);
            foreach (var terrain in geography.GetComponentsInChildren<Terrain>(true))
                CutTerrain(terrain, worldToCut, frame.localToWorldMatrix, localBounds, cut, report);
            foreach (var filter in geography.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || !filter.GetComponent<MeshRenderer>() || filter.GetComponentInParent<Rigidbody>()) continue;
                if (!Overlaps(filter.sharedMesh.bounds, worldToCut * filter.transform.localToWorldMatrix, localBounds)) continue;
                var result = CutMesh(filter.sharedMesh, worldToCut * filter.transform.localToWorldMatrix, localBounds, out double removed);
                if (!result) continue;
                var record = FindFilter(Id(filter));
                if (record != null && filter.sharedMesh != AssetDatabase.LoadAssetAtPath<Mesh>(record.outputPath))
                { Object.DestroyImmediate(result); throw new InvalidOperationException("An excavated mesh was changed or undone. Call Begin and rebuild cuts from the restored originals."); }
                if (record == null)
                {
                    record = new MeshRecord { ownerId = Id(filter.gameObject), filterId = Id(filter) };
                    record.original = Backup(filter.sharedMesh, "Mesh_" + Hash(record.filterId));
                    foreach (var collider in filter.GetComponents<Collider>())
                        if (!collider.isTrigger) record.colliders.Add(new ColliderRecord { id = Id(collider), enabled = collider.enabled });
                    record.outputPath = OutputPath("Mesh_" + Hash(record.filterId));
                    manifest.meshes.Add(record);
                    SaveManifest();
                }
                var saved = StoreMesh(result, record.outputPath);
                Undo.RecordObject(filter, "Excavate tutorial gap"); filter.sharedMesh = saved; EditorUtility.SetDirty(filter);
                ReplaceCollision(record, filter.gameObject, saved);
                report.meshes++; report.removedSurfaceArea += removed;
                SaveManifest();
            }
            var earlierColliderOnlyRecords = manifest.meshes.FindAll(value => !string.IsNullOrEmpty(value.sourceColliderId));
            // Collider-only rocks/road volumes still need real openings even if their visible mesh lives elsewhere.
            foreach (var collider in geography.GetComponentsInChildren<Collider>(true))
            {
                if (collider is TerrainCollider || collider.isTrigger || collider.attachedRigidbody || IsTrackedCollider(Id(collider))) continue;
                if (!collider.enabled) continue;
                if (!(collider is BoxCollider) && !(collider is MeshCollider))
                {
                    if (Overlaps(collider.bounds, worldToCut, localBounds)) report.unsupportedColliders++;
                    continue;
                }
                Mesh source = collider is MeshCollider originalMeshCollider ? originalMeshCollider.sharedMesh : MakeBox((BoxCollider)collider);
                if (!source) continue;
                bool temporarySource = collider is BoxCollider;
                try
                {
                    if (!Overlaps(source.bounds, worldToCut * collider.transform.localToWorldMatrix, localBounds)) continue;
                    var result = CutMesh(source, worldToCut * collider.transform.localToWorldMatrix, localBounds, out double removed);
                    if (!result) continue;
                    var record = new MeshRecord { ownerId = Id(collider.gameObject), sourceColliderId = Id(collider) };
                    record.original = Backup(source, "Collider_" + Hash(record.sourceColliderId));
                    record.colliders.Add(new ColliderRecord { id = Id(collider), enabled = collider.enabled });
                    record.outputPath = OutputPath("Collider_" + Hash(record.sourceColliderId));
                    manifest.meshes.Add(record);
                    SaveManifest();
                    var saved = StoreMesh(result, record.outputPath);
                    ReplaceCollision(record, collider.gameObject, saved);
                    report.colliderOnlyMeshes++; report.removedSurfaceArea += removed;
                    SaveManifest();
                }
                finally { if (temporarySource && source) Object.DestroyImmediate(source); }
            }
            // Previously generated collider-only meshes receive subsequent cuts too; originals stay disabled.
            foreach (var record in earlierColliderOnlyRecords)
            {
                if (string.IsNullOrEmpty(record.sourceColliderId)) continue;
                var owner = Resolve<GameObject>(record.ownerId);
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(record.outputPath);
                if (!owner || !source || !Overlaps(source.bounds, worldToCut * owner.transform.localToWorldMatrix, localBounds)) continue;
                var result = CutMesh(source, worldToCut * owner.transform.localToWorldMatrix, localBounds, out double removed);
                if (!result) continue;
                var saved = StoreMesh(result, record.outputPath); ReplaceCollision(record, owner, saved);
                report.colliderOnlyMeshes++; report.removedSurfaceArea += removed;
            }
            manifest.cutIds.Add(id); manifest.cutSignatures.Add(signature); SaveManifest();
            EditorSceneManager.MarkSceneDirty(geography.gameObject.scene);
            timer.Stop(); report.milliseconds = timer.Elapsed.TotalMilliseconds;
            if (report.unsupportedColliders > 0)
                UnityEngine.Debug.LogWarning("Tutorial gap " + id + " intersects " + report.unsupportedColliders + " unsupported static collider(s); inspect these before claiming the gap is passable.");
            return report;
        }

        public static string Restore(string assetFolder)
        {
            RequireEditMode(); assetFolder = NormalizeFolder(assetFolder);
            var saved = LoadManifest(assetFolder);
            if (saved == null) return "No excavation manifest exists in " + assetFolder;
            RestoreManifest(saved);
            AssetDatabase.SaveAssets();
            if (folder == assetFolder) { geography = null; manifest = null; }
            return "Restored " + saved.terrains.Count + " terrain(s) and " + saved.meshes.Count + " mesh/collider object(s). Derived backup assets are retained.";
        }

        [MenuItem("Coastal Temple/Tests/Tutorial Gap Geometry")]
        public static void RunChecksMenu() => UnityEngine.Debug.Log(RunChecks());

        public static string RunChecks()
        {
            RequireEditMode();
            var results = new List<string>(); var temporary = new List<Object>();
            void Check(bool value, string label)
            { results.Add((value ? "PASS " : "FAIL ") + label); if (!value) throw new InvalidOperationException(string.Join("\n", results)); }
            double Area(Mesh mesh, int submesh)
            {
                double area = 0; var vertices = mesh.vertices; var triangles = mesh.GetTriangles(submesh);
                for (int i = 0; i < triangles.Length; i += 3)
                    area += Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).magnitude * .5;
                return area;
            }
            try
            {
                var source = new Mesh { name = "TemporaryGapSource" }; temporary.Add(source);
                source.vertices = new[] { new Vector3(0, 0, 0), new Vector3(4, 0, 0), new Vector3(0, 0, 4), new Vector3(-4, 0, 0), new Vector3(-2, 0, 0), new Vector3(-4, 0, 2) };
                var normals = new Vector3[6]; var tangent = new Vector4[6]; var colors = new Color[6]; var uv = new Vector2[6]; var uv2 = new List<Vector4>();
                for (int i = 0; i < 6; i++) { normals[i] = Vector3.up; tangent[i] = new Vector4(1, 0, 0, -1); colors[i] = Color.yellow; uv[i] = new Vector2(source.vertices[i].x, source.vertices[i].z); uv2.Add(new Vector4(uv[i].x, uv[i].y, 3, 4)); }
                source.normals = normals; source.tangents = tangent; source.colors = colors; source.uv = uv; source.SetUVs(3, uv2);
                source.subMeshCount = 2; source.SetTriangles(new[] { 0, 2, 1 }, 0); source.SetTriangles(new[] { 3, 5, 4 }, 1); source.RecalculateBounds();
                var bounds = new Bounds(new Vector3(1.5f, 0, 1.5f), new Vector3(1, 2, 1));
                var cut = CutMesh(source, Matrix4x4.identity, bounds, out double removed); temporary.Add(cut);
                Check(cut && Math.Abs(removed - 1) < .00001 && Math.Abs(Area(cut, 0) - 7) < .00001, "crossing large triangle loses only the box interior");
                Check(cut.subMeshCount == 2 && Math.Abs(Area(cut, 1) - 2) < .00001, "untouched submesh preserves its original surface area and material slot");
                Check(source.vertexCount == 6 && source.GetTriangles(0).Length == 3 && Math.Abs(Area(source, 0) - 8) < .00001, "source mesh is unchanged");
                bool preserved = true; var cutPositions = cut.vertices; var cutUv = cut.uv; var cutUv3 = new List<Vector4>(); cut.GetUVs(3, cutUv3);
                for (int i = 0; i < cutPositions.Length; i++) preserved &= Mathf.Abs(cutUv[i].x - cutPositions[i].x) < .00001f
                    && Mathf.Abs(cutUv[i].y - cutPositions[i].z) < .00001f && Mathf.Abs(cutUv3[i].z - 3) < .00001f && Mathf.Abs(cutUv3[i].w - 4) < .00001f;
                Check(preserved, "intersections preserve primary UV and a four-dimensional secondary UV channel");
                preserved = true;
                for (int i = 0; i < cut.vertexCount; i++) preserved &= Vector3.Dot(cut.normals[i], Vector3.up) > .9999f && cut.tangents[i].w == -1 && cut.colors[i] == Color.yellow;
                Check(preserved, "normals tangent handedness and vertex colors are preserved");
                var repeat = CutMesh(cut, Matrix4x4.identity, bounds, out double repeatedRemoval); if (repeat) temporary.Add(repeat);
                Check(!repeat || (repeatedRemoval < .00001 && Math.Abs(Area(repeat, 0) - 7) < .00001), "repeating a mesh cut is stable within float storage tolerance");
                var yaw = Matrix4x4.Rotate(Quaternion.Euler(0, 90, 0));
                var rotated = CutMesh(source, yaw, new Bounds(new Vector3(1.5f, 0, -1.5f), bounds.size), out double rotatedRemoval); temporary.Add(rotated);
                Check(rotated && Math.Abs(rotatedRemoval - 1) < .00001, "yaw-frame cutting preserves the intended local area");
                var contact = CutMesh(source, Matrix4x4.identity, new Bounds(new Vector3(4.5f, 0, .5f), new Vector3(1, 2, 1)), out _);
                if (contact) temporary.Add(contact);
                Check(!contact, "a box touching one vertex does not alter the mesh");
                var colliderObject = new GameObject("TemporaryGapCollider"); temporary.Add(colliderObject);
                var collider = colliderObject.AddComponent<MeshCollider>(); collider.sharedMesh = cut; Physics.SyncTransforms();
                Check(!collider.Raycast(new Ray(new Vector3(1.5f, 2, 1.5f), Vector3.down), out _, 4), "real MeshCollider has no hidden support inside the cut");
                Check(collider.Raycast(new Ray(new Vector3(.5f, 2, .5f), Vector3.down), out _, 4), "real MeshCollider preserves support outside the cut");
                var terrainSource = new TerrainData { heightmapResolution = 33, size = new Vector3(8, 10, 8) }; temporary.Add(terrainSource);
                var heights = new float[33, 33]; for (int z = 0; z < 33; z++) for (int x = 0; x < 33; x++) heights[z, x] = .5f;
                terrainSource.SetHeights(0, 0, heights);
                var terrainCopy = Object.Instantiate(terrainSource); temporary.Add(terrainCopy);
                var terrainBounds = new Bounds(new Vector3(4, 5, 4), new Vector3(1.8f, 2, 1.8f));
                var patch = BuildHolePatch(terrainCopy, Matrix4x4.identity, terrainBounds, Box(terrainBounds), out int x0, out int z0, out int cells);
                Check(cells > 0, "terrain cells crossing the cut are found even when the box misses cell corners");
                terrainCopy.SetHoles(x0, z0, patch);
                bool[,] originalHoles = terrainSource.GetHoles(0, 0, terrainSource.holesResolution, terrainSource.holesResolution);
                bool originalIntact = true; foreach (bool solid in originalHoles) originalIntact &= solid;
                Check(originalIntact && !terrainCopy.IsHole(0, 0), "copied terrain holes leave the shared original and distant terrain intact");
                BuildHolePatch(terrainCopy, Matrix4x4.identity, terrainBounds, Box(terrainBounds), out _, out _, out int repeatedCells);
                Check(repeatedCells == 0, "repeating a terrain cut changes no additional cells");
                var above = new Bounds(new Vector3(4, 8, 4), new Vector3(2, 1, 2));
                BuildHolePatch(terrainSource, Matrix4x4.identity, above, Box(above), out _, out _, out int highCells);
                Check(highCells == 0, "a cutter above the terrain surface does not delete cells below it");
                terrainCopy.SetHoles(3, 18, new bool[1, 1]);
                var rectangularBounds = new Bounds(new Vector3(2, 5, 5), new Vector3(2, 2, .5f));
                var rectangularPatch = BuildHolePatch(terrainCopy, Matrix4x4.identity, rectangularBounds, Box(rectangularBounds), out int rectangleX, out int rectangleZ, out int rectangleCells);
                Check(rectangularPatch.GetLength(0) == 5 && rectangularPatch.GetLength(1) == 11 && rectangleCells > 0,
                    "rectangular terrain patches use height then width rather than the affected Unity GetHoles allocation order");
                Check(!rectangularPatch[18 - rectangleZ, 3 - rectangleX], "rectangular cuts preserve an existing hole outside the cutter in the padded patch");
                terrainCopy.SetHoles(rectangleX, rectangleZ, rectangularPatch);
                Check(terrainCopy.IsHole(8, 20) && !terrainCopy.IsHole(20, 8), "a rectangular patch changes its intended cells without transposing the cut");
                BuildHolePatch(terrainCopy, Matrix4x4.identity, rectangularBounds, Box(rectangularBounds), out _, out _, out int repeatedRectangleCells);
                Check(repeatedRectangleCells == 0, "repeating a rectangular terrain cut preserves cumulative holes");
                var edgeBounds = new Bounds(new Vector3(8, 5, 8), new Vector3(1, 2, .5f));
                var edgePatch = BuildHolePatch(terrainCopy, Matrix4x4.identity, edgeBounds, Box(edgeBounds), out int edgeX, out int edgeZ, out int edgeCells);
                Check(edgeCells > 0 && edgeX + edgePatch.GetLength(1) <= terrainCopy.holesResolution
                    && edgeZ + edgePatch.GetLength(0) <= terrainCopy.holesResolution,
                    "rectangular edge patches stay within the last valid holes cell");
                terrainCopy.SetHoles(edgeX, edgeZ, edgePatch);
                Check(terrainCopy.IsHole(31, 31) && !terrainSource.IsHole(31, 31), "edge cuts remove the last cell only on the copied terrain");
                return string.Join("\n", results) + "\n" + results.Count + " gap geometry checks passed; scene asset restoration still requires authoring verification.";
            }
            finally { for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i]) Object.DestroyImmediate(temporary[i]); }
        }

        public static Mesh CutMesh(Mesh source, Matrix4x4 meshToCut, Bounds bounds, out double removedArea)
        {
            if (!source.isReadable) throw new InvalidOperationException("Excavation needs a readable source mesh: " + source.name);
            removedArea = 0;
            var positions = source.vertices; var normals = source.normals; var tangents = source.tangents; var colors = source.colors;
            var outputPositions = new List<Vector3>(positions);
            var outputNormals = normals.Length == positions.Length ? new List<Vector3>(normals) : null;
            var outputTangents = tangents.Length == positions.Length ? new List<Vector4>(tangents) : null;
            var outputColors = colors.Length == positions.Length ? new List<Color>(colors) : null;
            var uv = new List<Vector4>[8]; var outputUv = new List<Vector4>[8];
            for (int channel = 0; channel < 8; channel++)
            {
                uv[channel] = new List<Vector4>(); source.GetUVs(channel, uv[channel]);
                if (uv[channel].Count == positions.Length) outputUv[channel] = new List<Vector4>(uv[channel]);
            }
            var cutPositions = new Vector3[positions.Length];
            for (int i = 0; i < positions.Length; i++) cutPositions[i] = meshToCut.MultiplyPoint3x4(positions[i]);
            var submeshes = new List<int>[source.subMeshCount]; var cutter = Box(bounds);
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
            {
                if (source.GetTopology(submesh) != MeshTopology.Triangles) throw new InvalidOperationException("Only triangle mesh submeshes can be excavated: " + source.name);
                var input = source.GetTriangles(submesh); var output = submeshes[submesh] = new List<int>(input.Length + 48);
                for (int t = 0; t < input.Length; t += 3)
                {
                    int ia = input[t], ib = input[t + 1], ic = input[t + 2];
                    var a = Vertex(cutPositions[ia], 1, 0, 0); var b = Vertex(cutPositions[ib], 0, 1, 0); var c = Vertex(cutPositions[ic], 0, 0, 1);
                    if (!TriangleBoundsOverlap(a, b, c, cutter)) { output.Add(ia); output.Add(ib); output.Add(ic); continue; }
                    var pieces = G.Subtract(a, b, c, cutter, out double removed);
                    if (removed == 0) { output.Add(ia); output.Add(ib); output.Add(ic); continue; }
                    removedArea += removed;
                    int Index(G.Vertex vertex)
                    {
                        if (vertex.A > 1 - 1e-12) return ia;
                        if (vertex.B > 1 - 1e-12) return ib;
                        if (vertex.C > 1 - 1e-12) return ic;
                        float wa = (float)vertex.A, wb = (float)vertex.B, wc = (float)vertex.C;
                        int index = outputPositions.Count;
                        outputPositions.Add(positions[ia] * wa + positions[ib] * wb + positions[ic] * wc);
                        if (outputNormals != null) outputNormals.Add((normals[ia] * wa + normals[ib] * wb + normals[ic] * wc).normalized);
                        if (outputTangents != null)
                        {
                            Vector4 tangent = tangents[ia] * wa + tangents[ib] * wb + tangents[ic] * wc;
                            Vector3 direction = new Vector3(tangent.x, tangent.y, tangent.z).normalized;
                            outputTangents.Add(new Vector4(direction.x, direction.y, direction.z, tangent.w >= 0 ? 1 : -1));
                        }
                        if (outputColors != null) outputColors.Add(colors[ia] * wa + colors[ib] * wb + colors[ic] * wc);
                        for (int channel = 0; channel < 8; channel++)
                            if (outputUv[channel] != null) outputUv[channel].Add(uv[channel][ia] * wa + uv[channel][ib] * wb + uv[channel][ic] * wc);
                        return index;
                    }
                    foreach (var piece in pieces) { output.Add(Index(piece.A)); output.Add(Index(piece.B)); output.Add(Index(piece.C)); }
                }
            }
            if (removedArea <= 1e-12) return null;
            var result = new Mesh { name = source.name + "_TutorialGap", indexFormat = outputPositions.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            result.SetVertices(outputPositions);
            if (outputNormals != null) result.SetNormals(outputNormals);
            if (outputTangents != null) result.SetTangents(outputTangents);
            if (outputColors != null) result.SetColors(outputColors);
            for (int channel = 0; channel < 8; channel++) if (outputUv[channel] != null) result.SetUVs(channel, outputUv[channel]);
            result.subMeshCount = source.subMeshCount;
            for (int submesh = 0; submesh < submeshes.Length; submesh++) result.SetTriangles(submeshes[submesh], submesh, false);
            if (outputNormals == null) result.RecalculateNormals();
            result.RecalculateBounds();
            return result;
        }

        static void CutTerrain(Terrain terrain, Matrix4x4 worldToCut, Matrix4x4 cutToWorld, Bounds bounds, G.Box cutter, CutReport report)
        {
            var data = terrain.terrainData; if (!data) return;
            var existingRecord = manifest.terrains.Find(value => value.terrainId == Id(terrain));
            if (existingRecord != null && data != AssetDatabase.LoadAssetAtPath<TerrainData>(existingRecord.outputPath))
                throw new InvalidOperationException("An excavated terrain was changed or undone. Call Begin before rebuilding; shared TerrainData will never be written.");
            Matrix4x4 terrainToCut = worldToCut * terrain.transform.localToWorldMatrix;
            if (!Overlaps(new Bounds(data.size * .5f, data.size), terrainToCut, bounds)) return;
            var terrainCutBounds = TransformBounds(bounds, terrain.transform.worldToLocalMatrix * cutToWorld);
            var holes = BuildHolePatch(data, terrainToCut, terrainCutBounds, cutter, out int x0, out int z0, out int changed);
            if (changed == 0) return;
            var record = existingRecord;
            if (record == null)
            {
                var collider = terrain.GetComponent<TerrainCollider>();
                if (collider && collider.terrainData && collider.terrainData != data)
                    throw new InvalidOperationException("Terrain and TerrainCollider use different data on " + terrain.name + "; align their authored sources before excavating this object.");
                record = new TerrainRecord { terrainId = Id(terrain), colliderId = collider ? Id(collider) : null };
                record.original = Backup(data, "Terrain_" + Hash(record.terrainId));
                record.originalCollider = collider && collider.terrainData ? record.original : null;
                record.outputPath = OutputPath("Terrain_" + Hash(record.terrainId));
                var clone = Object.Instantiate(data); clone.name = data.name + "_TutorialGap";
                data = StoreAsset(clone, record.outputPath); manifest.terrains.Add(record); SaveManifest();
                Undo.RecordObject(terrain, "Excavate tutorial terrain"); terrain.terrainData = data;
                if (collider && record.originalCollider != null) { Undo.RecordObject(collider, "Excavate tutorial terrain collision"); collider.terrainData = data; EditorUtility.SetDirty(collider); }
            }
            data.SetHoles(x0, z0, holes); EditorUtility.SetDirty(data); EditorUtility.SetDirty(terrain); terrain.Flush();
            report.terrains++; report.holeCells += changed; SaveManifest();
        }

        static bool[,] BuildHolePatch(TerrainData data, Matrix4x4 terrainToCut, Bounds terrainCutBounds, G.Box cutter, out int x0, out int z0, out int changed)
        {
            int resolution = data.holesResolution;
            x0 = Mathf.Clamp(Mathf.FloorToInt(terrainCutBounds.min.x / data.size.x * resolution) - 1, 0, resolution - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt(terrainCutBounds.max.x / data.size.x * resolution) + 1, 0, resolution - 1);
            z0 = Mathf.Clamp(Mathf.FloorToInt(terrainCutBounds.min.z / data.size.z * resolution) - 1, 0, resolution - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt(terrainCutBounds.max.z / data.size.z * resolution) + 1, 0, resolution - 1);
            // Unity 6000.5.9f1 GetHoles allocates [width,height], unlike SetHoles' [height,width] contract.
            // Read individual cells so rectangular patches have an explicit layout and preserve prior holes.
            var holes = new bool[z1 - z0 + 1, x1 - x0 + 1];
            changed = 0;
            for (int z = z0; z <= z1; z++) for (int x = x0; x <= x1; x++)
            {
                if (data.IsHole(x, z)) continue;
                holes[z - z0, x - x0] = true;
                G.Vertex Corner(int dx, int dz)
                {
                    float u = (float)(x + dx) / resolution, v = (float)(z + dz) / resolution;
                    var position = terrainToCut.MultiplyPoint3x4(new Vector3(u * data.size.x, data.GetInterpolatedHeight(u, v), v * data.size.z));
                    return Vertex(position, 0, 0, 0);
                }
                var a = Corner(0, 0); var b = Corner(1, 0); var c = Corner(1, 1); var d = Corner(0, 1);
                // Test both terrain diagonals conservatively. Any real intersecting surface triangle clears the whole hole cell.
                bool intersects = Intersects(a, b, c, cutter) || Intersects(a, c, d, cutter)
                    || Intersects(a, b, d, cutter) || Intersects(b, c, d, cutter);
                if (!intersects) continue;
                holes[z - z0, x - x0] = false; changed++;
            }
            return holes;
        }

        static bool Intersects(G.Vertex a, G.Vertex b, G.Vertex c, G.Box box)
        { if (!TriangleBoundsOverlap(a, b, c, box)) return false; G.Subtract(a, b, c, box, out double area); return area > 1e-12; }
        static bool TriangleBoundsOverlap(G.Vertex a, G.Vertex b, G.Vertex c, G.Box box) =>
            Math.Max(a.X, Math.Max(b.X, c.X)) >= box.MinX && Math.Min(a.X, Math.Min(b.X, c.X)) <= box.MaxX &&
            Math.Max(a.Y, Math.Max(b.Y, c.Y)) >= box.MinY && Math.Min(a.Y, Math.Min(b.Y, c.Y)) <= box.MaxY &&
            Math.Max(a.Z, Math.Max(b.Z, c.Z)) >= box.MinZ && Math.Min(a.Z, Math.Min(b.Z, c.Z)) <= box.MaxZ;
        static G.Vertex Vertex(Vector3 value, double a, double b, double c) => new G.Vertex(value.x, value.y, value.z, a, b, c);
        static G.Box Box(Bounds b) => new G.Box(b.min.x, b.min.y, b.min.z, b.max.x, b.max.y, b.max.z);
        static bool Overlaps(Bounds source, Matrix4x4 toCut, Bounds cutter) => TransformBounds(source, toCut).Intersects(cutter);
        static Bounds TransformBounds(Bounds source, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(source.min), Vector3.zero);
            for (int corner = 1; corner < 8; corner++) result.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                (corner & 1) == 0 ? source.min.x : source.max.x, (corner & 2) == 0 ? source.min.y : source.max.y,
                (corner & 4) == 0 ? source.min.z : source.max.z)));
            return result;
        }

        static void ReplaceCollision(MeshRecord record, GameObject owner, Mesh mesh)
        {
            bool needsCollision = false;
            foreach (var previous in record.colliders)
            {
                var collider = Resolve<Collider>(previous.id);
                if (!collider) continue;
                needsCollision |= previous.enabled;
                Undo.RecordObject(collider, "Replace excavated collision"); collider.enabled = false; EditorUtility.SetDirty(collider);
            }
            if (!needsCollision) return;
            MeshCollider generated = Resolve<MeshCollider>(record.generatedColliderId);
            foreach (var candidate in owner.GetComponents<MeshCollider>())
                if (!record.colliders.Exists(value => value.id == Id(candidate)) && candidate.sharedMesh
                    && AssetDatabase.GetAssetPath(candidate.sharedMesh) == record.outputPath) { generated = candidate; break; }
            bool hasTriangles = mesh.triangles.Length > 0;
            if (!generated && !hasTriangles) return;
            if (!generated) generated = Undo.AddComponent<MeshCollider>(owner);
            record.generatedColliderId = Id(generated);
            Undo.RecordObject(generated, "Update excavated collision");
            generated.enabled = false; generated.convex = false; generated.sharedMesh = null;
            if (hasTriangles) { generated.sharedMesh = mesh; generated.enabled = true; }
            EditorUtility.SetDirty(generated);
        }
        static Mesh MakeBox(BoxCollider box)
        {
            var mesh = new Mesh { name = "OriginalBoxCollision" };
            var vertices = new Vector3[8];
            for (int i = 0; i < 8; i++) vertices[i] = box.center + Vector3.Scale(box.size * .5f,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            mesh.vertices = vertices;
            mesh.triangles = new[] { 0, 2, 1, 1, 2, 3, 4, 5, 6, 5, 7, 6, 0, 1, 4, 1, 5, 4, 2, 6, 3, 3, 6, 7, 0, 4, 2, 2, 4, 6, 1, 3, 5, 3, 7, 5 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        static MeshRecord FindFilter(string id) => manifest.meshes.Find(value => value.filterId == id);
        static bool IsTrackedCollider(string id)
        {
            foreach (var record in manifest.meshes)
            {
                if (record.generatedColliderId == id || record.colliders.Exists(value => value.id == id)) return true;
                var collider = Resolve<MeshCollider>(id);
                if (collider && collider.sharedMesh && AssetDatabase.GetAssetPath(collider.sharedMesh) == record.outputPath) return true;
            }
            return false;
        }
        static void RestoreManifest(Manifest saved)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(saved.scenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Open the original scene before restoring terrain cuts: " + saved.scenePath);
            // Validate every surviving object's backup before changing any scene references.
            foreach (var record in saved.terrains)
                if (Resolve<Terrain>(record.terrainId) && !LoadAsset<TerrainData>(record.original))
                    throw new InvalidOperationException("Cannot restore terrain: both its original asset and saved backup are missing.");
            foreach (var record in saved.meshes)
                if (Resolve<MeshFilter>(record.filterId) && !LoadAsset<Mesh>(record.original))
                    throw new InvalidOperationException("Cannot restore mesh: both its original asset and saved backup are missing.");
            foreach (var record in saved.terrains)
            {
                var terrain = Resolve<Terrain>(record.terrainId); if (!terrain) continue;
                Undo.RecordObject(terrain, "Restore tutorial terrain"); terrain.terrainData = LoadAsset<TerrainData>(record.original); EditorUtility.SetDirty(terrain);
                var collider = Resolve<TerrainCollider>(record.colliderId);
                if (collider) { Undo.RecordObject(collider, "Restore tutorial terrain collision"); collider.terrainData = LoadAsset<TerrainData>(record.originalCollider); EditorUtility.SetDirty(collider); }
                terrain.Flush();
            }
            foreach (var record in saved.meshes)
            {
                var owner = Resolve<GameObject>(record.ownerId); if (!owner) continue;
                var filter = Resolve<MeshFilter>(record.filterId);
                if (filter) { Undo.RecordObject(filter, "Restore tutorial mesh"); filter.sharedMesh = LoadAsset<Mesh>(record.original); EditorUtility.SetDirty(filter); }
                foreach (var collider in owner.GetComponents<MeshCollider>())
                    if (Id(collider) == record.generatedColliderId || (!record.colliders.Exists(value => value.id == Id(collider)) && collider.sharedMesh
                        && AssetDatabase.GetAssetPath(collider.sharedMesh) == record.outputPath)) Undo.DestroyObjectImmediate(collider);
                foreach (var state in record.colliders)
                {
                    var collider = Resolve<Collider>(state.id);
                    if (collider) { Undo.RecordObject(collider, "Restore original collision"); collider.enabled = state.enabled; EditorUtility.SetDirty(collider); }
                }
            }
            EditorSceneManager.MarkSceneDirty(scene);
        }
        static AssetReference Reference(Object value)
        {
            if (!value) return null;
            var reference = new AssetReference { path = AssetDatabase.GetAssetPath(value) };
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out reference.guid, out reference.localId);
            return reference;
        }
        static AssetReference Backup<T>(T source, string name) where T : Object
        {
            var reference = Reference(source);
            reference.backup = ReservePath(folder + "/Originals/TutorialGap_" + name + ".asset");
            var clone = Object.Instantiate(source); clone.name = source.name + "_OriginalBackup";
            StoreAsset(clone, reference.backup); return reference;
        }
        static T LoadAsset<T>(AssetReference reference) where T : Object
        {
            if (reference == null) return null;
            string path = string.IsNullOrEmpty(reference.guid) ? reference.path : AssetDatabase.GUIDToAssetPath(reference.guid);
            if (!string.IsNullOrEmpty(path))
                foreach (var value in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (value is T typed && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localId)
                        && localId == reference.localId) return typed;
            return string.IsNullOrEmpty(reference.backup) ? null : AssetDatabase.LoadAssetAtPath<T>(reference.backup);
        }
        static T StoreAsset<T>(T value, string path) where T : Object
        {
            if (value is Mesh mesh) return (T)(Object)StoreMesh(mesh, path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing) { EditorUtility.CopySerialized(value, existing); Object.DestroyImmediate(value); EditorUtility.SetDirty(existing); return existing; }
            AssetDatabase.CreateAsset(value, path); ownedPaths.Add(path); return value;
        }
        static Mesh StoreMesh(Mesh value, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                if (value == existing) return existing;
                MeshAssetWriter.CopyMeshBuffers(value, existing);
                Object.DestroyImmediate(value);
                return existing;
            }
            value.UploadMeshData(false);
            AssetDatabase.CreateAsset(value, path); ownedPaths.Add(path); return value;
        }
        static string OutputPath(string name) => ReservePath(folder + "/TutorialGap_" + name + ".asset");
        static string ReservePath(string path)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) && !ownedPaths.Contains(path)) path = AssetDatabase.GenerateUniqueAssetPath(path);
            ownedPaths.Add(path); return path;
        }
        static void RememberOwned(Manifest saved)
        {
            foreach (var record in saved.meshes) { ownedPaths.Add(record.outputPath); if (record.original != null) ownedPaths.Add(record.original.backup); }
            foreach (var record in saved.terrains) { ownedPaths.Add(record.outputPath); if (record.original != null) ownedPaths.Add(record.original.backup); }
        }
        static string Id(Object value) => value ? GlobalObjectId.GetGlobalObjectIdSlow(value).ToString() : null;
        static T Resolve<T>(string id) where T : Object => !string.IsNullOrEmpty(id) && GlobalObjectId.TryParse(id, out var parsed)
            ? GlobalObjectId.GlobalObjectIdentifierToObjectSlow(parsed) as T : null;
        static string Hash(string value)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").Substring(0, 16); }
        static string NormalizeFolder(string value)
        {
            value = (value ?? "").Replace('\\', '/').TrimEnd('/');
            if (!value.StartsWith("Assets/", StringComparison.Ordinal) || value.Contains("..")) throw new ArgumentException("Excavation output must be a dedicated folder below Assets.");
            return value;
        }
        static void EnsureFolder(string path)
        { if (AssetDatabase.IsValidFolder(path)) return; int slash = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, slash)); AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1)); }
        static Manifest LoadManifest(string assetFolder)
        { string path = assetFolder + "/" + ManifestName; return File.Exists(path) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(path)) : null; }
        static void SaveManifest()
        { File.WriteAllText(folder + "/" + ManifestName, JsonUtility.ToJson(manifest, true), new UTF8Encoding(false)); }
        static void RequireEditMode()
        { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Tutorial excavation only runs in Edit mode; it does no runtime work."); }
    }
}
#endif
