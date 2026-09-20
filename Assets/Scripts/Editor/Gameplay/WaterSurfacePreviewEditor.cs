using System;
using System.IO;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoastalTemple.Editor
{
    [CustomEditor(typeof(WaterVolume))]
    public sealed class WaterSurfacePreviewEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUI.BeginChangeCheck();DrawDefaultInspector();bool changed=EditorGUI.EndChangeCheck();
            EditorGUILayout.HelpBox("编辑状态显示初始水位网格，运行时换成模拟水面。用 Size X / Size Z 调整范围，用 Initial Level 调整初始水位；保持旋转为零、缩放为一。",MessageType.Info);
            if(!Application.isPlaying&&(changed||GUILayout.Button("更新并保存水面预览")))WaterSurfacePreviewAssets.Refresh((WaterVolume)target,true);
        }
    }

    [InitializeOnLoad]
    public static class WaterSurfacePreviewAssets
    {
        const string Folder="Assets/Objects/Water/SurfacePreviews";
        static double next;
        static bool stableEditMode;
        [Serializable] sealed class GeometrySettings
        {
            public float x,z,cell,bottom,level,grade,fade;
            public bool dry;
            public Vector2 drain;
            public WaterBedBlock[] blocks;
            public string terrain;
            public Vector3 relativeTerrainPosition;
        }
        static WaterSurfacePreviewAssets()
        {
            stableEditMode=!Application.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode;
            EditorApplication.playModeStateChanged+=state=>
            {
                stableEditMode=state==PlayModeStateChange.EnteredEditMode;
                next=EditorApplication.timeSinceStartup+1;
            };
            EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            if(!stableEditMode||Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+1;
            foreach(var water in UnityEngine.Object.FindObjectsByType<WaterVolume>(FindObjectsSortMode.None))Refresh(water,false);
        }
        public static bool Refresh(WaterVolume water,bool force)
        {
            if(!water||!stableEditMode||Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||!water.gameObject.scene.IsValid()||EditorSceneManager.IsPreviewScene(water.gameObject.scene)||string.IsNullOrEmpty(water.gameObject.scene.path))return false;
            if(water.sizeX<=0||water.sizeZ<=0||water.cellSize<=0||float.IsNaN(water.sizeX)||float.IsInfinity(water.sizeX)||float.IsNaN(water.sizeZ)||float.IsInfinity(water.sizeZ)||float.IsNaN(water.cellSize)||float.IsInfinity(water.cellSize))return false;
            var settings=new GeometrySettings{x=water.sizeX,z=water.sizeZ,cell=water.cellSize,bottom=water.bottom,level=water.initialLevel,grade=water.floorSlope,fade=water.surfaceBoundaryFade,dry=water.keepDryCornersAtInitialLevel,drain=water.drainPosition,blocks=water.bedBlocks,
                terrain=water.bedTerrain?AssetDatabase.GetAssetPath(water.bedTerrain.terrainData):"",relativeTerrainPosition=water.bedTerrain?water.bedTerrain.transform.position-water.transform.position:Vector3.zero};
            string signature=Hash128.Compute(JsonUtility.ToJson(settings)).ToString();
            var filter=water.GetComponent<MeshFilter>();var renderer=water.GetComponent<MeshRenderer>();
            if(!force&&water.editorPreviewSignature==signature&&filter.sharedMesh&&EditorUtility.IsPersistent(filter.sharedMesh))
            {if(renderer.sharedMaterial!=water.surfaceMaterial){renderer.sharedMaterial=water.surfaceMaterial;EditorUtility.SetDirty(renderer);}return false;}
            Directory.CreateDirectory(Folder);
            var id=GlobalObjectId.GetGlobalObjectIdSlow(water);
            if(id.targetObjectId==0)return false; // Save new scene objects before assigning a stable asset name.
            string path=$"{Folder}/{Path.GetFileNameWithoutExtension(water.gameObject.scene.path)}_{id.targetObjectId}.asset";
            var generated=water.CreateInitialSurfaceMesh();var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(asset){EditorUtility.CopySerialized(generated,asset);UnityEngine.Object.DestroyImmediate(generated);EditorUtility.SetDirty(asset);}
            else{asset=generated;AssetDatabase.CreateAsset(asset,path);}
            Undo.RecordObjects(new UnityEngine.Object[]{water,filter,renderer},"Update initial water surface preview");
            filter.sharedMesh=asset;renderer.sharedMaterial=water.surfaceMaterial;water.editorPreviewSignature=signature;
            EditorUtility.SetDirty(filter);EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(water);
            EditorSceneManager.MarkSceneDirty(water.gameObject.scene);AssetDatabase.SaveAssetIfDirty(asset);return true;
        }
    }
}
