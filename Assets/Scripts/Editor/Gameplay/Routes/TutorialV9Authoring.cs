using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using CoastalTemple.Tutorial.Editor;

namespace CoastalTemple.Editor
{
    public static class TutorialV9Authoring
    {
        public const string ExcavationFolder="Assets/Objects/LevelGeometry/Tutorial/V9/Excavation";
        [MenuItem("Coastal Temple/Routes/Author V9 Broken Routes (replaces tutorial)")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(!scene.path.EndsWith("/CoastalTemple.unity"))throw new InvalidOperationException("Open the CoastalTemple scene.");
            string backup="Assets/Scenes/Archive/CoastalTemple_V8.unity";
            if(!File.Exists(backup))EditorSceneManager.SaveScene(scene,backup,true);
            var sceneRoots=scene.GetRootGameObjects();
            var tour=sceneRoots.SelectMany(r=>r.GetComponentsInChildren<CoastalWalkthrough>(true)).Single();
            var previous=sceneRoots.FirstOrDefault(r=>r.name=="40_Tutorial");
            var old=previous?previous.GetComponent<TutorialJourney>():null;
            var lamp=old?old.handLight:tour.walker.eye.GetComponentInChildren<Light>(true);
            var prop=old?old.carriedLantern:tour.walker.transform.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Lantern_Carried").gameObject;
            var bonus=previous?previous.transform.Find("05_Optional_WaterMirage"):null;
            if(bonus)bonus.SetParent(null,true);
            if(previous)UnityEngine.Object.DestroyImmediate(previous);
            var root=new GameObject("40_Tutorial").transform;if(bonus)bonus.SetParent(root,true);
            var j=root.gameObject.AddComponent<TutorialJourney>();j.walker=tour.walker;j.cameraRig=tour.playerCamera;j.handLight=lamp;j.carriedLantern=prop;
            tour.tutorial=j;TutorialModuleMigration.Bind(j);CourtyardDrainMigration.Migrate(tour);
            var a=Stage(root,"01_BorrowedBridge",new Vector3(-68,3.5f,189),Vector3.back);
            var b=Stage(root,"02_StoneWorkshop",new Vector3(-48,29,127),new Vector3(13,0,-11));
            var c=Stage(root,"03_FoldedBridge",new Vector3(-18,38.4f,117),Vector3.right);
            RouteLayouts.BorrowedBridge(new RouteAuthoringKit(a));RouteLayouts.StoneWorkshop(new RouteAuthoringKit(b));RouteLayouts.FoldedBridge(new RouteAuthoringKit(c));
            j.lessons=new[]{Lesson(j,a,"借光断廊",new[]{"看一看：光照到红石板与金色受光器时，脚下各发生了什么？","中间的小岛不会随光变化。站在那里，再决定下一段需要留下哪一种路。","先借光走到中岛；收光后，让红色承重板回来。"},"把光还回去，也能造出路。\n提灯可以带走，L 开关照明。"),
                Lesson(j,b,"断阶工坊",new[]{"升到最高处之前，中层有一台卷扬。石墙也可以当作脚下的工作台。","光移开后，升降墙会停在原处。把它停到侧廊高度，去试试横向卷扬。","横桥中线对齐金色刻线后停车，再乘石墙上去。"},"断开的高廊被你接起来了。\n回望海岸：一路走过的机关仍留在原处。"),
                Lesson(j,c,"折光修桥",new[]{"光门出口可以换位置，也可以用卷扬改变高度。光走过的每一段都能看见。","低处的光会穿过红石桥，撞在中墩上。怎样保住脚下，又越过遮挡？","先把石桥移到中线后收光；走到中墩，再抬高光门，把光送向远端受光器。"},"你把光的去向变成了自己的路。\n半山腰的视野打开了，隐蔽海湾就在山体后侧。")};
            j.stations=new[]{a,b,c,c.Find("CompletionPoint")};
            foreach(var stage in new[]{a,b,c})
            {
                AddRecovery(stage,tour);
                PrefabUtility.SaveAsPrefabAsset(stage.gameObject,"Assets/Prefabs/Tutorial/V9/"+stage.name+".prefab");
            }
            // Cut actual background geometry after authoring. Keep original resources in a reversible manifest.
            var geo=sceneRoots.Single(r=>r && r.name=="10_Geography_EDIT_TERRAIN_HERE").transform;
            TutorialGapExcavator.Begin(ExcavationFolder,geo);
            var cuts=new[]{
                TutorialGapExcavator.Cut(a,new Bounds(new Vector3(0,-2,-1.5f),new Vector3(16,20,7)),"borrowed-a"),
                TutorialGapExcavator.Cut(a,new Bounds(new Vector3(0,-2,9.5f),new Vector3(16,20,7)),"borrowed-b"),
                TutorialGapExcavator.Cut(b,new Bounds(new Vector3(0,-2,-9.5f),new Vector3(8,20,5)),"workshop-approach"),
                TutorialGapExcavator.Cut(b,new Bounds(new Vector3(0,-2,3),new Vector3(18,20,20)),"workshop"),
                TutorialGapExcavator.Cut(c,new Bounds(new Vector3(0,-2,-3.5f),new Vector3(18,20,13)),"folded-a"),
                TutorialGapExcavator.Cut(c,new Bounds(new Vector3(0,-2,8.5f),new Vector3(18,20,11)),"folded-b")};
            Directory.CreateDirectory("Documentation/CoastalV9Verification");
            File.WriteAllText("Documentation/CoastalV9Verification/Excavation.json","["+string.Join(",",cuts.Select(r=>JsonUtility.ToJson(r,true)))+"]");
            tour.walker.transform.SetPositionAndRotation(new Vector3(-65,.15f,204),Quaternion.Euler(0,160,0));
            EditorUtility.SetDirty(tour);EditorUtility.SetDirty(j);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root.gameObject;return "V9 authored: three open spatial puzzles, five real gaps, local fall recovery, independent runtime modules.";
        }
        static Transform Stage(Transform parent,string name,Vector3 p,Vector3 facing)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.position=p;t.rotation=Quaternion.LookRotation(facing);return t;}
        static LessonStation Lesson(TutorialJourney j,Transform stage,string title,string[] hints,string reward)
        {
            var condition=stage.gameObject.AddComponent<PlayerReachedCondition>();condition.player=j.walker.transform;condition.destination=stage.Find("CompletionPoint");condition.halfExtents=new Vector3(3,1,1.6f);
            return new LessonStation{name=stage.name,title=title,safetyNote="失足返回本段入口 · 机关保留当前状态",marker=stage,radius=22,hints=hints,completionCondition=condition,completionMessage=reward};
        }
        static void AddRecovery(Transform stage,CoastalWalkthrough tour)
        {
            var r=stage.gameObject.AddComponent<FallRecoveryRegion>();r.walker=tour.walker;r.safePoint=stage.Find("RecoveryPoint");
            bool shore=stage.name.StartsWith("01");r.center=new Vector3(0,shore?-3:-9,shore?4:3);r.size=shore?new Vector3(16,2,18):new Vector3(28,3,25);
            // A readable world inscription repeats the recoverable-fall rule; mechanics have no tutorial dependency.
            var t=new GameObject("Recovery_Notice").transform;t.SetParent(stage,false);t.localPosition=r.safePoint.localPosition+new Vector3(2.7f,1.6f,0);t.localRotation=Quaternion.Euler(0,180,0);
            var text=t.gameObject.AddComponent<TextMesh>();text.text="FALL RECOVERY\nYOUR WORK STAYS";text.fontSize=48;text.characterSize=.035f;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.23f,.23f,.2f);
        }
    }
}
