using System;
using System.IO;
using System.Linq;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoastalTemple.Editor
{
    // Migrate authored objects in place: do not rebuild levels or replace mechanism roots.
    public static class ColorMechanismAuthoring
    {
        const string Folder="Assets/Prefabs/Mechanisms/";
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
            EditorSceneManager.SaveOpenScenes();
            MechanismKitAuthoring.UpdateLaserPrefab();
            string guid=AssetDatabase.AssetPathToGUID("Assets/Scripts/Gameplay/Light/LightReceiver.cs");
            foreach(string id in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Prefabs"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(id);
                if(!File.ReadAllText(path).Contains(guid))continue;
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if(path==Folder+"LightReceiver.prefab")foreach(var r in root.GetComponentsInChildren<LightReceiver>(true))r.RequiredColor=ReceiverColor.Blue;
                    if(path==Folder+"LightDrivenLift.prefab")
                    {var lift=root.GetComponent<LightDrivenLift>();lift.receiverRaise.RequiredColor=ReceiverColor.Red;lift.receiverLower.RequiredColor=ReceiverColor.Yellow;}
                    if(path==Folder+"DrainGate.prefab")root.GetComponent<DrainGateDevice>().receiver.RequiredColor=ReceiverColor.Yellow;
                    Configure(root);PrefabUtility.SaveAsPrefabAsset(root,path);
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            foreach(var color in new[]{ReceiverColor.Red,ReceiverColor.Yellow,ReceiverColor.Blue})
            {
                var root=PrefabUtility.LoadPrefabContents(Folder+"LightReceiver.prefab");
                try
                {root.name=color+"LightReceiver";foreach(var r in root.GetComponentsInChildren<LightReceiver>(true))r.RequiredColor=color;Configure(root);PrefabUtility.SaveAsPrefabAsset(root,Folder+color+"LightReceiver.prefab");}
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            var active=SceneManager.GetActiveScene();
            foreach(var path in new[]{"Assets/Scenes/CoastalTemple.unity","Assets/Scenes/MechanismPlayground.unity"})
            {
                var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
                if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                foreach(var root in scene.GetRootGameObjects())Configure(root);
                EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                if(opened)EditorSceneManager.CloseScene(scene,true);
            }
            SceneManager.SetActiveScene(active);AssetDatabase.SaveAssets();
        }
        static void Configure(GameObject root)
        {
            foreach(var path in root.GetComponentsInChildren<LightPathDriver>(true))if(path.receiver)
            {path.receiver.RequiredColor=ReceiverColor.Blue;Changed(path.receiver);}
            foreach(var receiver in root.GetComponentsInChildren<LightReceiver>(true))
            {
                if(receiver.name=="StoneDrive_Receiver")receiver.RequiredColor=ReceiverColor.Blue;
                PaintReceiver(receiver);Changed(receiver);
            }
            foreach(var laser in root.GetComponentsInChildren<LaserEmitter>(true))
            {
                if(laser.name=="SunLens"||laser.name=="Fixed_Sun")
                {bool powered=laser.Powered;laser.SetChannel(LightColorChannel.Blue);laser.Powered=powered;Changed(laser);}
            }
            foreach(var control in root.GetComponentsInChildren<LaserEmitterConsole>(true))
            {
                var panel=control.transform.Find("Color readout panel");if(!panel)continue;
                control.currentReadout=panel.Find("Current light - large")?.GetComponent<Renderer>();
                control.nextIndicator=panel.Find("Next light - small")?.GetComponent<Renderer>();
                control.RefreshVisuals();Changed(control);
            }
        }
        static void PaintReceiver(LightReceiver receiver)
        {
            var feedback=receiver.GetComponent<LightTargetFeedback>();if(!feedback)feedback=receiver.gameObject.AddComponent<LightTargetFeedback>();
            feedback.ResetMaterial();feedback.target=receiver;if(!feedback.indicator)feedback.indicator=receiver.GetComponentInChildren<Renderer>(true);
            string path="Assets/Materials/Mechanisms/Receiver"+receiver.RequiredColor+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            var tint=LaserEmitter.ColorForChannel(receiver.AcceptedChannel);tint/=Mathf.Max(1,tint.maxColorComponent);tint.a=1;
            material.SetColor("_BaseColor",tint);material.SetFloat("_Smoothness",.18f);material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",tint*.3f);material.enableInstancing=true;EditorUtility.SetDirty(material);
            if(feedback.indicator){feedback.indicator.sharedMaterial=material;Changed(feedback.indicator);}
            feedback.Refresh(1);Changed(feedback);
        }
        static void Changed(UnityEngine.Object target){EditorUtility.SetDirty(target);PrefabUtility.RecordPrefabInstancePropertyModifications(target);}
    }
}
