using UnityEditor;
using UnityEditor.SceneManagement;

namespace CoastalTemple.Editor
{
    public static class ProjectSceneShortcuts
    {
        [MenuItem("Coastal Temple/Scenes/主地图 CoastalTemple")]
        public static void OpenMain()=>Open("Assets/Scenes/CoastalTemple.unity");
        [MenuItem("Coastal Temple/Scenes/机关实验场 MechanismPlayground")]
        public static void OpenPlayground()=>Open("Assets/Scenes/MechanismPlayground.unity");
        static void Open(string path)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)
            {EditorUtility.DisplayDialog("切换场景","请先停止 Play，再切换要编辑的场景。","确定");return;}
            if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
        }
    }
}
