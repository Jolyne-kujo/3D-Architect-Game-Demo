using System;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class WaterPrefabAuthoring
    {
        public const string WaterPath = "Assets/Prefabs/Water/WaterVolume.prefab";
        public const string FloatPath = "Assets/Prefabs/Water/BuoyantBlock.prefab";

        // Explicit authoring action, never run automatically on scene load.
        public static string ApplyToShowroom()
        {
            if (Application.isPlaying || SceneManager.GetActiveScene().path != MechanismPlaygroundAuthoring.ScenePath)
                throw new InvalidOperationException("Open MechanismPlayground in Edit mode first.");
            var root = GameObject.Find("Showroom water - use dimensions to resize");
            if (!root) throw new InvalidOperationException("Showroom water was not found.");
            var water = root.GetComponent<WaterVolume>();
            var position = root.transform.position; string name = root.name;
            if (water.bedTerrain || water.surfaceMotion)
                throw new InvalidOperationException("Remove external scene references before exporting the standalone pool.");
            WaterSurfacePreviewAssets.Refresh(water, true);
            try
            {
                root.name = "WaterVolume"; root.transform.position = Vector3.zero;
                PrefabUtility.SaveAsPrefabAssetAndConnect(root, WaterPath, InteractionMode.AutomatedAction);
            }
            finally
            {
                root.name = name; root.transform.position = position;
                if (PrefabUtility.IsPartOfPrefabInstance(root))
                {
                    PrefabUtility.RecordPrefabInstancePropertyModifications(root);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(root.transform);
                }
            }
            // A working sample for ordinary dynamic props; no reference to a particular pool.
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(FloatPath))
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                try
                {
                    block.name = "BuoyantBlock";
                    block.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/Ivory.mat");
                    var body = block.AddComponent<Rigidbody>(); body.mass = 400;
                    body.interpolation = RigidbodyInterpolation.Interpolate;
                    body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    var buoyancy = block.AddComponent<BuoyantBody>(); buoyancy.displacementSize = Vector3.one;
                    PrefabUtility.SaveAsPrefabAsset(block, FloatPath);
                }
                finally { Object.DestroyImmediate(block); }
            }
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
            Selection.activeGameObject = root;
            return "Connected the existing showroom water without replacing its component; saved WaterVolume and BuoyantBlock prefabs.";
        }
    }
}
