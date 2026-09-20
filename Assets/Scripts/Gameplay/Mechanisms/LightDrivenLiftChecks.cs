#if UNITY_EDITOR
using CoastalTemple.LightPuzzles;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    // Callable editor verification. Uses the real receiver geometry/state without advancing the global puzzle registry.
    public static class LightDrivenLiftChecks
    {
        public static string Run()
        {
            var report = new System.Collections.Generic.List<string>();
            GameObject root = null;
            void Check(bool good, string name)
            {
                report.Add((good ? "PASS " : "FAIL ") + name);
                if (!good) throw new System.InvalidOperationException(string.Join("\n", report));
            }
            try
            {
                root = new GameObject("TemporaryLiftChecks"); root.SetActive(false);
                root.transform.position = new Vector3(12000, 12000, 12000);
                var platform = new GameObject("Platform"); platform.transform.SetParent(root.transform, false);
                platform.AddComponent<BoxCollider>().size = new Vector3(3, .3f, 3);
                var body = platform.AddComponent<Rigidbody>(); body.isKinematic = true;
                var receiverObject = new GameObject("RaiseReceiver"); receiverObject.transform.SetParent(platform.transform, false);
                receiverObject.transform.localPosition = Vector3.up * 1.5f;
                var receiver = receiverObject.AddComponent<LightReceiver>(); receiver.OpticalSize = Vector3.one * .5f;
                receiver.RequiredHitSeconds = 0; receiver.ReturnGraceSeconds = 0; receiver.Latching = false;
                var lift = root.AddComponent<LightDrivenLift>(); lift.platform = platform.transform; lift.platformBody = body;
                lift.positionsAreLocal = true; lift.bottom = Vector3.zero; lift.top = Vector3.up * 5; lift.receiverRaise = receiver;
                root.SetActive(true); lift.Initialize();
                Check(body.isKinematic && !body.useGravity, "platform has a kinematic Rigidbody for rider contact");
                Check(Vector3.Distance(lift.BottomWorld, root.transform.position) < .001f &&
                    Vector3.Distance(lift.TopWorld, root.transform.position + Vector3.up * 5) < .001f, "local endpoints resolve through the authored lift root");
                Vector3 start = lift.BottomWorld;
                Vector3 next = LightDrivenLift.NextPosition(start, start, lift.TopWorld, 1.5f, true, false, .5f);
                Check(Mathf.Abs(next.y - start.y - .75f) < .002f, "real vector motion advances 0.75m in half a second");
                Check(LightDrivenLift.NextPosition(start, start, lift.TopWorld, 1.5f, true, true, .5f) == start, "both receivers request an immediate stop");
                Check(LightDrivenLift.NextPosition(lift.TopWorld - Vector3.up * .1f, start, lift.TopWorld, 1.5f, true, false, 1) == lift.TopWorld, "real vector motion stops exactly at upper landing");
                Vector3 beamOrigin = start + Vector3.up * 1.5f + Vector3.back * 4;
                Check(receiver.Intersect(beamOrigin, Vector3.forward, 8, out _), "lower-height beam reaches the platform receiver at bottom");
                receiver.BeginTrace(); receiver.Illuminate(); receiver.Resolve(.2f);
                lift.Simulate(.02f);
                Check(receiver.IsActive && lift.IsMoving, "an illuminated real receiver requests physical platform movement");
                receiver.enabled = false; lift.Simulate(.02f);
                Check(!lift.IsMoving, "disabled receiver cannot leave lift moving with stale active state");
                receiver.enabled = true; receiver.BeginTrace(); receiver.Resolve(.2f); lift.Simulate(.02f);
                Check(!receiver.IsActive && !lift.IsMoving, "light loss stops the lift request immediately with zero grace");
                body.position = lift.TopWorld; platform.transform.position = lift.TopWorld;
                Physics.SyncTransforms();
                Check(!receiver.Intersect(beamOrigin, Vector3.forward, 8, out _), "same beam cannot hit the receiver after it reaches upper height");
                Check(Mathf.Abs(lift.NormalizedHeight - 1) < .001f, "normalized height reaches one at the upper landing");
                return string.Join("\n", report) + "\n11 lift component checks passed; runtime rider travel still requires play-mode verification.";
            }
            finally { if (root) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif
