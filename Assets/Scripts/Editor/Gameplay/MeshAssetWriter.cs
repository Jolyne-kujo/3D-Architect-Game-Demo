#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoastalTemple.Editor
{
    /// <summary>Updates authored static mesh assets through the native mesh setters without replacing their identity.</summary>
    public static class MeshAssetWriter
    {
        public static void CopyMeshBuffers(Mesh source, Mesh destination)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            if (!destination) throw new ArgumentNullException(nameof(destination));
            if (source == destination) throw new ArgumentException("Source and destination must be different meshes; the source is never cleared.");
            if (!source.isReadable) throw new InvalidOperationException("Mesh buffer copying requires a readable source: " + source.name);

            // Read everything before touching the destination. Clear(false) also removes channels left by a previous version.
            var vertices = source.vertices;
            var normals = source.normals;
            var tangents = source.tangents;
            var colors = source.colors;
            var uv = new List<Vector4>[8];
            var uvDimensions = new int[8];
            for (int channel = 0; channel < uv.Length; channel++)
            {
                var attribute = (VertexAttribute)((int)VertexAttribute.TexCoord0 + channel);
                if (!source.HasVertexAttribute(attribute)) continue;
                uvDimensions[channel] = source.GetVertexAttributeDimension(attribute);
                uv[channel] = new List<Vector4>(); source.GetUVs(channel, uv[channel]);
            }
            var indices = new int[source.subMeshCount][];
            var topology = new MeshTopology[source.subMeshCount];
            var baseVertices = new int[source.subMeshCount];
            for (int submesh = 0; submesh < indices.Length; submesh++)
            {
                indices[submesh] = source.GetIndices(submesh, false);
                topology[submesh] = source.GetTopology(submesh);
                baseVertices[submesh] = source.GetSubMesh(submesh).baseVertex;
            }

            destination.Clear(false);
            destination.name = source.name;
            destination.indexFormat = source.indexFormat;
            destination.SetVertices(vertices);
            if (normals.Length != 0) destination.SetNormals(normals);
            if (tangents.Length != 0) destination.SetTangents(tangents);
            if (colors.Length != 0) destination.SetColors(colors);
            for (int channel = 0; channel < uv.Length; channel++)
            {
                if (uv[channel] == null) continue;
                if (uvDimensions[channel] <= 2)
                {
                    var values = new List<Vector2>(uv[channel].Count);
                    foreach (var value in uv[channel]) values.Add(new Vector2(value.x, value.y));
                    destination.SetUVs(channel, values);
                }
                else if (uvDimensions[channel] == 3)
                {
                    var values = new List<Vector3>(uv[channel].Count);
                    foreach (var value in uv[channel]) values.Add(new Vector3(value.x, value.y, value.z));
                    destination.SetUVs(channel, values);
                }
                else destination.SetUVs(channel, uv[channel]);
            }
            destination.subMeshCount = indices.Length;
            for (int submesh = 0; submesh < indices.Length; submesh++)
            {
                if (topology[submesh] == MeshTopology.Triangles)
                    destination.SetTriangles(indices[submesh], submesh, false, baseVertices[submesh]);
                else destination.SetIndices(indices[submesh], topology[submesh], submesh, false, baseVertices[submesh]);
            }
            destination.RecalculateBounds();
            destination.UploadMeshData(false);
            destination.MarkModified();
            EditorUtility.SetDirty(destination);
        }

        [MenuItem("Coastal Temple/Tests/Mesh Asset GPU Updates")]
        public static void RunChecksMenu() => Debug.Log(RunChecks());

        public static string RunChecks()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run mesh asset checks in Edit mode.");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                throw new InvalidOperationException("These checks require a real graphics device for native vertex/index-buffer readback.");
            var results = new List<string>();
            var temporary = new List<UnityEngine.Object>();
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/__MeshAssetWriter_Check.asset");
            bool createdAsset = false;
            void Check(bool value, string label)
            {
                results.Add((value ? "PASS " : "FAIL ") + label);
                if (!value) throw new InvalidOperationException(string.Join("\n", results));
            }
            try
            {
                var source = new Mesh { name = "TemporaryBufferSource" }; temporary.Add(source);
                source.vertices = new[] { Vector3.zero, new Vector3(4, 0, 0), new Vector3(0, 0, 4), new Vector3(4, 0, 4) };
                source.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                source.tangents = new[] { new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1), new Vector4(1, 0, 0, -1) };
                source.colors = new[] { Color.red, Color.green, Color.blue, Color.white };
                for (int channel = 0; channel < 8; channel++)
                {
                    var values = new List<Vector4>();
                    foreach (var vertex in source.vertices) values.Add(new Vector4(vertex.x / 4, vertex.z / 4, channel + .25f, channel + .75f));
                    source.SetUVs(channel, values);
                }
                source.subMeshCount = 2;
                source.SetTriangles(new[] { 0, 2, 1 }, 0); source.SetTriangles(new[] { 1, 2, 3 }, 1);
                var destination = new Mesh { name = "TemporaryPersistentBufferDestination" };
                AssetDatabase.CreateAsset(destination, path); createdAsset = true;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(destination, out string guid, out long localId);
                var owner = new GameObject("TemporaryMeshBufferRenderer") { hideFlags = HideFlags.HideAndDontSave }; temporary.Add(owner);
                owner.transform.position = new Vector3(12000, 12000, 12000);
                var filter = owner.AddComponent<MeshFilter>(); filter.sharedMesh = destination;
                owner.AddComponent<MeshRenderer>();

                void Verify(Mesh expected, string label)
                {
                    CopyMeshBuffers(expected, destination);
                    AssetDatabase.TryGetGUIDAndLocalFileIdentifier(destination, out string afterGuid, out long afterId);
                    Check(guid == afterGuid && localId == afterId && filter.sharedMesh == destination, label + " retains the existing asset GUID, local id and renderer reference");
                    Check(CpuChannelsMatch(expected, destination), label + " preserves all static vertex attributes, eight UV channels and every submesh");
                    Check(GpuBuffersMatch(destination), label + " native GPU vertex/index buffers match the current CPU mesh");
                }
                Verify(source, "Initial write");
                var first = TutorialGapExcavator.CutMesh(source, Matrix4x4.identity,
                    new Bounds(new Vector3(1, 0, 1.5f), new Vector3(1, 2, 2)), out double firstArea);
                temporary.Add(first); Check(first && firstArea > 0, "first cut removes real surface triangles");
                Verify(first, "First cut overwrites the already-uploaded asset");
                var second = TutorialGapExcavator.CutMesh(first, Matrix4x4.identity,
                    new Bounds(new Vector3(2.8f, 0, 2.5f), new Vector3(.8f, 2, 1.2f)), out double secondArea);
                temporary.Add(second); Check(second && secondArea > 0, "second distinct cut removes additional triangles");
                Verify(second, "Second cut overwrites the same asset again");
                Verify(source, "Restoring the original topology");
                int originalCount = destination.vertexCount; bool aliasRejected = false;
                try { CopyMeshBuffers(destination, destination); }
                catch (ArgumentException) { aliasRejected = true; }
                Check(aliasRejected && destination.vertexCount == originalCount && GpuBuffersMatch(destination), "source/destination alias is rejected without clearing CPU or GPU data");
                var sparse = new Mesh { name = "TemporarySparseBufferSource", indexFormat = IndexFormat.UInt32 }; temporary.Add(sparse);
                sparse.vertices = new[] { new Vector3(7, 2, 0), new Vector3(9, 2, 0), new Vector3(7, 2, 2) };
                sparse.SetTriangles(new[] { 0, 2, 1 }, 0);
                Verify(sparse, "Sparse replacement changes index format and removes old channels/submeshes");
                Check(source.vertexCount == 4 && source.GetTriangles(0).Length == 3 && source.colors.Length == 4,
                    "source geometry and attributes remain unchanged throughout repeated writes");
                return string.Join("\n", results) + "\n" + results.Count + " mesh asset checks passed, including native GPU buffer readback after consecutive persistent-asset updates.";
            }
            finally
            {
                for (int i = temporary.Count - 1; i >= 0; i--) if (temporary[i]) UnityEngine.Object.DestroyImmediate(temporary[i]);
                if (createdAsset) AssetDatabase.DeleteAsset(path);
            }
        }

        static bool CpuChannelsMatch(Mesh source, Mesh destination)
        {
            if (source.vertexCount != destination.vertexCount || source.subMeshCount != destination.subMeshCount || source.indexFormat != destination.indexFormat) return false;
            bool Equal<T>(T[] a, T[] b)
            {
                if (a.Length != b.Length) return false;
                for (int i = 0; i < a.Length; i++) if (!EqualityComparer<T>.Default.Equals(a[i], b[i])) return false;
                return true;
            }
            if (!Equal(source.vertices, destination.vertices) || !Equal(source.normals, destination.normals)
                || !Equal(source.tangents, destination.tangents) || !Equal(source.colors, destination.colors)) return false;
            for (int channel = 0; channel < 8; channel++)
            {
                var a = new List<Vector4>(); var b = new List<Vector4>(); source.GetUVs(channel, a); destination.GetUVs(channel, b);
                if (!Equal(a.ToArray(), b.ToArray())) return false;
            }
            for (int submesh = 0; submesh < source.subMeshCount; submesh++)
                if (source.GetTopology(submesh) != destination.GetTopology(submesh)
                    || !Equal(source.GetIndices(submesh), destination.GetIndices(submesh))) return false;
            return true;
        }

        static bool GpuBuffersMatch(Mesh mesh)
        {
            // Read native buffers after each overwrite; comparing two CPU arrays would miss the original failure.
            int stream = mesh.GetVertexAttributeStream(VertexAttribute.Position);
            int offset = mesh.GetVertexAttributeOffset(VertexAttribute.Position);
            using (var vertices = mesh.GetVertexBuffer(stream))
            {
                var bytes = new byte[vertices.count * vertices.stride]; vertices.GetData(bytes);
                var expected = mesh.vertices;
                for (int i = 0; i < expected.Length; i++)
                {
                    int start = i * vertices.stride + offset;
                    var actual = new Vector3(BitConverter.ToSingle(bytes, start), BitConverter.ToSingle(bytes, start + 4), BitConverter.ToSingle(bytes, start + 8));
                    if ((actual - expected[i]).sqrMagnitude > 1e-12f) return false;
                }
            }
            using (var indices = mesh.GetIndexBuffer())
            {
                var wide = mesh.indexFormat == IndexFormat.UInt32 ? new uint[indices.count] : null;
                var narrow = wide == null ? new ushort[indices.count] : null;
                if (wide != null) indices.GetData(wide); else indices.GetData(narrow);
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    int start = (int)mesh.GetIndexStart(submesh);
                    var expected = mesh.GetIndices(submesh, false);
                    for (int i = 0; i < expected.Length; i++)
                        if ((wide != null ? wide[start + i] : narrow[start + i]) != (uint)expected[i]) return false;
                }
            }
            return true;
        }
    }
}
#endif
