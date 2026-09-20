using System;
using System.IO;
using System.Linq;
using CoastalTemple.Diagnostics;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Player;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class DebugWaterAuthoring
    {
        public const string ConsolePath="Assets/Prefabs/Mechanisms/PoolsideDrainConsole.prefab";
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
            EditorSceneManager.SaveOpenScenes();BuildConsole();
            string guid=AssetDatabase.AssetPathToGUID("Assets/Scripts/Gameplay/Mechanisms/MirageBeamView.cs");
            foreach(var id in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}))
            {
                var path=AssetDatabase.GUIDToAssetPath(id);if(!File.ReadAllText(path).Contains(guid))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try{ConfigureMirages(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            const string playerPath="Assets/Prefabs/Player/ExplorerThirdPerson.prefab";
            var player=PrefabUtility.LoadPrefabContents(playerPath);
            try{ConfigurePlayers(player);PrefabUtility.SaveAsPrefabAsset(player,playerPath);}finally{PrefabUtility.UnloadPrefabContents(player);}
            var active=SceneManager.GetActiveScene();
            foreach(var path in new[]{"Assets/Scenes/CoastalTemple.unity","Assets/Scenes/MechanismPlayground.unity"})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                ConfigureScene(scene,path.EndsWith("MechanismPlayground.unity"));
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                if(opened)EditorSceneManager.CloseScene(scene,true);
            }
            SceneManager.SetActiveScene(active);AssetDatabase.SaveAssets();
        }
        public static void ConfigureScene(Scene scene,bool shoreConsole)
        {
            foreach(var root in scene.GetRootGameObjects()){ConfigureMirages(root);ConfigurePlayers(root);}
            if(!shoreConsole)return;
            var water=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<WaterVolume>(true)).Single();
            var existing=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Poolside drain - E drain or refill");
            if(!existing)
            {
                if(!AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePath))BuildConsole();
                existing=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePath),scene);
                existing.name="Poolside drain - E drain or refill";existing.transform.position=new Vector3(-20,0,4.8f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(existing.transform);
            }
            var device=existing.GetComponent<DrainGateDevice>();device.water=water;Changed(device);
        }
        public static void ConfigurePlayers(GameObject root)
        {
            foreach(var walker in root.GetComponentsInChildren<CourtyardWalker>(true))
            {
                var debug=walker.GetComponent<PlayerDebugLaser>();if(!debug)debug=walker.gameObject.AddComponent<PlayerDebugLaser>();
                debug.walker=walker;debug.cameraRig=walker.GetComponent<CoastalPlayerCamera>();if(debug.cameraRig)debug.view=debug.cameraRig.view;
                var source=walker.transform.Find("Debug laser - camera aim");
                if(!source){source=new GameObject("Debug laser - camera aim").transform;source.SetParent(walker.transform,false);}
                var emitter=source.GetComponent<LaserEmitter>();if(!emitter)emitter=source.gameObject.AddComponent<LaserEmitter>();
                emitter.SetChannel(LightColorChannel.None);emitter.Origin=debug.view?debug.view.transform:null;emitter.IgnoreRoot=walker.transform;emitter.IgnorePlayer=true;
                emitter.MaxDistance=100;emitter.BeamRadius=.015f;emitter.BeamMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/BeamWhite.mat");
                debug.emitter=emitter;Changed(emitter);Changed(debug);
            }
        }
        static void ConfigureMirages(GameObject root)
        {
            foreach(var view in root.GetComponentsInChildren<MirageBeamView>(true))
            {
                view.ApplyBlueTint();if(view.airSegment)Changed(view.airSegment);if(view.waterSegment)Changed(view.waterSegment);
            }
        }
        static void BuildConsole()
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePath))return;
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/Mechanisms/DrainGate.prefab");
            try
            {
                root.name="PoolsideDrainConsole";var device=root.GetComponent<DrainGateDevice>();
                device.water=null;device.receiver.transform.localPosition=new Vector3(0,1.35f,0);device.receiver.RequiredColor=ReceiverColor.Yellow;
                if(device.gate)Object.DestroyImmediate(device.gate.gameObject);
                var ivory=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/Ivory.mat");
                var dark=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/Slate.mat");
                var yellow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/ReceiverYellow.mat");
                Box(root.transform,"Console base",new Vector3(0,.12f,0),new Vector3(1.25f,.24f,1.1f),ivory);
                Box(root.transform,"Console pedestal",new Vector3(0,.7f,0),new Vector3(.5f,1.1f,.5f),ivory);
                var lever=new GameObject("Drain handle").transform;lever.SetParent(root.transform,false);lever.localPosition=new Vector3(.55f,1.08f,-.18f);device.gate=lever;
                Box(lever,"Handle",new Vector3(0,.22f,0),new Vector3(.1f,.44f,.12f),yellow);
                var point=new GameObject("Use point on shore").transform;point.SetParent(root.transform,false);point.localPosition=new Vector3(0,1.25f,-.6f);device.interactionPoint=point;device.useRadius=2.8f;
                Box(root.transform,"Sign backing",new Vector3(0,1.92f,0),new Vector3(1.6f,.36f,.12f),dark);
                var label=new GameObject("DRAIN - REFILL").AddComponent<TextMesh>();label.transform.SetParent(root.transform,false);label.transform.localPosition=new Vector3(0,1.93f,-.07f);
                label.text="DRAIN / REFILL";label.fontSize=48;label.characterSize=.04f;label.anchor=TextAnchor.MiddleCenter;label.color=Color.white;
                PrefabUtility.SaveAsPrefabAsset(root,ConsolePath);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);box.transform.localPosition=position;box.transform.localScale=scale;box.GetComponent<Renderer>().sharedMaterial=material;}
        static void Changed(Object target){EditorUtility.SetDirty(target);PrefabUtility.RecordPrefabInstancePropertyModifications(target);}
    }
}
