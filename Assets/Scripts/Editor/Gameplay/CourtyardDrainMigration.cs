#if UNITY_EDITOR
using CoastalTemple.Mechanisms;
using CoastalTemple.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoastalTemple.Editor
{
    public static class CourtyardDrainMigration
    {
        [MenuItem("Coastal Temple/Migrate Existing Drain Experiment")]
        public static void MigrateActiveScene()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Stop Play before migrating the authored experiment.");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int migrated = 0;
            foreach (var walkthrough in Resources.FindObjectsOfTypeAll<CoastalWalkthrough>())
                if (walkthrough.gameObject.scene == scene) { Migrate(walkthrough); migrated++; }
            if (migrated > 0) EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("Migrated " + migrated + " existing drain experiment(s). Save the scene to retain the references; no tutorial authoring rebuild is required.");
        }

        // Explicit authoring operation: preserves the walkthrough, console and all referenced objects.
        public static CourtyardDrainExperiment Migrate(CoastalWalkthrough walkthrough)
        {
            if (!walkthrough) throw new System.ArgumentNullException(nameof(walkthrough));
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new System.InvalidOperationException("Drain migration is an edit-mode operation.");
            var experiment = walkthrough.experiment;
            if (!experiment) experiment = walkthrough.GetComponent<CourtyardDrainExperiment>();
            if (!experiment) experiment = Undo.AddComponent<CourtyardDrainExperiment>(walkthrough.gameObject);
            Undo.RecordObjects(new Object[] { walkthrough, experiment }, "Migrate drain experiment");
            if (!experiment.water) experiment.water = walkthrough.water;
            if (!experiment.beam) experiment.beam = walkthrough.beam;
            if (!experiment.drainGate) experiment.drainGate = walkthrough.drainGate;
            if ((experiment.floaters == null || experiment.floaters.Length == 0) && walkthrough.floaters != null)
                experiment.floaters = (Courtyard.Water.BuoyantBody[])walkthrough.floaters.Clone();
            walkthrough.experiment = experiment;
            EditorUtility.SetDirty(experiment);
            EditorUtility.SetDirty(walkthrough);
            PrefabUtility.RecordPrefabInstancePropertyModifications(experiment);
            PrefabUtility.RecordPrefabInstancePropertyModifications(walkthrough);
            foreach (var console in Resources.FindObjectsOfTypeAll<CourtyardDrainConsole>())
            {
                if (console.gameObject.scene != walkthrough.gameObject.scene || console.walkthrough != walkthrough) continue;
                if (console.experiment) continue;
                Undo.RecordObject(console, "Connect drain console");
                console.experiment = experiment;
                EditorUtility.SetDirty(console);
                PrefabUtility.RecordPrefabInstancePropertyModifications(console);
            }
            if (walkthrough.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(walkthrough.gameObject.scene);
            return experiment;
        }
    }
}
#endif
