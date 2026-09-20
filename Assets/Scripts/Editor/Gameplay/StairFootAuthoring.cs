using System;
using System.Linq;
using CoastalTemple.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class StairFootAuthoring
    {
        public static string Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
            string previous=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            // Keep the old prefab GUID available for archived scenes. Current maps become editable geometry.
            TraversalV2Authoring.BuildStairPrefab(); RedBotAuthoring.BuildController();
            foreach(string path in new[]{RedBotAuthoring.PrefabPath,"Assets/Prefabs/Player/ExplorerThirdPerson.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try{Configure(root);PrefabUtility.SaveAsPrefabAsset(root,path);}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            int stairs=0,players=0;
            foreach(string path in new[]{"Assets/Scenes/CoastalTemple.unity","Assets/Scenes/MechanismPlayground.unity"})
            {
                var scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
                foreach(var root in scene.GetRootGameObjects())
                {
                    players+=Configure(root);
                    foreach(var stair in root.GetComponentsInChildren<CourtyardStaircase>(true))
                    {
                        if(PrefabUtility.IsOutermostPrefabInstanceRoot(stair.gameObject))
                            PrefabUtility.UnpackPrefabInstance(stair.gameObject,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                        stair.name=stair.name.Replace(" - reusable"," - ramp collision");
                        // Main courtyard is 4.5m deep; the old 4.2m stair stopped below its shore wall.
                        if(path=="Assets/Scenes/CoastalTemple.unity")
                        {
                            var scale=stair.transform.localScale;scale.y=4.5f/stair.rise;stair.transform.localScale=scale;
                        }
                        stairs++;
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(previous,OpenSceneMode.Single);
            return $"Updated {stairs} scene stairs and {players} humanoids; original scenes preserved.";
        }
        static int Configure(GameObject root)
        {
            int count=0;
            foreach(var a in root.GetComponentsInChildren<Animator>(true).Where(a=>a.isHuman))
            {
                if(!a.GetComponent<GroundFootIK>())a.gameObject.AddComponent<GroundFootIK>();
                EditorUtility.SetDirty(a.gameObject);count++;
            }
            return count;
        }
    }
}
