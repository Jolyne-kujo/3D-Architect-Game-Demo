using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using CoastalTemple.Mechanisms;
using CoastalTemple.Tutorial;

namespace CoastalTemple.Editor
{
    /// <summary>Editor-only recipe. All geometry, colliders and references are saved in the scene/prefabs.</summary>
    public static class TutorialSceneAuthoring
    {

        static Material white, soil, dark, red, cyan, gold;
        static Transform stage;
        static float grade;
        static CoastalWalkthrough tour;
        static TutorialJourney journey;

        // Archived V8 recipe retained for source history; current authoring uses TutorialV9Authoring.
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play before authoring.");
            Directory.CreateDirectory("Assets/Materials/Tutorial/V8"); Directory.CreateDirectory("Assets/Objects/LevelGeometry/Tutorial/V8"); Directory.CreateDirectory("Assets/Prefabs/Tutorial/V8");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!scene.path.EndsWith("/CoastalTemple.unity")) throw new System.InvalidOperationException("Open CoastalTemple scene first.");
            string archive="Assets/Scenes/Archive/CoastalTemple_V7.unity";
            if (!File.Exists(archive)) { Directory.CreateDirectory(Path.GetDirectoryName(archive)); EditorSceneManager.SaveScene(scene,archive,true); }
            var previous=GameObject.Find("40_Tutorial"); if(previous) Object.DestroyImmediate(previous);
            tour=Object.FindFirstObjectByType<CoastalWalkthrough>();
            white=Mat("Chalk",new Color(.93f,.925f,.88f)); soil=Mat("Sandstone",new Color(.83f,.75f,.57f));
            dark=Mat("Recess",new Color(.17f,.20f,.20f)); red=Mat("StoneCurtain_Red",new Color(.68f,.085f,.065f));
            cyan=Mat("Portal_Turquoise",new Color(.12f,.76f,.79f),true); gold=Mat("Sunlight_Gold",new Color(1,.64f,.18f),true);
            Organize();
            var gameplay=Group("40_Tutorial",null); journey=gameplay.gameObject.AddComponent<TutorialJourney>();
            journey.walker=tour.walker;
            var avatar=tour.walker.GetComponentInChildren<CoastalPlayerAvatar>(); if(avatar) Object.DestroyImmediate(avatar.gameObject);
            avatar=PlayerAvatarAuthoring.BuildAvatar(tour.walker.transform,white,soil,dark);
            var camera=tour.walker.GetComponent<CoastalPlayerCamera>(); if(!camera) camera=tour.walker.gameObject.AddComponent<CoastalPlayerCamera>();
            camera.walker=tour.walker; camera.view=tour.view; camera.avatar=avatar; camera.thirdPerson=true; camera.pitchBias=-8;
            tour.playerCamera=camera; tour.tutorial=journey; journey.cameraRig=camera;
            var drain=GameObject.Find("Drain experiment console");
            if(drain)
            {
                var control=drain.GetComponent<CourtyardDrainConsole>(); if(!control)control=drain.AddComponent<CourtyardDrainConsole>();
                control.walkthrough=tour;
                var point=drain.transform.Find("InteractionPoint"); if(!point)point=Group("InteractionPoint",drain.transform);
                point.position=drain.transform.position+Vector3.up*.9f; control.interactionPoint=point;
            }
            var oldLamp=tour.walker.eye.Find("AcquiredLanternLight"); if(oldLamp) Object.DestroyImmediate(oldLamp.gameObject);
            var lamp=Group("AcquiredLanternLight",tour.walker.eye).gameObject.AddComponent<Light>();
            lamp.type=LightType.Point; lamp.color=new Color(1,.77f,.40f); lamp.range=9; lamp.intensity=2.3f; lamp.shadows=LightShadows.None; lamp.enabled=false; journey.handLight=lamp;
            var prop=Box("Lantern_Carried",avatar.poseRoot,new Vector3(.36f,.13f,.12f),new Vector3(.12f,.18f,.12f),gold,false);
            prop.SetActive(false); journey.carriedLantern=prop;
            FirstStage(gameplay); SecondStage(gameplay); ThirdStage(gameplay);
            var bonus=MirageSandboxAuthoring.Build(gameplay,white,soil,cyan,tour.water.surfaceMaterial,new Vector3(-88,3,194));
            bonus.name="05_Optional_WaterMirage";
            var bonusWater=bonus.GetComponentInChildren<Courtyard.Water.WaterVolume>();
            tour.walker.additionalWaters=tour.walker.additionalWaters.Where(w=>w).Concat(new[]{bonusWater}).Distinct().ToArray();
            PrefabUtility.SaveAsPrefabAsset(bonus,"Assets/Prefabs/Tutorial/V8/WaterMirageSandbox.prefab");
            var end=Group("04_SeaReveal",gameplay); end.position=new Vector3(-3,41.2f,121);
            journey.stations=new[]{gameplay.Find("01_FirstLight"),gameplay.Find("02_BorrowedLight"),gameplay.Find("03_FoldedLight"),end};
            // A viewpoint reward uses the existing real view across the hidden bay.
            Marker("LookTowardsHiddenBay",end,new Vector3(48,-30,30));
            PrefabUtility.SaveAsPrefabAsset(avatar.gameObject,"Assets/Prefabs/Tutorial/V8/ExplorerVisual.prefab");
            PrefabUtility.SaveAsPrefabAsset(gameplay.Find("01_FirstLight").gameObject,"Assets/Prefabs/Tutorial/V8/FirstLight.prefab");
            PrefabUtility.SaveAsPrefabAsset(gameplay.Find("02_BorrowedLight").gameObject,"Assets/Prefabs/Tutorial/V8/BorrowedLight.prefab");
            PrefabUtility.SaveAsPrefabAsset(gameplay.Find("03_FoldedLight").gameObject,"Assets/Prefabs/Tutorial/V8/FoldedLight.prefab");
            tour.walker.transform.rotation=Quaternion.Euler(0,160,0);
            EditorUtility.SetDirty(tour); EditorUtility.SetDirty(journey);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Selection.activeGameObject=gameplay.gameObject;
        }

        static void Organize()
        {
            var geo=Group("10_Geography_EDIT_TERRAIN_HERE",null);
            var buildings=Group("20_Architecture",null); var ocean=Group("30_Water",null);
            var actors=Group("50_Player_And_Cameras",null); var lights=Group("60_Lighting",null);
            Parent("01_CoastalTerrain",geo); Parent("10_CompactMountain",geo); Parent("11_CoastalLandmarks",geo);
            Parent("00_ShoreStart",buildings); Parent("02_ShoreRuins_WaterCourt",buildings); Parent("04_SummitTemple",buildings);
            Parent("07_Sea",ocean); Parent("Player_ShoreStart",actors); Parent("Main Camera",actors); Parent("05_WalkthroughControls",actors);
            Parent("Sun",lights); Parent("Courtyard tone",lights);
            foreach(var name in new[]{"03_MountainPath","08_CliffLandforms","09_ModelHeadland","Sea_Backdrop_VisualOnly","Water Court — experiment controls"})
            { var found=Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(g=>g.scene.IsValid()&&g.scene==geo.gameObject.scene&&g.name==name); if(found&&!found.activeSelf) Object.DestroyImmediate(found); }
        }
        static void Parent(string name,Transform parent) { var g=GameObject.Find(name); if(g) g.transform.SetParent(parent,true); }

        static void FirstStage(Transform parent)
        {
            Stage("01_FirstLight",parent,new Vector3(-68,.04f,187),new Vector3(-3,0,-13),.035f,12);
            var curtain=Curtain("Permanent_Red_Curtain",0,CurtainMode.Permanent); journey.firstCurtain=curtain;
            var source=Emitter("Ancient_SunLens",new Vector3(-2.3f,Y(-3)+1.7f,-3));
            var blank=Marker("RestingLight_OnStone",stage,new Vector3(-3.3f,Y(-3)+1.7f,-3));
            var target=Marker("CurtainAim",stage,new Vector3(0,Y(0)+1.6f,0));
            var console=Console<AimConsole>("Turn_SunLens",new Vector3(-1.7f,Y(-3),-3));
            console.emitter=source; console.aimPoints=new[]{blank,target}; console.selected=0; console.prompt="E 转动导光镜";
            var altar=Box("LanternPedestal",stage,new Vector3(-1.7f,Y(3)+.4f,3),new Vector3(.9f,.8f,.9f),soil);
            var pickup=Group("Reusable_Lantern",stage); pickup.localPosition=new Vector3(-1.7f,Y(3)+1.02f,3);
            var cage=Box("LanternFrame",pickup,Vector3.zero,new Vector3(.36f,.48f,.36f),dark,false);
            Box("LanternGlow",pickup,new Vector3(0,.03f,-.20f),new Vector3(.25f,.3f,.06f),gold,false);
            var collect=pickup.gameObject.AddComponent<LanternPickup>(); collect.visuals=pickup.GetComponentsInChildren<Renderer>(); collect.prompt="E 取下提灯"; journey.lantern=collect;
            Sign("I   SUN",new Vector3(2.4f,Y(-4)+1.5f,-4),"一道裂痕，通向灯火");
            console.Apply();
        }
        static void SecondStage(Transform parent)
        {
            Stage("02_BorrowedLight",parent,new Vector3(-47.5f,28.4f,127.3f),new Vector3(13,0,-11),.34f,13);
            var terrain=Object.FindFirstObjectByType<Terrain>();var entry=stage.TransformPoint(new Vector3(0,0,-10));
            float entryHeight=terrain.SampleHeight(entry)+terrain.transform.position.y-stage.position.y+.04f;
            Deck("BorrowedLight_Approach",stage,Vector3.zero,7,-10,-6.5f,entryHeight,Y(-6.5f));
            var entryCurtain=Curtain("Temporary_Red_Curtain",-4,CurtainMode.WhileIlluminated);
            var exit=Curtain("Permanent_Exit_Curtain",4,CurtainMode.Permanent);
            journey.reversibleCurtain=entryCurtain; journey.shortcutCurtain=exit;
            var source=Emitter("OneBeam_TwoUses",new Vector3(-2.3f,Y(0)+2.1f,0));
            var a=Marker("HoldEntrance",stage,new Vector3(0,Y(-4)+1.8f,-4));
            var b=Marker("CarveEnduringExit",stage,new Vector3(0,Y(4)+1.8f,4));
            var console=Console<AimConsole>("Redistribute_Light",new Vector3(-1.8f,Y(0),0));
            console.emitter=source; console.aimPoints=new[]{a,b}; console.prompt="E 改变光的去向"; console.Apply();
            var restore=Console<AimConsoleRelay>("Restore_EntranceLight",new Vector3(-1.8f,Y(-6),-6));restore.console=console;restore.prompt="E 转动共用光镜 / 恢复入口";
            Sign("II   ECHO",new Vector3(2.4f,Y(-5)+1.5f,-5),"同一束光，能留下什么？");
            // A roofless view alcove becomes accessible after the permanent exit is removed.
            Box("ViewBench",stage,new Vector3(-1.9f,Y(5)+.28f,5),new Vector3(1.4f,.55f,.65f),soil);
        }
        static void ThirdStage(Transform parent)
        {
            Stage("03_FoldedLight",parent,new Vector3(-23,37.8f,116.5f),new Vector3(1,0,.04f),.12f,16);
            Object.DestroyImmediate(stage.Find("03_FoldedLight_CarvedFloor").gameObject);
            Deck("Lift_Approach",stage,Vector3.zero,7,-9,.15f,-2.9f,1.6f);
            var source=Emitter("Fixed_Beam_IntoSpace",new Vector3(-2.15f,2.2f,-5.6f));
            var entrance=Portal("A_LightEntrance",new Vector3(-2.15f,2.2f,-2.6f),Vector3.back);
            var exit=Portal("B_MovingExit",new Vector3(2.25f,6,-.8f),Vector3.left);
            entrance.Paired=exit; exit.Paired=entrance; source.transform.rotation=Quaternion.LookRotation(stage.forward);
            var rail=Console<PortalRailConsole>("PortalRail_OnLift",new Vector3(.9f,1.72f,2));
            rail.movingPortal=exit.transform; rail.speed=2; rail.prompt="E 移动导光门：停 / 上升 / 下降";
            rail.docks=new[]{Pose("Dock_Stop",new Vector3(2.25f,6,-.8f),Vector3.left),Pose("Dock_Raise",new Vector3(2.25f,6,1.1f),Vector3.left),Pose("Dock_Lower",new Vector3(2.25f,6,3.1f),Vector3.left)};
            rail.selected=0; rail.ApplyInitialPose();
            var raise=Receiver("Receiver_Raise",new Vector3(-2.7f,6,1.1f),gold);
            var lower=Receiver("Receiver_Lower",new Vector3(-2.7f,6,3.1f),cyan);
            Box("Receiver_Stop_Backboard",stage,new Vector3(-3.25f,6,1.15f),new Vector3(.3f,1.6f,5.6f),white);
            var platform=Box("Lift_Platform",stage,new Vector3(0,1.4f,2.1f),new Vector3(4.2f,.4f,4),soil);
            var rb=platform.AddComponent<Rigidbody>(); rb.isKinematic=true; rb.useGravity=false; rb.interpolation=RigidbodyInterpolation.Interpolate;
            var lift=stage.gameObject.AddComponent<LightDrivenLift>(); lift.platform=platform.transform; lift.platformBody=rb; lift.positionsAreLocal=true;
            lift.bottom=platform.transform.localPosition; lift.top=lift.bottom+Vector3.up*3.1f; lift.speed=1.3f; lift.receiverRaise=raise; lift.receiverLower=lower; journey.lift=lift;
            rail.transform.SetParent(platform.transform,true);
            var recall=Console<PortalConsoleRelay>("Recall_Handle",new Vector3(1.1f,.9f,-1.3f)); recall.rail=rail; recall.prompt="E 调整导光门 / 召回升降台";
            // A visible masonry rise replaces a door: light must transport the player upward.
            Box("High_Landing_Face",stage,new Vector3(0,2.1f,6.1f),new Vector3(7.2f,4.8f,3.8f),white);
            Box("High_Landing_Cap",stage,new Vector3(0,4.52f,6.1f),new Vector3(7.5f,.25f,4),soil);
            var upperRecall=Console<PortalConsoleRelay>("Upper_Recall_Handle",new Vector3(1,4.65f,6));upperRecall.rail=rail;upperRecall.prompt="E 调整导光门 / 召回升降台";
            Deck("Descent_To_Halfway",stage,new Vector3(0,0,0),6.6f,8,16,4.64f,2.75f);
            foreach(float x in new[]{-2.35f,2.35f}) Box("Lift_Guide",stage,new Vector3(x,2.7f,3.8f),new Vector3(.2f,5.5f,.2f),dark);
            Box("Visible_Rail",stage,new Vector3(2.68f,6,1.15f),new Vector3(.12f,.12f,5.4f),soil);
            foreach(float z in new[]{1.1f,3.1f})Box("Receiver_Linkage",stage,new Vector3(-2.7f,3.7f,z),new Vector3(.16f,4.5f,.16f),dark);
            Sign("III   FOLD",new Vector3(1.8f,Y(-5)+1.5f,-5),"光从另一扇窗出现");
        }
        static void Stage(string name,Transform parent,Vector3 position,Vector3 forward,float slope,float length)
        {
            stage=Group(name,parent); stage.position=position; stage.rotation=Quaternion.LookRotation(forward); grade=slope;
            Deck(name+"_CarvedFloor",stage,Vector3.zero,7,-length*.5f,length*.5f,Y(-length*.5f),Y(length*.5f));
            foreach(float side in new[]{-1f,1f})
            {
                var wall=Box("Ruined_Sidewall",stage,new Vector3(side*3.8f,2.2f,0),new Vector3(.8f,4.4f,length),white);
                wall.transform.localRotation=Quaternion.Euler(-Mathf.Atan(slope)*Mathf.Rad2Deg,0,0);
                for(int z=0;z<3;z++) Box("Buttress",stage,new Vector3(side*4.0f,Y((z-1)*4)+1.2f,(z-1)*4),new Vector3(1.2f,2.4f,.8f),soil);
            }
        }
        static float Y(float z) => .14f+z*grade;
        static RedStoneCurtain Curtain(string name,float z,CurtainMode mode)
        {
            var go=Box(name,stage,new Vector3(0,Y(z)+1.95f,z),new Vector3(7,3.9f,.5f),red);
            var curtain=go.AddComponent<RedStoneCurtain>(); curtain.Mode=mode; curtain.PhysicalCollider=go.GetComponent<BoxCollider>(); curtain.OpticalCollider=curtain.PhysicalCollider;
            curtain.Visuals=new[]{go.GetComponent<Renderer>()}; curtain.ContinuousHitSeconds=.5f;
            // One glyph = permanent fracture, two bands = sustained curtain. Geometry stays distinct without color.
            for(int i=0;i<(mode==CurtainMode.Permanent?1:2);i++)
            {
                var rune=Box("Rule_Mark",stage,new Vector3((i-.5f)*.48f,Y(z)+2.8f,z-.3f),new Vector3(.1f,.65f,.025f),gold,false);
                rune.transform.localRotation=Quaternion.Euler(0,0,mode==CurtainMode.Permanent?24:0);
                curtain.Visuals=curtain.Visuals.Concat(new[]{rune.GetComponent<Renderer>()}).ToArray();
            }
            Box("Threshold",stage,new Vector3(0,Y(z)+.03f,z),new Vector3(7,.06f,.8f),soil);
            var signal=Box("Curtain_ResponseGlyph",stage,new Vector3(-3.55f,Y(z)+1.8f,z-.3f),new Vector3(.16f,.5f,.13f),gold,false);
            var response=signal.AddComponent<LightTargetFeedback>();response.target=curtain;response.indicator=signal.GetComponent<Renderer>();
            return curtain;
        }
        static LaserEmitter Emitter(string name,Vector3 pos)
        {
            var root=Group(name,stage); root.localPosition=pos;
            Box("Lens_Base",root,new Vector3(0,-.6f,0),new Vector3(.6f,1,.6f),soil);
            var lens=GameObject.CreatePrimitive(PrimitiveType.Sphere); lens.name="Sun_Lens"; lens.transform.SetParent(root,false); lens.transform.localScale=Vector3.one*.5f; Object.DestroyImmediate(lens.GetComponent<Collider>()); lens.GetComponent<Renderer>().sharedMaterial=gold;
            var emitter=root.gameObject.AddComponent<LaserEmitter>(); emitter.IgnoreRoot=root; emitter.MaxDistance=40; emitter.BeamRadius=.035f; emitter.BeamColor=new Color(3,1.2f,.18f,1); return emitter;
        }
        static T Console<T>(string name,Vector3 feet) where T:TutorialInteractable
        {
            var root=Group(name,stage); root.localPosition=feet+Vector3.up*.85f;
            Box("Console_Pedestal",root,new Vector3(0,-.35f,0),new Vector3(.65f,.7f,.65f),soil);
            Box("Handwheel",root,new Vector3(0,.04f,-.12f),new Vector3(.8f,.13f,.45f),gold,false);
            return root.gameObject.AddComponent<T>();
        }
        static LightPortal Portal(string name,Vector3 pos,Vector3 facing)
        {
            var root=Pose(name,pos,facing); var portal=root.gameObject.AddComponent<LightPortal>(); portal.Aperture=new Vector2(1.3f,1.5f);
            foreach(float x in new[]{-.8f,.8f}) Box("Portal_Jamb",root,new Vector3(x,0,0),new Vector3(.2f,1.9f,.2f),soil);
            foreach(float y in new[]{-.85f,.85f}) Box("Portal_Rim",root,new Vector3(0,y,0),new Vector3(1.6f,.12f,.12f),cyan,false);
            return portal;
        }
        static LightReceiver Receiver(string name,Vector3 pos,Material mat)
        {
            var go=Box(name,stage,pos,new Vector3(.18f,.9f,.85f),mat); var receiver=go.AddComponent<LightReceiver>(); receiver.OpticalCollider=go.GetComponent<BoxCollider>(); receiver.RequiredHitSeconds=.05f; receiver.ReturnGraceSeconds=0;
            var response=go.AddComponent<LightTargetFeedback>(); response.target=receiver; response.indicator=go.GetComponent<Renderer>(); return receiver;
        }
        static Transform Pose(string name,Vector3 pos,Vector3 facing) { var t=Marker(name,stage,pos); t.localRotation=Quaternion.LookRotation(facing); return t; }
        static void Sign(string text,Vector3 pos,string subtitle)
        {
            var root=Group("Inscription",stage); root.localPosition=pos; root.localRotation=Quaternion.Euler(0,180,0);
            Box("Plaque",root,Vector3.zero,new Vector3(1.7f,1,.18f),soil);
            // TextMesh uses a generated atlas from the installed Chinese font; runtime HUD remains the readable control prompt.
            var mesh=Group("Title",root).gameObject.AddComponent<TextMesh>(); mesh.transform.localPosition=new Vector3(0,.14f,-.11f); mesh.transform.localRotation=Quaternion.Euler(0,180,0);
            mesh.text=text; mesh.fontSize=36; mesh.characterSize=.05f; mesh.anchor=TextAnchor.MiddleCenter; mesh.color=Color.white;
        }
        static Transform Group(string name,Transform parent)
        {
            if(parent==null){var old=GameObject.Find(name); if(old)return old.transform;}
            var root=new GameObject(name).transform; root.SetParent(parent,false); return root;
        }
        static Transform Marker(string name,Transform parent,Vector3 pos) { var t=Group(name,parent); t.localPosition=pos; return t; }
        static GameObject Box(string name,Transform parent,Vector3 pos,Vector3 size,Material mat,bool collider=true)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name; g.transform.SetParent(parent,false); g.transform.localPosition=pos; g.transform.localScale=size;
            g.GetComponent<Renderer>().sharedMaterial=mat; if(!collider) Object.DestroyImmediate(g.GetComponent<Collider>()); return g;
        }
        static void Deck(string name,Transform parent,Vector3 pos,float width,float z0,float z1,float y0,float y1)
        {
            float x=width*.5f; var v=new[]{new Vector3(-x,y0,z0),new Vector3(x,y0,z0),new Vector3(-x,y1,z1),new Vector3(x,y1,z1),new Vector3(-x,y0-.35f,z0),new Vector3(x,y0-.35f,z0),new Vector3(-x,y1-.35f,z1),new Vector3(x,y1-.35f,z1)};
            int[] t={0,2,1,1,2,3,4,5,6,5,7,6,0,1,4,1,5,4,2,6,3,3,6,7,0,4,2,2,4,6,1,3,5,3,7,5};
            var mesh=new Mesh{name=name}; mesh.vertices=v; mesh.triangles=t; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            string path="Assets/Objects/LevelGeometry/Tutorial/V8/"+name+".asset"; var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}else AssetDatabase.CreateAsset(mesh,path);
            var g=Group(name,parent).gameObject; g.transform.localPosition=pos; g.AddComponent<MeshFilter>().sharedMesh=mesh; g.AddComponent<MeshRenderer>().sharedMaterial=soil; g.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        static Material Mat(string name,Color color,bool emission=false)
        {
            string path="Assets/Materials/Tutorial/V8/"+name+".mat"; var m=AssetDatabase.LoadAssetAtPath<Material>(path); if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.1f); m.enableInstancing=true; if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*.65f);} return m;
        }
    }
}
