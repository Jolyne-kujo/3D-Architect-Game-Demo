#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoastalTemple.Mechanisms
{
    // Editor-only, Play-mode entry point. Physics advances only in a disposable local scene, never the user's open world.
    public static class MechanismComponentChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying)
                throw new InvalidOperationException("Mechanism component checks require Play mode for an isolated local physics scene. Enter Play mode, then call CoastalTemple.Mechanisms.MechanismComponentChecks.Run().");
            var report = new List<string>();
            Scene scene = default;
            GameObject root = null;
            void Check(bool good, string description)
            {
                report.Add((good ? "PASS " : "FAIL ") + description);
                if (!good) throw new InvalidOperationException(string.Join("\n", report));
            }
            try
            {
                scene = SceneManager.CreateScene("TemporaryMechanismChecks", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                root = new GameObject("TemporaryMechanismChecks");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.position = new Vector3(12000, 12000, 12000);
                var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = "AuthoredHorizontalPlatform";
                platform.transform.SetParent(root.transform, false);
                platform.transform.localScale = new Vector3(3, .4f, 3);
                var platformCollider = platform.GetComponent<Collider>();
                var platformRenderer = platform.GetComponent<Renderer>();
                var body = platform.AddComponent<Rigidbody>();
                var weight = new GameObject("AuthoredCounterweight");
                weight.transform.SetParent(root.transform, false);
                weight.transform.localPosition = Vector3.up * 3;
                var motor = root.AddComponent<LinearPlatformMotor>();
                motor.platform = platform.transform; motor.platformBody = body;
                motor.positionsAreLocal = true; motor.bottom = Vector3.zero; motor.top = Vector3.right * 6;
                motor.speed = 2; motor.counterweight = weight.transform; motor.counterweightTravel = Vector3.down * 3;

                var pathObject = new GameObject("AuthoredPath");
                pathObject.transform.SetParent(root.transform, false);
                var segment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                segment.transform.SetParent(pathObject.transform, false);
                segment.transform.localPosition = Vector3.forward * 8;
                var renderer = segment.GetComponent<Renderer>();
                var collider = segment.GetComponent<Collider>();
                var originalMaterial = renderer.sharedMaterial;
                var path = pathObject.AddComponent<SolidPath>();
                path.renderers = new[] { renderer }; path.colliders = new[] { collider };
                var receiverObject = new GameObject("DelayedReceiver");
                receiverObject.transform.SetParent(root.transform, false);
                var receiver = receiverObject.AddComponent<LightReceiver>();
                receiver.RequiredColor = ReceiverColor.Blue;
                receiver.RequiredHitSeconds = .4f; receiver.ReturnGraceSeconds = 9; receiver.Latching = true;
                var driver = pathObject.AddComponent<LightPathDriver>();
                driver.receiver = receiver; driver.path = path;

                var liftObject = new GameObject("CompatibleLift");
                liftObject.transform.SetParent(root.transform, false);
                liftObject.transform.localPosition = Vector3.left * 10;
                var liftPlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                liftPlatform.transform.SetParent(liftObject.transform, false);
                var liftBody = liftPlatform.AddComponent<Rigidbody>();
                var lift = liftObject.AddComponent<LightDrivenLift>();
                lift.platform = liftPlatform.transform; lift.platformBody = liftBody;
                lift.bottom = Vector3.zero; lift.top = Vector3.up * 5; lift.receiverRaise = receiver;
                var leverObject = new GameObject("HandLever");
                leverObject.transform.SetParent(root.transform, false);
                var lever = leverObject.AddComponent<MotorLever>(); lever.motor = motor;
                var otherLeverObject = new GameObject("OtherLandingLever");
                otherLeverObject.transform.SetParent(root.transform, false);
                var otherLever = otherLeverObject.AddComponent<MotorLever>(); otherLever.motor = motor;
                var handleObject = new GameObject("AuthoredHandle");
                handleObject.transform.SetParent(otherLeverObject.transform, false);
                Quaternion handleRest = Quaternion.Euler(8, 13, 5);
                handleObject.transform.localRotation = handleRest;
                otherLever.handle = handleObject.transform;
                otherLever.RefreshHandle();

                root.SetActive(true); motor.Initialize(); lift.Initialize(); path.SetSolid(false);
                body.interpolation = RigidbodyInterpolation.None;
                liftBody.interpolation = RigidbodyInterpolation.None;
                var physics = scene.GetPhysicsScene();
                Physics.SyncTransforms();
                Check(body.isKinematic && !body.useGravity && platformCollider.attachedRigidbody == body,
                    "generic motor uses the authored kinematic collision root");
                Check(motor.BottomWorld == root.transform.position && motor.TopWorld == root.transform.position + Vector3.right * 6,
                    "generic motor resolves a horizontal path through its parent");
                motor.SetDrive(1); motor.Simulate(.5f); physics.Simulate(.5f);
                Check(Mathf.Abs(body.position.x - motor.BottomWorld.x - 1) < .01f,
                    "one motor step physically advances the authored platform exactly one metre");
                Check(platformRenderer.enabled && platformCollider.enabled && Mathf.Abs(weight.transform.localPosition.y - 2.5f) < .01f,
                    "moving platform retains rendering and collision while counterweight follows inversely");
                motor.SetDrive(0); Vector3 paused = body.position;
                for (int i = 0; i < 4; i++) { motor.Simulate(.25f); physics.Simulate(.25f); }
                Check(!motor.IsMoving && Vector3.Distance(body.position, paused) < .001f,
                    "removing a generic drive holds an actual intermediate platform position");
                motor.SetDrive(-1); motor.Simulate(2); physics.Simulate(2);
                Check(Vector3.Distance(body.position, motor.BottomWorld) < .001f,
                    "reverse motion reaches the authored endpoint without overshoot");
                motor.enabled = false; motor.SetDrive(1); motor.Simulate(.5f); physics.Simulate(.5f);
                Check(!motor.IsMoving && Vector3.Distance(body.position, motor.BottomWorld) < .001f,
                    "disabled motor cannot advance its physical platform");
                motor.enabled = true; motor.SetDirection(0);

                driver.EvaluateNow();
                Check(!path.IsSolid && !renderer.enabled && !collider.enabled,
                    "unpowered authored path has neither visible geometry nor collision");
                receiver.BeginTrace(); receiver.Illuminate(LightColorChannel.Blue); receiver.Resolve(.2f); driver.EvaluateNow();
                Check(!path.IsSolid && !renderer.enabled && !collider.enabled,
                    "partial receiver activation cannot expose path collision early");
                receiver.BeginTrace(); receiver.Illuminate(LightColorChannel.Blue); receiver.Resolve(.2f); driver.EvaluateNow();
                Check(path.IsSolid && renderer.enabled && collider.enabled,
                    "completed receiver activation enables both authored rendering and collision");
                lift.Simulate(.02f);
                Check(lift.Direction == 1 && lift.IsMoving,
                    "compatible light adapter samples power through the single motor update");
                lift.SetManualDirection(-1); lift.Simulate(.02f);
                Check(lift.Direction == 0 && !lift.IsMoving,
                    "opposed manual and light commands stop instead of advancing twice");
                lift.StopManual();
                receiver.BeginTrace(); receiver.Resolve(.01f); driver.EvaluateNow(); lift.Simulate(.02f);
                Check(receiver.IsActive && !receiver.IsIlluminated && !path.IsSolid && !renderer.enabled && !collider.enabled,
                    "light loss removes path rendering and collision even when receiver retains grace or latch");
                Check(!lift.IsMoving && lift.Direction == 0,
                    "the light-driven motor stops when the beam moves away despite receiver latch");
                receiver.BeginTrace(); receiver.Illuminate(LightColorChannel.Blue); receiver.Resolve(.4f); driver.EvaluateNow();
                receiver.enabled = false; driver.EvaluateNow(); lift.Simulate(.02f);
                Check(!path.IsSolid && !renderer.enabled && !collider.enabled && !lift.IsMoving,
                    "disabling a still-active receiver cannot leave a platform moving or a path solid");
                receiver.enabled = true; driver.EvaluateNow(); driver.enabled = false; driver.EvaluateNow();
                Check(!path.IsSolid && !renderer.enabled && !collider.enabled,
                    "disabling the path driver removes only its assigned geometry and collision");
                path.SetSolid(true); path.enabled = false; path.SetSolid(true);
                Check(!path.IsSolid && !renderer.enabled && !collider.enabled,
                    "disabling a path independently clears its visible and physical state");
                path.enabled = true;
                int before = root.GetComponentsInChildren<Transform>(true).Length;
                for (int i = 0; i < 10; i++) { path.SetSolid(true); path.SetSolid(true); path.SetSolid(false); }
                Check(renderer.sharedMaterial == originalMaterial && root.GetComponentsInChildren<Transform>(true).Length == before,
                    "repeated path switching does not create geometry or material instances");
                Check(platformRenderer.enabled && platformCollider.enabled,
                    "path switching does not modify an unrelated platform");

                lever.SetDirection(0);
                foreach (int expected in new[] { 1, 0, -1, 0 })
                {
                    lever.Use(null);
                    Check(lever.Direction == expected && motor.ManualDirection == expected,
                        "real lever selects drive " + expected + " without simulating the motor");
                }
                lever.Use(null); otherLever.RefreshHandle();
                Check(otherLever.Direction == 1 && otherLever.DisplayPrompt.Contains(otherLever.forwardLabel),
                    "a second landing handle reads actual motion and prompt from the shared motor");
                Check(Quaternion.Angle(handleObject.transform.localRotation,
                    handleRest * Quaternion.AngleAxis(otherLever.handleAngle, otherLever.rotationAxis)) < .01f,
                    "untouched handle visual follows the other landing's forward command");
                otherLever.Use(null);
                Check(motor.Direction == 0 && lever.Direction == 0 && Quaternion.Angle(handleObject.transform.localRotation, handleRest) < .01f,
                    "using another landing handle stops existing motion and restores its authored pose");
                lever.Use(null);
                Check(motor.Direction == -1 && otherLever.Direction == -1,
                    "switching back to the first handle reverses after the shared forward stop");
                otherLever.Use(null);
                Check(motor.Direction == 0 && lever.Direction == 0,
                    "either landing can stop the same reverse command");
                motor.SetDirection(-1); lever.Use(null); otherLever.Use(null);
                Check(motor.Direction == 1 && lever.Direction == 1,
                    "external reverse commands join the shared stop then forward cycle");
                motor.SetDirection(0); otherLever.Use(null);
                Check(motor.Direction == -1 && lever.Direction == -1,
                    "external forward stop advances the shared cycle to reverse instead of restarting forward");
                motor.SetDirection(0);
                using (var serialized = new SerializedObject(lift))
                {
                    string[] preserved = { "receiverRaise", "receiverLower", "platform", "platformBody", "positionsAreLocal", "bottom", "top", "speed", "counterweight", "counterweightTravel" };
                    bool allPresent = true;
                    foreach (string field in preserved) allPresent &= serialized.FindProperty(field) != null;
                    Check(allPresent, "all legacy serialized lift fields remain on the original component identity");
                }
                return string.Join("\n", report) + "\n" + report.Count + " mechanism component checks passed in an isolated physics scene.";
            }
            finally
            {
                if (root) UnityEngine.Object.DestroyImmediate(root);
                // Objects are removed immediately; the empty runtime scene unloads on the next player-loop update.
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
#endif
