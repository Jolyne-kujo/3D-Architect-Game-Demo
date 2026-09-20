#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Interaction;
using CoastalTemple.Player;
using UnityEditor;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Tutorial.Editor
{
    public static class TutorialInteractionChecks
    {
        [MenuItem("Coastal Temple/Tests/Tutorial Interaction")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            var results = new List<string>();
            GameObject root = null;
            int failures = 0;
            void Check(bool ok, string name) { results.Add((ok ? "PASS " : "FAIL ") + name); if (!ok) failures++; }
            try
            {
                root = new GameObject("TemporaryTutorialInteractionChecks");
                root.transform.position = new Vector3(14000, 500, 14000);
                GameObject Child(string name, Vector3 position)
                {
                    var child = new GameObject(name);
                    child.transform.SetParent(root.transform, false); child.transform.localPosition = position;
                    return child;
                }
                var playerObject = Child("Player", Vector3.zero);
                var playerCollider = playerObject.AddComponent<CharacterController>();
                playerCollider.height = 1.8f; playerCollider.center = Vector3.up * .9f; playerCollider.radius = .28f;
                var walker = playerObject.AddComponent<CourtyardWalker>(); walker.active = true;
                var eye = new GameObject("Eye"); eye.transform.SetParent(playerObject.transform, false); eye.transform.localPosition = Vector3.up * 1.5f; walker.eye = eye.transform;
                var camera = playerObject.AddComponent<CoastalPlayerCamera>(); camera.walker = walker;
                var actor = playerObject.AddComponent<PlayerInteractor>(); actor.walker = walker; actor.cameraRig = camera; actor.readKeyboardInput = false;
                var lantern = playerObject.AddComponent<PlayerLantern>(); actor.lantern = lantern; lantern.interactor = actor; lantern.readKeyboardInput = false;
                var lampObject = Child("HandLight", Vector3.up * 2); var handLight = lampObject.AddComponent<Light>(); lantern.handLight = handLight;
                lantern.SetLantern(true);
                Check(!lantern.HasLantern && !handLight.enabled, "requesting light before pickup stays dark");

                var emitter = Child("AimSource", new Vector3(0, 4, 0)).AddComponent<LaserEmitter>(); emitter.RenderBeam = false; emitter.Powered = false;
                var console = Child("Console", new Vector3(1.7f, 1.5f, 0)).AddComponent<AimConsole>();
                var ownCollider = console.gameObject.AddComponent<BoxCollider>(); ownCollider.size = Vector3.one * .6f;
                console.emitter = emitter;
                console.aimPoints = new[] { Child("AimForward", new Vector3(0, 4, 4)).transform, Child("AimRight", new Vector3(4, 4, 0)).transform };
                console.Apply(); console.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);
                Physics.SyncTransforms();
                Check(actor.CanInteract(console), "nearby console ignores its own physical collider and player body");
                Check(actor.Interact(console) && console.selected == 1 && Vector3.Dot(emitter.transform.forward, Vector3.right) > .999f,
                    "console changes actual emitter aim before any curtain or reward exists");
                Check(actor.TryInteract() && console.selected == 0, "nearest registry interaction uses the same reachable console");
                Check(!root.GetComponent<TutorialJourney>() && !root.GetComponent<TutorialHud>(), "player and aim console operate without any tutorial or HUD component");
                var observer = root.AddComponent<TutorialJourney>(); observer.interactor = actor; observer.playerLantern = lantern;
                observer.enabled = false;
                Check(actor.Interact(console) && console.selected == 1, "disabling the tutorial never disables player interaction");
                observer.enabled = true;
                Check(observer.Interact(console) && console.selected == 0, "legacy facade delegates to the independent interaction module");
                actor.allowInteraction = false;
                Check(!actor.Interact(console), "explicit player action permission is enforced");
                actor.allowInteraction = true;

                var blocker = Child("EnvironmentWall", new Vector3(.85f, 1.5f, 0)).AddComponent<BoxCollider>(); blocker.size = new Vector3(.2f, 3, 3);
                Physics.SyncTransforms();
                Check(!actor.CanInteract(console) && !actor.Interact(console), "environmental collider blocks E through a nearby wall");
                blocker.isTrigger = true; Physics.SyncTransforms();
                Check(actor.CanInteract(console), "non-solid observation trigger does not block interaction");
                blocker.isTrigger = false;
                var denseAllowed = new GameObject("DenseOwnGeometry"); denseAllowed.transform.SetParent(console.transform, false);
                for (int i = 0; i < 72; i++)
                {
                    var part = new GameObject("AllowedConsolePart" + i); part.transform.SetParent(denseAllowed.transform, false);
                    part.transform.position = root.transform.position + new Vector3(.08f + i * .02f, 1.5f, 0);
                    part.AddComponent<BoxCollider>().size = new Vector3(.008f, .1f, .1f);
                }
                Physics.SyncTransforms();
                var hitBuffer = new RaycastHit[64];
                Check(Physics.RaycastNonAlloc(actor.InteractorPosition, Vector3.right, hitBuffer, 1.67f, ~0, QueryTriggerInteraction.Ignore) == 64,
                    "dense geometry fixture saturates the normal 64-hit interaction buffer");
                Check(!actor.CanInteract(console), "saturated interaction buffer still finds the environmental blocker");
                blocker.enabled = false; Physics.SyncTransforms();
                Check(actor.CanInteract(console), "dense allowed geometry remains usable without an environmental wall");
                UnityEngine.Object.DestroyImmediate(denseAllowed);
                blocker.enabled = false;
                console.transform.localPosition = new Vector3(4, 1.5f, 0); Physics.SyncTransforms();
                Check(!actor.Interact(console), "out-of-range console cannot be operated");
                console.transform.localPosition = new Vector3(1.7f, 1.5f, 0); Physics.SyncTransforms();
                walker.active = false;
                Check(!actor.TryInteract() && TutorialInteractable.FindNearest(actor) == null, "overview walker has neither an interaction nor a prompt candidate");
                walker.active = true; camera.SetOverview(true);
                Check(!actor.Interact(console), "camera overview also forbids console interaction");
                camera.SetOverview(false);
                console.enabled = false; console.SendMessage("OnDisable", SendMessageOptions.DontRequireReceiver); ownCollider.enabled = false;
                Check(!actor.Interact(console), "disabled console cannot interact through a retained reference");

                var pickup = Child("Lantern", new Vector3(1.2f, 1.5f, 0)).AddComponent<LanternPickup>();
                var pickupCollider = pickup.gameObject.AddComponent<BoxCollider>(); pickupCollider.size = Vector3.one * .25f;
                pickup.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);
                Physics.SyncTransforms();
                actor.lantern = null;
                Check(!actor.Interact(pickup) && !pickup.Collected && pickupCollider.enabled, "pickup requires a real lantern capability and is not consumed on failure");
                actor.lantern = lantern;
                int pickupEvents = 0; lantern.pickedUp.AddListener(() => pickupEvents++);
                Check(actor.Interact(pickup) && pickup.Collected && lantern.HasLantern && handLight.enabled,
                    "reachable lantern pickup grants and lights the reusable lantern");
                Check(pickupEvents == 1 && !lantern.GiveLantern() && pickupEvents == 1, "lantern pickup event is emitted once independently of rewards");
                Check(!pickupCollider.enabled && !actor.Interact(pickup), "collected pickup has no lingering collider or repeat interaction");
                lantern.SetLantern(false); Check(!handLight.enabled, "collected lantern can be switched off");
                lantern.SetLantern(true); Check(handLight.enabled, "collected lantern can be switched on again");

                var portal = Child("MovingPortal", new Vector3(0, 2, 5)).transform;
                var dockA = Child("DockA", new Vector3(0, 2, 5)).transform;
                var dockB = Child("DockB", new Vector3(0, 5, 5)).transform;
                var rail = Child("RailConsole", new Vector3(-1.2f, 1.5f, 0)).AddComponent<PortalRailConsole>();
                rail.movingPortal = portal; rail.docks = new[] { dockA, dockB }; rail.speed = 3; rail.ApplyInitialPose();
                rail.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver); Physics.SyncTransforms();
                Check(actor.Interact(rail) && rail.selected == 1 && portal.position == dockA.position, "rail selection starts travel without teleporting the portal");
                rail.Simulate(.25f);
                Check(Mathf.Abs(portal.position.y - dockA.position.y - .75f) < .001f && rail.IsMoving,
                    "portal rail advances through actual intermediate geometry");
                rail.Simulate(2);
                Check(Vector3.Distance(portal.position, dockB.position) < .001f && !rail.IsMoving, "portal rail stops at the authored dock");
                rail.Select(0); rail.Simulate(2);
                Check(Vector3.Distance(portal.position, dockA.position) < .001f, "portal rail can be reversed with no progression condition");
                observer.lessons = new[]
                {
                    new LessonStation { name = "Near", title = "Observe nearby route", marker = playerObject.transform, radius = 2, hints = new[] { "First observation", "Second observation" } },
                    new LessonStation { name = "Far", marker = dockB, radius = 1, hints = new[] { "Distant observation" } }
                };
                int rewards = 0; observer.RewardIssued += _ => rewards++;
                Check(observer.CurrentStage == 0, "authored station radius selects the current lesson");
                observer.ShowObservation(); Check(observer.ObservationText == "First observation", "authored first hint is displayed");
                observer.ShowObservation(); Check(observer.ObservationText == "Second observation", "authored progressive hint is displayed");
                observer.ObserveWorld();
                Check(rewards == 0, "arrival and reading hints never fabricate a completion reward");
                observer.lessons[0].marker = dockB;
                Check(observer.CurrentStage == -1, "leaving all lesson radii does not invent a nearby station");
                observer.NotifyReward("Actual mechanism event");
                Check(rewards == 1, "explicit actual-world reward event reaches observers");
                var reached = root.AddComponent<PlayerReachedCondition>();
                Check(!reached.IsComplete, "arrival condition cannot complete without actual player and destination references");
                var destination = Child("RewardDestination", new Vector3(6, 0, 6)).transform;
                reached.player = playerObject.transform; reached.destination = destination; reached.halfExtents = new Vector3(3, 2, 1);
                playerObject.transform.position = destination.TransformPoint(new Vector3(3, 2, 1));
                Check(reached.IsComplete, "arrival reward includes the exact authored local boundary");
                playerObject.transform.position = destination.TransformPoint(new Vector3(3.1f, 0, 0));
                Check(!reached.IsComplete, "being outside an authored arrival box cannot complete it");
                destination.localRotation = Quaternion.Euler(0, 90, 0); destination.localScale = new Vector3(2, 1, .5f);
                playerObject.transform.position = destination.TransformPoint(new Vector3(2.8f, 1.8f, .8f));
                Check(reached.IsComplete, "arrival condition respects actual destination rotation and scale");
                observer.lessons[0].completionCondition = reached; observer.lessons[0].completionMessage = "Reached real destination";
                observer.ObserveWorld();
                Check(rewards == 2, "observed physical arrival emits the authored lesson reward");
                playerObject.transform.position = destination.TransformPoint(new Vector3(0, 0, 1.2f));
                Check(!reached.IsComplete, "rotated narrow side of arrival box remains outside");
                observer.ObserveWorld(); playerObject.transform.position = destination.position; observer.ObserveWorld();
                Check(rewards == 2, "leaving and returning to the real destination does not repeat a completion reward");
                return string.Join("\n", results) + "\n" + (results.Count - failures) + "/" + results.Count + " interaction checks passed; failures=" + failures;
            }
            finally { if (root) UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
#endif

