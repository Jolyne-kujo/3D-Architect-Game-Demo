#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    /// <summary>Editor-only local subdivision before carving. Returns an unsaved mesh owned by the caller.</summary>
    public static class RuinRouteMeshCut
    {
        public const float BandRadius = 4.02f;
        public const float MaximumXZEdge = 2f;
        public const int MaximumTriangles = 300000;
        public const int MaximumVertices = 650000;
        const int MaximumRounds = 24;

        internal sealed class Plan
        {
            internal readonly List<Vector3> Positions;
            internal readonly List<Vector3> World;
            internal readonly List<Vector2Int> Parents = new List<Vector2Int>();
            internal List<int>[] Submeshes;
            internal readonly Dictionary<ulong, int> Midpoints = new Dictionary<ulong, int>();
            internal Plan(Vector3[] positions, Matrix4x4 frame, List<int>[] submeshes)
            {
                Positions = new List<Vector3>(positions); World = new List<Vector3>(positions.Length);
                foreach (var p in positions) World.Add(frame.MultiplyPoint3x4(p));
                Submeshes = submeshes;
            }
        }

        // Geometric edges, rather than index pairs, propagate subdivisions across duplicated
        // vertices at UV/normal/submesh seams. Vertex attributes remain separate at those seams.
        readonly struct Edge : IEquatable<Edge>
        {
            readonly Vector3 a, b;
            internal Edge(Vector3 first, Vector3 second)
            {
                bool ordered = first.x < second.x || (first.x == second.x &&
                    (first.y < second.y || (first.y == second.y && first.z <= second.z)));
                a = ordered ? first : second; b = ordered ? second : first;
            }
            public bool Equals(Edge other) => a.Equals(other.a) && b.Equals(other.b);
            public override bool Equals(object value) => value is Edge other && Equals(other);
            public override int GetHashCode() => unchecked(a.GetHashCode() * 397 ^ b.GetHashCode());
        }

        public static Mesh Prepare(Mesh source, Transform frame)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            if (!frame) throw new ArgumentNullException(nameof(frame));
            if (!source.isReadable) throw new InvalidOperationException("Route subdivision needs readable mesh " + source.name);
            if (source.blendShapeCount != 0 || source.HasVertexAttribute(VertexAttribute.BlendWeight))
                throw new InvalidOperationException("Route subdivision supports static meshes, not skinned/blend-shape meshes: " + source.name);
            var positions = source.vertices;
            var submeshes = new List<int>[source.subMeshCount];
            for (int s = 0; s < submeshes.Length; s++)
            {
                if (source.GetTopology(s) != MeshTopology.Triangles)
                    throw new InvalidOperationException("Non-triangle submesh in " + source.name);
                submeshes[s] = new List<int>(source.GetTriangles(s));
            }
            var plan = CreatePlan(positions, frame.localToWorldMatrix, submeshes, RuinCoastAuthoring.Route);
            var normals = source.normals; var tangents = source.tangents; var colors = source.colors;
            var outputNormals = normals.Length == positions.Length && positions.Length > 0 ? new List<Vector3>(normals) : null;
            var outputTangents = tangents.Length == positions.Length && positions.Length > 0 ? new List<Vector4>(tangents) : null;
            var outputColors = colors.Length == positions.Length && positions.Length > 0 ? new List<Color>(colors) : null;
            var uv = new List<Vector4>[8]; var dimensions = new int[8];
            for (int channel = 0; channel < 8; channel++)
            {
                var values = new List<Vector4>(); source.GetUVs(channel, values);
                if (values.Count == 0) continue;
                if (values.Count != positions.Length) throw new InvalidOperationException("Incomplete UV channel in " + source.name);
                dimensions[channel] = source.GetVertexAttributeDimension((VertexAttribute)((int)VertexAttribute.TexCoord0 + channel));
                if (dimensions[channel] < 2 || dimensions[channel] > 4)
                    throw new InvalidOperationException("Unsupported UV dimension " + dimensions[channel] + " in " + source.name);
                uv[channel] = values;
            }
            foreach (var parents in plan.Parents)
            {
                int a = parents.x, b = parents.y;
                if (outputNormals != null)
                {
                    var normal = outputNormals[a] + outputNormals[b];
                    outputNormals.Add(normal.sqrMagnitude > 1e-12f ? normal.normalized : outputNormals[a]);
                }
                if (outputTangents != null)
                {
                    var sum = (outputTangents[a] + outputTangents[b]) * .5f;
                    var direction = new Vector3(sum.x, sum.y, sum.z).normalized;
                    outputTangents.Add(new Vector4(direction.x, direction.y, direction.z, sum.w >= 0 ? 1 : -1));
                }
                if (outputColors != null) outputColors.Add((outputColors[a] + outputColors[b]) * .5f);
                for (int channel = 0; channel < 8; channel++)
                    if (uv[channel] != null) uv[channel].Add((uv[channel][a] + uv[channel][b]) * .5f);
            }
            var result = new Mesh { name = source.name + "_RouteSubdivided", indexFormat = plan.Positions.Count > 65535 ? IndexFormat.UInt32 : source.indexFormat };
            try
            {
                result.SetVertices(plan.Positions);
                if (outputNormals != null) result.SetNormals(outputNormals);
                if (outputTangents != null) result.SetTangents(outputTangents);
                if (outputColors != null) result.SetColors(outputColors);
                for (int channel = 0; channel < 8; channel++)
                {
                    if (uv[channel] == null) continue;
                    if (dimensions[channel] == 2)
                    {
                        var values = new List<Vector2>(uv[channel].Count);
                        foreach (var p in uv[channel]) values.Add(new Vector2(p.x, p.y));
                        result.SetUVs(channel, values);
                    }
                    else if (dimensions[channel] == 3)
                    {
                        var values = new List<Vector3>(uv[channel].Count);
                        foreach (var p in uv[channel]) values.Add(new Vector3(p.x, p.y, p.z));
                        result.SetUVs(channel, values);
                    }
                    else result.SetUVs(channel, uv[channel]);
                }
                result.subMeshCount = plan.Submeshes.Length;
                for (int s = 0; s < plan.Submeshes.Length; s++) result.SetTriangles(plan.Submeshes[s], s, false);
                result.RecalculateBounds();
                return result;
            }
            catch { Object.DestroyImmediate(result); throw; }
        }

        // Pure planning also permits regression checks without instantiating Unity meshes or touching the scene.
        internal static Plan CreatePlan(Vector3[] positions, Matrix4x4 frame, List<int>[] submeshes, Vector3[] route)
        {
            if (positions == null || submeshes == null || route == null || route.Length < 2)
                throw new ArgumentException("Supply vertices, triangle submeshes and at least two route nodes.");
            if (positions.Length > MaximumVertices) throw Budget("input vertex", positions.Length);
            int initialTriangles = 0;
            foreach (var indices in submeshes)
            {
                if (indices == null || indices.Count % 3 != 0) throw new ArgumentException("Invalid triangle index stream.");
                initialTriangles += indices.Count / 3;
                foreach (int index in indices) if (index < 0 || index >= positions.Length) throw new ArgumentException("Triangle index is out of range.");
            }
            if (initialTriangles > MaximumTriangles) throw Budget("input triangle", initialTriangles);
            var plan = new Plan(positions, frame, submeshes);
            foreach (var p in plan.World) if (!Finite(p)) throw new ArgumentException("Non-finite transformed mesh vertex.");
            foreach (var p in route) if (!Finite(p)) throw new ArgumentException("Non-finite route node.");
            for (int round = 0; round < MaximumRounds; round++)
            {
                var marked = new HashSet<Edge>();
                foreach (var indices in plan.Submeshes)
                    for (int t = 0; t < indices.Count; t += 3)
                    {
                        int a = indices[t], b = indices[t + 1], c = indices[t + 2];
                        if (!NearRoute(plan.World[a], plan.World[b], plan.World[c], route)) continue;
                        Mark(a, b); Mark(b, c); Mark(c, a);
                    }
                if (marked.Count == 0) return plan;
                var next = new List<int>[submeshes.Length]; int triangles = 0;
                for (int s = 0; s < next.Length; s++)
                {
                    var input = plan.Submeshes[s]; var output = next[s] = new List<int>(input.Count);
                    for (int t = 0; t < input.Count; t += 3)
                    {
                        int a = input[t], b = input[t + 1], c = input[t + 2];
                        bool ab = marked.Contains(Key(a, b)), bc = marked.Contains(Key(b, c)), ca = marked.Contains(Key(c, a));
                        int mask = (ab ? 1 : 0) | (bc ? 2 : 0) | (ca ? 4 : 0);
                        int m = ab ? Midpoint(plan, a, b) : -1, n = bc ? Midpoint(plan, b, c) : -1, p = ca ? Midpoint(plan, c, a) : -1;
                        switch (mask)
                        {
                            case 0: Add(a, b, c); break;
                            case 1: Add(a, m, c); Add(m, b, c); break;
                            case 2: Add(b, n, a); Add(n, c, a); break;
                            case 4: Add(c, p, b); Add(p, a, b); break;
                            case 3: Add(b, n, m); Add(a, m, c); Add(m, n, c); break;
                            case 6: Add(c, p, n); Add(b, n, a); Add(n, p, a); break;
                            case 5: Add(a, m, p); Add(c, p, b); Add(p, m, b); break;
                            case 7: Add(a, m, p); Add(m, b, n); Add(p, n, c); Add(m, n, p); break;
                        }
                    }
                    void Add(int a, int b, int c)
                    {
                        if (++triangles > MaximumTriangles) throw Budget("output triangle", triangles);
                        output.Add(a); output.Add(b); output.Add(c);
                    }
                }
                plan.Submeshes = next;
                Edge Key(int a, int b) => new Edge(plan.Positions[a], plan.Positions[b]);
                void Mark(int a, int b)
                {
                    if (DistanceSquared(plan.World[a], plan.World[b]) > MaximumXZEdge * MaximumXZEdge)
                        marked.Add(Key(a, b));
                }
            }
            throw new InvalidOperationException("Route subdivision exceeded " + MaximumRounds + " rounds; no partial mesh was returned.");
        }

        static int Midpoint(Plan plan, int a, int b)
        {
            uint lo = (uint)Math.Min(a, b), hi = (uint)Math.Max(a, b);
            ulong key = ((ulong)lo << 32) | hi;
            if (plan.Midpoints.TryGetValue(key, out int existing)) return existing;
            if (plan.Positions.Count >= MaximumVertices) throw Budget("output vertex", plan.Positions.Count + 1);
            int index = plan.Positions.Count;
            plan.Positions.Add((plan.Positions[a] + plan.Positions[b]) * .5f);
            plan.World.Add((plan.World[a] + plan.World[b]) * .5f);
            plan.Parents.Add(new Vector2Int(a, b)); plan.Midpoints.Add(key, index);
            return index;
        }

        internal static bool NearRoute(Vector3 a, Vector3 b, Vector3 c, Vector3[] route)
        {
            double minimumX = Math.Min(a.x, Math.Min(b.x, c.x)) - BandRadius;
            double maximumX = Math.Max(a.x, Math.Max(b.x, c.x)) + BandRadius;
            double minimumZ = Math.Min(a.z, Math.Min(b.z, c.z)) - BandRadius;
            double maximumZ = Math.Max(a.z, Math.Max(b.z, c.z)) + BandRadius;
            double radiusSquared = BandRadius * BandRadius;
            for (int i = 0; i < route.Length - 1; i++)
            {
                var u = route[i]; var v = route[i + 1];
                if (Math.Max(u.x, v.x) < minimumX || Math.Min(u.x, v.x) > maximumX ||
                    Math.Max(u.z, v.z) < minimumZ || Math.Min(u.z, v.z) > maximumZ) continue;
                if (Inside(u, a, b, c) || Inside(v, a, b, c)) return true;
                if (SegmentDistanceSquared(u, v, a, b) <= radiusSquared ||
                    SegmentDistanceSquared(u, v, b, c) <= radiusSquared ||
                    SegmentDistanceSquared(u, v, c, a) <= radiusSquared) return true;
            }
            return false;
        }
        static bool Inside(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            if (Math.Abs(Cross(a, b, c)) < 1e-12) return false;
            double ab = Cross(a, b, p), bc = Cross(b, c, p), ca = Cross(c, a, p);
            return (ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0);
        }
        static double SegmentDistanceSquared(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            double abx = b.x - (double)a.x, abz = b.z - (double)a.z;
            double cdx = d.x - (double)c.x, cdz = d.z - (double)c.z;
            double determinant = abx * cdz - abz * cdx;
            if (Math.Abs(determinant) > 1e-15)
            {
                double acx = c.x - (double)a.x, acz = c.z - (double)a.z;
                double t = (acx * cdz - acz * cdx) / determinant;
                double u = (acx * abz - acz * abx) / determinant;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return 0;
            }
            return Math.Min(Math.Min(PointSegmentSquared(a, c, d), PointSegmentSquared(b, c, d)),
                Math.Min(PointSegmentSquared(c, a, b), PointSegmentSquared(d, a, b)));
        }
        static double PointSegmentSquared(Vector3 point, Vector3 a, Vector3 b)
        {
            double dx = b.x - (double)a.x, dz = b.z - (double)a.z, denominator = dx * dx + dz * dz;
            double t = denominator < 1e-20 ? 0 : Math.Max(0, Math.Min(1, ((point.x - (double)a.x) * dx + (point.z - (double)a.z) * dz) / denominator));
            double x = point.x - (double)a.x - dx * t, z = point.z - (double)a.z - dz * t;
            return x * x + z * z;
        }
        static double Cross(Vector3 a, Vector3 b, Vector3 c) => (b.x - (double)a.x) * (c.z - (double)a.z) - (b.z - (double)a.z) * (c.x - (double)a.x);
        static double DistanceSquared(Vector3 a, Vector3 b) => (a.x - (double)b.x) * (a.x - (double)b.x) + (a.z - (double)b.z) * (a.z - (double)b.z);
        static bool Finite(Vector3 p) => !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.x) && !float.IsInfinity(p.y) && !float.IsInfinity(p.z);
        static InvalidOperationException Budget(string kind, int count) => new InvalidOperationException("Route subdivision " + kind + " budget exceeded at " + count + "; no partial mesh was returned. Limits: " + MaximumTriangles + " triangles / " + MaximumVertices + " vertices.");
    }
}
#endif
