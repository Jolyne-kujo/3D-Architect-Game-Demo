#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using CoastalTemple.LightPuzzles;
using UnityEditor;
using UnityEngine;
using WaterCourtyard;
using CoastalTemple.Player;

namespace CoastalTemple.Portals.Editor
{
    public static class PortalRuntimeChecks
    {
        [Serializable] public class Result { public string name; public bool passed; public string detail; }
        [Serializable] public class Report { public int passed; public int failed; public List<Result> results = new List<Result>(); }
        static GameObject fixture;
        static Report report;
        [MenuItem("Coastal Temple/Tests/Portal Runtime")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            report = new Report();
            Test("laser direction follows nonuniform spatial mapping", () => {
                Pair(out var a, out var b);
                b.transform.localScale = new Vector3(2, 1, 1);
                Vector3 incoming = new Vector3(.3f, 0, -1).normalized;
                Check(a.TryTransfer(Vector3.zero, incoming, out _, out var actual), "laser did not transfer");
                Vector3 expected = new Vector3(-.6f, 0, 1).normalized;
                Check(Vector3.Distance(expected, actual) < .0001f, "scaled outgoing angle does not follow portal geometry");
            });
            Test("runtime view and traveller components exist", () => {
                var assembly = typeof(LightPortal).Assembly;
                Check(assembly.GetType("CoastalTemple.Portals.PortalSurface") != null, "real-time portal view is missing");
                Check(assembly.GetType("CoastalTemple.Portals.PortalTraveller") != null, "traveller is missing");
            });
            Test("translated rotated unequal portals round trip points and velocities", () => {
                Pair(out var a, out var b);
                a.transform.SetPositionAndRotation(new Vector3(3, 8, -4), Quaternion.Euler(34, 81, 17));
                b.transform.SetPositionAndRotation(new Vector3(-12, 2, 18), Quaternion.Euler(-47, 122, 61));
                a.transform.localScale = new Vector3(.7f, 1.2f, 2);
                b.transform.localScale = new Vector3(1.8f, .6f, 1.1f);
                Vector3 point = new Vector3(7, -2, 11), velocity = new Vector3(-6, 8, 12);
                Check(Vector3.Distance(b.MapPoint(a.MapPoint(point)), point) < .0001f, "position round trip drift");
                Check(Vector3.Distance(b.MapVector(a.MapVector(velocity)), velocity) < .0001f, "momentum round trip drift");
            });
            Test("explicit one-sided gate catches fast travellers and rejects back side", () => {
                Pair(out var a, out _);a.TwoSided=false;
                Check(a.TryCrossing(new Vector3(.2f, .3f, 20), new Vector3(.2f, .3f, -20), out float t, out var point), "fast sweep missed");
                Check(Mathf.Abs(t - .5f) < .0001f && Mathf.Abs(point.z) < .0001f, "wrong intersection");
                Check(!a.TryCrossing(Vector3.back, Vector3.forward, out _, out _), "back side accepted");
                Check(!a.TryCrossing(new Vector3(4, 0, 2), new Vector3(4, 0, -2), out _, out _), "outside aperture accepted");
            });
            Test("smaller destination aperture rejects mismatched crossing", () => {
                Pair(out var a, out var b); b.Aperture = new Vector2(.2f, .2f);
                Check(!a.TryCrossing(new Vector3(.4f, 0, 2), new Vector3(.4f, 0, -2), out _, out _), "crossing emerged beyond destination aperture");
            });
            Test("negative and degenerate scale safely disable crossing", () => {
                Pair(out var a, out var b); b.transform.localScale = new Vector3(-1, 1, 1);
                Check(!a.CanTransfer, "negative determinant accepted");
                b.transform.localScale = new Vector3(1, 0, 1);
                Check(!a.CanTransfer, "singular scale accepted");
            });
            Test("walker portal warp preserves full mapped momentum", () => {
                Pair(out var a, out var b); b.transform.rotation = Quaternion.Euler(0, 90, 0);
                var go = new GameObject("Walker"); go.transform.SetParent(fixture.transform);
                var walker = go.AddComponent<CourtyardWalker>(); walker.SendMessage("Awake");
                // Gravity plus steering creates actual controller state rather than a test-only velocity setter.
                walker.SimulateMovement(Vector2.up, true, false, 0, .04f);
                Vector3 expected = a.MapVector(walker.WorldVelocity);
                walker.WarpThroughPortal(new Vector3(8, 3, 4), a.MapRotation(go.transform.rotation), a.TransferMatrix);
                Check(Vector3.Distance(walker.WorldVelocity, expected) < .0001f, "warp reset or rotated momentum incorrectly");
                Check(walker.Controller.enabled, "controller left disabled");
            });
            Test("rigidbody warp preserves mapped linear and angular velocity", () => {
                Pair(out var a, out var b); b.transform.SetPositionAndRotation(new Vector3(12, 2, 4), Quaternion.Euler(32, 90, 17));
                var go = new GameObject("Body"); go.transform.SetParent(fixture.transform); go.transform.position = new Vector3(0, 0, -.4f);
                var body = go.AddComponent<Rigidbody>(); body.useGravity = false;
                body.linearVelocity = new Vector3(1, -2, -9); body.angularVelocity = new Vector3(1, 2, 3);
                Vector3 expected = a.MapVector(body.linearVelocity);
                Quaternion expectedRotation = a.MapRotation(body.rotation);
                Vector3 angular = expectedRotation * body.angularVelocity;
                var traveller = go.AddComponent<PortalTraveller>(); traveller.SendMessage("Awake");
                Check(traveller.WarpThrough(a), "warp rejected");
                Check(Vector3.Distance(body.linearVelocity, expected) < .0001f, "linear velocity lost");
                Check(Vector3.Distance(body.angularVelocity, angular) < .0001f, "angular velocity lost");
                Check(Quaternion.Angle(body.rotation, expectedRotation) < .02f, "orientation lost");
            });
            Test("third person camera stays continuous while actor crosses first", () => {
                Pair(out var a, out var b); b.transform.position = new Vector3(20, 0, 0);
                var go = new GameObject("Camera walker"); go.transform.SetParent(fixture.transform);
                var walker = go.AddComponent<CourtyardWalker>(); walker.SendMessage("Awake");
                var rig = go.AddComponent<CoastalPlayerCamera>(); rig.walker = walker; rig.thirdPerson = true;
                var cameraObject = new GameObject("Observer"); cameraObject.transform.SetParent(fixture.transform);
                var view = cameraObject.AddComponent<Camera>(); view.enabled = false; rig.view = view;
                view.transform.SetPositionAndRotation(new Vector3(0, 1.5f, 4), Quaternion.Euler(0, 180, 0));
                Vector3 before = view.transform.position; Quaternion rotation = view.transform.rotation;
                rig.WarpThroughPortal(a.TransferMatrix, a);
                Check(Vector3.Distance(view.transform.position, before) < .0001f, "camera jumped when actor crossed");
                Check(Quaternion.Angle(view.transform.rotation, rotation) < .02f, "camera facing snapped");
            });
            Test("registered traveller crossing keeps overshoot and does not bounce", () => {
                Pair(out var a, out var b); b.transform.position = new Vector3(20, 0, 0);
                var aSurface = a.gameObject.AddComponent<PortalSurface>(); aSurface.SendMessage("OnEnable");
                var bSurface = b.gameObject.AddComponent<PortalSurface>(); bSurface.SendMessage("OnEnable");
                var go = new GameObject("Fast traveller"); go.transform.SetParent(fixture.transform); go.transform.position = new Vector3(.2f, .3f, 2);
                var traveller = go.AddComponent<PortalTraveller>(); traveller.SendMessage("Awake"); traveller.ResetTracking();
                go.transform.position = new Vector3(.2f, .3f, -3);
                Check(traveller.EvaluateNow(), "registered traveller did not cross");
                Check(Vector3.Distance(go.transform.position, new Vector3(19.8f, .3f, 3)) < .0001f, "sweep overshoot was discarded");
                Check(!traveller.EvaluateNow() && traveller.TransferCount == 1, "exit immediately bounced traveller back");
            });
            Test("reversing during third person bridge keeps original camera space", () => {
                Pair(out var a, out var b); b.transform.position = new Vector3(20, 0, 0);
                var go = new GameObject("Reverse camera walker"); go.transform.SetParent(fixture.transform);
                var walker = go.AddComponent<CourtyardWalker>(); walker.SendMessage("Awake");
                var rig = go.AddComponent<CoastalPlayerCamera>(); rig.walker = walker; rig.thirdPerson = true;
                var cameraObject = new GameObject("Observer"); cameraObject.transform.SetParent(fixture.transform);
                var view = cameraObject.AddComponent<Camera>(); view.enabled = false; rig.view = view;
                view.transform.SetPositionAndRotation(new Vector3(0, 1.5f, 4), Quaternion.Euler(0, 180, 0));
                Vector3 before = view.transform.position;
                rig.WarpThroughPortal(a.TransferMatrix, a); rig.WarpThroughPortal(b.TransferMatrix, b);
                Check(Vector3.Distance(view.transform.position, before) < .0001f, "reverse traversal double-mapped camera");
            });
            var json = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory("Documentation/Mechanisms");
            File.WriteAllText("Documentation/Mechanisms/PortalRuntimeChecks.json", json);
            return json;
        }
        static void Pair(out LightPortal a, out LightPortal b)
        {
            var left = new GameObject("A"); left.transform.SetParent(fixture.transform);
            var right = new GameObject("B"); right.transform.SetParent(fixture.transform);
            a = left.AddComponent<LightPortal>(); b = right.AddComponent<LightPortal>();
            a.Paired = b; b.Paired = a;
        }
        static void Test(string name, Action run)
        {
            fixture = new GameObject("Portal check fixture"); fixture.hideFlags = HideFlags.DontSave;
            var result = new Result { name = name };
            try { run(); result.passed = true; result.detail = "passed"; report.passed++; }
            catch (Exception e) { result.detail = e.Message; report.failed++; }
            finally
            {
                foreach (var surface in fixture.GetComponentsInChildren<PortalSurface>()) surface.SendMessage("OnDisable");
                UnityEngine.Object.DestroyImmediate(fixture);
            }
            report.results.Add(result);
        }
        static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
#endif
