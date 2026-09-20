#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastalTemple.Player;
using UnityEngine;
using UnityEngine.Rendering;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    [AddComponentMenu("")]
    public sealed class ThirdPersonPlayChecks : MonoBehaviour
    {
        const string Output = "Documentation/ThirdPersonPreview";
        readonly List<string> lines = new List<string>();
        CoastalWalkthrough tour; CourtyardWalker walker; CoastalPlayerCamera rig; RiggedPlayerAnimation body;
        GameObject floor, wall; bool cleaned;
        public static string Begin()
        {
            if (!Application.isPlaying) throw new System.InvalidOperationException("Enter Play first.");
            Directory.CreateDirectory(Output);
            new GameObject("TemporaryThirdPersonPlayChecks").AddComponent<ThirdPersonPlayChecks>();
            return "Third-person movement, orbit, collision, swimming and item checks running.";
        }
        void Check(bool ok, string label) => lines.Add((ok ? "PASS " : "FAIL ") + label);
        IEnumerator Move(float duration, Vector2 input, bool fast = false, float dive = 0)
        {
            float elapsed = 0;
            while (elapsed < duration)
            {
                // Use complete rendered frame steps, as live input does. A fractional
                // final step of a few microseconds rounds movement to zero in PhysX
                // and clears CharacterController.isGrounded before the next jump.
                float step = Mathf.Min(Time.deltaTime, .05f);
                walker.SimulateMovement(input, fast, false, dive, step); elapsed += step; yield return null;
            }
        }
        void Capture(string name) => ScreenCapture.CaptureScreenshot(Output + "/" + name + ".png");
        IEnumerator Start()
        {
            tour = Object.FindFirstObjectByType<CoastalWalkthrough>(); walker = tour.walker; rig = tour.playerCamera;
            body = walker.GetComponentInChildren<RiggedPlayerAnimation>();
            Check(rig.thirdPerson, "saved scene starts in third person");
            tour.SetOverview(false); walker.enabled = false; rig.SetThirdPerson(true);
            floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "TemporaryThirdPersonFloor";
            floor.transform.position = new Vector3(-65, 149.9f, 204); floor.transform.localScale = new Vector3(80, .2f, 80);
            Physics.SyncTransforms(); walker.RespawnAt(new Vector3(-65, 150.03f, 204), 0); rig.SnapToTarget();
            yield return Move(.8f, Vector2.zero);
            Check(rig.avatar.bodyRenderers.All(r => r.enabled && r.shadowCastingMode == ShadowCastingMode.On), "complete imported body and world shadow are visible");
            Check(!rig.hands.overlayCamera.enabled && !rig.hands.model.gameObject.activeInHierarchy, "viewmodel arms and overlay remain disabled");
            var start = walker.transform.position; var heading = walker.transform.rotation;
            for (int i = 0; i < 12; i++) { walker.ApplyLook(new Vector2(30, i == 0 ? -25 : i == 11 ? 25 : 0)); yield return null; }
            Check(Vector3.Distance(start, walker.transform.position) < .001f, "360-degree orbit never translates actor");
            Check(Quaternion.Angle(heading, walker.transform.rotation) < .01f, "stationary orbit does not turn body or shift its shadow");
            Check(body.transform.localPosition.sqrMagnitude < .000001f, "animated body stays at the controller origin");
            walker.ApplyLook(new Vector2(90, 0)); yield return null;
            start = walker.transform.position; yield return Move(1.4f, Vector2.up);
            Check(walker.transform.position.x > start.x + 3 && Mathf.Abs(walker.transform.position.z - start.z) < .2f, "W moves along camera yaw independently of starting body heading");
            Check(Vector3.Dot(walker.transform.forward, Vector3.right) > .99f, "body faces actual forward movement");
            Check(body.animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Slow Run" && c.weight > .95f), "normal movement plays supplied Slow Run clip");
            Capture("01-SlowRun"); yield return Move(1.3f, Vector2.up, true);
            Check(body.animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "Fast Run" && c.weight > .95f), "sprint plays supplied Fast Run clip");
            Capture("02-FastRun"); yield return Move(.8f, Vector2.left);
            Check(Vector3.Dot(walker.transform.forward, Vector3.forward) > .98f, "sideways input turns body toward movement instead of playing forward run sideways");
            yield return Move(.5f, Vector2.zero);
            walker.SimulateMovement(Vector2.up, false, true, 0, .016f);
            yield return Move(.2f, Vector2.zero);
            Check(!walker.Controller.isGrounded && walker.VerticalSpeed > 0, "jump remains functional in third person");
            Capture("03-Jump"); yield return Move(.9f, Vector2.zero);
            var pivot = walker.transform.position + Vector3.up * rig.pivotHeight;
            var direction = (tour.view.transform.position - pivot).normalized;
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.name = "TemporaryThirdPersonWall";
            wall.transform.SetPositionAndRotation(pivot + direction * 2, Quaternion.LookRotation(direction));
            wall.transform.localScale = new Vector3(5, 5, .3f); Physics.SyncTransforms(); yield return null; yield return null;
            Check(rig.CurrentDistance > .5f && rig.CurrentDistance < 1.8f, "wall shortens follow distance before camera clips");
            Check(!Physics.CheckSphere(tour.view.transform.position, .1f, ~0, QueryTriggerInteraction.Ignore), "camera remains outside solid wall");
            float retracted = rig.CurrentDistance;
            wall.SetActive(false); Physics.SyncTransforms(); yield return null; yield return null;
            Check(rig.CurrentDistance > retracted && rig.CurrentDistance < 4, "clearance restores camera distance gradually");
            yield return Move(.9f, Vector2.zero);
            Check(rig.CurrentDistance > 4, "camera recovers full follow distance");
            tour.ResetWater(); walker.RespawnAt(tour.water.transform.position + new Vector3(0, -.8f, 0), 160); rig.SnapToTarget();
            yield return Move(3, Vector2.zero);
            walker.SampleWater(walker.transform.position + Vector3.up * .8f, out float surface, out _, out _);
            Check(walker.Swimming && body.animator.GetCurrentAnimatorStateInfo(0).IsName("Swimming"), "real pool activates full-body swimming animation");
            Check(tour.view.transform.position.y > surface + .25f, "surface swimming keeps third-person view above water");
            Capture("04-Swimming"); float feet = walker.transform.position.y;
            yield return Move(.8f, Vector2.zero, false, -1);
            Check(walker.transform.position.y < feet - .3f && rig.thirdPerson, "diving moves the swimmer while retaining follow view");
            walker.ResetPosition(); rig.SnapToTarget(); yield return Move(.8f, Vector2.zero);
            Capture("05-ShorePreview"); yield return null;
            var lantern = walker.GetComponent<PlayerLantern>(); lantern.GiveLantern(); yield return null; yield return null;
            Check(lantern.carriedLantern.transform.IsChildOf(body.transform) && lantern.carriedLantern.activeInHierarchy && lantern.LanternOn, "picked-up lantern is visible on world body with working light");
            Check(lantern.carriedLantern.GetComponentsInChildren<Renderer>().All(r => (tour.view.cullingMask & (1 << r.gameObject.layer)) != 0), "held object uses a layer rendered by third-person camera");
            rig.SetThirdPerson(false); yield return Move(.5f, Vector2.zero);
            int carry = rig.hands.animator.GetLayerIndex("Carried Lantern");
            Check(rig.hands.overlayCamera.enabled && rig.hands.animator.isInitialized && carry >= 0
                && rig.hands.animator.GetLayerWeight(carry) > .95f, "initially hidden hands initialize correctly when first-person fallback is enabled");
            Check(lantern.carriedLantern.transform.parent == lantern.firstPersonHand && lantern.carriedLantern.activeInHierarchy, "perspective fallback transfers held lantern to visible first-person hand");
            rig.SetThirdPerson(true); yield return null;
            Check(lantern.carriedLantern.transform.parent == lantern.worldHand && lantern.carriedLantern.activeInHierarchy, "returning to third person restores world-held lantern");
            tour.SetOverview(true); tour.SetOverview(false); yield return null;
            Check(rig.thirdPerson && !rig.hands.overlayCamera.enabled, "overview exit restores third-person gameplay");
            var interactor = walker.GetComponent<CoastalTemple.Interaction.PlayerInteractor>();
            Check(Vector3.Distance(interactor.InteractorPosition, walker.eye.position) < .001f, "interaction reach stays on actor instead of distant camera");
            File.WriteAllLines(Output + "/PlayChecks.txt", lines); Cleanup(); Destroy(gameObject);
        }
        void Cleanup()
        {
            if (cleaned) return; cleaned = true;
            if (floor) Destroy(floor); if (wall) Destroy(wall);
            if (walker) { walker.enabled = true; walker.ResetPosition(); tour.SetOverview(false); }
        }
        void OnDestroy() => Cleanup();
    }
}
#endif
