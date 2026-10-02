using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    public static class FirstPersonPresentationAuthoring
    {
        public static void ConfigurePlayer(GameObject player)
        {
            if(Application.isPlaying)throw new InvalidOperationException("Configure the player in Edit mode.");
            var walker=player.GetComponent<CourtyardWalker>();var rig=player.GetComponent<CoastalPlayerCamera>();
            if(!walker||!rig||!rig.view||!walker.eye)throw new InvalidOperationException("Player needs its walker, eye and camera.");
            int layer=LayerMask.NameToLayer("FirstPersonHands");
            if(layer<0)throw new InvalidOperationException("The existing FirstPersonHands presentation layer is missing.");
            rig.lantern=player.GetComponent<PlayerLantern>();rig.followAnchor=walker.eye;
            rig.showFirstPersonHands=false;rig.thirdPerson=false;walker.rotateBodyWithLook=true;
            rig.view.fieldOfView=75;rig.view.nearClipPlane=.04f;rig.view.cullingMask&=~(1<<layer);
            if(rig.avatar)
            {
                rig.avatar.keepShadowsWhenHidden=true;rig.avatar.SetVisible(false);Record(rig.avatar);
                foreach(var renderer in rig.avatar.bodyRenderers)Record(renderer);
            }
            if(rig.hands)rig.hands.SetVisible(false);
            var anchor=rig.view.transform.Find("LanternViewAnchor");
            if(!anchor){anchor=new GameObject("LanternViewAnchor").transform;anchor.SetParent(rig.view.transform,false);}
            anchor.localPosition=new Vector3(.35f,-.20f,.85f);anchor.localRotation=Quaternion.Euler(0,-20,-4);anchor.localScale=Vector3.one;anchor.gameObject.layer=layer;
            var overlay=rig.view.transform.Find("LanternOnlyOverlay");
            if(!overlay){overlay=new GameObject("LanternOnlyOverlay").transform;overlay.SetParent(rig.view.transform,false);}
            overlay.localPosition=Vector3.zero;overlay.localRotation=Quaternion.identity;overlay.localScale=Vector3.one;
            var camera=overlay.GetComponent<Camera>();if(!camera)camera=overlay.gameObject.AddComponent<Camera>();
            camera.enabled=false;camera.cullingMask=1<<layer;camera.fieldOfView=65;camera.nearClipPlane=.01f;camera.farClipPlane=4;
            camera.clearFlags=CameraClearFlags.Depth;camera.useOcclusionCulling=false;
            var data=camera.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;data.renderShadows=false;
            data.requiresColorOption=CameraOverrideOption.Off;data.requiresDepthOption=CameraOverrideOption.Off;data.volumeLayerMask=0;
            var settings=new SerializedObject(data);settings.FindProperty("m_ClearDepth").boolValue=true;settings.ApplyModifiedPropertiesWithoutUndo();
            var main=rig.view.GetUniversalAdditionalCameraData();main.renderType=CameraRenderType.Base;
            if(!main.cameraStack.Contains(camera))main.cameraStack.Add(camera);rig.heldItemCamera=camera;
            if(rig.lantern)
            {
                rig.lantern.firstPersonHand=anchor;
                rig.lantern.SetPerspective(false);
                if(rig.lantern.carriedLantern)
                {
                    ConfigureLanternVisual(rig.lantern.carriedLantern);
                    rig.lantern.carriedLantern.SetActive(false);
                    foreach(var node in rig.lantern.carriedLantern.GetComponentsInChildren<Transform>(true)){Record(node);Record(node.gameObject);}
                    foreach(var renderer in rig.lantern.carriedLantern.GetComponentsInChildren<Renderer>(true))Record(renderer);
                }
                if(rig.lantern.handLight){rig.lantern.handLight.cullingMask&=~(1<<layer);Record(rig.lantern.handLight);}
                Record(rig.lantern);
            }
            rig.SetThirdPerson(false);
            foreach(var value in new UnityEngine.Object[]{rig,walker,rig.view,main,data,camera,anchor,overlay})Record(value);
        }

        static void ConfigureLanternVisual(GameObject lamp)
        {
            const string meshPath="Assets/Objects/Player/HeldLanternMesh.asset";
            Directory.CreateDirectory("Assets/Objects/Player");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!mesh)
            {
                var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);
                var source=cube.GetComponent<MeshFilter>().sharedMesh;
                var frame=new List<CombineInstance>();var glow=new List<CombineInstance>();
                void Part(List<CombineInstance> list,Vector3 position,Vector3 size)
                {list.Add(new CombineInstance{mesh=source,transform=Matrix4x4.TRS(position,Quaternion.identity,size)});}
                Part(frame,new Vector3(0,-.42f,0),new Vector3(.86f,.14f,.86f));
                Part(frame,new Vector3(0,.36f,0),new Vector3(.86f,.12f,.86f));
                Part(frame,new Vector3(0,.46f,0),new Vector3(.55f,.1f,.55f));
                for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
                    Part(frame,new Vector3(x*.35f,-.03f,z*.35f),new Vector3(.08f,.68f,.08f));
                Part(frame,new Vector3(-.22f,.65f,0),new Vector3(.07f,.38f,.07f));
                Part(frame,new Vector3(.22f,.65f,0),new Vector3(.07f,.38f,.07f));
                Part(frame,new Vector3(0,.82f,0),new Vector3(.5f,.07f,.07f));
                Part(glow,new Vector3(0,-.03f,0),new Vector3(.48f,.58f,.48f));
                var dark=new Mesh();dark.CombineMeshes(frame.ToArray(),true,true);
                var light=new Mesh();light.CombineMeshes(glow.ToArray(),true,true);
                mesh=new Mesh{name="HeldLanternMesh"};mesh.CombineMeshes(new[]{new CombineInstance{mesh=dark,transform=Matrix4x4.identity},new CombineInstance{mesh=light,transform=Matrix4x4.identity}},false,true);
                AssetDatabase.CreateAsset(mesh,meshPath);
                UnityEngine.Object.DestroyImmediate(dark);UnityEngine.Object.DestroyImmediate(light);UnityEngine.Object.DestroyImmediate(cube);
            }
            Material Tint(string name,Color color)
            {
                string path="Assets/Materials/Player/"+name+".mat";
                var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Unlit")){name=name};material.SetColor("_BaseColor",color);AssetDatabase.CreateAsset(material,path);}
                return material;
            }
            var filter=lamp.GetComponent<MeshFilter>();if(!filter)filter=lamp.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
            var renderer=lamp.GetComponent<MeshRenderer>();if(!renderer)renderer=lamp.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=new[]{Tint("LanternFrame",new Color(.24f,.19f,.12f)),Tint("LanternGlow",new Color(1,.76f,.34f))};
            renderer.receiveShadows=false;Record(filter);Record(renderer);
            foreach(var shape in lamp.GetComponentsInChildren<Collider>(true))UnityEngine.Object.DestroyImmediate(shape);
        }

        public static string SaveReusablePlayer()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Save the prefab in Edit mode.");
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/ExplorerThirdPerson.prefab");
            var copy=(GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                // A variant cannot retain reparenting of the base camera/lantern hierarchy.
                PrefabUtility.UnpackPrefabInstance(copy,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
                copy.name="ExplorerFirstPerson";
                var walker=copy.GetComponentInChildren<CourtyardWalker>(true);
                var rig=walker.GetComponent<CoastalPlayerCamera>();rig.overviewRoot=copy.transform;
                walker.water=null;walker.additionalWaters=Array.Empty<Courtyard.Water.WaterVolume>();walker.discoverSceneWater=true;
                ConfigurePlayer(walker.gameObject);
                PrefabUtility.SaveAsPrefabAsset(copy,"Assets/Prefabs/Player/ExplorerFirstPerson.prefab");
                return "Saved Assets/Prefabs/Player/ExplorerFirstPerson.prefab";
            }
            finally{UnityEngine.Object.DestroyImmediate(copy);}
        }

        static void Record(UnityEngine.Object value)
        {if(!value)return;EditorUtility.SetDirty(value);if(PrefabUtility.IsPartOfPrefabInstance(value))PrefabUtility.RecordPrefabInstancePropertyModifications(value);}

        public static string ConfigureScene()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach(var rig in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CoastalPlayerCamera>(true)))
            {
                ConfigurePlayer(rig.gameObject);
                if(scene.name=="CoastalTemple")
                {rig.walker.RespawnAt(rig.walker.transform.position,180);rig.SnapToTarget();Record(rig.walker.transform);}
                var guide=rig.walker.eye.GetComponent<SceneObjectGuide>();
                if(guide){guide.label="第一人称眼部 · 原地转头";guide.explanation="主摄像机绑定固定 Eye，不跟随动画骨骼。身体只投影；镜头内没有身体或手臂。提灯由 LanternOnlyOverlay 单独显示。E 操纵机关时升高到关卡俯视角，WASD / 空格 / Esc 返回。";Record(guide);}
            }
            var shore=GameObject.Find("SU_ShoreTutorial");
            if(shore)
            {
                var area=shore.GetComponent<MechanismObservationArea>();if(!area)area=shore.AddComponent<MechanismObservationArea>();
                area.localCenter=shore.transform.InverseTransformPoint(new Vector3(-64.5f,3,198));
                area.localSize=new Vector3(52,7,44);area.yaw=180;Record(area);
            }
            if(scene.name=="MechanismPlayground")
            {
                foreach(var control in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TutorialInteractable>(true)))
                {
                    if(!control.UsesObservationView)continue;
                    var ownArea=control.GetComponent<MechanismObservationArea>();
                    var inheritedArea=control.GetComponentInParent<MechanismObservationArea>();
                    if(inheritedArea&&inheritedArea!=ownArea)continue;
                    var p=control.InteractionPosition;
                    if(p.x>-34&&p.x<34&&p.z>-15&&p.z<31)
                    {
                        var area=ownArea?ownArea:control.gameObject.AddComponent<MechanismObservationArea>();
                        var center=p.x<4?new Vector3(-15,1,9):new Vector3(16,3,7);
                        var size=p.x<4?new Vector3(42,12,44):new Vector3(44,12,44);
                        area.localCenter=control.transform.InverseTransformPoint(center);
                        var scale=control.transform.lossyScale;
                        area.localSize=new Vector3(size.x/Mathf.Max(.001f,Mathf.Abs(scale.x)),size.y/Mathf.Max(.001f,Mathf.Abs(scale.y)),size.z/Mathf.Max(.001f,Mathf.Abs(scale.z)));
                        area.yaw=180;Record(area);
                    }
                }
            }
            foreach(var tour in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CoastalWalkthrough>(true))){tour.inspectionShortcuts=false;Record(tour);}
            EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return "Saved first-person eye camera, hidden body/hands, isolated lantern and mechanism observation areas: "+scene.path;
        }
    }
}
