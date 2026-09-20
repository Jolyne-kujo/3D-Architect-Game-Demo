#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using CoastalTemple.Interaction;
using UnityEditor;
using UnityEngine;

namespace CoastalTemple.LightPuzzles.Editor
{
    public static class MechanismLightChecks
    {
        [Serializable] public class Result { public string name; public bool passed; public string detail; }
        [Serializable] public class Report { public int passed; public int failed; public List<Result> results = new List<Result>(); }
        static readonly Vector3 Offset = new Vector3(11000f, 400f, 11000f);
        static GameObject root;
        static Report report;

        [MenuItem("Coastal Temple/Tests/Color Light Mechanisms")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            report = new Report();
            Test("yellow never latches even after prolonged exposure", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Yellow);
                var wall = Curtain(new Vector3(0, 0, 4));
                Step(20f);
                Check(wall.IsOpen && !wall.IsPermanent, "yellow must only open temporarily");
                source.SetChannel(LightColorChannel.None); Step(.16f);
                Check(!wall.IsOpen && wall.PhysicalCollider.enabled, "yellow permanently removed wall");
            });
            Test("red requires uninterrupted exposure and then remains gone", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Red);
                var wall = Curtain(new Vector3(0, 0, 4));
                Step(.2f); Check(!wall.IsOpen, "red opened too early");
                source.SetChannel(LightColorChannel.None); Step(.01f);
                source.SetChannel(LightColorChannel.Red); Step(.2f);
                Check(!wall.IsOpen, "interruption retained red charge");
                Step(.15f); Check(wall.IsOpen && wall.IsPermanent, "continuous red failed to latch");
                source.SetChannel(LightColorChannel.None); Step(20f);
                Check(wall.IsOpen && !wall.PhysicalCollider.enabled, "permanent wall returned");
                wall.ResetState(); Check(!wall.IsOpen && !wall.IsPermanent, "reset failed");
            });
            Test("yellow does not count toward red exposure", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Yellow);
                var wall = Curtain(new Vector3(0, 0, 4));
                Step(10f); source.SetChannel(LightColorChannel.Red); Step(.2f);
                Check(!wall.IsPermanent, "yellow contributed red charge");
                source.SetChannel(LightColorChannel.Yellow); Step(.01f);
                source.SetChannel(LightColorChannel.Red); Step(.2f);
                Check(!wall.IsPermanent, "yellow preserved interrupted red charge");
            });
            Test("all source colors aggregate independently of creation order", () =>
            {
                CheckColorOrder(false); CheckColorOrder(true);
            });
            Test("blue cannot open the red wall or hold a yellow opening", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Blue);
                var wall = Curtain(new Vector3(0, 0, 4));
                Step(20f); Check(!wall.IsOpen, "blue opened wall");
                source.SetChannel(LightColorChannel.Yellow); Step(.01f);
                source.SetChannel(LightColorChannel.Blue); Step(.16f);
                Check(!wall.IsOpen && !wall.IsPermanent, "blue held yellow opening");
            });
            Test("yellow wall preserves optical detection and passes light onward", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Yellow);
                var wall = Curtain(new Vector3(0, 0, 3));
                var receiverObject = Cube("Receiver", new Vector3(0, 0, 7), Vector3.one);
                var receiver = receiverObject.AddComponent<LightReceiver>();
                receiver.RequiredColor = ReceiverColor.Yellow;
                receiver.RequiredHitSeconds = 0f; Activate(receiver);
                Step(.01f); Step(.01f);
                Check(wall.IsIlluminated && wall.IsOpen && receiver.IsActive, "invisible wall lost detection or blocked light");
                Check(receiver.IlluminatedColors == LightColorChannel.Yellow, "beam color was lost behind open wall");
            });
            Test("rotated scaled wall waits for an occupant before restoring", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Yellow);
                var wall = Curtain(new Vector3(0, 0, 4));
                wall.transform.rotation = Quaternion.Euler(0, 37, 0);
                wall.transform.localScale = new Vector3(4, 2, .8f);
                Step(.01f); Check(wall.IsOpen, "rotated scaled wall did not detect beam");
                var player = ObjectAt("Occupant", new Vector3(0, 0, 4));
                player.AddComponent<CharacterController>();
                source.SetChannel(LightColorChannel.None); Step(.16f);
                Check(wall.IsOpen && wall.RestorePending && !wall.PhysicalCollider.enabled, "wall restored inside occupant");
                player.transform.position += Vector3.right * 8; Step(.01f);
                Check(!wall.IsOpen && !wall.RestorePending && wall.PhysicalCollider.enabled, "clear wall did not restore");
            });
            Test("legacy modes retain their color independent behavior", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Blue);
                var wall = Curtain(new Vector3(0, 0, 4));
                wall.Mode = CurtainMode.Permanent; Step(.35f);
                source.SetChannel(LightColorChannel.None); Step(1f);
                Check(wall.IsPermanent && wall.IsOpen, "legacy permanent behavior changed");
                wall.ResetState(); wall.Mode = CurtainMode.WhileIlluminated;
                source.SetChannel(LightColorChannel.Red); Step(0f);
                Check(wall.IsOpen && !wall.IsPermanent, "legacy temporary behavior changed");
                source.SetChannel(LightColorChannel.None); Step(.16f);
                Check(!wall.IsOpen, "legacy temporary wall did not return");
            });
            Test("E interaction cycles red yellow blue off and respects reach", () =>
            {
                var source = Source(new Vector3(0, 0, 2), LightColorChannel.Red);
                var console = source.gameObject.AddComponent<LaserEmitterConsole>();
                console.emitter = source; Activate(console);
                var player = ObjectAt("Interactor", Vector3.zero).AddComponent<PlayerInteractor>();
                player.readKeyboardInput = false;
                Physics.SyncTransforms();
                Check(player.Interact(console) && source.Channel == LightColorChannel.Yellow && source.Powered, "E did not advance red to yellow");
                Check(player.Interact(console) && source.Channel == LightColorChannel.Blue && source.Powered, "E did not advance yellow to blue");
                Check(player.Interact(console) && !source.Powered && source.Channel == LightColorChannel.None, "E did not switch blue off");
                Step(0f); Check(source.SegmentCount == 0, "off emitter still traced");
                Check(player.Interact(console) && source.Channel == LightColorChannel.Red && source.Powered, "E did not restart red");
                player.transform.position += Vector3.right * 20;
                Check(!player.Interact(console) && source.Channel == LightColorChannel.Red, "out of reach interaction changed emitter");
            });
            Test("E interaction cannot operate a console through an opaque wall", () =>
            {
                var source = Source(new Vector3(0, 0, 2), LightColorChannel.Red);
                var console = source.gameObject.AddComponent<LaserEmitterConsole>();
                console.emitter = source; Activate(console);
                var player = ObjectAt("Interactor", Vector3.zero).AddComponent<PlayerInteractor>();
                player.readKeyboardInput = false;
                Cube("Obstruction", new Vector3(0, 0, 1), new Vector3(3, 3, .2f));
                Physics.SyncTransforms();
                Check(!player.Interact(console) && source.Channel == LightColorChannel.Red, "console ignored interaction visibility");
            });
            Test("origin follows a moved rotated nonuniformly scaled emitter", () =>
            {
                var source = Source(Vector3.zero, LightColorChannel.Yellow);
                source.transform.rotation = Quaternion.Euler(12, 53, 4);
                source.transform.localScale = new Vector3(2, .5f, 1.5f);
                source.transform.position += new Vector3(3, 2, -1);
                var origin = new GameObject("Origin").transform;
                origin.SetParent(source.transform, false); origin.localPosition = new Vector3(0, .4f, .2f);
                source.Origin = origin;
                var wall = Curtain(Vector3.zero);
                wall.transform.position = origin.position + origin.forward * 5;
                wall.transform.rotation = origin.rotation;
                Step(.01f);
                Check(wall.IsOpen, "transformed emitter missed wall");
                Check(Vector3.Distance(source.GetSegment(0).Start, origin.position) < .0001f, "beam ignored authored origin");
            });
            Test("blue beam replaces red material tint without mutating the asset", () =>
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                Check(shader, "URP Unlit shader missing");
                var material = new Material(shader);
                try
                {
                    material.SetColor("_BaseColor", Color.red);
                    var source = Source(Vector3.zero, LightColorChannel.Blue);
                    source.RenderBeam = true; source.BeamMaterial = material; Step(0f);
                    var line = source.GetComponentInChildren<LineRenderer>();
                    Check(line && line.sharedMaterial != material, "shared red material was reused");
                    Color tint = line.sharedMaterial.GetColor("_BaseColor");
                    Check(tint.b > tint.r * 5 && line.startColor == Color.white, "blue beam was multiplied by red");
                    Check(material.GetColor("_BaseColor") == Color.red, "source material asset was modified");
                    source.SetChannel(LightColorChannel.Yellow); Step(0f);
                    tint = line.sharedMaterial.GetColor("_BaseColor");
                    Check(tint.r > tint.b * 5 && tint.g > tint.b * 5, "existing line failed to update color");
                }
                finally { UnityEngine.Object.DestroyImmediate(material); }
            });
            string json = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory("Documentation/CoastalTemple");
            File.WriteAllText("Documentation/CoastalTemple/MechanismLightChecks.json", json);
            return json;
        }

        static void CheckColorOrder(bool reverse)
        {
            float x = reverse ? 8f : 0f;
            var first = Source(new Vector3(x - .3f, 0, 0), reverse ? LightColorChannel.Yellow : LightColorChannel.Red);
            var second = Source(new Vector3(x + .3f, 0, 0), reverse ? LightColorChannel.Red : LightColorChannel.Yellow);
            var wall = Curtain(new Vector3(x, 0, 4));
            Step(.2f);
            Check(wall.IsOpen && !wall.IsPermanent, "combined light opened permanently too soon");
            Check(wall.IlluminatedColors == (LightColorChannel.Red | LightColorChannel.Yellow), "source order discarded a color");
            Step(.15f); Check(wall.IsPermanent, "yellow prevented red from latching");
            first.SetChannel(LightColorChannel.None); second.SetChannel(LightColorChannel.None);
            Step(.2f); Check(wall.IsOpen, "combined light failed permanent behavior");
        }
        static void Test(string name, Action action)
        {
            root = new GameObject("Color light mechanism checks") { hideFlags = HideFlags.DontSave };
            var result = new Result { name = name };
            try { action(); result.passed = true; result.detail = "passed"; report.passed++; }
            catch (Exception error) { result.detail = error.ToString(); report.failed++; }
            finally
            {
                foreach (var component in root.GetComponentsInChildren<MonoBehaviour>())
                    component.SendMessage("OnDisable", SendMessageOptions.DontRequireReceiver);
                UnityEngine.Object.DestroyImmediate(root); Physics.SyncTransforms();
            }
            report.results.Add(result);
        }
        static void Step(float seconds) { Physics.SyncTransforms(); LightPuzzleWorld.Step(seconds); }
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        static void Activate(MonoBehaviour component)
        {
            component.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            component.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver);
        }
        static GameObject ObjectAt(string name, Vector3 position)
        {
            var value = new GameObject(name); value.transform.SetParent(root.transform);
            value.transform.position = Offset + position; return value;
        }
        static GameObject Cube(string name, Vector3 position, Vector3 size)
        {
            var value = GameObject.CreatePrimitive(PrimitiveType.Cube); value.name = name;
            value.transform.SetParent(root.transform); value.transform.position = Offset + position;
            value.transform.localScale = size; return value;
        }
        static LaserEmitter Source(Vector3 position, LightColorChannel channel)
        {
            var source = ObjectAt("Emitter", position).AddComponent<LaserEmitter>();
            source.RenderBeam = false; source.SetChannel(channel); Activate(source); return source;
        }
        static RedStoneCurtain Curtain(Vector3 position)
        {
            var value = Cube("Wall", position, new Vector3(2, 3, .4f));
            var wall = value.AddComponent<RedStoneCurtain>(); wall.Mode = CurtainMode.ByLightColor;
            wall.PhysicalCollider = value.GetComponent<BoxCollider>(); Activate(wall); return wall;
        }
    }
}
#endif
