#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Tutorial;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    /// <summary>Read-only diagnostics. No refresh, saving, transform synchronization, movement or gameplay activation.</summary>
    public static class RuinCoastChecks
    {
        static Vector3[] Route => RuinCoastAuthoring.Route;
        const float Spacing = .45f;
        const float LaneOffset = 1f;
        const float HeightWarning = .85f;

        sealed class Report
        {
            internal readonly StringBuilder Text = new StringBuilder();
            internal readonly Dictionary<string, int> DetailCounts = new Dictionary<string, int>();
            internal int Warnings, Failures;
            internal void Line(string state, string message)
            {
                if (state == "WARN") Warnings++;
                if (state == "FAIL") Failures++;
                Text.AppendLine(state + " " + message);
            }
            internal void Detail(string kind, string message)
            {
                int count = DetailCounts.TryGetValue(kind, out int previous) ? previous : 0;
                DetailCounts[kind] = count + 1;
                if (count < 12) Line("WARN", kind + " " + message);
            }
        }
        sealed class Blocker
        {
            internal int Samples;
            internal string First;
        }

        public static string Audit()
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.name != "CoastalTemple")
                return "FAIL Open the CoastalTemple scene before running RuinCoastChecks.Audit().";
            var report = new Report();
            bool wasDirty = scene.isDirty;
            report.Line("INFO", "Ruin coast read-only audit; physics queries use the current physics snapshot. No SyncTransforms/refresh/save/play transition is performed.");
            report.Line("INFO", "Sampling checks geometry, not completed CharacterController traversal or a solved reflection puzzle.");
            var all = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Transform>(true)).ToArray();
            var active = all.Where(t => t.gameObject.activeInHierarchy).ToArray();
            var walkers = active.Select(t => t.GetComponent<CourtyardWalker>()).Where(w => w).ToArray();
            var playerRoots = walkers.Select(w => w.transform).ToArray();
            AuditHierarchy(all, active, report);
            AuditMechanisms(active, report);
            AuditRoute(scene, active, playerRoots, walkers.FirstOrDefault(), report);
            foreach (var item in report.DetailCounts.Where(p => p.Value > 12))
                report.Line("INFO", item.Key + ": " + item.Value + " anomalous samples; first 12 shown.");
            report.Line(scene.isDirty == wasDirty ? "PASS" : "FAIL", "Scene dirty flag unchanged=" + (scene.isDirty == wasDirty));
            report.Text.AppendLine("SUMMARY failures=" + report.Failures + ", warning lines=" + report.Warnings + ". INFO preview results do not imply puzzle completion.");
            return report.Text.ToString();
        }

        static void AuditHierarchy(Transform[] all, Transform[] active, Report report)
        {
            foreach (string name in new[] { "SU_ShoreTutorial", "Mountain_RuinCorridor", "Mountain_Corridor_Paving", "12_ShoreTalus_EDIT_ROCKS", "10_CompactMountain" })
            {
                var matches = active.Where(t => t.name == name).ToArray();
                report.Line(matches.Length == 1 ? "PASS" : "FAIL", name + " active count=" + matches.Length);
            }
            var shore = active.FirstOrDefault(t => t.name == "SU_ShoreTutorial");
            if (shore)
            {
                foreach (string name in new[] { "01_BrokenPaving_EDIT_MODULES", "02_RuinedWalls_NOT_COLUMNS", "03_LightPuzzles", "04_SingleFallenColumns_Guidance", "05_CollapsedRooms_And_Debris" })
                    report.Line(shore.Find(name) ? "PASS" : "FAIL", "Shore child " + name);
                var legacy = shore.GetComponentsInChildren<Transform>().Where(t => t.name.Contains("连续碰撞边界") || t.name.Contains("上层残柱") || t.name == "02_FallenRomanColumns_ROUTE_BOUNDARIES").ToArray();
                report.Line(legacy.Length == 0 ? "PASS" : "FAIL", "Active legacy stacked-column/fence objects=" + legacy.Length);
                foreach (var t in legacy.Take(8)) report.Line("INFO", Path(t));
                var guides = shore.Find("04_SingleFallenColumns_Guidance");
                if (guides)
                {
                    var shafts = guides.Cast<Transform>().Where(t => t.gameObject.activeInHierarchy).ToArray();
                    report.Line(shafts.Length == 12 ? "PASS" : "WARN", "Single fallen guide modules=" + shafts.Length + " (expected 12)");
                    int stacks = 0;
                    for (int i = 0; i < shafts.Length; i++) for (int j = i + 1; j < shafts.Length; j++)
                    {
                        if (!BoundsOf(shafts[i], out var a) || !BoundsOf(shafts[j], out var b)) continue;
                        if (Planar(a.center, b.center) < .45f && Mathf.Abs(a.center.y - b.center.y) > .35f)
                        { stacks++; report.Line("WARN", "Possible vertically stacked guides: " + Path(shafts[i]) + " / " + shafts[j].name); }
                    }
                    report.Line(stacks == 0 ? "PASS" : "WARN", "Coincident guide-center stack pairs=" + stacks);
                }
            }
            foreach (string name in new[] { "02_StoneWorkshop", "03_FoldedBridge" })
            {
                var matches = all.Where(t => t.name == name).ToArray();
                report.Line(matches.Length > 0 && matches.All(t => !t.gameObject.activeInHierarchy) ? "PASS" : "WARN",
                    "Old " + name + " total=" + matches.Length + " active=" + matches.Count(t => t.gameObject.activeInHierarchy));
            }
            foreach (var journey in active.Select(t => t.GetComponent<TutorialJourney>()).Where(j => j))
                foreach (var lesson in journey.lessons ?? Array.Empty<LessonStation>())
                    if (lesson != null && ((lesson.marker && !lesson.marker.gameObject.activeInHierarchy) ||
                        (lesson.completionCondition && !lesson.completionCondition.gameObject.activeInHierarchy)))
                        report.Line("WARN", "Tutorial lesson still references inactive content: " + lesson.name);
        }

        static void AuditMechanisms(Transform[] active, Report report)
        {
            var shore = active.FirstOrDefault(t => t.name == "SU_ShoreTutorial");
            var devices = shore ? shore.Find("03_LightPuzzles") : null;
            if (!devices) { report.Line("FAIL", "Shore light-puzzle group is missing."); return; }
            var emitters = devices.GetComponentsInChildren<LaserEmitter>(true);
            var mirrors = devices.GetComponentsInChildren<LightMirror>(true);
            var receivers = devices.GetComponentsInChildren<LightReceiver>(true);
            report.Line(emitters.Length == 2 && mirrors.Length == 4 && receivers.Length == 2 ? "PASS" : "FAIL",
                "Shore optics emitters=" + emitters.Length + " mirrors=" + mirrors.Length + " receivers=" + receivers.Length + " (expected 2/4/2)");
            foreach (var emitter in emitters)
                report.Line(emitter.Origin && emitter.MaxDistance >= 80 ? "PASS" : "WARN", emitter.name + " origin=" +
                    (emitter.Origin ? Path(emitter.Origin) : "missing") + " range=" + F(emitter.MaxDistance));
            foreach (var mirror in mirrors)
                if (!mirror.face) report.Line("FAIL", "Mirror optical face is unassigned: " + Path(mirror.transform));
            foreach (var console in devices.GetComponentsInChildren<LightMirrorConsole>(true))
                if (!console.pivot) report.Line("FAIL", "Mirror rotation pivot is unassigned: " + Path(console.transform));
            foreach (var receiver in receivers)
                report.Line(receiver.RequiredColor == ReceiverColor.Red && receiver.Latching ? "PASS" : "WARN",
                    Path(receiver.transform) + " required=" + receiver.RequiredColor + " latching=" + receiver.Latching + " opticalCollider=" + (receiver.OpticalCollider != null));

            var curtain = devices.Find("StoneCurtain_SU");
            bool opensCurtain = false;
            foreach (var receiver in receivers)
            {
                var calls = new SerializedObject(receiver).FindProperty("Activated.m_PersistentCalls.m_Calls");
                if (calls == null) continue;
                for (int i = 0; i < calls.arraySize; i++)
                {
                    var call = calls.GetArrayElementAtIndex(i);
                    var target = call.FindPropertyRelative("m_Target").objectReferenceValue;
                    string method = call.FindPropertyRelative("m_MethodName").stringValue;
                    bool argument = call.FindPropertyRelative("m_Arguments.m_BoolArgument").boolValue;
                    bool enabled = call.FindPropertyRelative("m_CallState").enumValueIndex != 0;
                    report.Line("INFO", "Receiver event " + receiver.transform.parent.name + " -> " + (target ? target.name : "missing") + "." + method + "(" + argument + "), enabled=" + enabled);
                    if (curtain && target == curtain.gameObject && method == "SetActive" && !argument && enabled) opensCurtain = true;
                }
            }
            report.Line(curtain && opensCurtain ? "PASS" : "FAIL", "Persistent receiver -> StoneCurtain_SU.SetActive(false) link preserved=" + opensCurtain);
            if (curtain && BoundsOf(curtain, out var curtainBounds))
                report.Line("INFO", "Stone curtain world bounds=" + V(curtainBounds.size) + "; check both ends against the expanded passage.");

            int compared = 0, missing = 0, mismatched = 0, unlinked = 0;
            foreach (var renderer in devices.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is LineRenderer || renderer.GetComponent<TextMesh>()) continue;
                if (renderer.sharedMaterials.Any(m => !m)) { missing++; report.Line("WARN", "Missing mechanism material: " + Path(renderer.transform)); }
                var source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                if (!source) { unlinked++; continue; }
                compared++;
                if (!renderer.sharedMaterials.SequenceEqual(source.sharedMaterials))
                { mismatched++; report.Line("WARN", "Mechanism materials differ from linked prefab: " + Path(renderer.transform)); }
            }
            report.Line(missing == 0 && mismatched == 0 && compared > 0 ? "PASS" : "WARN",
                "Mechanism material audit: prefab renderers compared=" + compared + " missing=" + missing + " differing=" + mismatched + " unlinked=" + unlinked);

            var targets = active.Select(t => t.GetComponent<LightTarget>()).Where(t => t).ToArray();
            var sceneMirrors = active.Select(t => t.GetComponent<LightMirror>()).Where(t => t).ToArray();
            var portals = active.Select(t => t.GetComponent<LightPortal>()).Where(t => t).ToArray();
            foreach (var emitter in emitters)
            {
                var path = new List<BeamSegment>(); var reached = new List<LightTarget>();
                bool powered = emitter.Powered; var channel = emitter.Channel; int segments = emitter.SegmentCount;
                var states = receivers.Select(r => new Vector3(r.IsActive ? 1 : 0, r.IsIlluminated ? 1 : 0, r.Progress)).ToArray();
                LightPuzzleWorld.Preview(emitter, path, targets, sceneMirrors, portals, reached);
                bool unchanged = powered == emitter.Powered && channel == emitter.Channel && segments == emitter.SegmentCount;
                for (int i = 0; i < receivers.Length; i++) unchanged &= states[i] == new Vector3(receivers[i].IsActive ? 1 : 0, receivers[i].IsIlluminated ? 1 : 0, receivers[i].Progress);
                report.Line(unchanged ? "PASS" : "FAIL", "Read-only beam preview leaves source/receiver state unchanged: " + emitter.name);
                report.Line("INFO", "CURRENT ORIENTATION " + emitter.name + " segments=" + path.Count + " reached=" +
                    (reached.Count == 0 ? "none (reflection can intentionally begin unsolved)" : string.Join(", ", reached.Select(t => Path(t.transform)))) +
                    (path.Count == 0 ? "" : " end=" + V(path[path.Count - 1].End)));
            }
        }

        static void AuditRoute(Scene scene, Transform[] active, Transform[] playerRoots, CourtyardWalker walker, Report report)
        {
            var controller = walker ? walker.GetComponent<CharacterController>() : null;
            float radius = controller ? controller.radius * Mathf.Max(Mathf.Abs(controller.transform.lossyScale.x), Mathf.Abs(controller.transform.lossyScale.z)) : .3f;
            float height = controller ? controller.height * Mathf.Abs(controller.transform.lossyScale.y) : 1.8f;
            float step = controller ? controller.stepOffset : .3f;
            float slopeLimit = controller ? controller.slopeLimit : 50;
            var mountain = active.FirstOrDefault(t => t.name == "10_CompactMountain");
            var ground = active.Select(t => t.GetComponent<Collider>()).Where(c => c && c.enabled && !c.isTrigger &&
                (c is TerrainCollider || (mountain && c.transform.IsChildOf(mountain)))).ToArray();
            var groundSet = new HashSet<Collider>(ground);
            float rayTop = ground.Length == 0 ? 200 : Mathf.Max(200, ground.Max(c => c.bounds.max.y) + 5);
            var blockers = new Dictionary<string, Blocker>();
            report.Line(ground.Length > 0 ? "PASS" : "FAIL", "Ground colliders=" + ground.Length + "; only Terrain and 10_CompactMountain are road support; roofs, ruins, players excluded.");
            report.Line("INFO", "Route step=" + F(Spacing) + "m lanes=0,+/-" + F(LaneOffset) + "m capsule radius=" + F(radius) + " height=" + F(height) + " stepOffset=" + F(step) + " slopeLimit=" + F(slopeLimit));
            for (int segment = 0; segment < Route.Length - 1; segment++)
            {
                var a = Route[segment]; var b = Route[segment + 1];
                var forward = Vector3.ProjectOnPlane(b - a, Vector3.up).normalized;
                var right = Vector3.Cross(Vector3.up, forward);
                int count = Mathf.CeilToInt(Planar(a, b) / Spacing);
                foreach (float lane in new[] { 0f, -LaneOffset, LaneOffset })
                {
                    int missing = 0, badHeight = 0, steep = 0, blocked = 0;
                    float maxError = 0, maxSlope = 0, maxNormal = 0;
                    bool priorValid = false; Vector3 prior = default;
                    for (int i = 0; i <= count; i++)
                    {
                        var expected = Vector3.Lerp(a, b, (float)i / count) + right * lane;
                        string location = "segment=" + segment + " lane=" + F(lane) + " station=" + F(Planar(a, b) * i / count) + " xz=" + F(expected.x) + "," + F(expected.z);
                        if (!GroundAt(expected, ground, rayTop, out var hit))
                        { missing++; priorValid = false; report.Detail("NO_GROUND", location); continue; }
                        float error = hit.point.y - (expected.y - .09f);
                        float normalSlope = Vector3.Angle(hit.normal, Vector3.up);
                        maxError = Mathf.Max(maxError, Mathf.Abs(error)); maxNormal = Mathf.Max(maxNormal, normalSlope);
                        if (Mathf.Abs(error) > HeightWarning)
                        { badHeight++; report.Detail("GRADE_ERROR", location + " y=" + F(hit.point.y) + " delta=" + F(error) + " support=" + Path(hit.transform)); }
                        float slope = priorValid ? Mathf.Atan2(Mathf.Abs(hit.point.y - prior.y), Planar(hit.point, prior)) * Mathf.Rad2Deg : 0;
                        maxSlope = Mathf.Max(maxSlope, slope);
                        if (slope > slopeLimit || normalSlope > slopeLimit)
                        { steep++; report.Detail("STEEP", location + " along=" + F(slope) + " normal=" + F(normalSlope) + " support=" + Path(hit.transform)); }
                        prior = hit.point; priorValid = true;
                        float lift = Mathf.Max(step, .25f) + .035f;
                        var lower = hit.point + Vector3.up * (lift + radius);
                        var upper = hit.point + Vector3.up * Mathf.Max(lift + radius, height - radius);
                        bool obstructed = false;
                        foreach (var c in Physics.OverlapCapsule(lower, upper, radius * .95f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            if (!c || !c.enabled || c.gameObject.scene != scene || groundSet.Contains(c) || c is CharacterController ||
                                playerRoots.Any(p => c.transform.IsChildOf(p))) continue;
                            obstructed = true; string key = Path(c.transform) + " [" + c.GetType().Name + "]";
                            if (!blockers.TryGetValue(key, out var obstruction)) blockers[key] = obstruction = new Blocker { First = location + " groundY=" + F(hit.point.y) };
                            obstruction.Samples++;
                        }
                        if (obstructed) blocked++;
                    }
                    report.Line(missing + badHeight + steep + blocked == 0 ? "PASS" : "WARN", "ROUTE segment=" + segment + " lane=" + F(lane) + " samples=" + (count + 1) +
                        " noGround=" + missing + " gradeErrors=" + badHeight + " steep=" + steep + " blocked=" + blocked + " maxGradeError=" + F(maxError) +
                        " maxAlongSlope=" + F(maxSlope) + " maxNormalSlope=" + F(maxNormal));
                }
            }
            report.Line(blockers.Count == 0 ? "PASS" : "WARN", "Non-ground colliders intersecting walking body=" + blockers.Count + "; body query starts above stepOffset, so low paving is not counted.");
            foreach (var item in blockers.OrderByDescending(p => p.Value.Samples).Take(24))
                report.Line("WARN", "BLOCKER samples=" + item.Value.Samples + " " + item.Key + " first " + item.Value.First);
            if (blockers.Count > 24) report.Line("INFO", "Only the 24 most frequent blocker colliders are shown.");
        }

        static bool GroundAt(Vector3 expected, Collider[] colliders, float top, out RaycastHit highest)
        {
            highest = default; bool found = false;
            var ray = new Ray(new Vector3(expected.x, top, expected.z), Vector3.down);
            foreach (var collider in colliders)
            {
                var bounds = collider.bounds;
                if (expected.x < bounds.min.x || expected.x > bounds.max.x || expected.z < bounds.min.z || expected.z > bounds.max.z) continue;
                if (collider.Raycast(ray, out var hit, top + 100) && (!found || hit.point.y > highest.point.y))
                { highest = hit; found = true; }
            }
            return found;
        }
        static bool BoundsOf(Transform root, out Bounds bounds)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            bounds = default;
            if (renderers.Length == 0) return false;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return true;
        }
        static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        static string F(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        static string V(Vector3 value) => "(" + F(value.x) + "," + F(value.y) + "," + F(value.z) + ")";
        static string Path(Transform t)
        {
            if (!t) return "missing";
            string path = t.name;
            while (t.parent) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }
    }
}
#endif
