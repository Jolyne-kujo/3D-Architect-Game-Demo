using System;
using System.Collections.Generic;
using System.Reflection;
using Courtyard.Water;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    /// <summary>Disposable, real-water/local-physics checks; never edits the user's open scene.</summary>
    public static class GuidedPlatformChecks
    {
        public static string Run()
        {
            var lines = new List<string>();
            void Check(bool pass, string label)
            {
                lines.Add((pass ? "PASS " : "FAIL ") + label);
                if (!pass) throw new InvalidOperationException(string.Join("\n", lines));
            }
            Type componentType = typeof(CourtyardWalker).Assembly.GetType("CoastalTemple.Mechanisms.GuidedBuoyantPlatform");
            if (componentType == null)
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                    if ((componentType = assembly.GetType("CoastalTemple.Mechanisms.GuidedBuoyantPlatform")) != null) break;
            Check(componentType != null, "a reusable guided buoyancy component exists");
            if (!Application.isPlaying) return string.Join("\n", lines) + "\nEnter Play mode for water, transform, and rider checks.";
            var scene = SceneManager.CreateScene("TemporaryGuidedPlatformChecks", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var holder = new GameObject("GuidedPlatformChecks");
            SceneManager.MoveGameObjectToScene(holder, scene);
            WaterVolume water = null;
            void Field(Component c, string name, object value) => componentType.GetField(name).SetValue(c, value);
            object Property(Component c, string name) => componentType.GetProperty(name).GetValue(c);
            object Call(Component c, string name, params object[] values) => componentType.GetMethod(name).Invoke(c, values);
            Component Make(string name, Vector3 position, Quaternion rotation, Vector3 scale, bool horizontal)
            {
                var frame = new GameObject(name); frame.transform.SetParent(holder.transform, false); frame.SetActive(false);
                frame.transform.SetPositionAndRotation(position, rotation); frame.transform.localScale = scale;
                var deck = GameObject.CreatePrimitive(PrimitiveType.Cube); deck.name = "Platform"; deck.transform.SetParent(frame.transform, false);
                // Geometry is authored in a child, leaving the moving rigidbody's scale at one.
                var geometry = new GameObject("DeckGeometry"); geometry.transform.SetParent(deck.transform, false);
                UnityEngine.Object.DestroyImmediate(deck.GetComponent<MeshRenderer>());
                UnityEngine.Object.DestroyImmediate(deck.GetComponent<MeshFilter>());
                var shape = deck.GetComponent<BoxCollider>(); shape.size = new Vector3(3, .6f, 3);
                var body = deck.AddComponent<Rigidbody>();
                var c = frame.AddComponent(componentType);
                Field(c, "platform", deck.transform); Field(c, "platformBody", body);
                Field(c, "mode", Enum.ToObject(componentType.GetField("mode").FieldType, horizontal ? 1 : 0));
                Field(c, "pathStart", horizontal ? new Vector3(-3, 0, 0) : new Vector3(0, -1, 0));
                Field(c, "pathEnd", horizontal ? new Vector3(3, 0, 0) : new Vector3(0, 3, 0));
                Field(c, "displacementSize", new Vector3(3, .6f, 3));
                Field(c, "initialTravel", 0f); Field(c, "floatMin", -2f); Field(c, "floatMax", 2f);
                frame.SetActive(true); Check((bool)Call(c, "Initialize"), name + " initializes its own child body");
                body.interpolation = RigidbodyInterpolation.None;
                return c;
            }
            var physics = scene.GetPhysicsScene();
            void Advance(Component c, float seconds)
            {
                for (float t = 0; t < seconds - .0001f; t += .02f) { Call(c, "Simulate", .02f); physics.Simulate(.02f); }
            }
            try
            {
                var basin = new GameObject("RealWater"); basin.SetActive(false); basin.transform.SetParent(holder.transform, false);
                basin.transform.position = new Vector3(3000, 20, 3000);
                water = basin.AddComponent<WaterVolume>(); water.sizeX = water.sizeZ = 40; water.cellSize = 1;
                water.bottom = -5; water.initialLevel = 0; basin.SetActive(true); water.Initialize(); water.enabled = false;
                // Explicit assignment exercises the authored-water option; automatic discovery is checked below.
                var vertical = Make("Vertical", new Vector3(3000, 20, 3000), Quaternion.identity, Vector3.one, false);
                Field(vertical, "water", water); water.enabled = true;
                var body = (Rigidbody)componentType.GetField("platformBody").GetValue(vertical);
                Vector3 framePosition = vertical.transform.position;
                Advance(vertical, 10);
                Check(Mathf.Abs(body.position.y - 20.09f) < .07f, "Archimedes equilibrium matches 350 kg/m3 density and 0.6 m thickness");
                Check(Vector3.Distance(vertical.transform.position, framePosition) < .00001f, "buoyancy moves only the deck, never its rail frame");
                Check(Mathf.Abs(body.position.x - framePosition.x) < .001f && Mathf.Abs(body.position.z - framePosition.z) < .001f,
                    "vertical guides reject lateral motion");
                water.initialLevel = 1; water.ResetWater(); Advance(vertical, 8);
                Check(Mathf.Abs(body.position.y - 21.09f) < .07f, "raising real water raises the floating deck");
                water.initialLevel = -1; water.ResetWater(); Advance(vertical, 8);
                Check(Mathf.Abs(body.position.y - 19.09f) < .07f, "lowering real water lowers the floating deck");
                water.initialLevel = 10; water.ResetWater(); Advance(vertical, 6);
                Check(Vector3.Distance(body.position, (Vector3)Property(vertical, "EndWorld")) < .002f,
                    "submerged platform stops at the upper rail limit");
                water.initialLevel = -5; water.ResetWater(); Advance(vertical, 6);
                Check(Vector3.Distance(body.position, (Vector3)Property(vertical, "StartWorld")) < .002f,
                    "drained water lets gravity return the deck to the lower rail limit");
                water.initialLevel = 0; water.ResetWater();

                var tilted = Make("TiltedScaled", new Vector3(3008, 20, 3000), Quaternion.Euler(12, 43, 17), new Vector3(1.4f, 1.8f, .75f), false);
                Advance(tilted, 10);
                var tiltedBody = (Rigidbody)componentType.GetField("platformBody").GetValue(tilted);
                Vector3 segment = (Vector3)Property(tilted, "EndWorld") - (Vector3)Property(tilted, "StartWorld");
                Vector3 displacement = tiltedBody.position - (Vector3)Property(tilted, "StartWorld");
                Check(Vector3.Cross(segment.normalized, displacement).magnitude < .003f,
                    "rotated and nonuniformly scaled guide keeps the deck on its authored local rail");
                Check((float)Property(tilted, "Submersion") > .15f && (float)Property(tilted, "Submersion") < .6f,
                    "unassigned water is discovered automatically after rotation and scaling");
                Check(Mathf.Abs((float)Property(tilted, "WorldVolume") - 3 * .6f * 3 * 1.4f * 1.8f * .75f) < .005f,
                    "displaced volume includes positive nonuniform root scale");

                var ferry = Make("Horizontal", new Vector3(3000, 20, 3008), Quaternion.Euler(0, 32, 0), new Vector3(1.5f, 1.2f, .8f), true);
                var ferryBody = (Rigidbody)componentType.GetField("platformBody").GetValue(ferry);
                Advance(ferry, 10); Vector3 before = ferryBody.position;
                foreach (int expected in new[] { 1, 0, -1, 0 })
                {
                    Call(ferry, "CycleDirection");
                    Check((int)Property(ferry, "Direction") == expected, "horizontal shared control cycles to direction " + expected);
                }
                Call(ferry, "SetDirection", 1); Advance(ferry, 1);
                Check(Vector3.Distance(before, ferryBody.position) > 1.2f, "horizontal guide moves in its rotated path direction");
                Call(ferry, "SetDirection", 0); float paused = (float)Property(ferry, "NormalizedTravel");
                water.initialLevel = .7f; water.ResetWater(); Advance(ferry, 8);
                Check(Mathf.Abs((float)Property(ferry, "NormalizedTravel") - paused) < .0001f && ferryBody.position.y > before.y + .55f,
                    "horizontal stop preserves path progress while buoyancy follows the new water level");
                Call(ferry, "SetDirection", 1); Advance(ferry, 12);
                Check(Mathf.Abs((float)Property(ferry, "NormalizedTravel") - 1) < .0001f && (int)Property(ferry, "Direction") == 0,
                    "horizontal drive stops exactly at the far endpoint");
                Call(ferry, "CycleDirection");
                Check((int)Property(ferry, "Direction") == -1, "E after the far endpoint starts the return trip");
                Call(ferry, "SetDirection", 0);

                var duplicate = UnityEngine.Object.Instantiate(ferry.gameObject, holder.transform);
                duplicate.name = "IndependentDuplicate"; duplicate.transform.position += Vector3.forward * 6;
                var copied = duplicate.GetComponent(componentType); Call(copied, "Initialize");
                var copiedBody = (Rigidbody)componentType.GetField("platformBody").GetValue(copied);
                Check(copiedBody != ferryBody && copiedBody.transform.IsChildOf(duplicate.transform), "duplicating the frame remaps the platform to its own child body");
                float originalProgress = (float)Property(ferry, "NormalizedTravel"); Call(copied, "SetDirection", 1); Advance(copied, 1);
                Check(Mathf.Abs((float)Property(ferry, "NormalizedTravel") - originalProgress) < .0001f,
                    "operating a duplicate cannot change the original platform");

                // The shared walker carries by attachedRigidbody.position, exactly as on the existing lift.
                var rider = new GameObject("ExistingWalker"); rider.transform.SetParent(holder.transform, false);
                var controller = rider.AddComponent<CharacterController>(); controller.height = 1.8f; controller.radius = .25f;
                controller.center = new Vector3(0, .9f, 0); controller.skinWidth = .025f;
                var walker = rider.AddComponent<CourtyardWalker>(); walker.enabled = false;
                walker.RespawnAt(ferryBody.position + Vector3.up * (.6f * 1.2f / 2 + .05f), 0);
                Physics.SyncTransforms();
                for (int i = 0; i < 30; i++) walker.SimulateMovement(Vector2.zero, false, false, 0, .02f);
                Check(controller.isGrounded, "existing walker can stand on the floating deck collider");
                Vector3 riderBefore = rider.transform.position, deckBefore = ferryBody.position;
                Call(ferry, "SetDirection", -1);
                for (int i = 0; i < 30; i++) { Advance(ferry, .02f); walker.SimulateMovement(Vector2.zero, false, false, 0, .02f); }
                Vector3 expectedCarry = ferryBody.position - deckBefore;
                Check(expectedCarry.magnitude > .5f && Vector3.Distance(rider.transform.position - riderBefore, expectedCarry) < .12f,
                    "existing walker rides horizontal deck movement without a player script change");
                return string.Join("\n", lines) + "\n" + lines.Count + " guided platform checks passed.";
            }
            finally
            {
                if (holder) UnityEngine.Object.DestroyImmediate(holder);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
