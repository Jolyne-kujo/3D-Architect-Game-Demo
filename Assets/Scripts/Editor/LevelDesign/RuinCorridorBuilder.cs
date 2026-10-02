#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    /// <summary>Builds editable, stepped ruins beside a world-space route; never builds the road or terrain.</summary>
    public static class RuinCorridorBuilder
    {
        public const string RootName = "Mountain_RuinCorridor";
        const string Kit = "Assets/Prefabs/地形建筑/单件模块/";
        const float ClearRadius = 1.5f;
        const float TurnCourtRadius = 2.8f;

        internal struct Piece
        {
            internal string AssetPath, Label, Building;
            internal int Segment, Side;
            internal Vector3 Position, Axis, Scale;
            internal float Width, Depth;
        }

        /// <summary>
        /// Route nodes are world positions and are never changed. Call only in Edit mode.
        /// Replaces only the direct child named Mountain_RuinCorridor after a successful build.
        /// The supplied parent must have unit world scale. No assets, terrain, lights or other roots are edited.
        /// </summary>
        public static GameObject Build(Transform parent, Vector3[] route)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build the ruin corridor in Edit mode.");
            if (!parent) throw new ArgumentNullException(nameof(parent));
            if ((parent.lossyScale - Vector3.one).sqrMagnitude > .0001f)
                throw new ArgumentException("The corridor parent must have unit world scale.", nameof(parent));

            var plan = CreatePlan(route);
            var assets = new Dictionary<string, GameObject>();
            foreach (var piece in plan)
                if (!assets.ContainsKey(piece.AssetPath))
                    assets.Add(piece.AssetPath, RequireAsset<GameObject>(piece.AssetPath));

            Transform previous = null;
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == RootName) { previous = parent.GetChild(i); break; }

            var root = new GameObject(RootName + "_Building");
            root.transform.SetParent(parent, true);
            try
            {
                var sections = new Transform[route.Length - 1, 2];
                for (int segment = 0; segment < route.Length - 1; segment++)
                {
                    var section = Group(root.transform, "Segment_" + (segment + 1).ToString("00") + "_SteppedBuildings", route[segment]);
                    sections[segment, 0] = Group(section, "Left_Rooms", route[segment]);
                    sections[segment, 1] = Group(section, "Right_Rooms", route[segment]);
                }
                var houses = new Dictionary<string, Transform>();
                foreach (var piece in plan)
                {
                    string houseKey = piece.Segment + ":" + piece.Side + ":" + piece.Building;
                    if (!houses.TryGetValue(houseKey, out var house))
                    {
                        house = Group(sections[piece.Segment, piece.Side < 0 ? 0 : 1], piece.Building, piece.Position);
                        houses.Add(houseKey, house);
                    }
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(assets[piece.AssetPath], house);
                    instance.name = piece.Label;
                    instance.transform.SetPositionAndRotation(piece.Position, Quaternion.FromToRotation(Vector3.right, piece.Axis));
                    instance.transform.localScale = piece.Scale;
                    // Keep the complete prefab's original limestone materials, LODs and colliders.
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                }

                var markers = Group(root.transform, "Route_Nodes_Exact_Editable", route[0]);
                for (int i = 0; i < route.Length; i++)
                {
                    string label = i == 0 ? "Entry" : i == route.Length - 1 ? "Exit" : "Open_TurnCourt";
                    var marker = Group(markers, "Node_" + i.ToString("00") + "_" + label, route[i]);
                    if (i < route.Length - 1)
                        marker.rotation = Quaternion.LookRotation(HorizontalDirection(route[i], route[i + 1]), Vector3.up);
                }
                Undo.RegisterCreatedObjectUndo(root, "Build mountain ruin corridor");
                if (previous) Undo.DestroyObjectImmediate(previous.gameObject);
                root.name = RootName;
                return root;
            }
            catch
            {
                if (root) Object.DestroyImmediate(root);
                throw;
            }
        }

        static T RequireAsset<T>(string path) where T : Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) throw new InvalidOperationException("Missing corridor asset: " + path);
            return asset;
        }

        static Transform Group(Transform parent, string name, Vector3 worldPosition)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, true);
            group.position = worldPosition;
            return group;
        }

        // Pure placement planning is separate from Unity scene mutation, so clearance can be checked before replacement.
        internal static List<Piece> CreatePlan(Vector3[] route)
        {
            ValidateRoute(route);
            var pieces = new List<Piece>();
            for (int segment = 0; segment < route.Length - 1; segment++)
            {
                Vector3 a = route[segment], b = route[segment + 1];
                Vector3 axis = HorizontalDirection(a, b);
                Vector3 right = new Vector3(axis.z, 0, -axis.x);
                float length = HorizontalLength(a, b);
                float start = segment == 0 ? 1.2f : 3.2f;
                float end = length - (segment == route.Length - 2 ? 1.2f : 3.2f);
                if (end - start < 2.5f) continue;
                int bays = Math.Max(1, (int)Math.Ceiling((end - start) / 4.2f));
                float bay = (end - start) / bays;
                for (int side = -1; side <= 1; side += 2)
                {
                    bool main = side == (segment % 2 == 0 ? -1 : 1);
                    for (int houseStart = 0, houseIndex = 0; houseStart < bays; houseStart += 3, houseIndex++)
                    {
                        int houseBays = Math.Min(3, bays - houseStart);
                        string building = "House_" + (houseIndex + 1).ToString("00") + (main ? "_Major_LimestoneHall" : "_Side_RuinRooms");
                        float front = main ? 2.15f : 2.1f;
                        float back = main ? 5.85f : 5.5f;
                        float middle = (front + back) * .5f;
                        float roomDepth = back - front;
                        float houseHeight = main ? 5.65f + (segment % 3) * .25f : 4.15f + (segment % 3) * .25f;
                        for (int room = 0; room < houseBays; room++)
                        {
                            int index = houseStart + room;
                            float station = start + (index + .5f) * bay;
                            Vector3 center = PointAt(a, b, station / length);
                            float y0 = PointAt(a, b, (station - bay * .5f) / length).y;
                            float y1 = PointAt(a, b, (station + bay * .5f) / length).y;
                            float footingBottom = Math.Min(y0, y1) - .65f;
                            float deck = Math.Max(y0, y1) + .30f;
                            float wallBase = deck - .06f;
                            string prefix = "Bay_" + (room + 1).ToString("00") + "_";

                            // The thick podium fills the slope below a terrace. The actual wall stays above its top.
                            Add(pieces, route, segment, side, building, prefix + "Deep_RoughStone_Podium", "平台/V2_G18_Platform_4m_Sheared_High_Podium",
                                AtSide(center, right, side * middle, footingBottom), axis,
                                bay + .04f, deck - footingBottom, roomDepth + .65f, 4, 1.5f, 4);

                            // One entrance per house, with broad, nearly continuous wall panels on either side.
                            bool doorway = room == (houseBays > 1 ? 1 : 0);
                            string frontAsset = doorway ? (main ? "门洞/V2_W16_Gate_4m_Clear_6m_Module" : "门洞/V2_W17_Gate_Broken_Lintel")
                                : room == 0 ? "墙体/V2_W03_Straight_4m_Full" : "墙体/V2_W04_Straight_4m_Light_Damage";
                            Add(pieces, route, segment, side, building, prefix + (doorway ? "Entrance" : "Broad_Facade"), frontAsset,
                                AtSide(center, right, side * front, wallBase), axis,
                                bay + .025f, houseHeight - (doorway ? .35f : 0), .6f, doorway ? 6 : 4, 4, .6f);

                            string backAsset = room == houseBays - 1 ? "墙体/V2_W04_Straight_4m_Light_Damage" : "墙体/V2_W03_Straight_4m_Full";
                            Add(pieces, route, segment, side, building, prefix + "Continuous_BackWall", backAsset,
                                AtSide(center, right, side * back, wallBase), axis,
                                bay + .025f, houseHeight + .25f, .75f, 4, 4, .6f);

                            // The house has two substantial end walls, not isolated fence-like piers between every bay.
                            if (room == 0 || room == houseBays - 1)
                            {
                                int count = houseBays == 1 ? 2 : 1;
                                for (int endIndex = 0; endIndex < count; endIndex++)
                                {
                                    float sign = houseBays == 1 ? (endIndex == 0 ? -1 : 1) : (room == 0 ? -1 : 1);
                                    Vector3 endPoint = center + axis * (sign * (bay * .5f - .18f));
                                    Add(pieces, route, segment, side, building, prefix + (sign < 0 ? "Lower_EndWall" : "Upper_EndWall"),
                                        main ? "墙体/V2_W04_Straight_4m_Light_Damage" : "墙体/V2_W05_Straight_4m_Breach",
                                        AtSide(endPoint, right, side * middle, wallBase), right,
                                        roomDepth + .25f, houseHeight + .1f, .6f, 4, 4, .6f);
                                }
                            }

                            if (room == 0)
                            {
                                // A rear-attached broken upper deck reads as the remnant of a storey inside the house.
                                float deckDepth = roomDepth * .78f;
                                Add(pieces, route, segment, side, building, prefix + "Broken_UpperStorey", "地板/V2_G04_Floor_4m_Missing_Corner",
                                    AtSide(center, right, side * (back - deckDepth * .5f + .12f), wallBase + (main ? 3.85f : 2.9f)), axis,
                                    bay + .08f, .30f, deckDepth, 4, .2f, 4);
                            }
                            if (room == houseBays - 1)
                            {
                                Add(pieces, route, segment, side, building, prefix + "Collapsed_Interior", "碎石/V2_G28_Debris_4m_Massive_Fallen_Stone",
                                    AtSide(center + axis * .25f, right, side * (front + 1.5f), deck - .08f), axis,
                                    2.15f, 1.05f, 2.15f, 4, 1.5f, 4);
                            }
                        }
                    }
                }
            }
            if (pieces.Count >= 150) throw new InvalidOperationException("The corridor exceeds the 149-module budget; split a longer route before building.");
            return pieces;
        }

        static void Add(List<Piece> pieces, Vector3[] route, int segment, int side, string building, string label, string asset,
            Vector3 position, Vector3 axis, float width, float height, float depth,
            float sourceWidth, float sourceHeight, float sourceDepth)
        {
            var piece = new Piece { AssetPath = Kit + asset + ".prefab", Label = label, Building = building, Segment = segment, Side = side,
                Position = position, Axis = axis, Width = width, Depth = depth,
                Scale = new Vector3(width / sourceWidth, height / sourceHeight, depth / sourceDepth) };
            for (int i = 0; i < route.Length - 1; i++)
                if (DistanceToFootprintSquared(piece, route[i], route[i + 1]) < ClearRadius * ClearRadius) return;
            for (int i = 1; i < route.Length - 1; i++)
                if (DistanceToFootprintSquared(piece, route[i], route[i]) < TurnCourtRadius * TurnCourtRadius) return;
            pieces.Add(piece);
        }

        static float DistanceToFootprintSquared(Piece piece, Vector3 start, Vector3 end)
        {
            Vector3 tangent = piece.Axis * (piece.Width * .5f);
            Vector3 across = new Vector3(piece.Axis.z, 0, -piece.Axis.x) * (piece.Depth * .5f);
            var corners = new[] { piece.Position - tangent - across, piece.Position + tangent - across,
                piece.Position + tangent + across, piece.Position - tangent + across };
            if (Inside(piece, start) || Inside(piece, end)) return 0;
            float minimum = float.MaxValue;
            for (int i = 0; i < 4; i++)
                minimum = Math.Min(minimum, SegmentDistanceSquared(start, end, corners[i], corners[(i + 1) % 4]));
            return minimum;
        }

        static bool Inside(Piece piece, Vector3 point)
        {
            float x = point.x - piece.Position.x, z = point.z - piece.Position.z;
            return Math.Abs(x * piece.Axis.x + z * piece.Axis.z) <= piece.Width * .5f &&
                   Math.Abs(x * piece.Axis.z - z * piece.Axis.x) <= piece.Depth * .5f;
        }

        static float SegmentDistanceSquared(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            float abx = b.x - a.x, abz = b.z - a.z, cdx = d.x - c.x, cdz = d.z - c.z;
            float determinant = abx * cdz - abz * cdx;
            if (Math.Abs(determinant) > .000001f)
            {
                float acx = c.x - a.x, acz = c.z - a.z;
                float t = (acx * cdz - acz * cdx) / determinant;
                float u = (acx * abz - acz * abx) / determinant;
                if (t >= 0 && t <= 1 && u >= 0 && u <= 1) return 0;
            }
            return Math.Min(Math.Min(PointSegmentSquared(a, c, d), PointSegmentSquared(b, c, d)),
                            Math.Min(PointSegmentSquared(c, a, b), PointSegmentSquared(d, a, b)));
        }

        static float PointSegmentSquared(Vector3 point, Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x, dz = b.z - a.z, denominator = dx * dx + dz * dz;
            float t = denominator < .000001f ? 0 : Math.Max(0, Math.Min(1, ((point.x - a.x) * dx + (point.z - a.z) * dz) / denominator));
            float x = point.x - a.x - dx * t, z = point.z - a.z - dz * t;
            return x * x + z * z;
        }

        static Vector3 AtSide(Vector3 point, Vector3 right, float offset, float y)
            => new Vector3(point.x + right.x * offset, y, point.z + right.z * offset);
        static Vector3 PointAt(Vector3 a, Vector3 b, float t)
            => new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
        static float HorizontalLength(Vector3 a, Vector3 b)
            => (float)Math.Sqrt((b.x - a.x) * (b.x - a.x) + (b.z - a.z) * (b.z - a.z));
        static Vector3 HorizontalDirection(Vector3 a, Vector3 b)
        {
            float length = HorizontalLength(a, b);
            return new Vector3((b.x - a.x) / length, 0, (b.z - a.z) / length);
        }
        static void ValidateRoute(Vector3[] route)
        {
            if (route == null || route.Length < 2) throw new ArgumentException("Supply at least two world-space route nodes.", nameof(route));
            for (int i = 0; i < route.Length; i++)
            {
                Vector3 p = route[i];
                if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.y) || float.IsInfinity(p.z))
                    throw new ArgumentException("Route coordinates must be finite.", nameof(route));
                if (i > 0 && HorizontalLength(route[i - 1], p) < .01f)
                    throw new ArgumentException("Consecutive route nodes need distinct horizontal positions.", nameof(route));
            }
        }
    }
}
#endif
