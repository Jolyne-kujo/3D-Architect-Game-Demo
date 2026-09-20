#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using UnityEngine;
using WaterCourtyard;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    // Explicit opt-in diagnostics. No authoring, mechanism setters or scene saves occur here.
    [AddComponentMenu("")]
    public sealed class V9PlayChecks : MonoBehaviour
    {
        [Serializable] public sealed class Check
        {
            public string name, detail;
            public bool passed;
            public Vector3 position, target;
            public float elapsedSeconds, horizontalDistance;
        }
        [Serializable] public sealed class Report
        {
            public string startedUtc, endedUtc, scene, outcome, progress, diagnostic;
            public bool running, routePassed, recoveryChecksPassed;
            public int routeTeleports, postRouteTeleports, postRouteRepositions, postRouteRecoveries, unexplainedDiscontinuities;
            public int interactionCount, simulatedFrames;
            public float elapsedSeconds;
            public Vector3 currentPosition;
            public List<Check> checks = new List<Check>();
            public string scope = "Main route uses CourtyardWalker.SimulateMovement/ApplyLook with no jump intent, teleport, motor setter, direct optical selection or forced bridge state. Every E action goes through PlayerInteractor.Interact with the ordinary availability, distance and obstruction policy, and the target must be the nearest reachable prompt. Real rendered/physics frames drive all mechanisms. Physical keyboard events are not injected. Game fall/sea-boundary recoveries and unexplained position discontinuities fail the main route and are counted. BeginRecoveryChecks is separate and requires a previously accepted zero-teleport route; every deliberate post-route reposition and automatic recovery is reported separately. Exit Play restores authored state.";
        }

        static V9PlayChecks current;
        public Report Results = new Report();
        string reportDirectory="Documentation/CoastalV9Verification";
        public string ReportPath => Path.GetFullPath(Path.Combine(Application.dataPath,"..",reportDirectory,"PlayChecks.json"));
        CoastalWalkthrough tour;
        CourtyardWalker walker;
        TutorialJourney journey;
        PlayerInteractor actor;
        Transform first, second, third;
        AimConsole firstControl, wallControl;
        AimConsoleRelay firstIsland, wallOnboard;
        LanternPickup pickup;
        SolidPath firstBridge, farBridge;
        RedStoneCurtain firstReturning, firstWindow, movingStone;
        LightDrivenLift wall, stoneMotor;
        LinearPlatformMotor crossBridge, portalHeight;
        MotorLever bridgeWinch, heightWinch;
        PortalRailConsole portalControl;
        PortalConsoleRelay middlePortal;
        LightReceiver farReceiver;
        LaserEmitter foldedSource;
        FallRecoveryRegion[] regions;
        int[] recoveryCounts;
        int seaDeaths, lastStepFrame = -1;
        bool cancelled, finalized, captured, recoverySession;
        bool originalActive, originalSimulateInactive, originalActorInput, originalWalkthroughEnabled;
        TutorialInput[] tutorialInputs;
        bool[] originalTutorialInputs;
        float startTime;
        Vector3 segmentTarget, lastObservedPosition;
        bool haveObservedPosition;

        public static string Begin(string reportDirectory="Documentation/CoastalV9Verification")
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode in the authored V9 scene before calling V9PlayChecks.Begin().");
            if (current && current.Results.running) return Status();
            if (current) Object.Destroy(current.gameObject);
            var host = new GameObject("__V9_PlayChecks_OptIn") { hideFlags = HideFlags.DontSave };
            current = host.AddComponent<V9PlayChecks>();
            if(current)current.reportDirectory=reportDirectory;
            if (!current)
            {
                Object.Destroy(host);
                throw new InvalidOperationException("V9PlayChecks could not attach. Refresh Unity so the guarded diagnostics source is available in the runtime assembly.");
            }
            current.StartCoroutine(current.RunSafely(null));
            return Status();
        }

        public static string Status()
        {
            if (!current) return "V9 checks have not started. Call V9PlayChecks.Begin() during Play.";
            var r = current.Results;
            return $"{r.outcome}: {r.progress}; checks={r.checks.Count}; route={r.routePassed}; recovery={r.recoveryChecksPassed}; routeTeleports={r.routeTeleports}; postRouteTeleports={r.postRouteTeleports}; position={(current.walker ? current.walker.transform.position : r.currentPosition)}; report={current.ReportPath}";
        }

        public static string Cancel()
        { if (current && current.Results.running) current.cancelled = true; return Status(); }

        public static string BeginRecoveryChecks()
        {
            if (!Application.isPlaying || !current || !current.Results.routePassed || current.Results.routeTeleports != 0)
                throw new InvalidOperationException("Recovery checks require an accepted, zero-teleport V9 route in the same Play session.");
            if (current.Results.running) throw new InvalidOperationException("Wait for the active V9 checks before starting recovery checks.");
            current.cancelled = false; current.finalized = false; current.recoverySession = true;
            current.Results.recoveryChecksPassed = false;
            current.StartCoroutine(current.RunSafely(current.RecoveryRoute()));
            return Status();
        }

        IEnumerator RunSafely(IEnumerator selected)
        {
            if (selected == null) { Results.startedUtc = DateTime.UtcNow.ToString("o"); startTime = Time.realtimeSinceStartup; }
            Results.endedUtc = null; Results.diagnostic = null;
            Results.scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            Results.running = true; Results.outcome = "RUNNING"; Results.progress = selected == null ? "Resolve V9 scene" : "Post-route recovery checks";
            var stack = new Stack<IEnumerator>(); stack.Push(selected ?? MainRoute());
            while (stack.Count > 0)
            {
                object next = null; bool more = false; Exception failure = null;
                try
                {
                    if (cancelled) throw new OperationCanceledException("V9 checks cancelled explicitly.");
                    if (captured) AuditMovement();
                    more = stack.Peek().MoveNext();
                    if (more) next = stack.Peek().Current;
                }
                catch (Exception error) { failure = error; }
                if (failure != null)
                {
                    Results.outcome = failure is OperationCanceledException ? "CANCELLED" : "FAIL";
                    Results.diagnostic = failure.ToString() + "\n" + SafeDiagnostics();
                    Record(false, Results.progress, Results.diagnostic, segmentTarget, 0);
                    break;
                }
                if (!more) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) { stack.Push(nested); continue; }
                yield return next;
            }
            while (stack.Count > 0) (stack.Pop() as IDisposable)?.Dispose();
            if (Results.outcome == "RUNNING") Results.outcome = "PASS";
            Finish();
        }

        IEnumerator MainRoute()
        {
            ResolveScene();
            Require(walker.Controller && walker.Controller.enabled, "The real CharacterController must be active.");
            Require(!tour.playerCamera || !tour.playerCamera.IsOverview, "Start in walking mode, outside overview.");
            Require(Mathf.Abs(Time.timeScale - 1) < .001f, "Use normal timeScale=1 for route evidence.");
            Require(Vector3.Distance(walker.transform.position, new Vector3(-65, .15f, 204)) < 2.5f,
                "Start from the authored shore spawn in a fresh Play session; Begin does not reposition the player.");
            CaptureControls();
            yield return Hold("Initial authored states settle", .12f);
            Require(firstControl.selected == 0 && !firstBridge.IsSolid && !firstReturning.IsOpen && !firstWindow.IsOpen && !journey.HasLantern,
                "The first puzzle and lantern must be untouched in a fresh Play session.");
            Require(wall.NormalizedTravel < .02f && crossBridge.NormalizedTravel < .02f && stoneMotor.NormalizedTravel < .02f
                && portalHeight.NormalizedTravel < .02f && portalControl.selected == 0, "V9 motors must begin at their authored starting positions.");
            Record(true, "Start at authored shore spawn", "No test reposition; all three puzzles are initially unsolved. Keyboard movement is suspended while real controller intent is supplied.", walker.transform.position, 0);
            RequireNoSupport(first, new Vector3(0, 0, -1.5f), "01 missing light bridge is a real physical gap");

            yield return WalkLocal("01 reach shore ramp", first, new Vector3(0, -3.35f, -15.7f), .45f, 1);
            yield return WalkLocal("01 climb to entry mirror", first, new Vector3(-.8f, 0, -7.1f), .22f, .6f);
            Interact("01 E borrows light from the entry mirror", firstControl);
            Require(firstControl.selected == 1, "The entry mirror did not advance to its bridge aim.");
            yield return WaitFor("01 A gains support while B disappears", () => firstBridge.IsSolid && firstReturning.IsOpen && firstWindow.IsOpen, 5);
            Require(AssignedCollision(firstBridge, true) && !firstReturning.PhysicalCollider.enabled,
                "Bridge state and the actual assigned colliders disagree.");
            Record(true, "01 optical tradeoff is physical", "A light-bridge colliders are enabled; returning B collider is disabled; permanent cracked window remains open.", firstBridge.transform.position, 0);
            yield return WalkLocal("01 cross A onto the safe middle island", first, new Vector3(-.6f, 0, 3.7f), .22f, .5f);
            Interact("01 E returns the borrowed light from the island", firstIsland);
            yield return WaitFor("01 A disappears and B returns", () => !firstBridge.IsSolid && !firstReturning.IsOpen && firstReturning.PhysicalCollider.enabled, 4);
            Require(AssignedCollision(firstBridge, false) && firstWindow.IsOpen, "Returning light must remove A without closing the permanent window.");
            yield return WalkLocal("01 cross the returned stone bridge", first, new Vector3(0, 0, 14.4f), .25f, .5f);
            Interact("01 E collects the reusable lantern", pickup);
            Require(pickup.Collected && journey.HasLantern && journey.LanternOn, "The ordinary pickup interaction did not grant the lit reusable lantern.");
            Record(true, "01 lantern acquired", "Possession and reusable illumination came from the normal nearby pickup.", walker.transform.position, 0);
            yield return WalkLocal("01 leave the lantern landing", first, new Vector3(0, 1.666667f, 19.5f), .4f, .9f);
            yield return WalkTo("Shore mountain path", new Vector3(-68, 10, 158), .65f, 1.8f);
            yield return WalkTo("Path toward the stone workshop", new Vector3(-58, 19, 143), .65f, 1.8f);

            yield return WalkLocal("02 reach the workshop approach", second, new Vector3(0, -7.4f, -12), .4f, 1.2f);
            yield return WalkLocal("02 approach the riding wall from the entry", second, new Vector3(-.65f, -4, -2.5f), .12f, .5f);
            Interact("02 E starts raising the stone wall from the entry", wallControl);
            Require(wallControl.selected == 1, "Wall entry control did not select raise.");
            yield return WalkToMoving("02 board the real rising wall", () => Local(second, new Vector3(.55f, WallTop(), -.7f)), .18f, .5f, 8);
            RequireRiding(wall, "02 CharacterController stands on the moving wall Rigidbody");
            yield return WaitFor("02 ride to the middle gallery height", () => WallTop() >= -.045f, 10);
            Interact("02 E stops the wall at the middle gallery", wallOnboard);
            Require(wallControl.selected == 2, "Onboard control did not select the middle stop.");
            yield return WaitFor("02 light loss stops the wall", () => wall.Direction == 0 && !wall.IsMoving, 3);
            float middleTop = WallTop();
            yield return HoldStableMotor("02 middle stop holds its physical height", wall, .35f, .07f);
            Require(Mathf.Abs(middleTop) <= .22f && Mathf.Abs(second.InverseTransformPoint(walker.transform.position).y) <= .45f,
                "The wall did not stop level with the middle workbench: wallTop=" + F(middleTop));
            yield return WalkLocal("02 step sideways onto the fixed gallery", second, new Vector3(2.8f, 0, -.7f), .16f, .45f);
            yield return WalkLocal("02 go around the winch pedestal", second, new Vector3(3.1f, 0, 1.75f), .17f, .45f);
            yield return WalkLocal("02 reach the bridge winch", second, new Vector3(4.2f, 0, 1.75f), .17f, .45f);
            Interact("02 E starts the bridge leftward", bridgeWinch);
            Require(crossBridge.Direction > 0, "First winch use must request the positive travel direction.");
            yield return WaitFor("02 bridge passes the center alignment", () => BridgeX() <= -1, 15);
            Interact("02 E stops the bridge beyond center", bridgeWinch);
            Require(crossBridge.Direction == 0, "Second winch use must stop the bridge.");
            yield return HoldStableMotor("02 bridge holds the first intermediate stop", crossBridge, .25f, .06f);
            Interact("02 E reverses the bridge rightward", bridgeWinch);
            Require(crossBridge.Direction < 0, "Third winch use must reverse the bridge.");
            yield return WaitFor("02 bridge returns to its centerline", () => BridgeX() >= -.035f, 5);
            Interact("02 E completes the four-step winch cycle at center", bridgeWinch);
            Require(crossBridge.Direction == 0, "Fourth winch use must stop the bridge.");
            yield return HoldStableMotor("02 aligned cross-bridge remains stopped", crossBridge, .35f, .06f);
            Require(Mathf.Abs(BridgeX()) <= .35f, "Cross-bridge missed the required x=0 +/-0.35m alignment: x=" + F(BridgeX()));
            Record(true, "02 winch completes forward-stop-reverse-stop", "All four commands used normal nearby E; actual Rigidbody x=" + F(BridgeX()) + "m, no motor setters were called.", crossBridge.platformBody.position, 0);
            yield return WalkLocal("02 leave the winch without crossing its pedestal", second, new Vector3(3.1f, 0, 1.75f), .17f, .45f);
            yield return WalkLocal("02 return along the fixed gallery", second, new Vector3(2.8f, 0, -.7f), .17f, .45f);
            yield return WalkToMoving("02 reboard the stopped wall", () => Local(second, new Vector3(.55f, WallTop(), -.7f)), .18f, .45f, 8);
            RequireRiding(wall, "02 rider reboards from the middle gallery");
            Interact("02 E advances middle stop to lower", wallOnboard);
            Require(wallControl.selected == 3, "Wall cycle did not reach its lower aim.");
            yield return WaitFor("02 lower aim produces an actual lowering request", () => wall.Direction < 0, 2);
            Interact("02 E advances lower to resting light", wallOnboard);
            Require(wallControl.selected == 0, "Wall cycle did not return to its first stop.");
            yield return WaitFor("02 resting light stops the downward request", () => wall.Direction == 0 && !wall.IsMoving, 2);
            Interact("02 E starts the second ascent", wallOnboard);
            Require(wallControl.selected == 1, "Wall did not return to the raise aim.");
            yield return WaitFor("02 rider reaches the upper wall stop", () => WallTop() >= 2.96f, 8);
            Interact("02 E parks the wall at the upper gallery", wallOnboard);
            yield return WaitFor("02 upper wall stops physically", () => wall.Direction == 0 && !wall.IsMoving, 2);
            yield return HoldStableMotor("02 upper stop holds the rider", wall, .3f, .07f);
            Require(Mathf.Abs(WallTop() - 3) <= .22f, "The wall did not reach the upper bridge level.");
            RequireRiding(wall, "02 rider remains on the wall at the upper stop");
            yield return WalkLocal("02 approach the aligned bridge from the wall", second, new Vector3(.55f, 3, 1.4f), .18f, .45f);
            yield return WalkLocal("02 board the aligned sliding bridge", second, new Vector3(.55f, 3, 3.1f), .18f, .45f);
            RequireRiding(crossBridge, "02 real sliding bridge supports the crossing");
            yield return WalkLocal("02 cross the repaired upper corridor", second, new Vector3(0, 3, 10.5f), .25f, .45f);
            yield return WalkLocal("02 pass the workshop completion landing", second, new Vector3(0, 3, 11.5f), .25f, .45f);
            yield return WalkLocal("02 leave by the authored joining ramp", second, new Vector3(0, 5.1f, 17), .35f, .8f);
            yield return WalkTo("Mountain connection toward folded light", new Vector3(-35, 34, 117), .55f, 1.3f);

            yield return WalkLocal("03 reach the folded-light approach", third, new Vector3(0, -4.6f, -18), .4f, 1.1f);
            yield return WalkLocal("03 climb to the entrance portal control", third, new Vector3(-.8f, 0, -8.4f), .18f, .55f);
            RequireNoSupport(third, new Vector3(0, 0, -3.5f), "03 unaligned stone bridge leaves a real gap");
            Interact("03 E directs portal light to the stone-drive receiver", portalControl);
            Require(portalControl.selected == 1, "Entrance portal control did not select stone-drive mode.");
            yield return WaitFor("03 redirected light moves A into the centerline", () => stoneMotor.NormalizedTravel >= .997f, 12);
            Require(Mathf.Abs(StoneX()) <= .08f && stoneMotor.receiverRaise.IsIlluminated, "The stone bridge must actually reach its centered position under receiver illumination.");
            Interact("03 E passes through the long-corridor mode", portalControl);
            yield return Hold("03 visible control-cycle transition", .08f);
            Interact("03 E returns the portal to rest before crossing", portalControl);
            Require(portalControl.selected == 0, "Stone bridge crossing requires resting portal light.");
            yield return WaitFor("03 A returns as a stationary physical bridge", () => !portalControl.IsMoving && !movingStone.IsOpen && movingStone.PhysicalCollider.enabled && stoneMotor.Direction == 0, 5);
            yield return WalkLocal("03 cross centered A onto the safe middle pier", third, new Vector3(-.8f, 0, 1.3f), .2f, .5f);
            Interact("03 E passes through stone-drive mode from the middle pier", middlePortal);
            yield return Hold("03 middle control-cycle transition", .08f);
            Interact("03 E aims the low beam toward the far corridor", middlePortal);
            Require(portalControl.selected == 2, "Middle control did not select the far corridor aim.");
            yield return WaitFor("03 low portal aim settles", () => !portalControl.IsMoving, 5);
            yield return WaitFor("03 low beam dissolves A while B remains dark", () => movingStone.IsOpen && !farBridge.IsSolid && !farReceiver.IsActive, 3);
            yield return Hold("03 observe the low beam obstruction", .35f);
            Require(!farBridge.IsSolid && !farReceiver.IsActive && movingStone.IsOpen,
                "The deliberate low-light trial must dissolve A while leaving the far bridge dark.");
            Require(foldedSource.PortalHopCount > 0 && foldedSource.SegmentCount >= 2, "Low trial did not use the real paired portal trace.");
            Vector3 lowEnd = third.InverseTransformPoint(foldedSource.GetSegment(foldedSource.SegmentCount - 1).End);
            Require(Mathf.Abs(lowEnd.z) < .22f && Mathf.Abs(lowEnd.y + .1f) < .25f,
                "The low beam must visibly terminate at the middle-pier face, not elsewhere: local endpoint=" + lowEnd);
            Record(true, "03 failed low aim is observable", "Actual final beam endpoint=" + lowEnd + "; B has no collider support; A disappeared. The player stands on the permanent middle pier.", foldedSource.GetSegment(foldedSource.SegmentCount - 1).End, 0);
            yield return WalkLocal("03 move across the safe pier to its height winch", third, new Vector3(.8f, 0, 1.3f), .18f, .45f);
            Interact("03 E raises the portal carriage", heightWinch);
            Require(portalHeight.Direction > 0, "Height winch did not request upward travel.");
            yield return WaitFor("03 carriage reaches 5.6m and far B becomes solid", () => portalHeight.NormalizedTravel >= .998f && farBridge.IsSolid && farReceiver.IsIlluminated, 10);
            Require(Mathf.Abs(third.InverseTransformPoint(portalHeight.platformBody.position).y - 5.6f) <= .08f && AssignedCollision(farBridge, true),
                "The lifted portal carriage or the actual B collider is not at its intended state.");
            Record(true, "03 elevated portal light repairs the far route", "The original beam cleared the physical middle pier, illuminated the far receiver and enabled the authored rising bridge.", farBridge.transform.position, 0);
            yield return WalkLocal("03 approach the rising light bridge", third, new Vector3(0, 0, 2.8f), .18f, .4f);
            yield return WalkLocal("03 climb the real illuminated corridor", third, new Vector3(0, 2.6f, 10.65f), .2f, .5f);
            yield return WalkLocal("03 step around the receiver stand", third, new Vector3(1.2f, 2.6f, 10.65f), .2f, .45f);
            yield return WalkLocal("03 reach the sea-view reward landing", third, new Vector3(1.2f, 2.6f, 12.4f), .2f, .45f);
            AuditMovement();
            Require(Results.routeTeleports == 0 && Results.unexplainedDiscontinuities == 0 && journey.HasLantern && firstWindow.IsOpen,
                "Main route must retain the lantern/permanent window and contain no recovery or teleport.");
            Results.routePassed = true;
            Record(true, "Full V9 shore-to-sea-view route passed", "Three real broken routes crossed by CharacterController; normal nearest E interactions; wall riding and intermediate stops; full winch reversal cycle; observable failed low light followed by a real elevated portal solution; no jump intent or reposition.", walker.transform.position, 0);
        }

        void ResolveScene()
        {
            tour = Object.FindAnyObjectByType<CoastalWalkthrough>();
            Require(tour && tour.walker && tour.tutorial, "Authored walkthrough, walker and V9 tutorial must exist.");
            walker = tour.walker; journey = tour.tutorial; actor = journey.interactor;
            Require(actor && actor.walker == walker && actor.allowInteraction && actor.isActiveAndEnabled, "The authored PlayerInteractor must be enabled and reference this walker.");
            var root = journey.transform;
            first = RequiredTransform(root, "01_BorrowedBridge"); second = RequiredTransform(root, "02_StoneWorkshop"); third = RequiredTransform(root, "03_FoldedBridge");
            firstControl = Required<AimConsole>(first, "Light_Control_Entry"); firstIsland = Required<AimConsoleRelay>(first, "Light_Control_Island");
            firstBridge = Required<SolidPath>(first, "A_Light_Bridge"); firstReturning = Required<RedStoneCurtain>(first, "B_Returning_Stone_Bridge");
            firstWindow = Required<RedStoneCurtain>(first, "Permanent_Cracked_Window"); pickup = Required<LanternPickup>(first, "Reusable_Lantern");
            wall = Required<LightDrivenLift>(second, "Wall_Lift_Motor"); wallControl = Required<AimConsole>(second, "Wall_Control_Entry");
            wallOnboard = Required<AimConsoleRelay>(second, "Wall_Control_Onboard"); crossBridge = Required<LinearPlatformMotor>(second, "CrossBridge_Motor"); bridgeWinch = Required<MotorLever>(second, "Bridge_Winch");
            stoneMotor = Required<LightDrivenLift>(third, "StoneBridge_Motor"); movingStone = Required<RedStoneCurtain>(third, "A_Movable_Returning_Bridge");
            portalHeight = Required<LinearPlatformMotor>(third, "Portal_Height_Motor"); portalControl = Required<PortalRailConsole>(third, "Portal_Control_Entry");
            middlePortal = Required<PortalConsoleRelay>(third, "Portal_Control_Middle"); heightWinch = Required<MotorLever>(third, "Height_Winch_Middle");
            farBridge = Required<SolidPath>(third, "B_Rising_LightBridge"); farReceiver = Required<LightReceiver>(third, "Far_LightBridge_Receiver"); foldedSource = Required<LaserEmitter>(third, "Fixed_Sun");
            regions = new[] { first.GetComponent<FallRecoveryRegion>(), second.GetComponent<FallRecoveryRegion>(), third.GetComponent<FallRecoveryRegion>() };
            foreach (var region in regions) Require(region && region.walker == walker && region.safePoint, "Each V9 stage needs its authored fall recovery region.");
        }

        IEnumerator RecoveryRoute()
        {
            Require(Results.routePassed && Results.routeTeleports == 0, "Recovery cannot substitute for a passed main route.");
            CaptureControls();
            Require(!tour.playerCamera || !tour.playerCamera.IsOverview, "Run recovery checks in walking mode.");
            Vector3 wallState = wall.platformBody.position, crossState = crossBridge.platformBody.position, stoneState = stoneMotor.platformBody.position, heightState = portalHeight.platformBody.position;
            walker.ResetPosition(); CountReposition("Recovery Home retains completed world state", walker.transform.position);
            yield return Hold("Recovery settles after actual Home reset", .2f);
            Require(journey.HasLantern && firstWindow.IsOpen, "Home reset discarded possession or the permanent opening.");
            for (int i = 0; i < regions.Length; i++)
            {
                var region = regions[i]; int before = region.RecoveryCount;
                Vector3 inside = region.transform.TransformPoint(region.center);
                walker.RespawnAt(inside, region.safePoint.eulerAngles.y);
                CountReposition("Recovery deliberately enters stage " + (i + 1) + " fall region", inside);
                yield return WaitFor("Recovery stage " + (i + 1) + " uses its authored automatic return", () => region.RecoveryCount == before + 1, 3);
                Require(Vector3.Distance(walker.transform.position, region.safePoint.position) < .6f,
                    "Fall recovery did not return to the authored safe point for " + region.name);
                Require(journey.HasLantern && firstWindow.IsOpen && Vector3.Distance(wall.platformBody.position, wallState) < .08f
                    && Vector3.Distance(crossBridge.platformBody.position, crossState) < .08f && Vector3.Distance(stoneMotor.platformBody.position, stoneState) < .08f
                    && Vector3.Distance(portalHeight.platformBody.position, heightState) < .08f, "Fall recovery changed the completed world mechanisms.");
                yield return Hold("Recovery stage " + (i + 1) + " remains stable at its safe point", .35f);
                Require(region.RecoveryCount == before + 1, "The authored safe point retriggered recovery repeatedly.");
                Record(true, "Recovery stage " + (i + 1) + " preserves the solved world", "Post-route placement into the fall volume was explicit; the ordinary LateUpdate performed one automatic return. No mechanism was reset.", region.safePoint.position, 0);
            }
            Results.recoveryChecksPassed = true;
        }

        IEnumerator WalkLocal(string name, Transform stage, Vector3 local, float horizontal = .25f, float vertical = .55f) => WalkTo(name, Local(stage, local), horizontal, vertical);
        IEnumerator WalkTo(string name, Vector3 target, float horizontal, float vertical) => WalkToMoving(name, () => target, horizontal, vertical, 25);
        IEnumerator WalkToMoving(string name, Func<Vector3> targetProvider, float horizontal, float vertical, float timeout)
        {
            Results.progress = name; segmentTarget = targetProvider(); Save(); float began = Time.realtimeSinceStartup;
            while (true)
            {
                segmentTarget = targetProvider(); Vector3 delta = segmentTarget - walker.transform.position; Vector3 flat = new Vector3(delta.x, 0, delta.z);
                if (flat.magnitude <= horizontal && Mathf.Abs(delta.y) <= vertical) break;
                Require(Time.realtimeSinceStartup - began <= timeout, "Waypoint timed out: horizontal=" + F(flat.magnitude) + "m; vertical=" + F(delta.y) + "m; target=" + segmentTarget);
                if (flat.magnitude > horizontal)
                {
                    float wantedYaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                    walker.ApplyLook(new Vector2(Mathf.DeltaAngle(walker.LookYaw, wantedYaw), 0));
                    Vector3 local = walker.transform.InverseTransformDirection(flat.normalized);
                    Step(new Vector2(local.x, local.z) * Mathf.Clamp(flat.magnitude / .7f, .2f, 1));
                }
                else Step(Vector2.zero);
                yield return null;
            }
            Record(true, name, "Reached using ordinary CharacterController intent; vertical error=" + F(segmentTarget.y - walker.transform.position.y) + "m.", segmentTarget, Time.realtimeSinceStartup - began);
        }
        IEnumerator WaitFor(string name, Func<bool> condition, float seconds)
        {
            Results.progress = name; segmentTarget = walker.transform.position; Save(); float began = Time.realtimeSinceStartup;
            while (!condition())
            {
                Require(Time.realtimeSinceStartup - began <= seconds, "Condition was not satisfied within " + seconds + " seconds.");
                Step(Vector2.zero); yield return null;
            }
            Record(true, name, "Satisfied through normal render/physics updates with neutral controller intent.", walker.transform.position, Time.realtimeSinceStartup - began);
        }
        IEnumerator Hold(string name, float seconds)
        {
            Results.progress = name; segmentTarget = walker.transform.position; Save(); float began = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - began < seconds) { Step(Vector2.zero); yield return null; }
            Record(true, name, "Observed " + F(Time.realtimeSinceStartup - began) + " seconds of ordinary frames.", walker.transform.position, Time.realtimeSinceStartup - began);
        }
        IEnumerator HoldStableMotor(string name, LinearPlatformMotor motor, float seconds, float tolerance)
        {
            // Allow the already-scheduled Rigidbody MovePosition to settle before measuring the hold.
            yield return Hold(name + " settles", .12f);
            Vector3 before = motor.platformBody.position;
            yield return Hold(name, seconds);
            Require(Vector3.Distance(before, motor.platformBody.position) <= tolerance && motor.Direction == 0,
                "Stopped motor drifted " + F(Vector3.Distance(before, motor.platformBody.position)) + "m: " + motor.name);
        }
        void Interact(string name, TutorialInteractable target)
        {
            Require(target, "Missing interaction target: " + name);
            Results.progress = name; segmentTarget = target.InteractionPosition;
            float distance = Vector3.Distance(actor.InteractorPosition, target.InteractionPosition);
            bool accepted = false; string nearest = "none";
            walker.active = true;
            try
            {
                actor.RefreshNearby(); nearest = actor.Nearby ? actor.Nearby.name : "none";
                Require(actor.Nearby == target, "Expected nearest E prompt " + target.name + ", but nearest reachable prompt is " + nearest + ".");
                accepted = actor.Interact(target);
            }
            finally { walker.active = false; }
            Require(accepted, "Normal E policy rejected " + target.name + "; distance=" + F(distance) + "m, radius=" + target.useRadius + "m; nearest=" + nearest);
            Results.interactionCount++;
            Record(true, name, "PlayerInteractor.Interact accepted the nearest reachable object; distance=" + F(distance) + "m; radius=" + F(target.useRadius) + "m; no policy bypass.", target.InteractionPosition, 0);
        }
        void Step(Vector2 input)
        {
            AuditMovement();
            if (lastStepFrame == Time.frameCount) return;
            lastStepFrame = Time.frameCount; walker.active = false;
            Require(!walker.simulateWhileInactive, "Inactive-player simulation was re-enabled and would double-step the diagnostic motor.");
            Require(!tour.playerCamera || !tour.playerCamera.IsOverview, "View changed to overview during the walking route.");
            walker.SimulateMovement(input, false, false, 0, Time.deltaTime);
            Results.simulatedFrames++;
            AuditMovement();
            if (tour.playerCamera) tour.playerCamera.SnapToTarget();
            Results.currentPosition = walker.transform.position;
        }

        void CaptureControls()
        {
            originalActive = walker.active; originalSimulateInactive = walker.simulateWhileInactive; originalActorInput = actor.readKeyboardInput;
            // Suspend the separate Home/overview/debug input owner as well as movement and E input.
            // Its OnDisable only clears manual walking; mechanism and recovery components remain active.
            originalWalkthroughEnabled = tour.enabled; tour.enabled = false;
            tutorialInputs = journey.GetComponentsInChildren<TutorialInput>(true); originalTutorialInputs = new bool[tutorialInputs.Length];
            for (int i = 0; i < tutorialInputs.Length; i++) { originalTutorialInputs[i] = tutorialInputs[i].readKeyboardInput; tutorialInputs[i].readKeyboardInput = false; }
            walker.active = false; walker.simulateWhileInactive = false; actor.readKeyboardInput = false; captured = true;
            recoveryCounts = new int[regions.Length]; for (int i = 0; i < regions.Length; i++) recoveryCounts[i] = regions[i].RecoveryCount;
            seaDeaths = tour.seaBoundary ? tour.seaBoundary.DeathCount : 0;
            lastObservedPosition = walker.transform.position; haveObservedPosition = true; lastStepFrame = -1;
        }
        void AuditMovement()
        {
            if (!captured || !walker) return;
            int automatic = 0;
            for (int i = 0; i < regions.Length; i++)
            { int now = regions[i] ? regions[i].RecoveryCount : recoveryCounts[i]; automatic += Math.Max(0, now - recoveryCounts[i]); recoveryCounts[i] = now; }
            int deaths = tour.seaBoundary ? tour.seaBoundary.DeathCount : seaDeaths;
            automatic += Math.Max(0, deaths - seaDeaths); seaDeaths = deaths;
            float displacement = haveObservedPosition ? Vector3.Distance(lastObservedPosition, walker.transform.position) : 0;
            lastObservedPosition = walker.transform.position; haveObservedPosition = true;
            if (automatic > 0)
            {
                if (recoverySession) { Results.postRouteRecoveries += automatic; Results.postRouteTeleports += automatic; }
                else { Results.routeTeleports += automatic; throw new InvalidOperationException("The game automatically recovered/respawned the player during the main route; zero-teleport acceptance is impossible."); }
            }
            else if (displacement > 2.5f)
            {
                Results.unexplainedDiscontinuities++;
                if (recoverySession) Results.postRouteTeleports++; else Results.routeTeleports++;
                throw new InvalidOperationException("Unexplained player discontinuity of " + F(displacement) + "m between observations; conservatively counted as a teleport.");
            }
        }
        void CountReposition(string name, Vector3 position)
        {
            Require(recoverySession && Results.routePassed && Results.routeTeleports == 0, "Only explicit post-route recovery checks may reposition the player.");
            Results.postRouteRepositions++; Results.postRouteTeleports++;
            lastObservedPosition = walker.transform.position; haveObservedPosition = true;
            if (tour.playerCamera) tour.playerCamera.SnapToTarget();
            Record(true, name, "Explicit post-route test reposition; excluded from the accepted main route. World mechanism state was not altered.", position, 0);
        }
        void RequireRiding(LinearPlatformMotor motor, string name)
        {
            bool found = false;
            foreach (var hit in Physics.RaycastAll(walker.transform.position + Vector3.up * .3f, Vector3.down, .8f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(walker.transform) && hit.collider.attachedRigidbody == motor.platformBody) found = true;
            Require(found && walker.Controller.isGrounded, "The character is not physically supported by " + motor.name);
            Record(true, name, "Real downward support ray found the expected Rigidbody and the controller is grounded.", walker.transform.position, 0);
        }
        void RequireNoSupport(Transform stage, Vector3 local, string name)
        {
            Vector3 point = Local(stage, local);
            foreach (var hit in Physics.RaycastAll(point + Vector3.up * .25f, Vector3.down, 1.25f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(walker.transform)) throw new InvalidOperationException(name + " failed: hidden support=" + Hierarchy(hit.collider.transform));
            Record(true, name, "A real downward ray at the missing route found no nearby solid support.", point, 0);
        }
        static bool AssignedCollision(SolidPath path, bool enabled)
        {
            if (!path || path.colliders == null || path.colliders.Length == 0) return false;
            foreach (var collider in path.colliders) if (!collider || collider.enabled != enabled) return false;
            return true;
        }
        float WallTop() => second.InverseTransformPoint(wall.platformBody.position).y + 1.7f;
        float BridgeX() => second.InverseTransformPoint(crossBridge.platformBody.position).x;
        float StoneX() => third.InverseTransformPoint(stoneMotor.platformBody.position).x;
        static Vector3 Local(Transform stage, Vector3 position) => stage.TransformPoint(position);
        static Transform RequiredTransform(Transform root, string name)
        { foreach (var value in root.GetComponentsInChildren<Transform>(true)) if (value.name == name) return value; throw new InvalidOperationException("Missing authored object " + name + " beneath " + root.name); }
        static T Required<T>(Transform root, string name) where T : Component
        { var value = RequiredTransform(root, name).GetComponent<T>(); if (!value) throw new InvalidOperationException(name + " lacks " + typeof(T).Name); return value; }
        static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }

        void Record(bool passed, string name, string detail, Vector3 target, float elapsed)
        {
            Vector3 position = walker ? walker.transform.position : Vector3.zero;
            Results.checks.Add(new Check { name = name, detail = detail, passed = passed, position = position, target = target, elapsedSeconds = elapsed,
                horizontalDistance = new Vector2(position.x - target.x, position.z - target.z).magnitude });
            Results.currentPosition = position; Save();
        }
        void Save()
        {
            Results.elapsedSeconds = Time.realtimeSinceStartup - startTime;
            string directory = Path.GetDirectoryName(ReportPath); Directory.CreateDirectory(directory);
            File.WriteAllText(ReportPath, JsonUtility.ToJson(Results, true), new UTF8Encoding(false));
            var text = new StringBuilder("# V9 continuous route verification\n\n");
            text.AppendLine("Result: **" + Results.outcome + "**\n\nProgress: " + Results.progress);
            text.AppendLine("\nRoute passed: " + Results.routePassed + "; recovery passed: " + Results.recoveryChecksPassed + ".");
            text.AppendLine("\nMain-route teleports: " + Results.routeTeleports + "; post-route repositions: " + Results.postRouteRepositions + "; post-route automatic recoveries: " + Results.postRouteRecoveries + "; post-route teleport total: " + Results.postRouteTeleports + ".");
            text.AppendLine("\nNormal interactions: " + Results.interactionCount + "; unique rendered frames with motor intent: " + Results.simulatedFrames + ".\n\n" + Results.scope + "\n");
            foreach (var check in Results.checks) text.AppendLine("- " + (check.passed ? "PASS" : "FAIL") + " — " + check.name + ": " + check.detail.Replace("\n", " | "));
            if (!string.IsNullOrEmpty(Results.diagnostic)) text.AppendLine("\n## Failure diagnostics\n\n```text\n" + Results.diagnostic + "\n```");
            File.WriteAllText(Path.Combine(directory, "PlayChecks.md"), text.ToString(), new UTF8Encoding(false));
        }
        string SafeDiagnostics()
        {
            // Missing scene references must not replace the original nested-coroutine failure.
            try { return Diagnostics(); }
            catch (Exception error) { return "Additional collision diagnostics could not be completed: " + error.Message; }
        }
        string Diagnostics()
        {
            if (!walker) return "Walker was not resolved.";
            var text = new StringBuilder(); Vector3 feet = walker.transform.position;
            text.AppendLine("Feet=" + feet + "; target=" + segmentTarget + "; grounded=" + (walker.Controller && walker.Controller.isGrounded) + "; swimming=" + walker.Swimming + "; verticalSpeed=" + F(walker.VerticalSpeed));
            if (actor) text.AppendLine("ActorOrigin=" + actor.InteractorPosition + "; active=" + actor.PlayerCanAct + "; nearest=" + (actor.Nearby ? actor.Nearby.name : "none"));
            if (wall && wall.platformBody && second) text.AppendLine("WallTop=" + F(WallTop()) + "; direction=" + wall.Direction + "; selected=" + (wallControl ? wallControl.selected.ToString() : "missing") + "; raiseLit=" + (wall.receiverRaise && wall.receiverRaise.IsIlluminated) + "; lowerLit=" + (wall.receiverLower && wall.receiverLower.IsIlluminated));
            if (crossBridge && crossBridge.platformBody && second) text.AppendLine("CrossBridgeX=" + F(BridgeX()) + "; direction=" + crossBridge.Direction);
            if (stoneMotor && stoneMotor.platformBody && third) text.AppendLine("StoneX=" + F(StoneX()) + "; direction=" + stoneMotor.Direction + "; stoneOpen=" + (movingStone && movingStone.IsOpen));
            if (portalHeight && portalHeight.platformBody && third) text.AppendLine("PortalHeight=" + third.InverseTransformPoint(portalHeight.platformBody.position) + "; portalMode=" + (portalControl ? portalControl.selected.ToString() : "missing") + "; portalMoving=" + (portalControl && portalControl.IsMoving) + "; Bsolid=" + (farBridge && farBridge.IsSolid));
            if (foldedSource && third) for (int i = 0; i < foldedSource.SegmentCount; i++) { var beam = foldedSource.GetSegment(i); text.AppendLine("Beam " + i + ": " + third.InverseTransformPoint(beam.Start) + " -> " + third.InverseTransformPoint(beam.End)); }
            foreach (var shape in Physics.OverlapCapsule(feet + Vector3.up * .4f, feet + Vector3.up * 1.4f, .7f, ~0, QueryTriggerInteraction.Ignore))
                if (shape && !shape.transform.IsChildOf(walker.transform)) text.AppendLine("Nearby: " + Hierarchy(shape.transform) + "; bounds=" + shape.bounds);
            Vector3 forward = segmentTarget - feet; forward.y = 0;
            if (forward.sqrMagnitude > .001f) foreach (var hit in Physics.RaycastAll(feet + Vector3.up * .7f, forward.normalized, 3, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(walker.transform)) text.AppendLine("Forward: " + Hierarchy(hit.collider.transform) + "; distance=" + F(hit.distance) + "; normal=" + hit.normal);
            foreach (var hit in Physics.RaycastAll(feet + Vector3.up * 2, Vector3.down, 10, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.collider.transform.IsChildOf(walker.transform)) text.AppendLine("Ground: " + Hierarchy(hit.collider.transform) + "; y=" + F(hit.point.y) + "; normal=" + hit.normal);
            return text.ToString();
        }
        static string Hierarchy(Transform value)
        { string path = value.name; while (value.parent) { value = value.parent; path = value.name + "/" + path; } return path; }
        static string F(float value) => value.ToString("0.000", CultureInfo.InvariantCulture);
        void Finish()
        {
            if (finalized) return; finalized = true;
            Results.running = false; Results.endedUtc = DateTime.UtcNow.ToString("o");
            if (captured)
            {
                if (tour) tour.enabled = originalWalkthroughEnabled;
                if (walker) { walker.active = originalActive; walker.simulateWhileInactive = originalSimulateInactive; }
                if (actor) actor.readKeyboardInput = originalActorInput;
                for (int i = 0; i < tutorialInputs.Length; i++) if (tutorialInputs[i]) tutorialInputs[i].readKeyboardInput = originalTutorialInputs[i];
                captured = false;
            }
            Save(); UnityEngine.Debug.Log("V9PlayChecks " + Results.outcome + "; " + Results.checks.Count + " records; " + ReportPath);
        }
        void OnDisable()
        {
            if (finalized || !Results.running) return;
            Results.outcome = "STOPPED"; Results.diagnostic = "Play or the opt-in diagnostic host stopped before completion."; Finish();
        }
    }
}
#endif
