using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CoastalTemple.Player;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class FirstPersonAuthoring
    {

        public const string PrefabPath="Assets/Prefabs/Player/FirstPersonHands.prefab";

        [MenuItem("Coastal Temple/Legacy Quaternius/Configure first-person hands and eye camera")]
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before authoring.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var tour=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<CoastalWalkthrough>(true)).Single();
            Directory.CreateDirectory("Assets/Prefabs/Player");AssetDatabase.Refresh();int layer=HandsLayer();
            PreparePrefab(layer);
            var camera=tour.view;var rig=tour.playerCamera;var player=tour.walker;
            var lantern=player.GetComponent<PlayerLantern>();var carried=lantern?lantern.carriedLantern:null;
            if(carried)carried.transform.SetParent(player.transform,true);
            var cameraData=camera.GetUniversalAdditionalCameraData();
            if(rig.hands)
            {
                if(rig.hands.overlayCamera)cameraData.cameraStack.Remove(rig.hands.overlayCamera);
                Object.DestroyImmediate(rig.hands.gameObject);
            }
            var handsObject=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),camera.transform);
            handsObject.transform.localPosition=Vector3.zero;handsObject.transform.localRotation=Quaternion.identity;
            var hands=handsObject.GetComponent<FirstPersonHands>();hands.walker=player;hands.lantern=lantern;
            cameraData.renderType=CameraRenderType.Base;cameraData.cameraStack.Add(hands.overlayCamera);
            camera.cullingMask&=~(1<<layer);camera.nearClipPlane=.025f;camera.fieldOfView=75;
            rig.hands=hands;rig.followAnchor=player.eye;rig.thirdPerson=false;
            var bodyDriver=player.GetComponentInChildren<RiggedPlayerAnimation>(true);
            if(bodyDriver)
            {
                var bodyAnchor=bodyDriver.GetComponent<PlayerBodyAnchor>();
                if(!bodyAnchor)bodyAnchor=bodyDriver.gameObject.AddComponent<PlayerBodyAnchor>();
                bodyAnchor.walker=player;bodyAnchor.animationDriver=bodyDriver;bodyAnchor.SnapToWalker();
                PrefabUtility.RecordPrefabInstancePropertyModifications(bodyDriver.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bodyDriver.facingRoot);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bodyAnchor);
            }
            if(rig.avatar){rig.avatar.keepShadowsWhenHidden=true;PrefabUtility.RecordPrefabInstancePropertyModifications(rig.avatar);}
            tour.inspectionShortcuts=false;rig.SnapToTarget();
            var oldAnchor=player.transform.Find("CameraFollow_稳定跟随点");
            if(oldAnchor&&oldAnchor.childCount==0)Object.DestroyImmediate(oldAnchor.gameObject);
            if(carried)
            {
                // Quaternius Idle_Torch_Loop holds the torch in the left hand.
                var hand=hands.animator.GetBoneTransform(HumanBodyBones.LeftHand);
                lantern.firstPersonHand=hand;
                if(bodyDriver)lantern.worldHand=bodyDriver.animator.GetBoneTransform(HumanBodyBones.LeftHand);
                carried.transform.SetParent(hand,false);carried.transform.localPosition=new Vector3(0,-.07f,0);
                carried.transform.localRotation=Quaternion.identity;var s=hand.lossyScale;
                float viewScale=hands.transform.lossyScale.x;
                carried.transform.localScale=new Vector3(.12f/Mathf.Abs(s.x),.18f/Mathf.Abs(s.y),.12f/Mathf.Abs(s.z))*viewScale;
                foreach(var node in carried.GetComponentsInChildren<Transform>(true))node.gameObject.layer=layer;
                foreach(var renderer in carried.GetComponentsInChildren<Renderer>(true))renderer.shadowCastingMode=ShadowCastingMode.Off;
                // A near-eye viewmodel must not be overexposed by the player's world light.
                if(lantern.handLight)lantern.handLight.cullingMask&=~(1<<layer);
                string lanternMaterialPath="Assets/Materials/Player/HeldLantern.mat";
                var lanternMaterial=AssetDatabase.LoadAssetAtPath<Material>(lanternMaterialPath);
                if(!lanternMaterial)
                {
                    lanternMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="HeldLantern"};
                    lanternMaterial.SetColor("_BaseColor",new Color(.85f,.65f,.27f));
                    lanternMaterial.SetFloat("_Smoothness",.25f);
                    lanternMaterial.EnableKeyword("_EMISSION");lanternMaterial.SetColor("_EmissionColor",new Color(.25f,.15f,.025f));
                    AssetDatabase.CreateAsset(lanternMaterial,lanternMaterialPath);
                }
                var lanternRenderer=carried.GetComponent<Renderer>();if(lanternRenderer)lanternRenderer.sharedMaterial=lanternMaterial;
                carried.SetActive(false);lantern.carriedLantern=carried;
                if(tour.tutorial)tour.tutorial.carriedLantern=carried;
            }
            var guide=player.eye.GetComponent<SceneObjectGuide>();if(!guide)guide=player.eye.gameObject.AddComponent<SceneObjectGuide>();
            guide.label="第一人称眼部 · 原地转头";
            guide.explanation="位置在角色胶囊体内，鼠标左右转动角色根，俯仰只转动 Eye。主摄像机在此处局部位置为零，无后退距离、肩偏移或环绕。FirstPersonHands 只显示下载人偶的手臂；HandsOverlay 单独渲染手臂防止贴墙裁切。全身模型只留世界阴影。";
            foreach(var marker in tour.viewpoints)
            {
                var markerGuide=marker.GetComponent<SceneObjectGuide>();if(!markerGuide)continue;
                markerGuide.label="编辑检查用全景取景点";
                markerGuide.explanation="只保存编辑检查用的摄像机位置与朝向，不控制地形。正式第一人称玩法已关闭 Tab / F1–F3 切换；开发检查时可显式调用 CoastalWalkthrough.ShowView，结束后 SetOverview(false) 返回眼部。";
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(hands);
            EditorUtility.SetDirty(rig);EditorUtility.SetDirty(tour);if(lantern)EditorUtility.SetDirty(lantern);
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return "First-person scene authored: eye-locked camera, extracted rigged arms, isolated overlay, world-only body shadow.";
        }

        static int HandsLayer()
        {
            int existing=LayerMask.NameToLayer("FirstPersonHands");if(existing>=0)return existing;
            var manager=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers=manager.FindProperty("layers");
            for(int i=8;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {layers.GetArrayElementAtIndex(i).stringValue="FirstPersonHands";manager.ApplyModifiedPropertiesWithoutUndo();return i;}
            throw new InvalidOperationException("No unused layer for first-person presentation.");
        }

        static void PreparePrefab(int layer)
        {
            var root=new GameObject("FirstPersonHands");
            try
            {
                var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ReusableGameplayAuthoring.ModelPath));
                model.name="Downloaded_Arms_And_Rig";model.transform.SetParent(root.transform,false);
                var skin=model.GetComponentInChildren<SkinnedMeshRenderer>();
                float scale=1.8f/skin.bounds.size.y;
                var original=skin.sharedMesh;
                var arms=ExtractArms(original,skin.bones);
                string path="Assets/Objects/Characters/Quaternius/Quaternius_ArmsOnly.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(old)
                {
                    MeshAssetWriter.CopyMeshBuffers(arms,old);old.bindposes=arms.bindposes;old.boneWeights=arms.boneWeights;old.MarkModified();old.UploadMeshData(false);EditorUtility.SetDirty(old);Object.DestroyImmediate(arms);arms=old;
                }
                else AssetDatabase.CreateAsset(arms,path);
                skin.sharedMesh=arms;skin.sharedMaterials=new[]{AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Player/Mannequin_Ivory.mat")};
                skin.shadowCastingMode=ShadowCastingMode.Off;skin.receiveShadows=false;skin.updateWhenOffscreen=true;
                model.transform.localScale=Vector3.one*scale;model.transform.localRotation=Quaternion.Euler(0,180,0);
                var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=Controller();animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                var cameraObject=new GameObject("HandsOverlay");cameraObject.transform.SetParent(root.transform,false);
                var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=1<<layer;camera.fieldOfView=65;camera.nearClipPlane=.015f;camera.farClipPlane=4;
                camera.clearFlags=CameraClearFlags.Depth;camera.useOcclusionCulling=false;camera.allowMSAA=true;
                var data=camera.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;data.renderShadows=false;
                var cameraSettings=new SerializedObject(data);cameraSettings.FindProperty("m_ClearDepth").boolValue=true;cameraSettings.ApplyModifiedPropertiesWithoutUndo();
                data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;data.volumeLayerMask=0;
                var hands=root.AddComponent<FirstPersonHands>();hands.model=model.transform;hands.animator=animator;hands.overlayCamera=camera;model.transform.localPosition=hands.restingPosition;
                foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=layer;
                // Shrink camera-space geometry around the eye without changing its apparent screen size.
                // The hands then remain near the protected center of the player's collision capsule.
                root.transform.localScale=Vector3.one*.35f;
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            }
            finally{Object.DestroyImmediate(root);}
        }

        public static Mesh ExtractArms(Mesh source,Transform[] bones)
        {
            bool Allowed(int i)=>i>=0&&i<bones.Length&&(bones[i].name.Contains("upper_arm")||bones[i].name.Contains("forearm")||bones[i].name.Contains("hand.")||bones[i].name.Contains("f_")||bones[i].name.Contains("thumb"));
            var weights=source.boneWeights;
            float ArmWeight(int i){var w=weights[i];return (Allowed(w.boneIndex0)?w.weight0:0)+(Allowed(w.boneIndex1)?w.weight1:0)+(Allowed(w.boneIndex2)?w.weight2:0)+(Allowed(w.boneIndex3)?w.weight3:0);}
            var selected=new List<int>();var triangles=source.triangles;
            for(int i=0;i<triangles.Length;i+=3)if(ArmWeight(triangles[i])>.99f&&ArmWeight(triangles[i+1])>.99f&&ArmWeight(triangles[i+2])>.99f)
            {selected.Add(triangles[i]);selected.Add(triangles[i+1]);selected.Add(triangles[i+2]);}
            var ids=selected.Distinct().OrderBy(i=>i).ToArray();if(ids.Length<100)throw new InvalidOperationException("Source model has no usable weighted arm geometry.");
            var map=new Dictionary<int,int>();for(int i=0;i<ids.Length;i++)map[ids[i]]=i;
            var mesh=new Mesh{name="Quaternius_ArmsOnly"};var positions=source.vertices;var normals=source.normals;var uv=source.uv;var tangents=source.tangents;
            mesh.vertices=ids.Select(i=>positions[i]).ToArray();if(normals.Length>0)mesh.normals=ids.Select(i=>normals[i]).ToArray();
            if(uv.Length>0)mesh.uv=ids.Select(i=>uv[i]).ToArray();if(tangents.Length>0)mesh.tangents=ids.Select(i=>tangents[i]).ToArray();
            mesh.bindposes=source.bindposes;mesh.boneWeights=ids.Select(i=>weights[i]).ToArray();mesh.triangles=selected.Select(i=>map[i]).ToArray();mesh.RecalculateBounds();return mesh;
        }

        static AnimatorController Controller()
        {
            string path="Assets/Animations/Player/FirstPersonHands.controller";
            var c=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            // One-time V11 -> V12 upgrade in place keeps all existing prefab references (GUID) valid.
            if(c&&c.parameters.Any(p=>p.name=="Grounded"))return PlayerLocomotionAuthoring.ApplyTo(RelaxedHandsAuthoring.Configure(c));
            if(!c)c=AnimatorController.CreateAnimatorControllerAtPath(path);
            c.layers=Array.Empty<AnimatorControllerLayer>();c.parameters=Array.Empty<AnimatorControllerParameter>();
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))if(asset!=c)Object.DestroyImmediate(asset,true);
            var machine=new AnimatorStateMachine{name="Base Layer",hideFlags=HideFlags.HideInHierarchy};AssetDatabase.AddObjectToAsset(machine,c);
            c.layers=new[]{new AnimatorControllerLayer{name="Base Layer",stateMachine=machine,defaultWeight=1}};
            c.AddParameter("Swimming",AnimatorControllerParameterType.Bool);c.AddParameter("Grounded",AnimatorControllerParameterType.Bool);c.AddParameter("Speed",AnimatorControllerParameterType.Float);
            var clips=AssetDatabase.LoadAllAssetsAtPath(ReusableGameplayAuthoring.ModelPath).OfType<AnimationClip>().Where(x=>!x.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string name)=>clips.Single(x=>x.name=="Rig|"+name);
            var sm=c.layers[0].stateMachine;
            var land=sm.AddState("Locomotion");sm.defaultState=land;
            var locomotion=new BlendTree{name="Imported Idle Walk Jog",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            locomotion.AddChild(Clip("Idle_Loop"),0);locomotion.AddChild(Clip("Walk_Loop"),3.4f);locomotion.AddChild(Clip("Jog_Fwd_Loop"),5.3f);
            AssetDatabase.AddObjectToAsset(locomotion,c);land.motion=locomotion;
            var air=sm.AddState("Airborne");air.motion=Clip("Jump_Loop");
            var landing=sm.AddState("Landing");landing.motion=Clip("Jump_Land");landing.speed=3;
            var swim=sm.AddState("Swimming");var tree=new BlendTree{name="Imported Swimming Arms",blendType=BlendTreeType.Simple1D,blendParameter="Speed",useAutomaticThresholds=false};
            tree.AddChild(Clip("Swim_Idle_Loop"),0);tree.AddChild(Clip("Swim_Fwd_Loop"),1.87f);AssetDatabase.AddObjectToAsset(tree,c);swim.motion=tree;
            void Setup(AnimatorStateTransition t){t.hasExitTime=false;t.hasFixedDuration=true;t.duration=.12f;t.canTransitionToSelf=false;}
            var toSwim=sm.AddAnyStateTransition(swim);Setup(toSwim);toSwim.AddCondition(AnimatorConditionMode.If,0,"Swimming");
            var toAir=sm.AddAnyStateTransition(air);Setup(toAir);toAir.AddCondition(AnimatorConditionMode.IfNot,0,"Swimming");toAir.AddCondition(AnimatorConditionMode.IfNot,0,"Grounded");
            var touchDown=air.AddTransition(landing);Setup(touchDown);touchDown.AddCondition(AnimatorConditionMode.IfNot,0,"Swimming");touchDown.AddCondition(AnimatorConditionMode.If,0,"Grounded");
            var afterLand=landing.AddTransition(land);Setup(afterLand);afterLand.hasExitTime=true;afterLand.exitTime=.65f;
            var exitWater=swim.AddTransition(land);Setup(exitWater);exitWater.AddCondition(AnimatorConditionMode.IfNot,0,"Swimming");exitWater.AddCondition(AnimatorConditionMode.If,0,"Grounded");
            // Carry only overrides the hand actually holding the lamp. The free arm still walks/runs/jumps.
            var mask=new AvatarMask{name="Left torch arm only",hideFlags=HideFlags.HideInHierarchy};
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm,true);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,true);AssetDatabase.AddObjectToAsset(mask,c);
            var carryMachine=new AnimatorStateMachine{name="Carried Lantern",hideFlags=HideFlags.HideInHierarchy};AssetDatabase.AddObjectToAsset(carryMachine,c);
            var carry=carryMachine.AddState("Carry");carry.motion=Clip("Idle_Torch_Loop");carryMachine.defaultState=carry;
            c.AddLayer(new AnimatorControllerLayer{name="Carried Lantern",stateMachine=carryMachine,avatarMask=mask,defaultWeight=0,blendingMode=AnimatorLayerBlendingMode.Override});
            EditorUtility.SetDirty(c);return PlayerLocomotionAuthoring.ApplyTo(RelaxedHandsAuthoring.Configure(c));
        }
    }
}
