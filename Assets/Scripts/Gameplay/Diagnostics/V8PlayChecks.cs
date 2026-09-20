#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Tutorial;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    // Opt-in play-mode probe. Nothing runs on scene load and no scene/assets are saved.
    [AddComponentMenu("")]
    public sealed class V8PlayChecks : MonoBehaviour
    {
        [Serializable]
        public sealed class Check
        {
            public string name, detail;
            public bool passed;
            public Vector3 position, target;
            public float horizontalDistance, elapsedSeconds;
        }

        [Serializable]
        public sealed class Report
        {
            public string startedUtc, endedUtc, scene, progress, outcome, diagnostic;
            public bool running, routePassed, extraChecksPassed, recoveryChecksPassed;
            public int routeTeleports, postRouteTeleports;
            public float elapsedSeconds;
            public Vector3 currentPosition;
            public List<Check> checks = new List<Check>();
            public string scope = "Route uses real CharacterController movement and normal-frame physics, with no teleport or synthetic jump. E is exercised through TutorialJourney.Interact, including its reach/occlusion/player-active policy; physical keyboard events are not injected. Supplementary pool/recovery/mirage repositioning is permitted only after the route passes and is counted separately. Mirage assertions cover real physics raycasts, actual CharacterController standing on an above-water step, and support removal followed by swimming recovery; each is claimed only by its passing result record. Optional respawn/reentry and upper-lift recall regressions require a separate BeginRecoveryChecks call after route acceptance. Exit Play to restore the authored scene.";
        }

        static V8PlayChecks current;
        public Report Results = new Report();
        public string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath, "../Documentation/CoastalV8Verification/PlayChecks.json"));
        CoastalWalkthrough tour;
        TutorialJourney journey;
        CourtyardWalker walker;
        bool includeExtras, cancelled, controlsCaptured, originalActive, finalized, overviewRideActive;
        float startTime;
        Vector3 segmentTarget;
        WaterMirage testedMirage;
        int originalMirageAim;
        bool originalMiragePowered, mirageStateCaptured;
        Quaternion originalMirageRotation;

        public static string Begin(bool includeExtraChecks = true)
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode before starting V8PlayChecks.");
            if (current && current.Results.running) return Status();
            if (current) UnityEngine.Object.Destroy(current.gameObject);
            var host = new GameObject("__V8_PlayChecks_OptIn") { hideFlags = HideFlags.DontSave };
            current = host.AddComponent<V8PlayChecks>();
            if (!current)
            {
                UnityEngine.Object.Destroy(host);
                throw new InvalidOperationException("V8PlayChecks could not attach. Refresh Unity after moving the guarded diagnostics source into the runtime assembly.");
            }
            current.includeExtras = includeExtraChecks;
            current.StartCoroutine(current.RunSafely());
            return Status();
        }

        public static string Status()
        {
            if (!current) return "V8 play checks are not running. Call V8PlayChecks.Begin() explicitly during Play.";
            var r = current.Results;
            Vector3 position = current.walker ? current.walker.transform.position : r.currentPosition;
            return $"{r.outcome}: {r.progress}; checks={r.checks.Count}; route={r.routePassed}; extras={r.extraChecksPassed}; recovery={r.recoveryChecksPassed}; position={position}; report={current.ReportPath}";
        }

        // Deliberately separate from Begin(): the first full route is never delayed by recovery regressions.
        public static string BeginRecoveryChecks()
        {
            if (!Application.isPlaying || !current || !current.Results.routePassed)
                throw new InvalidOperationException("First accept the full route in this Play session. Recovery repositioning cannot bypass route validation.");
            if (current.Results.running) throw new InvalidOperationException("Wait for the active checks to finish before starting recovery checks.");
            current.cancelled = false; current.finalized = false;
            current.originalActive = current.walker.active; current.controlsCaptured = true; current.walker.active = false;
            current.Results.recoveryChecksPassed = false;
            current.StartCoroutine(current.RunSafely(current.CheckRecoveryRoutes()));
            return Status();
        }

        public static string Cancel()
        {
            if (current && current.Results.running) current.cancelled = true;
            return Status();
        }

        IEnumerator RunSafely(IEnumerator selectedChecks = null)
        {
            if (selectedChecks == null) { Results.startedUtc = DateTime.UtcNow.ToString("o"); startTime = Time.realtimeSinceStartup; }
            Results.endedUtc = null; Results.diagnostic = null;
            Results.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            Results.running = true; Results.outcome = "RUNNING"; Results.progress = selectedChecks == null ? "Resolve authored scene" : "Post-route recovery regressions";
            var stack = new Stack<IEnumerator>(); stack.Push(selectedChecks ?? Run());
            while (stack.Count > 0)
            {
                object next = null;
                bool move = false;
                Exception failure = null;
                try
                {
                    if (cancelled) throw new OperationCanceledException("Checks cancelled explicitly.");
                    move = stack.Peek().MoveNext();
                    if (move) next = stack.Peek().Current;
                }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Results.outcome = failure is OperationCanceledException ? "CANCELLED" : "FAIL";
                    Results.diagnostic = failure.Message + "\n" + DescribeCollisionContext();
                    Record(false, Results.progress, Results.diagnostic, segmentTarget, 0);
                    break;
                }
                if (!move) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (Results.outcome == "RUNNING") Results.outcome = "PASS";
            Finish();
        }

        IEnumerator Run()
        {
            tour = UnityEngine.Object.FindAnyObjectByType<CoastalWalkthrough>();
            Require(tour && tour.walker && tour.tutorial, "The authored CoastalWalkthrough, walker and tutorial must exist.");
            walker = tour.walker; journey = tour.tutorial;
            Require(walker.Controller && walker.Controller.enabled, "CharacterController must be enabled in Play.");
            Require(journey.stations != null && journey.stations.Length >= 4, "Four authored tutorial stations are required.");
            Require(journey.firstCurtain && journey.reversibleCurtain && journey.shortcutCurtain && journey.lift && journey.lantern, "Tutorial mechanism references are incomplete.");
            Require(!tour.playerCamera || !tour.playerCamera.IsOverview, "Start checks in walking mode, outside overview.");
            originalActive = walker.active; controlsCaptured = true; walker.active = false;
            segmentTarget = walker.transform.position;
            Record(true, "Start at authored spawn", "Input disabled only for deterministic motor intent; no route teleport. timeScale=" + Time.timeScale, segmentTarget, 0);
            Require(Mathf.Abs(Time.timeScale - 1) < .001f, "Run at normal timeScale=1 for real-time physical route evidence.");
            Require(!journey.firstCurtain.IsOpen && !journey.HasLantern, "Route must begin from a fresh Play session, before opening the first curtain or acquiring the lantern.");

            Transform first = journey.stations[0], second = journey.stations[1], third = journey.stations[2];
            var firstConsole = first.GetComponentInChildren<AimConsole>();
            var secondConsole = second.GetComponentInChildren<AimConsole>();
            var rail = third.GetComponentInChildren<PortalRailConsole>();
            Require(firstConsole && secondConsole && rail, "Authored aim and portal-rail consoles are required.");

            yield return WalkTo("01 approach first light", first.TransformPoint(new Vector3(0, .14f - 6 * .035f, -6)), .5f, 1.3f);
            yield return WalkTo("01 reach sun-lens console", first.TransformPoint(new Vector3(-1, .14f - 3 * .035f, -3)), .23f, .8f);
            Interact("01 E rotates first sun lens", firstConsole);
            yield return WaitFor("01 permanent curtain opens", () => journey.firstCurtain.IsOpen, 3);
            Require(!journey.firstCurtain.PhysicalCollider.enabled, "First curtain reported open but its physical collider is still enabled.");
            Record(true, "01 curtain is physically passable", "IsOpen=true; physical collider disabled=true.", walker.transform.position, 0);
            yield return WalkTo("01 cross opened curtain", first.TransformPoint(new Vector3(0, .14f + 2 * .035f, 2)), .35f, .8f);
            Interact("01 E collects reusable lantern within reach", journey.lantern);
            Require(journey.HasLantern && journey.LanternOn, "Collecting the lantern did not grant possession and usable light.");
            Record(true, "01 lantern acquired", "HasLantern=true; LanternOn=true; pickup used through normal E policy.", walker.transform.position, 0);

            yield return WalkTo("shore route waypoint 1", new Vector3(-71, 3, 174), .65f, 1.8f);
            yield return WalkTo("mountain route waypoint 2", new Vector3(-68, 10, 158), .65f, 1.8f);
            yield return WalkTo("mountain route waypoint 3", new Vector3(-58, 19, 143), .65f, 1.8f);
            yield return WalkTo("02 approach borrowed-light passage", second.TransformPoint(new Vector3(0, .14f - 6 * .34f, -6)), .45f, 1.25f);
            yield return WaitFor("02 temporary entrance is lit and open", () => journey.reversibleCurtain.IsOpen, 3);
            yield return WalkTo("02 enter and reach redistribution console", second.TransformPoint(new Vector3(-1, .14f, 0)), .25f, .9f);
            Interact("02 E redirects the single beam", secondConsole);
            yield return WaitFor("02 enduring exit opens and temporary entrance returns", () => journey.shortcutCurtain.IsOpen && !journey.reversibleCurtain.IsOpen, 3);
            Require(!journey.shortcutCurtain.PhysicalCollider.enabled && journey.reversibleCurtain.PhysicalCollider.enabled, "Second-stage curtain colliders do not match their open/closed states.");
            Record(true, "02 physical curtain rules differ after redirect", "Permanent exit open/collider off; temporary entrance closed/collider on.", walker.transform.position, 0);
            yield return WalkTo("02 cross enduring exit", second.TransformPoint(new Vector3(0, .14f + 5.5f * .34f, 5.5f)), .45f, 1);
            yield return WalkTo("route toward folded light", new Vector3(-35, 34, 117), .6f, 1.4f);

            yield return WalkTo("03 reach lift approach ramp", third.TransformPoint(new Vector3(0, -2.9f, -9)), .4f, .9f);
            yield return WalkTo("03 ascend authored ramp", third.TransformPoint(new Vector3(0, 1.6f, 0)), .3f, .65f);
            yield return WalkTo("03 board real lift platform", third.TransformPoint(new Vector3(0, 1.6f, 2.1f)), .25f, .5f);
            yield return Frames("03 settle on platform", 8, 0);
            Require(journey.lift.NormalizedHeight < .03f && rail.selected == 0, "Lift and rail must start at their authored bottom/stop state.");
            float riderStart = walker.transform.position.y;
            float platformStart = journey.lift.platformBody.position.y;
            Interact("03 E changes rail from stop to raise", rail);
            Require(rail.selected == 1, "Rail E interaction did not select the raise dock.");
            yield return WaitFor("03 neutral-input physical lift ride", () => journey.lift.NormalizedHeight > .97f, 10);
            yield return Frames("03 settle at upper landing", 10, 0);
            float riderRise = walker.transform.position.y - riderStart;
            float platformRise = journey.lift.platformBody.position.y - platformStart;
            Require(riderRise > 2.7f, "Platform rose but the CharacterController did not ride it: rider rise=" + F(riderRise) + "m, platform rise=" + F(platformRise) + "m.");
            Record(true, "03 rider carried by actual Rigidbody platform", "Neutral input throughout ride; no jumps/teleports. Rider rise=" + F(riderRise) + "m; platform rise=" + F(platformRise) + "m; normalized height=" + F(journey.lift.NormalizedHeight), walker.transform.position, 0);
            yield return WalkTo("03 cross upper landing", third.TransformPoint(new Vector3(0, 4.66f, 6)), .3f, .65f);
            yield return WalkTo("03 follow descent deck", third.TransformPoint(new Vector3(0, 2.75f, 16)), .4f, .8f);
            yield return WalkTo("exit folded-light structure", new Vector3(-7, 40.55f, 117), .6f, 1);
            yield return WalkTo("04 reach halfway sea reveal", new Vector3(-3, 41, 121), .55f, 1.1f);
            Require(Results.routeTeleports == 0, "Route validation cannot contain teleports.");
            Require(journey.firstCurtain.IsOpen && journey.shortcutCurtain.IsOpen, "An enduring curtain unexpectedly closed later in the route.");
            Results.routePassed = true;
            Record(true, "Full spawn-to-halfway route passed", "Actual controller movement, four E interactions across three stages, two curtain rules and a physical lift ride; route teleports=0.", walker.transform.position, 0);
            if (!includeExtras) yield break;
            yield return CheckSwimming();
            yield return CheckMirage();
            Results.extraChecksPassed = true;
            Record(true, "Supplementary checks passed", "Original-pool float/dive/ascent and optional mirage collision states passed within the declared scope.", walker.transform.position, 0);
        }

        IEnumerator WalkTo(string name, Vector3 target, float horizontalTolerance, float heightTolerance)
        {
            Results.progress = name; segmentTarget = target; Save();
            float began = Time.realtimeSinceStartup;
            while (true)
            {
                Vector3 delta = target - walker.transform.position;
                Vector3 flat = new Vector3(delta.x, 0, delta.z);
                if (flat.magnitude <= horizontalTolerance && Mathf.Abs(delta.y) <= heightTolerance) break;
                if (Time.realtimeSinceStartup - began > 20)
                    throw new InvalidOperationException("Waypoint timed out after 20 seconds; horizontal remaining=" + F(flat.magnitude) + "m; height difference=" + F(delta.y) + "m; target=" + target);
                if (flat.magnitude > horizontalTolerance)
                {
                    float wantedYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                    walker.ApplyLook(new Vector2(Mathf.DeltaAngle(walker.LookYaw, wantedYaw), 0));
                    Vector3 local = walker.transform.InverseTransformDirection(flat.normalized);
                    float magnitude = Mathf.Clamp(flat.magnitude / .8f, .25f, 1);
                    Step(new Vector2(local.x, local.z) * magnitude, 0);
                }
                else Step(Vector2.zero, 0);
                yield return null;
            }
            Step(Vector2.zero, 0);
            Record(true, name, "Reached through CharacterController movement; vertical error=" + F(target.y - walker.transform.position.y) + "m.", target, Time.realtimeSinceStartup - began);
        }

        IEnumerator WaitFor(string name, Func<bool> condition, float maximumSeconds)
        {
            Results.progress = name; segmentTarget = walker.transform.position; Save();
            float began = Time.realtimeSinceStartup;
            while (!condition())
            {
                if (Time.realtimeSinceStartup - began > maximumSeconds) throw new InvalidOperationException("Condition did not become true within " + maximumSeconds + " seconds.");
                Step(Vector2.zero, 0); yield return null;
            }
            Record(true, name, "Satisfied during real rendered/physics frames with neutral controller input.", walker.transform.position, Time.realtimeSinceStartup - began);
        }

        IEnumerator Frames(string name, int count, float swimInput, bool repeatedAscentPress = false)
        {
            Results.progress = name; segmentTarget = walker.transform.position; Save();
            float began = Time.realtimeSinceStartup;
            for (int i = 0; i < count; i++)
            {
                if (Time.realtimeSinceStartup - began > 20) throw new InvalidOperationException("Frame check exceeded 20 seconds before its required frame count completed.");
                Step(Vector2.zero, swimInput, repeatedAscentPress && i % 15 == 0);
                yield return null;
            }
            Record(true, name, "Simulated motor intent across " + count + " real frames; swim input=" + swimInput + ".", walker.transform.position, Time.realtimeSinceStartup - began);
        }

        void Interact(string name, TutorialInteractable target)
        {
            Results.progress = name; segmentTarget = target.InteractionPosition;
            float distance = Vector3.Distance(journey.InteractorPosition, target.InteractionPosition);
            bool accepted;
            walker.active = true;
            try { accepted = journey.Interact(target); }
            finally { walker.active = false; }
            Require(accepted, "Normal E policy rejected " + target.name + "; eye-to-control distance=" + F(distance) + "m; radius=" + target.useRadius + "m. The request did not bypass reach or occlusion.");
            Record(true, name, "TutorialJourney.Interact accepted; eye-to-control distance=" + F(distance) + "m, allowed radius=" + F(target.useRadius) + "m.", target.InteractionPosition, 0);
        }

        IEnumerator CheckRecoveryRoutes()
        {
            Require(Results.routePassed && Results.routeTeleports == 0, "Recovery checks require an already accepted route without teleports.");
            Require(!tour.playerCamera || !tour.playerCamera.IsOverview, "Start recovery checks in walking mode, outside overview.");
            Transform second = journey.stations[1], third = journey.stations[2];
            var secondConsole = second.GetComponentInChildren<AimConsole>();
            var rail = third.GetComponentInChildren<PortalRailConsole>();
            var outsideHandle = second.Find("Restore_EntranceLight");
            var upperHandle = third.Find("Upper_Recall_Handle");
            var entranceRelay = outsideHandle ? outsideHandle.GetComponent<TutorialInteractable>() : null;
            var upperRelay = upperHandle ? upperHandle.GetComponent<PortalConsoleRelay>() : null;
            Require(secondConsole && rail && entranceRelay && upperRelay,
                "Recovery controls are missing: expected Restore_EntranceLight and Upper_Recall_Handle in the authored second/third stations.");
            Require(journey.shortcutCurtain.IsOpen && !journey.reversibleCurtain.IsOpen,
                "Run reentry regression after the accepted route leaves its enduring exit open and temporary entrance closed.");

            Results.progress = "Recovery Home/respawn retains solved permanent openings";
            walker.ResetPosition(); Results.postRouteTeleports++;
            if (tour.playerCamera) tour.playerCamera.SnapToTarget();
            yield return Frames("Recovery settle after actual Home reset", 8, 0);
            Require(journey.HasLantern && journey.firstCurtain.IsOpen && journey.shortcutCurtain.IsOpen,
                "Home reset unexpectedly lost the lantern or an enduring opening.");
            Record(true, "Home/respawn preserves acquired and enduring state",
                "Called the real walker.ResetPosition used by Home; counted separately as a post-route reposition. Lantern and permanent openings remain.", walker.transform.position, 0);

            RepositionAfterRoute("Recovery position outside closed temporary entrance",
                second.TransformPoint(new Vector3(-1, .14f - 6 * .34f + .05f, -6)));
            yield return Frames("Recovery settle at exterior entrance handle", 8, 0);
            Interact("Recovery E restores entrance illumination from outside", entranceRelay);
            yield return WaitFor("Recovery temporary entrance reopens", () => journey.reversibleCurtain.IsOpen, 4);
            Require(!journey.reversibleCurtain.PhysicalCollider.enabled && journey.shortcutCurtain.IsOpen,
                "Exterior recovery must reopen the entry physically while preserving the enduring exit.");
            yield return WalkTo("Recovery physically reenter restored entrance",
                second.TransformPoint(new Vector3(0, .14f - 2 * .34f, -2)), .35f, .9f);
            Record(true, "Previously solved passage remains recoverable after respawn",
                "Normal exterior E interaction reopened the temporary barrier, and the real controller crossed it after Home. No collision or interaction bypass.", walker.transform.position, 0);
            yield return WalkTo("Recovery reach interior console again",
                second.TransformPoint(new Vector3(-1, .14f, 0)), .25f, .9f);
            Interact("Recovery restore original interior beam allocation", secondConsole);
            yield return WaitFor("Recovery temporary entrance closes while enduring exit stays open",
                () => !journey.reversibleCurtain.IsOpen && journey.shortcutCurtain.IsOpen, 4);

            Require(journey.lift.NormalizedHeight > .97f && rail.selected == 1,
                "Upper-lift recovery starts from the accepted route's top/raise state.");
            RepositionAfterRoute("Recovery position at upper landing recall handle",
                third.TransformPoint(new Vector3(0, 4.66f, 6)));
            yield return Frames("Recovery settle on fixed upper landing", 8, 0);
            float upperStandingHeight = walker.transform.position.y;
            Interact("Recovery E lowers lift from upper landing", upperRelay);
            Require(rail.selected == 2, "Upper handle did not advance raise to lower.");
            yield return WaitFor("Recovery platform leaves upper landing and reaches bottom",
                () => journey.lift.NormalizedHeight < .03f, 10);
            Require(Mathf.Abs(walker.transform.position.y - upperStandingHeight) < .3f,
                "Player did not remain safely on the fixed upper landing while the lift left.");
            Record(true, "Upper landing handle remains reachable with lift absent",
                "Player stayed on static landing; platform reached bottom through the normal light-driven motor.", walker.transform.position, 0);
            Interact("Recovery E selects stop from upper landing", upperRelay);
            Require(rail.selected == 0, "Upper recall lower-to-stop selection failed.");
            yield return WaitFor("Recovery moving portal reaches stop dock", () => !rail.IsMoving, 5);
            Interact("Recovery E recalls lift upward from upper landing", upperRelay);
            Require(rail.selected == 1, "Upper recall stop-to-raise selection failed.");
            yield return WaitFor("Recovery absent platform returns to upper landing",
                () => journey.lift.NormalizedHeight > .97f, 10);
            yield return WalkTo("Recovery physically reboard recalled lift",
                third.TransformPoint(new Vector3(0, 4.7f, 2.1f)), .3f, .6f);
            yield return Frames("Recovery settle on recalled platform", 8, 0);
            bool onPlatform = Physics.Raycast(walker.transform.position + Vector3.up * .35f, Vector3.down,
                out RaycastHit support, 1, ~0, QueryTriggerInteraction.Ignore)
                && support.collider.attachedRigidbody == journey.lift.platformBody;
            Require(onPlatform, "After recall, a real support ray did not find the lift Rigidbody beneath the character.");
            Record(true, "Upper lift recall prevents a stranded return route",
                "Upper E handle lowered an occupied-world platform, selected stop, recalled it upward, and the real controller reboarded its physical collider. Main route remained teleport-free; supplemental repositions=" + Results.postRouteTeleports,
                walker.transform.position, 0);
            yield return CheckOverviewRide(rail);
            Results.recoveryChecksPassed = true;
        }

        IEnumerator CheckOverviewRide(PortalRailConsole rail)
        {
            rail.Select(2);
            yield return WaitFor("Overview regression returns occupied platform to bottom",
                () => journey.lift.NormalizedHeight < .03f, 10);
            yield return Frames("Overview regression settles rider at bottom", 8, 0);
            float riderStart = walker.transform.position.y;
            float platformStart = journey.lift.platformBody.position.y;
            rail.Select(1);
            Results.progress = "Actual overview mode retains built-in rider physics";
            segmentTarget = journey.lift.TopWorld; Save();
            float began = Time.realtimeSinceStartup;
            int observedFrames = 0;
            overviewRideActive = true;
            try
            {
                tour.SetOverview(true);
                Require(!walker.active && walker.simulateWhileInactive && (!tour.playerCamera || tour.playerCamera.IsOverview),
                    "Entering real overview did not preserve inactive-player simulation.");
                // Deliberately no Step, SimulateMovement, input assignment, camera snap or synthetic key event here.
                // The regular CourtyardWalker.Update must carry the inactive player on its own.
                while (Time.realtimeSinceStartup - began < 4.25f)
                {
                    Require(!walker.active && walker.simulateWhileInactive,
                        "Overview unexpectedly enabled player input or disabled the built-in inactive motor.");
                    Results.currentPosition = walker.transform.position;
                    observedFrames++;
                    yield return null;
                }
                float riderRise = walker.transform.position.y - riderStart;
                float platformRise = journey.lift.platformBody.position.y - platformStart;
                Require(platformRise > 2.7f && journey.lift.NormalizedHeight > .97f,
                    "Overview lift did not reach its upper stop; physical platform rise=" + F(platformRise));
                Require(riderRise > 2.7f,
                    "Overview froze the player while its platform moved: rider rise=" + F(riderRise) + "; platform rise=" + F(platformRise));
                Record(true, "Actual overview mode carries player on the moving lift",
                    "Called CoastalWalkthrough.SetOverview(true), with input inactive. Observed " + observedFrames
                    + " real frames for " + F(Time.realtimeSinceStartup - began)
                    + "s without any harness motor call or synthetic keystroke; built-in Update carried rider "
                    + F(riderRise) + "m while Rigidbody rose " + F(platformRise) + "m.", walker.transform.position, Time.realtimeSinceStartup - began);
            }
            finally
            {
                if (tour) tour.SetOverview(false);
                if (walker) walker.active = false;
                overviewRideActive = false;
            }
        }

        void RepositionAfterRoute(string name, Vector3 position)
        {
            Require(Results.routePassed && Results.routeTeleports == 0, "Supplementary repositioning is forbidden before route acceptance.");
            walker.Controller.enabled = false; walker.transform.position = position; walker.Controller.enabled = true;
            Physics.SyncTransforms(); Results.postRouteTeleports++;
            if (tour.playerCamera) tour.playerCamera.SnapToTarget();
            Record(true, name, "Explicit supplementary reposition after the completed route; subsequent interactions and crossing use normal policies/physics.", position, 0);
        }

        IEnumerator CheckSwimming()
        {
            Require(Results.routePassed, "Pool reposition is forbidden before the route passes.");
            Results.progress = "Pool reposition after accepted route";
            Vector3 spawn = new Vector3(-50, tour.water.Level - 1.4f, 182);
            Require(tour.water.Sample(spawn, out float surface, out _, out float depth) && depth > 1.5f, "The prescribed original-pool point is not deep enough for surface swimming.");
            spawn.y = surface - 1.4f;
            walker.Controller.enabled = false; walker.transform.position = spawn; walker.Controller.enabled = true;
            Physics.SyncTransforms(); Results.postRouteTeleports++;
            if (tour.playerCamera) tour.playerCamera.SnapToTarget();
            Record(true, "Supplementary pool reposition", "Pool check reposition occurs after accepted route; route teleport count remains zero.", spawn, 0);
            yield return Frames("Pool initial 30 floating frames", 30, 0);
            Require(walker.Swimming, "Walker did not enter swimming at the deep-water surface.");
            yield return Frames("Pool continued 120 floating frames", 120, 0);
            Require(walker.Swimming && tour.water.Sample(walker.transform.position, out surface, out _, out _), "Walker left the original pool during neutral floating.");
            float neutralDraft = surface - walker.transform.position.y;
            Require(Mathf.Abs(neutralDraft - 1.4f) < .16f, "Neutral float height drifted: draft=" + F(neutralDraft) + "m instead of 1.4m.");
            Record(true, "Pool stable physical surface draft", "After 150 real frames, draft=" + F(neutralDraft) + "m; Swimming=true.", walker.transform.position, 0);
            float beforeDive = walker.transform.position.y;
            yield return Frames("Pool Ctrl-style deliberate dive", 90, -1);
            float descent = beforeDive - walker.transform.position.y;
            Require(walker.Swimming && descent > .55f, "Dive did not descend at least 0.55m through the real CharacterController: descent=" + F(descent));
            Record(true, "Pool dive changes physical depth", "Feet descended " + F(descent) + "m.", walker.transform.position, 0);
            yield return Frames("Pool Space-style ascent with repeated presses", 240, 1, true);
            Require(walker.Swimming && tour.water.Sample(walker.transform.position, out surface, out _, out _), "Holding ascent incorrectly exited surface-swim state.");
            float surfacedDraft = surface - walker.transform.position.y;
            Require(Mathf.Abs(surfacedDraft - 1.4f) < .17f, "Ascent did not settle at its bounded float height; draft=" + F(surfacedDraft));
            float maximumOvershoot = 0;
            Results.progress = "Pool held ascent cannot repeatedly jump above surface"; Save();
            float holdBegan = Time.realtimeSinceStartup;
            for (int i = 0; i < 120; i++)
            {
                Require(Time.realtimeSinceStartup - holdBegan <= 20, "Held-ascent frame check exceeded its 20-second bound.");
                Step(Vector2.zero, 1, i % 12 == 0);
                Require(tour.water.Sample(walker.transform.position, out surface, out _, out _), "Held-ascent sample left the pool.");
                maximumOvershoot = Mathf.Max(maximumOvershoot, walker.transform.position.y - (surface - 1.4f));
                Require(walker.Swimming, "Repeated ascent presses switched the swimmer into land-jump mode.");
                yield return null;
            }
            Require(maximumOvershoot < .17f, "Repeated Space presses exceeded the surface height cap by " + F(maximumOvershoot) + "m.");
            Record(true, "Pool surface ascent remains bounded", "120 additional held-ascent frames, repeated jump intents; largest float-target overshoot=" + F(maximumOvershoot) + "m.", walker.transform.position, 0);
        }

        IEnumerator CheckMirage()
        {
            testedMirage = journey.GetComponentInChildren<WaterMirage>();
            Require(testedMirage && testedMirage.source && testedMirage.stepColliders != null && testedMirage.stepColliders.Length == 3, "Authored optional mirage with three step colliders was not found.");
            originalMirageAim = testedMirage.AimIndex; originalMiragePowered = testedMirage.powered; originalMirageRotation = testedMirage.source.rotation;
            mirageStateCaptured = true;
            Results.progress = "Optional mirage 50-degree solid-state physics"; Save();
            testedMirage.SetAimState(0); yield return null; testedMirage.EvaluateNow(); Physics.SyncTransforms();
            Require(testedMirage.HasProjection && testedMirage.IsSolid, "50-degree authored aim did not create aligned solid steps: " + testedMirage.Status);
            var origins = new Vector3[testedMirage.stepColliders.Length];
            for (int i = 0; i < testedMirage.stepColliders.Length; i++)
            {
                Collider shape = testedMirage.stepColliders[i];
                Require(shape && shape.enabled, "Solid-state step collider is disabled at index " + i);
                origins[i] = shape.bounds.center + Vector3.up * 2;
                bool hit = Physics.Raycast(origins[i], Vector3.down, out RaycastHit ray, 4, ~0, QueryTriggerInteraction.Ignore);
                Require(hit && ray.collider == shape, "A real downward ray did not hit solid step " + i + "; first hit=" + (hit ? Hierarchy(ray.collider.transform) : "none"));
                Record(true, "Mirage solid step " + (i + 1) + " physically raycastable", "Hit actual authored collider at " + ray.point + ".", shape.transform.position, 0);
            }
            Collider standingStep = testedMirage.stepColliders[1];
            Vector3 standingPoint = standingStep.bounds.center;
            standingPoint.y = standingStep.bounds.max.y + .6f;
            RepositionAfterRoute("Mirage place character above middle solid step", standingPoint);
            yield return Frames("Mirage neutral physical landing on raised step", 45, 0);
            float stepTop = standingStep.bounds.max.y;
            Require(walker.Controller.isGrounded && !walker.Swimming,
                "Character did not stand on the raised mirage step: grounded=" + walker.Controller.isGrounded + "; Swimming=" + walker.Swimming);
            Require(Mathf.Abs(walker.transform.position.y - stepTop) <= .16f,
                "Standing feet do not match step top: feet=" + F(walker.transform.position.y) + "; top=" + F(stepTop));
            bool supported = Physics.Raycast(walker.transform.position + Vector3.up * .25f, Vector3.down,
                out RaycastHit standingSupport, .65f, ~0, QueryTriggerInteraction.Ignore)
                && standingSupport.collider == standingStep;
            Require(supported, "Real support ray under the standing CharacterController did not hit the middle mirage step.");
            Record(true, "Mirage solid step physically supports the actual player",
                "45 neutral real frames after landing; grounded=true, Swimming=false, feet=" + F(walker.transform.position.y)
                + "m, step top=" + F(stepTop) + "m; supporting collider=" + Hierarchy(standingSupport.collider.transform), walker.transform.position, 0);

            Results.progress = "Optional mirage 65-degree nonsolid-state physics";
            testedMirage.SetAimState(1); yield return null; testedMirage.EvaluateNow(); Physics.SyncTransforms();
            Require(!testedMirage.IsSolid, "65-degree misaligned aim left the steps solid.");
            for (int i = 0; i < testedMirage.stepColliders.Length; i++)
            {
                Collider shape = testedMirage.stepColliders[i];
                Require(!shape.enabled, "Misaligned-state step collider remains enabled at index " + i);
                bool hit = Physics.Raycast(origins[i], Vector3.down, out RaycastHit ray, 4, ~0, QueryTriggerInteraction.Ignore);
                Require(!hit || ray.collider != shape, "Real physics still raycasts disabled mirage step " + i);
            }
            Record(true, "Mirage misalignment removes physical step colliders", "65 degrees: IsSolid=false; all three colliders disabled and absent from downward rays. Status=" + testedMirage.Status, testedMirage.transform.position, 0);
            yield return Frames("Mirage unsupported player falls for 45 real frames", 45, 0);
            yield return WaitFor("Mirage removed support transitions to actual swimming", () => walker.Swimming, 5);
            yield return Frames("Mirage swimmer recovers surface buoyancy", 90, 0);
            var recoveryWater = walker.SampleWater(walker.transform.position, out float recoverySurface, out _, out _);
            Require(recoveryWater == testedMirage.water && walker.Swimming && !walker.Controller.isGrounded,
                "Player did not recover as an unsupported swimmer in the optional basin after step removal.");
            Require(walker.transform.position.y < stepTop - .75f,
                "Removed step did not produce a physical fall: previous step top=" + F(stepTop) + "; feet=" + F(walker.transform.position.y));
            float recoveryDraft = recoverySurface - walker.transform.position.y;
            Require(Mathf.Abs(recoveryDraft - 1.4f) < .2f,
                "Post-fall swimming did not regain surface draft: actual=" + F(recoveryDraft) + "m.");
            Record(true, "Mirage support loss causes falling and swimming recovery",
                "No movement input or rescue teleport after support removal; grounded=false, Swimming=true, recovered draft="
                + F(recoveryDraft) + "m in the authored optional basin.", walker.transform.position, 0);
            testedMirage.SetAimState(0); yield return null; testedMirage.EvaluateNow(); Physics.SyncTransforms();
            Require(testedMirage.IsSolid, "Returning to the correct 50-degree aim did not restore solid steps.");
            Record(true, "Mirage correct aim restores solid steps after the fall",
                "50-degree aim restored IsSolid=true after the physical support-loss/swim cycle.", testedMirage.transform.position, 0);
            testedMirage.SetAimState(originalMirageAim); testedMirage.powered = originalMiragePowered; testedMirage.source.rotation = originalMirageRotation; testedMirage.EvaluateNow();
            testedMirage = null; mirageStateCaptured = false;
        }

        void Step(Vector2 input, float swimInput, bool jumpPressed = false)
        {
            walker.active = false;
            walker.SimulateMovement(input, false, jumpPressed, swimInput, Time.deltaTime);
            if (tour.playerCamera) tour.playerCamera.SnapToTarget();
            Results.currentPosition = walker.transform.position;
        }

        void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        void Record(bool passed, string name, string detail, Vector3 target, float elapsed)
        {
            Vector3 position = walker ? walker.transform.position : Vector3.zero;
            Results.checks.Add(new Check { name = name, passed = passed, detail = detail, position = position, target = target,
                horizontalDistance = new Vector2(position.x - target.x, position.z - target.z).magnitude, elapsedSeconds = elapsed });
            Results.currentPosition = position; Save();
        }

        void Save()
        {
            Results.elapsedSeconds = Time.realtimeSinceStartup - startTime;
            string directory = Path.GetDirectoryName(ReportPath); Directory.CreateDirectory(directory);
            File.WriteAllText(ReportPath, JsonUtility.ToJson(Results, true), new UTF8Encoding(false));
            var human = new StringBuilder("# V8 Play verification\n\n");
            human.AppendLine("Result: **" + Results.outcome + "**");
            human.AppendLine("\nProgress: " + Results.progress);
            human.AppendLine("\nRoute passed: " + Results.routePassed + "; extra checks passed: " + Results.extraChecksPassed + "; optional recovery checks passed: " + Results.recoveryChecksPassed + ".");
            human.AppendLine("\nRoute teleports: " + Results.routeTeleports + "; post-route teleports: " + Results.postRouteTeleports + ".");
            human.AppendLine("\n" + Results.scope + "\n");
            foreach (var check in Results.checks) human.AppendLine("- " + (check.passed ? "PASS" : "FAIL") + " — " + check.name + ": " + check.detail.Replace("\n", " | "));
            if (!string.IsNullOrEmpty(Results.diagnostic)) human.AppendLine("\n## Failure diagnostics\n\n```text\n" + Results.diagnostic + "\n```");
            File.WriteAllText(Path.Combine(directory, "PlayChecks.md"), human.ToString(), new UTF8Encoding(false));
        }

        string DescribeCollisionContext()
        {
            if (!walker) return "Walker was not available.";
            var text = new StringBuilder();
            Vector3 feet = walker.transform.position;
            text.AppendLine("Feet=" + feet + "; target=" + segmentTarget + "; grounded=" + walker.Controller.isGrounded + "; swimming=" + walker.Swimming + "; vertical=" + F(walker.VerticalSpeed));
            var volume = walker.SampleWater(feet, out float surface, out _, out float depth);
            text.AppendLine("Water=" + (volume ? volume.name : "none") + "; surface=" + F(surface) + "; depth=" + F(depth));
            if (journey && journey.lift) text.AppendLine("Lift=" + F(journey.lift.NormalizedHeight) + "; platform=" + journey.lift.platform.position + "; moving=" + journey.lift.IsMoving);
            foreach (var shape in Physics.OverlapCapsule(feet + Vector3.up * .4f, feet + Vector3.up * 1.4f, .72f, ~0, QueryTriggerInteraction.Ignore))
                if (shape && !shape.transform.IsChildOf(walker.transform)) text.AppendLine("Nearby collider: " + Hierarchy(shape.transform) + "; bounds=" + shape.bounds + "; enabled=" + shape.enabled);
            Vector3 toward = segmentTarget - feet; toward.y = 0;
            if (toward.sqrMagnitude > .001f)
                foreach (var hit in Physics.RaycastAll(feet + Vector3.up * .7f, toward.normalized, 3, ~0, QueryTriggerInteraction.Ignore))
                    if (!hit.collider.transform.IsChildOf(walker.transform)) text.AppendLine("Forward obstacle: " + Hierarchy(hit.collider.transform) + "; distance=" + F(hit.distance) + "; normal=" + hit.normal);
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * 2, Vector3.down, 8, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(walker.transform)) text.AppendLine("Ground: " + Hierarchy(hit.collider.transform) + "; y=" + F(hit.point.y) + "; normal=" + hit.normal);
            return text.ToString();
        }

        static string Hierarchy(Transform value)
        {
            string path = value.name;
            while (value.parent) { value = value.parent; path = value.name + "/" + path; }
            return path;
        }

        static string F(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);

        void Finish()
        {
            if (finalized) return;
            finalized = true; Results.running = false; Results.endedUtc = DateTime.UtcNow.ToString("o");
            if (overviewRideActive && tour)
            {
                tour.SetOverview(false);
                if (walker) walker.active = false;
                overviewRideActive = false;
            }
            if (testedMirage && mirageStateCaptured)
            {
                testedMirage.SetAimState(originalMirageAim); testedMirage.powered = originalMiragePowered;
                if (testedMirage.source) testedMirage.source.rotation = originalMirageRotation;
                testedMirage.EvaluateNow(); testedMirage = null;
            }
            if (controlsCaptured && walker) walker.active = originalActive;
            Save();
            Debug.Log("V8PlayChecks " + Results.outcome + "; " + Results.checks.Count + " records; " + ReportPath);
        }

        void OnDisable()
        {
            if (finalized || !Results.running) return;
            Results.outcome = "STOPPED"; Results.diagnostic = "Play mode or the opt-in harness stopped before completion. Authored scene state is restored by exiting Play.";
            Finish();
        }
    }
}
#endif
