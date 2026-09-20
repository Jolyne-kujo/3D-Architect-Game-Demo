#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using CoastalTemple.Mechanisms;
using CoastalTemple.Tutorial;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class CourtyardDrainExperimentChecks
    {
        [MenuItem("Coastal Temple/Tests/Independent Drain Experiment")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run the drain component and migration checks in Edit mode.");
            var results = new List<string>();
            void Check(bool value, string description)
            {
                results.Add((value ? "PASS " : "FAIL ") + description);
                if (!value) throw new InvalidOperationException(string.Join("\n", results));
            }
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            Mesh waterMesh = null;
            try
            {
                root = new GameObject("TemporaryIndependentDrainChecks");
                root.SetActive(false);
                SceneManager.MoveGameObjectToScene(root, preview);
                GameObject Child(string name)
                {
                    var child = new GameObject(name);
                    child.transform.SetParent(root.transform, false);
                    return child;
                }
                var water = Child("SmallRealWaterVolume").AddComponent<WaterVolume>();
                water.sizeX = water.sizeZ = 2; water.cellSize = 1; water.bottom = -1; water.initialLevel = 0;
                water.drainPosition = Vector2.zero; water.drainRadius = 2; water.outletArea = 1;
                water.Initialize(); waterMesh = water.GetComponent<MeshFilter>().sharedMesh;
                var beam = Child("ExistingRefractionBeam").AddComponent<RefractedDrainBeam>();
                beam.water = water;
                var gate = Child("VisibleDrainGate").transform;
                var floatingObject = Child("RealBuoyantBody");
                floatingObject.transform.position = new Vector3(0, 2, 0);
                var body = floatingObject.AddComponent<Rigidbody>(); body.useGravity = false;
                var floater = floatingObject.AddComponent<BuoyantBody>(); floater.water = water;
                // Edit-mode inactive objects do not run Awake; SendMessage also skips this inactive fixture.
                // Invoke the real component initialization only after every authored dependency is assigned.
                var lifecycleFlags = BindingFlags.Instance | BindingFlags.NonPublic;
                var floaterAwake = typeof(BuoyantBody).GetMethod("Awake", lifecycleFlags)
                    ?? throw new InvalidOperationException("BuoyantBody initialization entry point was not found.");
                floaterAwake.Invoke(floater, null);
                Check(typeof(BuoyantBody).GetField("body", lifecycleFlags)?.GetValue(floater) as Rigidbody == body,
                    "Edit-mode fixture explicitly initializes the real buoyant-body Rigidbody reference");
                var restPosition = body.position; var restRotation = body.rotation;
                var experiment = root.AddComponent<CourtyardDrainExperiment>();
                experiment.water = water; experiment.beam = beam; experiment.drainGate = gate;
                experiment.floaters = new[] { floater, null };
                Check(root.GetComponentsInChildren<TutorialJourney>(true).Length == 0, "experiment fixture has no tutorial or player component");

                experiment.StartDrain();
                Check(experiment.Powered && beam.powered, "StartDrain powers the existing refraction beam");
                water.OpenDrain();
                for (int i = 0; i < 8; i++) water.Advance(.1f);
                Check(water.Remaining < .999f && water.Gate.IsOpen, "real water solver drains after the gate opens");
                float drained = water.Remaining;
                experiment.StartDrain();
                Check(Mathf.Abs(water.Remaining - drained) < .00001f && water.Gate.IsOpen,
                    "repeated StartDrain does not refill water or reset the latched gate");
                experiment.SimulateGate(1);
                Check(Quaternion.Angle(gate.localRotation, Quaternion.Euler(35, 0, 0)) < .001f,
                    "visible gate follows the real water gate at 35 degrees per second");
                body.position = restPosition + Vector3.right * 3;
                body.rotation = Quaternion.Euler(10, 20, 30);
                body.linearVelocity = Vector3.right * 2; body.angularVelocity = Vector3.up;
                experiment.ResetExperiment();
                Check(!experiment.Powered && !beam.powered, "reset disables the refraction light source");
                Check(Mathf.Abs(water.Remaining - 1) < .00001f && !water.Gate.IsOpen,
                    "reset refills the real water volume and closes its gate");
                Check(Vector3.Distance(body.position, restPosition) < .0001f && Quaternion.Angle(body.rotation, restRotation) < .001f,
                    "reset restores the authored Rigidbody position and rotation");
                Check(body.linearVelocity.sqrMagnitude < .00001f && body.angularVelocity.sqrMagnitude < .00001f,
                    "reset clears floating-body linear and angular momentum");
                Check(Quaternion.Angle(gate.localRotation, Quaternion.identity) < .001f,
                    "reset closes the visible gate immediately without waiting for Update");
                experiment.ResetExperiment();
                Check(!experiment.Powered && Mathf.Abs(water.Remaining - 1) < .00001f && Vector3.Distance(body.position, restPosition) < .0001f,
                    "repeated reset is safe with a null floater entry");
                experiment.Toggle(); Check(experiment.Powered, "Toggle starts the standalone experiment");
                experiment.Toggle(); Check(!experiment.Powered && !water.Gate.IsOpen, "Toggle resets the powered experiment");

                var walkthrough = Child("LegacyWalkthrough").AddComponent<CoastalWalkthrough>();
                walkthrough.water = water; walkthrough.beam = beam; walkthrough.drainGate = gate;
                walkthrough.floaters = new[] { floater };
                var console = Child("LegacyDrainConsole").AddComponent<CourtyardDrainConsole>(); console.walkthrough = walkthrough;
                var walkthroughId = walkthrough.GetEntityId();
                var consoleId = console.GetEntityId();
                var migrated = CourtyardDrainMigration.Migrate(walkthrough);
                Check(migrated && walkthrough.experiment == migrated && migrated.water == water && migrated.beam == beam
                    && migrated.drainGate == gate && migrated.floaters.Length == 1 && migrated.floaters[0] == floater,
                    "explicit migration copies existing experiment references without rebuilding objects");
                Check(console.experiment == migrated && console.GetEntityId() == consoleId && walkthrough.GetEntityId() == walkthroughId,
                    "migration wires the existing console and preserves component identities");
                var repeated = CourtyardDrainMigration.Migrate(walkthrough);
                Check(repeated == migrated && walkthrough.GetComponents<CourtyardDrainExperiment>().Length == 1,
                    "repeated migration reuses its experiment component");
                migrated.StartDrain(); walkthrough.ResetWater();
                Check(!migrated.Powered && !water.Gate.IsOpen, "legacy ResetWater delegates to the independent experiment");
                // Only activate the console; incomplete beam visuals and the walkthrough stay inactive.
                console.transform.SetParent(null, true);
                console.Use(null);
                Check(migrated.Powered, "migrated console operates without a TutorialJourney dependency");
                var empty = Child("UnassignedExperiment").AddComponent<CourtyardDrainExperiment>();
                empty.StartDrain(); empty.ResetExperiment(); empty.Toggle(); empty.SimulateGate(.1f);
                Check(!empty.Powered, "unassigned experiment references are safe to call repeatedly");
                return string.Join("\n", results) + "\n" + results.Count + " drain component and migration checks passed; no keyboard or full-scene route was exercised.";
            }
            finally
            {
                // WaterVolume destroys its generated mesh in Play; prevent delayed destruction in Edit-mode checks.
                if (waterMesh) Object.DestroyImmediate(waterMesh);
                if (root) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }
    }
}
#endif
