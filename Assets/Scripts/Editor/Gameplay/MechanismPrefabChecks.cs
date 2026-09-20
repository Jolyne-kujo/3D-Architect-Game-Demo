#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Portals;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    /// <summary>Checks the delivered prefab assets in disposable scenes, without a tutorial scene or authoring builder.</summary>
    public static class MechanismPrefabChecks
    {
        const string Folder = "Assets/Prefabs/Mechanisms/";
        static readonly string[] Names = { "LaserDevice", "RedStoneCurtain", "LightReceiver", "RedLightReceiver", "YellowLightReceiver", "BlueLightReceiver", "LightBridge", "LightDrivenLift",
            "VerticalBuoyantPlatform", "HorizontalBuoyantPlatform", "Portal1", "Portal2", "PortalPair", "DrainGate", "PoolsideDrainConsole", "WaterMirage" };
        static readonly Vector3 Offset = new Vector3(15000, 800, 15000);
        [Serializable] public class Result { public string name; public bool passed; public string detail; }
        [Serializable] public class Report
        {
            public string mode;
            public int passed, failed, assetsChecked, serializedReferences, missingReferences, externalObjectReferences;
            public bool originalScenesPreserved;
            public List<Result> results = new List<Result>();
        }
        sealed class Context
        {
            public Scene scene;
            public GameObject root;
            public PhysicsScene physics;
        }
        struct SceneSnapshot { public Scene scene; public bool dirty; }
        static Context context;
        static Report report;

        [MenuItem("Coastal Temple/Tests/Mechanism prefab assets (Edit mode)")]
        public static void RunAssetsMenu() => Debug.Log(RunAssets());
        [MenuItem("Coastal Temple/Tests/Mechanism prefabs with physics (Play mode)")]
        public static void RunMenu() => Debug.Log(Run());

        public static string RunAssets()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Run() in Play mode; RunAssets() is the Edit-mode asset check.");
            return RunSuite(false);
        }
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode for isolated real-body simulation, then run MechanismPrefabChecks.Run(). RunAssets() works in Edit mode.");
            return RunSuite(true);
        }

        static string RunSuite(bool play)
        {
            report = new Report { mode = play ? "Prefab assets and isolated local physics" : "Prefab assets only" };
            var scenes = new List<SceneSnapshot>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                scenes.Add(new SceneSnapshot { scene = scene, dirty = scene.isDirty });
            }
            Scene activeScene = SceneManager.GetActiveScene();
            Scene testScene = play
                ? SceneManager.CreateScene("DisposableMechanismPrefabChecks", new CreateSceneParameters(LocalPhysicsMode.Physics3D))
                : EditorSceneManager.NewPreviewScene();
            context = new Context { scene = testScene, physics = testScene.GetPhysicsScene() };
            try
            {
                using (new IsolatedLightRegistry())
                {
                    foreach (string name in Names)
                    {
                        string captured = name;
                        Test(captured + " asset, drag-out, and duplicate references", () => CheckAsset(captured));
                    }
                    if (play)
                    {
                        foreach(var color in new[]{LightColorChannel.Red,LightColorChannel.Yellow,LightColorChannel.Blue})
                        {
                            var captured=color;
                            Test(captured+" receiver accepts only matching laser and fires its effect",()=>CheckReceiverColor(captured));
                        }
                        Test("Wrong color interrupts receiver charge",CheckInterruptedColorCharge);
                        Test("Large current lamp and small next lamp follow the full E cycle",CheckLaserReadout);
                        for (int i = 0; i < 3; i++)
                        {
                            int pose = i;
                            Test("laser and red/yellow wall after root transform " + pose, () => CheckCurtain(pose));
                            Test("light bridge on/off after root transform " + pose, () => CheckBridge(pose));
                            Test("lift physical raise/hold/lower after root transform " + pose, () => CheckLift(pose));
                        }
                        Test("vertical buoyancy prefab follows water on transformed rail", CheckVertical);
                        Test("horizontal buoyancy prefab moves and holds its transformed path", CheckHorizontal);
                        Test("portal pair duplicate maps within its own transformed pair", CheckPortalPair);
                        Test("drain prefab discovers local water and opens/resets it", CheckDrain);
                        Test("mirage prefab binds water, projects, and switches authored geometry", CheckMirage);
                    }
                }
            }
            finally
            {
                if (context.root) Object.DestroyImmediate(context.root);
                if (testScene.IsValid() && testScene.isLoaded)
                {
                    if (play) SceneManager.UnloadSceneAsync(testScene);
                    else EditorSceneManager.ClosePreviewScene(testScene);
                }
                if (activeScene.IsValid() && activeScene.isLoaded && SceneManager.GetActiveScene() != activeScene)
                    SceneManager.SetActiveScene(activeScene);
                context = null;
            }
            report.originalScenesPreserved = SceneManager.GetActiveScene() == activeScene;
            foreach (var before in scenes)
                report.originalScenesPreserved &= before.scene.IsValid() && before.scene.isLoaded && before.scene.isDirty == before.dirty;
            var preservation = new Result { name = "original scenes remain loaded with original active/dirty state", passed = report.originalScenesPreserved,
                detail = report.originalScenesPreserved ? "passed" : "scene state changed" };
            report.results.Add(preservation);
            if (preservation.passed) report.passed++; else report.failed++;
            string json = JsonUtility.ToJson(report, true);
            Directory.CreateDirectory("Documentation/CoastalTemple");
            File.WriteAllText("Documentation/CoastalTemple/MechanismPrefab" + (play ? "Checks" : "AssetChecks") + ".json", json);
            return json;
        }

        static void Test(string name, Action action)
        {
            context.root = new GameObject("Disposable prefab test") { hideFlags = HideFlags.DontSave };
            context.root.SetActive(false);
            SceneManager.MoveGameObjectToScene(context.root, context.scene);
            var result = new Result { name = name };
            try { action(); result.passed = true; result.detail = "passed"; report.passed++; }
            catch (Exception error) { result.detail = error.ToString(); report.failed++; }
            finally { Object.DestroyImmediate(context.root); context.root = null; Physics.SyncTransforms(); }
            report.results.Add(result);
        }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        static GameObject Spawn(string name, Transform parent = null, Vector3? localPosition = null)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Check(asset, "Missing prefab " + name);
            var value = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent ? parent : context.root.transform);
            if (localPosition.HasValue) value.transform.localPosition = localPosition.Value;
            // Portal rendering's player discovery belongs to normal gameplay, not an isolated asset test.
            foreach (var surface in value.GetComponentsInChildren<PortalSurface>(true)) surface.enabled = false;
            return value;
        }
        static void Activate() { context.root.SetActive(true); Physics.SyncTransforms(); }
        static Transform Rig(int pose)
        {
            var rig = new GameObject("Transformed mechanism group").transform;
            rig.SetParent(context.root.transform, false);
            rig.position = Offset + new Vector3(pose * 60, 0, 0);
            if (pose == 1) { rig.rotation = Quaternion.Euler(0, 62, 0); rig.localScale = Vector3.one * 1.4f; }
            if (pose == 2) { rig.rotation = Quaternion.Euler(11, 43, 7); rig.localScale = new Vector3(1.6f, .8f, 1.25f); }
            return rig;
        }
        static LaserEmitter Laser(Transform rig, Vector3 localPosition)
        {
            var value = Spawn("LaserDevice", rig, localPosition).GetComponent<LaserEmitter>();
            value.RenderBeam = false; return value;
        }
        static void LightStep(float seconds) { Physics.SyncTransforms(); LightPuzzleWorld.Step(seconds); }
        static void Advance(LinearPlatformMotor motor, float seconds)
        {
            for (float t = 0; t < seconds - .0001f; t += .02f) { motor.Simulate(.02f); context.physics.Simulate(.02f); }
        }
        static void Advance(GuidedBuoyantPlatform rail, float seconds)
        {
            for (float t = 0; t < seconds - .0001f; t += .02f) { rail.Simulate(.02f); context.physics.Simulate(.02f); }
        }

        static void CheckAsset(string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + ".prefab");
            Check(asset && PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.NotAPrefab, "Asset is not a prefab: " + name);
            CheckReferences(asset); CheckBindings(asset);
            var first = Spawn(name);
            Check(PrefabUtility.GetCorrespondingObjectFromSource(first) == asset, "Drag-out lost its source prefab connection");
            var copy = Object.Instantiate(first, context.root.transform);
            copy.name = name + " duplicate";
            CheckReferences(first); CheckReferences(copy); CheckBindings(first); CheckBindings(copy);
            Check(first.GetComponentsInChildren<Transform>(true).Length == copy.GetComponentsInChildren<Transform>(true).Length,
                "Duplicate hierarchy differs");
            report.assetsChecked++;
        }
        static void CheckReferences(GameObject owner)
        {
            var problems = new List<string>();
            foreach (var node in owner.GetComponentsInChildren<Transform>(true))
            {
                int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject);
                report.missingReferences += missing;
                if (missing != 0) problems.Add(node.name + " has " + missing + " missing scripts");
                foreach (var component in node.GetComponents<Component>())
                {
                    if (!component) continue;
                    using (var serialized = new SerializedObject(component))
                    {
                        var property = serialized.GetIterator();
                        while (property.Next(true))
                        {
                            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                            report.serializedReferences++;
                            var reference = property.objectReferenceValue;
                            if (!reference && property.objectReferenceEntityIdValue != default)
                            {
                                report.missingReferences++; problems.Add(node.name + "." + property.propertyPath + " is missing");
                            }
                            if (!reference || property.propertyPath.StartsWith("m_", StringComparison.Ordinal)) continue;
                            Transform referenced = reference is Component c ? c.transform : reference is GameObject g ? g.transform : null;
                            if (referenced && !referenced.IsChildOf(owner.transform))
                            {
                                report.externalObjectReferences++; problems.Add(node.name + "." + property.propertyPath + " points outside its own prefab instance");
                            }
                        }
                    }
                }
                foreach (var renderer in node.GetComponents<Renderer>())
                    foreach (var material in renderer.sharedMaterials)
                        Check(material && material.shader, node.name + " contains a missing material/shader");
            }
            Check(problems.Count == 0, string.Join("\n", problems));
        }
        static void CheckBindings(GameObject root)
        {
            foreach (var source in root.GetComponentsInChildren<LaserEmitter>(true))
                Check(source.Origin && source.IgnoreRoot && source.BeamMaterial, "Emitter lost its origin, ignore root, or beam material");
            foreach (var console in root.GetComponentsInChildren<LaserEmitterConsole>(true))
                Check(console.emitter && console.indicator && console.interactionPoint, "Emitter control lost a required reference");
            foreach (var wall in root.GetComponentsInChildren<RedStoneCurtain>(true))
                Check(wall.Mode == CurtainMode.ByLightColor && wall.PhysicalCollider && wall.OpticalCollider && wall.Visuals.Length > 0,
                    "New wall prefab is not configured for color rules");
            foreach (var receiver in root.GetComponentsInChildren<LightReceiver>(true)) Check(receiver.OpticalCollider, "Receiver has no optical shape");
            foreach (var driver in root.GetComponentsInChildren<LightPathDriver>(true))
                Check(driver.receiver && driver.path && driver.path.renderers.Length > 0 && driver.path.colliders.Length > 0, "Bridge lost its receiver or authored spans");
            foreach (var lift in root.GetComponentsInChildren<LightDrivenLift>(true))
                Check(lift.platform && lift.platformBody && lift.receiverRaise && lift.receiverLower && lift.positionsAreLocal,
                    "Lift lost its internal deck, receivers, or local path setting");
            foreach (var lever in root.GetComponentsInChildren<MotorLever>(true)) Check(lever.motor && lever.handle, "Lift lever lost its own motor or handle");
            foreach (var rail in root.GetComponentsInChildren<GuidedBuoyantPlatform>(true))
                Check(rail.platform && rail.platformBody && rail.platform.parent == rail.transform && rail.pathStart != rail.pathEnd,
                    "Buoyant frame lost its owned moving deck or path");
            foreach (var control in root.GetComponentsInChildren<GuidedBuoyantPlatformControl>(true)) Check(control.platform && control.interactionPoint, "Ferry control lost its own platform");
            foreach (var surface in root.GetComponentsInChildren<PortalSurface>(true))
                Check(surface.Screen && surface.GetComponent<LightPortal>() && surface.Screen.sharedMaterial.shader.name == "Coastal Temple/Portal Window",
                    "Portal window lost its renderer, portal, or shader");
            var portals = root.GetComponentsInChildren<LightPortal>(true);
            if (portals.Length == 2)
                Check(portals[0].Paired == portals[1] && portals[1].Paired == portals[0], "Pair links escaped the duplicated pair");
            foreach (var drain in root.GetComponentsInChildren<DrainGateDevice>(true)) Check(drain.receiver && drain.gate, "Drain lost its light receiver or hinge");
            foreach (var mirage in root.GetComponentsInChildren<WaterMirage>(true))
                Check(mirage.source && mirage.sampleObject && mirage.projectionPlane && mirage.projectionMarker && mirage.markerRenderer && mirage.target
                    && mirage.aimReference && mirage.solidSteps.Length > 0 && mirage.stepRenderers.Length > 0 && mirage.stepColliders.Length > 0,
                    "Mirage lost an authored projection or solid-step reference");
        }

        static void CheckReceiverColor(LightColorChannel required)
        {
            var rig=Rig(1);var receiver=Spawn(required+"LightReceiver",rig).GetComponentInChildren<LightReceiver>();
            var source=Laser(rig,new Vector3(0,-.3f,-4));int activations=0;receiver.Activated.AddListener(()=>activations++);
            Activate();
            Check(receiver.AcceptedChannel==required,"Delivered receiver has wrong configured color");
            foreach(var color in new[]{LightColorChannel.Red,LightColorChannel.Yellow,LightColorChannel.Blue})
            {
                receiver.ResetState();activations=0;source.SetChannel(color);LightStep(receiver.RequiredHitSeconds+.1f);
                bool expected=color==required;
                Check(receiver.IsActive==expected&&activations==(expected?1:0),color+" incorrectly triggered "+required+" effect");
                Check(receiver.IsMatchingIlluminated==expected,"Matching illumination disagrees with color");
            }
        }
        static void CheckInterruptedColorCharge()
        {
            var rig=Rig(0);var receiver=Spawn("BlueLightReceiver",rig).GetComponentInChildren<LightReceiver>();
            var source=Laser(rig,new Vector3(0,-.3f,-4));receiver.RequiredHitSeconds=.3f;receiver.ReturnGraceSeconds=0;Activate();
            source.SetChannel(LightColorChannel.Blue);LightStep(.2f);Check(!receiver.IsActive,"Activated before charge completed");
            source.SetChannel(LightColorChannel.Red);LightStep(.05f);Check(receiver.Progress==0,"Wrong color retained charge");
            source.SetChannel(LightColorChannel.Blue);LightStep(.2f);Check(!receiver.IsActive,"Wrong-color interval counted toward blue charge");
            LightStep(.11f);Check(receiver.IsActive,"Uninterrupted matching beam failed");
        }
        static void CheckLaserReadout()
        {
            var control=Spawn("LaserDevice",Rig(2)).GetComponent<LaserEmitterConsole>();Activate();
            foreach(var expected in new[]{LightColorChannel.None,LightColorChannel.Yellow,LightColorChannel.Blue,LightColorChannel.Red,LightColorChannel.None})
            {
                Check(control.CurrentChannel==expected,"E cycle has wrong current color");control.RefreshVisuals();
                var current=new MaterialPropertyBlock();var next=new MaterialPropertyBlock();control.currentReadout.GetPropertyBlock(current);control.nextIndicator.GetPropertyBlock(next);
                Color shown=expected==LightColorChannel.None?control.offColor:LaserEmitter.ColorForChannel(expected);
                Color upcoming=control.NextChannel==LightColorChannel.None?control.offColor:LaserEmitter.ColorForChannel(control.NextChannel);
                Check(current.GetColor("_BaseColor")==shown&&next.GetColor("_BaseColor")==upcoming,"Displayed current/next colors disagree with next E action");
                Check(control.currentReadout.transform.localPosition.y>control.nextIndicator.transform.localPosition.y&&control.currentReadout.transform.localScale.x>control.nextIndicator.transform.localScale.x*1.8f,"Current lamp is not the larger upper lamp");
                control.Use(null);
            }
        }
        static void CheckCurtain(int pose)
        {
            Transform rig = Rig(pose);
            var source = Laser(rig, Vector3.zero);
            var wall = Spawn("RedStoneCurtain", rig, new Vector3(0, -.2f, 6)).GetComponent<RedStoneCurtain>();
            Activate(); source.SetChannel(LightColorChannel.Yellow); LightStep(5);
            Check(wall.IsOpen && !wall.IsPermanent && !wall.PhysicalCollider.enabled, "Yellow did not open the delivered wall temporarily");
            source.SetChannel(LightColorChannel.None); LightStep(wall.ReturnGraceSeconds + .05f);
            Check(!wall.IsOpen && wall.PhysicalCollider.enabled, "Yellow opening did not restore");
            source.SetChannel(LightColorChannel.Red); LightStep(wall.ContinuousHitSeconds * .6f);
            Check(!wall.IsPermanent, "Red opened too early");
            source.SetChannel(LightColorChannel.None); LightStep(.01f);
            source.SetChannel(LightColorChannel.Red); LightStep(wall.ContinuousHitSeconds * .6f);
            Check(!wall.IsPermanent, "Transformed wall retained interrupted charge");
            LightStep(wall.ContinuousHitSeconds * .5f);
            Check(wall.IsPermanent && wall.IsOpen, "Continuous red missed transformed wall");
            source.SetChannel(LightColorChannel.None); LightStep(5);
            Check(wall.IsOpen, "Permanent wall returned");
        }
        static void CheckBridge(int pose)
        {
            Transform rig = Rig(pose);
            var bridge = Spawn("LightBridge", rig).GetComponent<LightPathDriver>();
            var source = Laser(rig, new Vector3(-1.7f, -.4f, -4));
            Activate(); bridge.EvaluateNow(); CheckPath(bridge.path, false);
            foreach (var wrong in new[] { LightColorChannel.Red, LightColorChannel.Yellow })
            {
                source.SetChannel(wrong); LightStep(1); bridge.EvaluateNow();
                Check(!bridge.IsPowered, wrong + " must not activate the blue bridge"); CheckPath(bridge.path, false);
            }
            source.SetChannel(LightColorChannel.Blue); LightStep(bridge.receiver.RequiredHitSeconds + .05f); bridge.EvaluateNow();
            Check(bridge.IsPowered, "Transformed bridge receiver was not reached"); CheckPath(bridge.path, true);
            source.SetChannel(LightColorChannel.Yellow); LightStep(.001f); bridge.EvaluateNow();
            Check(!bridge.IsPowered, "Wrong color held bridge through activation grace"); CheckPath(bridge.path, false);
            source.SetChannel(LightColorChannel.None); LightStep(.001f); bridge.EvaluateNow();
            Check(!bridge.IsPowered, "Bridge retained power after light removal"); CheckPath(bridge.path, false);
        }
        static void CheckPath(SolidPath path, bool solid)
        {
            Check(path.IsSolid == solid, "Bridge state differs from power");
            foreach (var renderer in path.renderers) Check(renderer && renderer.enabled == solid, "A bridge span has the wrong visibility");
            foreach (var collider in path.colliders) Check(collider && collider.enabled == solid, "A bridge span has the wrong collision state");
        }
        static void CheckLift(int pose)
        {
            Transform rig = Rig(pose);
            var lift = Spawn("LightDrivenLift", rig).GetComponent<LightDrivenLift>();
            var up = Laser(rig, new Vector3(-2.2f, -.3f, -5));
            var down = Laser(rig, new Vector3(2.2f, -.3f, -5));
            Activate(); Check(lift.Initialize(), "Prefab lift did not initialize"); lift.platformBody.interpolation = RigidbodyInterpolation.None;
            Check(Vector3.Distance(lift.BottomWorld, lift.transform.TransformPoint(lift.bottom)) < .005f,
                "Lift bottom ignored transformed root");
            Check(Vector3.Distance(lift.TopWorld, lift.transform.TransformPoint(lift.top)) < .005f,
                "Lift top ignored transformed root");
            Vector3 before = lift.platformBody.position;
            up.SetChannel(LightColorChannel.Red); LightStep(lift.receiverRaise.RequiredHitSeconds + .05f);
            Check(lift.Direction == 1, "Raise receiver does not drive prefab lift"); Advance(lift, .6f);
            Check(Vector3.Distance(lift.platformBody.position, before) > .5f, "Kinematic deck failed to physically rise");
            CheckOnRail(lift.platformBody.position, lift.BottomWorld, lift.TopWorld);
            up.SetChannel(LightColorChannel.None); LightStep(.001f); before = lift.platformBody.position; Advance(lift, .3f);
            Check(lift.Direction == 0 && Vector3.Distance(before, lift.platformBody.position) < .005f, "Unlit deck did not hold its intermediate position");
            down.SetChannel(LightColorChannel.Yellow); LightStep(lift.receiverLower.RequiredHitSeconds + .05f);
            Check(lift.Direction == -1, "Lower receiver does not drive prefab lift"); Advance(lift, 1f);
            Check(Vector3.Distance(lift.platformBody.position, lift.BottomWorld) < .01f, "Reverse deck failed to reach transformed bottom");
            down.SetChannel(LightColorChannel.None); LightStep(.2f);
            var lever = lift.GetComponentInChildren<MotorLever>();
            foreach (int direction in new[] { 1, 0, -1, 0 }) { lever.Use(null); Check(lift.Direction == direction, "Authored manual lever cycle failed"); }
        }
        static void CheckOnRail(Vector3 point, Vector3 a, Vector3 b)
        {
            Check(Vector3.Cross((b - a).normalized, point - a).magnitude < .01f, "Moving deck left its transformed guide");
        }
        static WaterVolume Water(float surfaceLevel = 0f)
        {
            var basin = new GameObject("Temporary real water"); basin.transform.SetParent(context.root.transform, false);
            basin.transform.position = Offset; var water = basin.AddComponent<WaterVolume>();
            water.sizeX = 400; water.sizeZ = 80; water.cellSize = 4; water.bottom = -5; water.initialLevel = surfaceLevel;
            return water;
        }
        static void CheckVertical()
        {
            var water = Water(1.5f);
            var rail = Spawn("VerticalBuoyantPlatform", Rig(2)).GetComponent<GuidedBuoyantPlatform>();
            rail.water = water; Activate(); Check(rail.Initialize(), "Vertical prefab did not initialize");
            rail.platformBody.interpolation = RigidbodyInterpolation.None; Advance(rail, 6f);
            Check(rail.NormalizedTravel > .08f && rail.Submersion > .1f, "Real water failed to raise transformed vertical prefab");
            CheckOnRail(rail.platformBody.position, rail.StartWorld, rail.EndWorld);
            water.initialLevel = -4; water.ResetWater(); Advance(rail, 6f);
            Check(Vector3.Distance(rail.platformBody.position, rail.StartWorld) < .02f, "Drained vertical prefab did not return to its lower guide limit");
        }
        static void CheckHorizontal()
        {
            var water = Water(.5f);
            var rail = Spawn("HorizontalBuoyantPlatform", Rig(1)).GetComponent<GuidedBuoyantPlatform>();
            rail.transform.localScale = new Vector3(1.2f, .8f, 1.1f); rail.water = water;
            Activate(); Check(rail.Initialize(), "Horizontal prefab did not initialize"); rail.platformBody.interpolation = RigidbodyInterpolation.None;
            Advance(rail, 5f); var control = rail.GetComponentInChildren<GuidedBuoyantPlatformControl>();
            control.Use(null); Check(rail.Direction == 1, "Prefab ferry control did not start travel");
            Advance(rail, .8f); Check(rail.NormalizedTravel > .05f, "Prefab ferry failed to travel");
            control.Use(null); float held = rail.NormalizedTravel; water.initialLevel = 1.2f; water.ResetWater(); Advance(rail, 4);
            Check(rail.Direction == 0 && Mathf.Abs(rail.NormalizedTravel - held) < .0001f && rail.Submersion > .1f,
                "Stopped ferry failed to hold path progress while floating");
            control.Use(null); Check(rail.Direction == -1, "Prefab ferry control did not reverse"); Advance(rail, 2);
            Check(rail.NormalizedTravel < .001f, "Prefab ferry did not reach its start");
        }
        static void CheckPortalPair()
        {
            var original = Spawn("PortalPair", Rig(2));
            var duplicate = Object.Instantiate(original, original.transform.parent);
            duplicate.transform.localPosition += Vector3.right * 18;
            foreach (var surface in duplicate.GetComponentsInChildren<PortalSurface>()) surface.enabled = false;
            Activate(); var pair = duplicate.GetComponentsInChildren<LightPortal>();
            Check(pair.Length == 2 && pair[0].Paired == pair[1] && pair[1].Paired == pair[0], "Copied pair points to original portals");
            Vector3 point = pair[0].transform.TransformPoint(new Vector3(.2f, .3f, 0));
            Vector3 incoming = -pair[0].PlaneNormal;
            Check(pair[0].TryTransfer(point, incoming, out Vector3 exit, out Vector3 direction), "Scaled rotated portal rejected an interior point");
            Vector3 expected = pair[1].transform.TransformPoint(new Vector3(-.2f, .3f, 0));
            Check(Vector3.Distance(exit - direction * pair[0].ExitOffset, expected) < .01f, "Scaled rotated portal mapped to the wrong point");
        }
        static void CheckDrain()
        {
            var water = Water(.5f);
            var drain = Spawn("DrainGate", Rig(0)).GetComponent<DrainGateDevice>(); Activate();
            drain.Use(null); Check(drain.water == water && water.Gate.IsOpen, "Portable drain did not find and open its local water");
            drain.Use(null); Check(!water.Gate.IsOpen && water.Remaining > .99f, "Portable drain reset failed");
        }
        static void CheckMirage()
        {
            var water = Water();
            var root = Spawn("WaterMirage", Rig(0)); var mirage = root.GetComponent<WaterMirage>(); Activate();
            Check(root.GetComponent<MirageWaterBinding>().Resolve() && mirage.water == water, "Mirage prefab failed local water binding");
            mirage.EvaluateNow(); Check(mirage.HasProjection, "Authored mirage source cannot create a projection: " + mirage.Status);
            mirage.target.position = mirage.ProjectedPoint; mirage.EvaluateNow();
            Check(mirage.IsSolid, "Aligned real projection failed to make prefab steps solid");
            foreach (var collider in mirage.stepColliders) Check(collider.enabled, "Projected step has no collision");
            mirage.powered = false; mirage.EvaluateNow();
            Check(!mirage.IsSolid && !mirage.HasProjection, "Power loss did not remove mirage projection");
            foreach (var collider in mirage.stepColliders) Check(!collider.enabled, "Unpowered projected step retained collision");
        }

        // LightPuzzleWorld is deliberately global in gameplay. Scope its registries while synchronous tests run,
        // so neither its targets nor its callbacks can advance any mechanism in the user's loaded scenes.
        sealed class IsolatedLightRegistry : IDisposable
        {
            readonly List<IList> lists = new List<IList>();
            readonly List<object[]> saved = new List<object[]>();
            readonly FieldInfo frameField;
            readonly object savedFrame;
            public IsolatedLightRegistry()
            {
                const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;
                var type = typeof(LightPuzzleWorld);
                Check(!(bool)type.GetField("resolving", flags).GetValue(null) && !(bool)type.GetField("resetting", flags).GetValue(null),
                    "Do not invoke prefab checks from an active light callback");
                foreach (string name in new[] { "sources", "targets", "portals", "resolvingTargets", "resettingTargets" })
                {
                    var list = (IList)type.GetField(name, flags).GetValue(null);
                    var values = new object[list.Count]; list.CopyTo(values, 0); lists.Add(list); saved.Add(values); list.Clear();
                }
                frameField = type.GetField("frame", flags); savedFrame = frameField.GetValue(null);
            }
            public void Dispose()
            {
                for (int i = 0; i < lists.Count; i++) { lists[i].Clear(); foreach (var value in saved[i]) lists[i].Add(value); }
                frameField.SetValue(null, savedFrame);
            }
        }
    }
}
#endif
