using System;
using System.Collections.Generic;
using System.IO;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaterCourtyard.Editor
{
    public static class CourtyardBuilder
    {
        const string Root="Assets/WaterCourtyard/Generated";
        static Material stone,white,dark,accent,brass,waterMat,lens,beamMat,tracer;
        static Transform court;
        [MenuItem("Water Court/Rebuild whitebox courtyard")]
        public static void CreateScene()
        {
            Directory.CreateDirectory(Root+"/Materials");Directory.CreateDirectory(Root+"/Prefabs");Directory.CreateDirectory("Assets/WaterCourtyard/Scenes");Directory.CreateDirectory("Assets/WaterSystem/Materials");Directory.CreateDirectory("Assets/WaterSystem/Prefabs");AssetDatabase.Refresh();
            stone=Mat("Limestone",new Color(.73f,.73f,.69f),.22f);
            white=Mat("Chalk",new Color(.86f,.87f,.83f),.22f);
            dark=Mat("Recess",new Color(.075f,.12f,.13f),.35f);
            accent=Mat("Instrument teal",new Color(.16f,.30f,.30f),.45f);
            brass=Mat("Mechanism",new Color(.48f,.40f,.24f),.58f,.6f);
            lens=Mat("Receiver",new Color(.22f,.7f,.58f),.5f);lens.EnableKeyword("_EMISSION");lens.SetColor("_EmissionColor",new Color(.15f,.7f,.45f));
            beamMat=Unlit("Light ray",new Color(2.3f,1.45f,.42f));
            tracer=Unlit("Flow marks",new Color(.63f,.87f,.84f));
            waterMat=AssetDatabase.LoadAssetAtPath<Material>("Assets/WaterSystem/Materials/Water.mat");if(!waterMat){waterMat=new Material(Shader.Find("Courtyard/Heightfield Water"));AssetDatabase.CreateAsset(waterMat,"Assets/WaterSystem/Materials/Water.mat");}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            court=new GameObject("COURTYARD — replace visual children with art").transform;
            // Continuous, collidable surround with an open 12 x 16 metre basin.
            Box("South terrace",new Vector3(0,-.25f,-11),new Vector3(24,.5f,6),stone);
            Box("North terrace",new Vector3(0,-.25f,11),new Vector3(24,.5f,6),stone);
            Box("West terrace",new Vector3(-9,-.25f,0),new Vector3(6,.5f,16),stone);
            Box("East terrace",new Vector3(9,-.25f,0),new Vector3(6,.5f,16),stone);
            Box("Pool foundation",new Vector3(0,-4.7f,0),new Vector3(12.4f,.4f,16.4f),white);
            Box("Pool west wall",new Vector3(-6.15f,-2.25f,0),new Vector3(.3f,4.5f,16.3f),white);
            Box("Pool east wall",new Vector3(6.15f,-2.25f,0),new Vector3(.3f,4.5f,16.3f),white);
            Box("Pool north wall",new Vector3(0,-2.25f,8.15f),new Vector3(12.6f,4.5f,.3f),white);
            Box("Pool south wall",new Vector3(0,-2.25f,-8.15f),new Vector3(12.6f,4.5f,.3f),white);
            // Lip is interrupted at the stair entrance.
            Box("West coping",new Vector3(-6.15f,.10f,0),new Vector3(.42f,.2f,16.5f),stone);
            Box("East coping",new Vector3(6.15f,.10f,0),new Vector3(.42f,.2f,16.5f),stone);
            Box("North coping",new Vector3(0,.10f,8.15f),new Vector3(12.6f,.2f,.42f),stone);
            Box("South coping",new Vector3(1.33f,.10f,-8.15f),new Vector3(9.9f,.2f,.42f),stone);
            for(int y=1;y<=4;y++)
            {
                Box("Depth band east",new Vector3(5.985f,-y,0),new Vector3(.012f,.028f,16),accent,false);
                Box("Depth band north",new Vector3(0,-y,7.985f),new Vector3(12,.028f,.012f),accent,false);
            }
            for(int z=-11;z<=11;z+=2)for(int x=-10;x<=10;x+=2)if(Mathf.Abs(x)>6||Mathf.Abs(z)>8)
                Box("Terrace joint",new Vector3(x,.003f,z),new Vector3(1.97f,.012f,.018f),white,false);
            Box("North boundary",new Vector3(0,2.7f,14),new Vector3(24,5.4f,.55f),stone);
            Box("West boundary",new Vector3(-12,2.7f,0),new Vector3(.55f,5.4f,28),stone);
            Box("East boundary",new Vector3(12,2.7f,0),new Vector3(.55f,5.4f,28),stone);
            Box("South boundary",new Vector3(0,1.1f,-14),new Vector3(24,2.2f,.55f),stone);
            for(int side=-1;side<=1;side+=2)
            {
                for(int z=-10;z<=10;z+=5)
                {
                    Box("Column plinth",new Vector3(side*9.2f,.18f,z),new Vector3(1.25f,.36f,1.25f),white);
                    Cylinder("Column",new Vector3(side*9.2f,2.45f,z),new Vector3(.73f,2.1f,.73f),white);
                    Box("Capital",new Vector3(side*9.2f,4.6f,z),new Vector3(1.22f,.32f,1.22f),white);
                }
                Box("Colonnade beam",new Vector3(side*9.2f,5.05f,0),new Vector3(1.55f,.55f,22),white);
            }
            for(int x=-4;x<=4;x+=4){Box("North niche",new Vector3(x,2.3f,13.68f),new Vector3(2.1f,3.3f,.1f),dark,false);Box("Niche inner",new Vector3(x,2.35f,13.6f),new Vector3(1.75f,2.9f,.08f),white,false);}
            var blocks=new List<WaterBedBlock>();
            for(int i=0;i<15;i++)
            {
                float top=-(i+1)*.3f,z=-8+(i+.5f)*.55f,height=top+4.5f;
                if(height>.01f)Box("Stair "+(i+1),new Vector3(-4.65f,-4.5f+height*.5f,z),new Vector3(2.2f,height,.55f),white);
                Box("Step nosing",new Vector3(-4.65f,top+.006f,z-.255f),new Vector3(2.2f,.012f,.032f),accent,false);
                blocks.Add(new WaterBedBlock{center=new Vector2(-4.65f,z),size=new Vector2(2.2f,.55f),top=top});
            }
            // Stairs and simulation share these same solid step dimensions.
            var waterObject=new GameObject("WaterVolume — reusable simulation");var water=waterObject.AddComponent<WaterVolume>();water.bedBlocks=blocks.ToArray();water.surfaceMaterial=waterMat;water.floorSlope=.02f;
            BuildPoolFloor(water);
            PrefabUtility.SaveAsPrefabAsset(waterObject,Root+"/Prefabs/CourtyardWater.prefab");
            // A separate clean domain prefab does not carry this courtyard's staircase.
            var reusable=new GameObject("WaterVolume");var generic=reusable.AddComponent<WaterVolume>();generic.surfaceMaterial=waterMat;
            PrefabUtility.SaveAsPrefabAsset(reusable,"Assets/WaterSystem/Prefabs/WaterVolume.prefab");UnityEngine.Object.DestroyImmediate(reusable);
            var flow=new GameObject("Surface flow tracers").AddComponent<FlowTracers>();flow.water=water;flow.material=tracer;
            Vector3 drainCenter=new Vector3(2.2f,-4.48f,2.8f);
            Cylinder("Outlet dark throat",drainCenter,new Vector3(1.84f,.015f,1.84f),dark,false);
            var ring=new GameObject("Drain collar",typeof(MeshFilter),typeof(MeshRenderer));ring.transform.position=drainCenter+Vector3.up*.055f;ring.transform.SetParent(court);
            ring.GetComponent<MeshFilter>().sharedMesh=Ring(.94f,1.13f);ring.GetComponent<MeshRenderer>().sharedMaterial=brass;
            var gate=new GameObject("Latched drain gate").transform;gate.position=new Vector3(2.2f,-4.38f,1.92f);gate.SetParent(court);
            for(int i=0;i<9;i++){var bar=Box("Gate grille",gate.position+new Vector3(0,0,.08f+i*.2f),new Vector3(1.62f,.07f,.055f),brass,false);bar.transform.SetParent(gate,true);}
            var floaters=new List<BuoyantBody>();
            var lift=Float("Guided buoyant lift",new Vector3(3.75f,-.30f,-2),new Vector3(2.15f,.55f,2.25f),560,true,water);floaters.Add(lift);
            for(int sign=-1;sign<=1;sign+=2){Box("Lift guide",new Vector3(3.75f+sign*1.2f,-2.1f,-2),new Vector3(.09f,4.7f,.09f),brass);}
            floaters.Add(Float("Floating sample cube",new Vector3(-.7f,-.36f,1.3f),new Vector3(.7f,.7f,.7f),150,false,water));
            floaters.Add(Float("Floating sample block",new Vector3(-2.5f,-.31f,4),new Vector3(1.1f,.45f,.6f),130,false,water));
            var source=Box("Oblique light source",new Vector3(2.2f,3,-6),new Vector3(.45f,.35f,.5f),brass).transform;
            Box("Emitter post",new Vector3(2.2f,1.3f,-7.5f),new Vector3(.2f,2.6f,.2f),accent);
            Box("Emitter arm",new Vector3(2.2f,2.85f,-6.75f),new Vector3(.17f,.17f,1.75f),accent);
            var beam=new GameObject("Physical refracted drain beam").AddComponent<RefractedDrainBeam>();beam.water=water;beam.source=source;
            Vector3 incident=beam.incidentDirection.normalized;
            Vector3 entry=source.position+incident*((water.initialLevel-source.position.y)/incident.y);
            float rz=incident.z/1.333f,ry=-Mathf.Sqrt(1-rz*rz),targetY=-4.31f;
            Vector3 target=entry+new Vector3(0,ry,rz)*((targetY-entry.y)/ry);
            Box("Receiver mount",target-Vector3.up*.09f,new Vector3(1.05f,.18f,1.05f),accent);
            var receptor=Cylinder("Underwater drain receiver",target,new Vector3(.77f,.026f,.77f),lens,false);
            beam.receiver=receptor.transform;beam.receiverLens=receptor.GetComponent<Renderer>();
            var glow=new GameObject("Receiver glow").AddComponent<Light>();glow.transform.position=target+Vector3.up*.4f;glow.type=LightType.Point;glow.range=3;glow.intensity=2;beam.receiverLight=glow;
            var conduit=Box("Signal conduit",new Vector3(2.2f,-4.5f+(2.8f-(target.z+1.9f)*.5f)*.02f+.025f,(target.z+1.9f)*.5f),new Vector3(.06f,.025f,1.9f-target.z),brass,false);conduit.transform.rotation=Quaternion.Euler(1.146f,0,0);
            beam.airLine=Line("Incident ray");beam.waterLine=Line("Refracted ray");
            var console=Box("Drain experiment console",new Vector3(-.2f,.65f,-9.7f),new Vector3(1.35f,1.3f,.7f),accent);
            var cap=Box("Console face",new Vector3(-.2f,1.34f,-9.7f),new Vector3(1.48f,.12f,.8f),brass);
            Label("01 / DRAIN",new Vector3(-.2f,1.42f,-9.7f),Quaternion.Euler(90,0,0),.05f,white);
            Label("WATER COURT",new Vector3(0,3.4f,13.36f),Quaternion.identity,.22f,white);
            var player=new GameObject("Player — walk swim climb");player.transform.position=new Vector3(-4.65f,.07f,-10.8f);
            var cc=player.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.28f;cc.center=Vector3.up*.9f;cc.stepOffset=.35f;cc.skinWidth=.025f;cc.slopeLimit=50;
            var walker=player.AddComponent<CourtyardWalker>();walker.water=water;
            var eye=new GameObject("Eye").transform;eye.SetParent(player.transform,false);eye.localPosition=Vector3.up*1.62f;walker.eye=eye;
            var cameraObject=new GameObject("Main Camera");cameraObject.tag="MainCamera";var camera=cameraObject.AddComponent<Camera>();camera.nearClipPlane=.05f;camera.farClipPlane=130;camera.fieldOfView=58;camera.backgroundColor=new Color(.53f,.66f,.70f);camera.clearFlags=CameraClearFlags.Skybox;
            cameraObject.AddComponent<AudioListener>();var cameraData=cameraObject.AddComponent<UniversalAdditionalCameraData>();cameraData.requiresColorTexture=true;cameraData.requiresDepthTexture=true;cameraData.renderPostProcessing=true;
            var lab=new GameObject("Water Court — experiment controls").AddComponent<CourtyardLab>();lab.water=water;lab.walker=walker;lab.view=camera;lab.beam=beam;lab.drainGate=gate;lab.console=console.transform;lab.floaters=floaters.ToArray();
            camera.transform.position=new Vector3(8,11.5f,-15);camera.transform.LookAt(new Vector3(0,-1.9f,0));
            Lighting();
            EditorSceneManager.SaveScene(scene,"Assets/WaterCourtyard/Scenes/WaterCourtyard.unity");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene("Assets/WaterCourtyard/Scenes/WaterCourtyard.unity",true)};
            PlayerSettings.companyName="Water Court Prototype";PlayerSettings.productName="Water Court";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
            AssetDatabase.SaveAssets();Debug.Log("WATER COURT scene generated successfully");
        }
        static GameObject Box(string name,Vector3 p,Vector3 s,Material m,bool collision=true){var go=Primitive(name,PrimitiveType.Cube,p,s,m,collision);return go;}
        static GameObject Cylinder(string name,Vector3 p,Vector3 s,Material m,bool collision=true)=>Primitive(name,PrimitiveType.Cylinder,p,s,m,collision);
        static GameObject Primitive(string name,PrimitiveType type,Vector3 p,Vector3 s,Material m,bool collision)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(court);go.transform.position=p;go.transform.localScale=s;go.GetComponent<Renderer>().sharedMaterial=m;if(!collision)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        static Material Mat(string name,Color color,float smoothness,float metallic=0)
        {
            string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);m.SetFloat("_Metallic",metallic);return m;
        }
        static Material Unlit(string name,Color color){string path=Root+"/Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",color);m.SetFloat("_Cull",0);return m;}
        static BuoyantBody Float(string name,Vector3 position,Vector3 size,float mass,bool guided,WaterVolume water)
        {
            var root=new GameObject(name);root.transform.position=position;root.transform.SetParent(court);
            var collider=root.AddComponent<BoxCollider>();collider.size=size;
            var body=root.AddComponent<Rigidbody>();body.mass=mass;body.linearDamping=.12f;body.angularDamping=.7f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            var visual=Box("Visual — replace with art",position,size,white,false);visual.transform.SetParent(root.transform,true);
            var trim=Box("Float upper inset",position+Vector3.up*(size.y*.5f+.014f),new Vector3(size.x*.83f,.025f,size.z*.83f),accent,false);trim.transform.SetParent(root.transform,true);
            var buoy=root.AddComponent<BuoyantBody>();buoy.water=water;buoy.displacementSize=size;buoy.guided=guided;return buoy;
        }
        static LineRenderer Line(string name){var l=new GameObject(name).AddComponent<LineRenderer>();l.positionCount=2;l.startWidth=.035f;l.endWidth=.035f;l.sharedMaterial=beamMat;l.numCapVertices=4;l.useWorldSpace=true;l.shadowCastingMode=ShadowCastingMode.Off;return l;}
        static void Label(string text,Vector3 position,Quaternion rotation,float size,Material color)
        {
            var go=new GameObject(text);go.transform.SetParent(court);go.transform.SetPositionAndRotation(position,rotation);var tm=go.AddComponent<TextMesh>();tm.text=text;tm.fontSize=64;tm.characterSize=size;tm.anchor=TextAnchor.MiddleCenter;tm.color=new Color(.45f,.52f,.5f);
        }
        static Mesh Ring(float inner,float outer)
        {
            var v=new Vector3[128];var tris=new int[64*6];for(int i=0;i<64;i++){float a=i*Mathf.PI*2/64;v[i*2]=new Vector3(Mathf.Cos(a)*inner,0,Mathf.Sin(a)*inner);v[i*2+1]=new Vector3(Mathf.Cos(a)*outer,0,Mathf.Sin(a)*outer);int n=(i+1)%64;int t=i*6;tris[t]=i*2;tris[t+1]=n*2;tris[t+2]=i*2+1;tris[t+3]=i*2+1;tris[t+4]=n*2;tris[t+5]=n*2+1;}
            var mesh=new Mesh{name="Drain collar ring"};mesh.vertices=v;mesh.triangles=tris;mesh.RecalculateNormals();string path=Root+"/DrainRing.asset";if(AssetDatabase.LoadAssetAtPath<Mesh>(path))AssetDatabase.DeleteAsset(path);AssetDatabase.CreateAsset(mesh,path);return mesh;
        }
        static void BuildPoolFloor(WaterVolume water)
        {
            const int nx=48,nz=64;var vertices=new Vector3[(nx+1)*(nz+1)];var uv=new Vector2[vertices.Length];var triangles=new int[nx*nz*6];
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){int i=x+z*(nx+1);Vector2 p=new Vector2(x*.25f-6,z*.25f-8);vertices[i]=new Vector3(p.x,water.bottom+Vector2.Distance(p,water.drainPosition)*water.floorSlope,p.y);uv[i]=p;}
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int i=x+z*(nx+1),t=(x+z*nx)*6;triangles[t]=i;triangles[t+1]=i+nx+1;triangles[t+2]=i+1;triangles[t+3]=i+1;triangles[t+4]=i+nx+1;triangles[t+5]=i+nx+2;}
            var mesh=new Mesh{name="Pool floor graded to outlet"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();
            string meshPath=Root+"/GradedFloor.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,meshPath);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/TileGrid.asset");if(!texture){texture=new Texture2D(128,128,TextureFormat.RGB24,true){name="Whitebox tile joints",wrapMode=TextureWrapMode.Repeat,filterMode=FilterMode.Bilinear};var pixels=new Color[128*128];for(int y=0;y<128;y++)for(int x=0;x<128;x++)pixels[x+y*128]=(x<1||y<1)?new Color(.64f,.68f,.66f):Color.white;texture.SetPixels(pixels);texture.Apply();AssetDatabase.CreateAsset(texture,Root+"/TileGrid.asset");}
            var material=Mat("Pool tile",new Color(.84f,.86f,.82f),.27f);material.SetTexture("_BaseMap",texture);
            var floor=new GameObject("Graded pool floor — 2 percent to outlet",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));floor.transform.SetParent(court);floor.GetComponent<MeshFilter>().sharedMesh=mesh;floor.GetComponent<MeshRenderer>().sharedMaterial=material;floor.GetComponent<MeshCollider>().sharedMesh=mesh;
        }
        static void Lighting()
        {
            var sun=new GameObject("Sun").AddComponent<Light>();sun.type=LightType.Directional;sun.transform.rotation=Quaternion.Euler(48,-32,0);sun.color=new Color(1,.92f,.79f);sun.intensity=2.2f;sun.shadows=LightShadows.Soft;sun.shadowBias=.02f;sun.shadowNormalBias=.25f;
            RenderSettings.sun=sun;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.53f,.65f,.72f);RenderSettings.ambientEquatorColor=new Color(.41f,.47f,.46f);RenderSettings.ambientGroundColor=new Color(.25f,.28f,.28f);
            var sky=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/Sky.mat");if(!sky){sky=new Material(Shader.Find("Skybox/Procedural"));AssetDatabase.CreateAsset(sky,Root+"/Materials/Sky.mat");}sky.SetColor("_SkyTint",new Color(.58f,.65f,.66f));sky.SetFloat("_Exposure",.9f);RenderSettings.skybox=sky;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"/CourtVolume.asset");if(!profile){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"/CourtVolume.asset");}
            if(!profile.TryGet<Tonemapping>(out var tone)){tone=profile.Add<Tonemapping>();AssetDatabase.AddObjectToAsset(tone,profile);}tone.mode.Override(TonemappingMode.ACES);
            if(!profile.TryGet<ColorAdjustments>(out var color)){color=profile.Add<ColorAdjustments>();AssetDatabase.AddObjectToAsset(color,profile);}color.postExposure.Override(.35f);color.contrast.Override(8);color.saturation.Override(-12);
            var volume=new GameObject("Courtyard tone").AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=profile;
        }
        public static void BuildWindows()
        {
            CreateScene();Directory.CreateDirectory("Builds/WaterCourt-Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/WaterCourtyard/Scenes/WaterCourtyard.unity"},locationPathName="Builds/WaterCourt-Windows/WaterCourt.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            File.WriteAllText("Logs/WaterBuild.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nWarnings: "+report.summary.totalWarnings+"\nBytes: "+report.summary.totalSize);
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("WaterCourt build failed");
        }
    }
}
