using System.Linq;
using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class MechanismPlaygroundAuthoring
    {
        public const string ScenePath="Assets/Scenes/MechanismPlayground.unity";
        const string PlayerPath="Assets/Prefabs/Player/ExplorerThirdPerson.prefab";
        static Material ivory,gold,blue;

        [MenuItem("Coastal Temple/Mechanisms/Rebuild showroom (overwrites layout)")]
        public static void Build()
        {
            if(EditorApplication.isPlaying) throw new System.InvalidOperationException("Exit Play first.");
            if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
            if(System.IO.File.Exists(ScenePath)&&!EditorUtility.DisplayDialog("重建实验场景","此操作会重新生成实验场景并覆盖其中的手动布局。仅切换场景请使用 Coastal Temple > Scenes 菜单。","重建并覆盖","取消"))return;
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath)) ExportPlayer();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            ivory=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/Ivory.mat");
            gold=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/Ochre.mat");
            blue=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/PortalBlue.mat");
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor=new Color(.7f,.78f,.88f); RenderSettings.ambientEquatorColor=new Color(.45f,.48f,.48f); RenderSettings.ambientGroundColor=new Color(.28f,.25f,.2f);
            RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Environment/CoastalSky.mat");
            RenderSettings.fog=false;
            var light=new GameObject("Sun").AddComponent<Light>(); light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(45,-35,0);light.shadows=LightShadows.Soft;
            Box("Main floor",new Vector3(0,-.25f,-7),new Vector3(65,.5f,26),ivory);
            Box("Eastern court",new Vector3(16,-.25f,17),new Vector3(33,.5f,22),ivory);
            Box("Pool west walk",new Vector3(-29,-.25f,17),new Vector3(5,.5f,23),ivory);
            Box("Pool rear walk",new Vector3(-16,-.25f,29),new Vector3(30,.5f,4),ivory);
            Place("LaserDevice",new Vector3(-17,0,-7));
            Place("RedStoneCurtain",new Vector3(-17,0,0));
            // A second, rotated and nonuniformly scaled pair demonstrates transform independence.
            var laser=Place("LaserDevice",new Vector3(-9,0,-7));laser.transform.localScale=new Vector3(.8f,1.3f,1.2f);
            var curtain=Place("RedStoneCurtain",new Vector3(-9,0,0));curtain.transform.localScale=new Vector3(.7f,1.3f,1.2f);curtain.transform.rotation=Quaternion.Euler(0,20,0);
            var bridge=Place("LightBridge",new Vector3(-1,0,1));
            var bridgeLaser=Place("LaserDevice",new Vector3(-2.7f,0,-4));
            Aim(bridgeLaser,bridge.GetComponentInChildren<LightReceiver>().transform.position);
            Place("LightDrivenLift",new Vector3(9,0,3));
            var pair=Place("PortalPair",Vector3.zero);
            var gates=pair.GetComponentsInChildren<LightPortal>();
            gates[0].transform.SetPositionAndRotation(new Vector3(7,1.8f,-8),Quaternion.identity);
            gates[1].transform.SetPositionAndRotation(new Vector3(22,1.8f,17),Quaternion.Euler(0,90,0));
            foreach(var gate in gates) PrefabUtility.RecordPrefabInstancePropertyModifications(gate.transform);
            Box("Destination gold floor",new Vector3(26,.015f,17),new Vector3(8,.03f,8),gold);
            Box("Destination blue pillar",new Vector3(27,1.5f,19),new Vector3(.7f,3,.7f),blue);
            Box("Destination gold pillar",new Vector3(27,1,15),new Vector3(.7f,2,.7f),gold);
            var portalLaser=Place("LaserDevice",new Vector3(7,0,-3));portalLaser.transform.rotation=Quaternion.Euler(0,180,0);
            var portalReceiver=Place("LightReceiver",new Vector3(29,.3f,17));portalReceiver.transform.rotation=Quaternion.Euler(0,90,0);
            // Keep this laser off initially so the player can approach and switch its color with E.
            var waterObj=new GameObject("Showroom water - use dimensions to resize");waterObj.SetActive(false);waterObj.transform.position=new Vector3(-16,0,17);
            var water=waterObj.AddComponent<WaterVolume>();water.sizeX=21;water.sizeZ=19;water.cellSize=.5f;water.bottom=-4;water.initialLevel=-.3f;
            water.surfaceMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Water/Water.mat");water.drainPosition=new Vector2(-5,-3);water.outletArea=4;
            Box("Pool bottom",new Vector3(-16,-4.2f,17),new Vector3(22,.4f,20),ivory);
            Box("Pool west wall",new Vector3(-26.7f,-2,17),new Vector3(.4f,4,20),ivory);
            Box("Pool east wall",new Vector3(-5.3f,-2,17),new Vector3(.4f,4,20),ivory);
            Box("Pool far wall",new Vector3(-16,-2,26.7f),new Vector3(22,4,.4f),ivory);
            // Open south edge has broad steps down to the bottom.
            if(!AssetDatabase.LoadAssetAtPath<GameObject>(TraversalV2Authoring.StairPrefab))TraversalV2Authoring.BuildStairPrefab();
            TraversalV2Authoring.PlaceStair(null,new Vector3(-24,-4,11.71f),Quaternion.Euler(0,180,0),new Vector3(3/2.2f,4/4.2f,5/7.7f)).name="Pool staircase - reusable";
            var vertical=Place("VerticalBuoyantPlatform",new Vector3(-20,-3.3f,15));
            var ferry=Place("HorizontalBuoyantPlatform",new Vector3(-16,-.2f,22));
            var drain=Place("DrainGate",new Vector3(-20,-3.8f,15));
            var mirage=Place("WaterMirage",new Vector3(-11,-.3f,13));
            waterObj.SetActive(true);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath));
            var walker=player.GetComponentInChildren<CourtyardWalker>();walker.water=water;walker.additionalWaters=System.Array.Empty<WaterVolume>();
            walker.transform.SetPositionAndRotation(new Vector3(7,.05f,-.8f),Quaternion.Euler(0,180,0));walker.active=true;
            var camera=player.GetComponentInChildren<CoastalPlayerCamera>();walker.ApplyLook(new Vector2(Mathf.DeltaAngle(walker.LookYaw,180),0));camera.SnapToTarget();
            var hud=player.AddComponent<MechanismPlaygroundHud>();hud.walker=walker;hud.cameraRig=camera;hud.interactor=walker.GetComponent<PlayerInteractor>();
            DebugWaterAuthoring.ConfigureScene(scene,true);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Selection.activeGameObject=pair;
        }
        static void ExportPlayer()
        {
            if(SceneManager.GetActiveScene().path!="Assets/Scenes/CoastalTemple.unity") EditorSceneManager.OpenScene("Assets/Scenes/CoastalTemple.unity");
            var walker=Object.FindFirstObjectByType<CourtyardWalker>();
            var copy=Object.Instantiate(walker.transform.root.gameObject);copy.name="ExplorerThirdPerson";
            try
            {
                foreach(var component in copy.GetComponentsInChildren<Component>(true))
                    if(component is CoastalWalkthrough || component is CourtyardDrainExperiment || component is SceneObjectGuide || component is TutorialInput)Object.DestroyImmediate(component);
                foreach(var child in copy.transform.Cast<Transform>().ToArray())
                    if(!child.GetComponentInChildren<CourtyardWalker>(true) && !child.GetComponentInChildren<Camera>(true))Object.DestroyImmediate(child.gameObject);
                var w=copy.GetComponentInChildren<CourtyardWalker>();w.water=null;w.additionalWaters=System.Array.Empty<WaterVolume>();w.active=true;
                if(!w.GetComponent<CourtyardLedgeClimb>())w.gameObject.AddComponent<CourtyardLedgeClimb>();
                if(!w.GetComponent<CourtyardSurfaceSwimmer>())w.gameObject.AddComponent<CourtyardSurfaceSwimmer>();
                w.transform.SetPositionAndRotation(Vector3.up*.05f,Quaternion.identity);
                var lamp=w.GetComponent<PlayerLantern>();if(lamp)lamp.pickedUp=new UnityEvent();
                var rig=w.GetComponent<CoastalPlayerCamera>();rig.overviewRoot=copy.transform;rig.thirdPerson=true;rig.SetThirdPerson(true);
                PrefabUtility.SaveAsPrefabAsset(copy,PlayerPath);
            }
            finally{Object.DestroyImmediate(copy);}
        }
        static GameObject Place(string name,Vector3 position)
        {
            var item=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MechanismKitAuthoring.Folder+"/"+name+".prefab"));item.transform.position=position;return item;
        }
        static void Aim(GameObject source,Vector3 target)
        {
            var emitter=source.GetComponent<LaserEmitter>(); emitter.Origin.rotation=Quaternion.LookRotation(target-emitter.Origin.position);
            PrefabUtility.RecordPrefabInstancePropertyModifications(emitter.Origin);
        }
        static void Box(string name,Vector3 position,Vector3 size,Material material)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.position=position;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
