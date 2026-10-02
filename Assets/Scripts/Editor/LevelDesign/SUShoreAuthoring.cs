using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using WaterCourtyard;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    // Editor-only authoring. The saved scene contains editable nested prefab instances.
    public static class SUShoreAuthoring
    {
        const string Kit = "Assets/Prefabs/地形建筑/单件模块/";
        const string Output = "Assets/Prefabs/地形建筑/关卡片段";
        public const float Scale = 2.4f;
        public static Vector3 Map(float x, float z) => new Vector3(-65 + (x - 63.2f) * Scale, .18f, 212 + (z - 82.18f) * Scale);
        static Transform floor, columns, devices;
        static System.Random random;
        static readonly List<Vector3> placedColumns = new List<Vector3>();
        static GameObject Group(string name, Transform parent)
        { var g = new GameObject(name); g.transform.SetParent(parent, false); return g; }
        static GameObject Module(string category, string name, Transform parent, Vector3 p, Vector3 size)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Kit + category + "/" + name + ".prefab");
            if (!asset) throw new Exception("Missing module " + name);
            var g = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            g.transform.position = p; g.transform.localScale = size;
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform); return g;
        }
        static void FloorRect(string name, float x0, float x1, float z0, float z1)
        {
            var parent = Group(name, floor).transform;
            int nx = Mathf.CeilToInt((x1-x0)*Scale/4), nz = Mathf.CeilToInt((z1-z0)*Scale/4);
            float dx=(x1-x0)/nx, dz=(z1-z0)/nz;
            for (int x=0;x<nx;x++) for(int z=0;z<nz;z++)
            {
                if(random.NextDouble()<.18)continue;
                string[] choices={"V2_G04_Floor_4m_Missing_Corner","V2_G03_Floor_4m_Through_Fissure","V2_G05_Floor_4m_Collapse_Pit","V2_G06_Floor_4m_Diamond_Paving"};
                float Range(float lo,float hi)=>Mathf.Lerp(lo,hi,(float)random.NextDouble());
                var p=Map(x0+(x+.5f)*dx,z0+(z+.5f)*dz)+new Vector3(Range(-.48f,.48f),Range(-.31f,-.21f),Range(-.48f,.48f));
                var tile=Module("地板",choices[random.Next(choices.Length)],parent,p,new Vector3(dx*Scale/4*Range(.65f,.95f),1,dz*Scale/4*Range(.7f,.97f)));
                tile.transform.rotation=Quaternion.Euler(Range(-.5f,.5f),90*random.Next(4)+Range(-14,14),Range(-.5f,.5f));
                if((x+z)%3==0)
                {
                    var debris=Module("碎石","V2_G30_Debris_4m_Shattered_Flagstones",parent,p+new Vector3(Range(-1,1),-.03f,Range(-1,1)),new Vector3(.45f,.22f,.45f));
                    debris.transform.rotation=Quaternion.Euler(0,Range(0,360),0);
                }
            }
        }
        static void FallenColumn(Vector3 start,Vector3 end,float heightOffset,bool reverse)
        {
            if(reverse){var temporary=start;start=end;end=temporary;}
            var direction=(end-start).normalized;float length=Vector3.Distance(start,end);
            var g=Module("柱子","V2_A01_Stout_Doric_4m",columns,start+Vector3.up*(.64f+heightOffset),new Vector3(.80f,length/4,.80f));
            g.name="倒柱_"+placedColumns.Count+(reverse?"_上层残柱":"_下层倒柱");
            g.transform.rotation=Quaternion.FromToRotation(Vector3.up,direction);
            foreach(var c in g.GetComponentsInChildren<Collider>())c.enabled=false;
            placedColumns.Add((start+end)*.5f);
        }
        static void Fence(float x0,float z0,float x1,float z1)
        {
            Vector3 a=Map(x0,z0),b=Map(x1,z1);float length=Vector3.Distance(a,b);
            int n=Mathf.Max(1,Mathf.CeilToInt(length/5.5f));
            for(int i=0;i<n;i++)
            {
                Vector3 start=Vector3.Lerp(a,b,(float)i/n),end=Vector3.Lerp(a,b,(float)(i+1)/n);
                FallenColumn(start,end,0,false);FallenColumn(start,end,1.21f,true);
            }
            var contact=Group("倒柱堆_连续碰撞边界",columns);
            contact.transform.position=(a+b)*.5f;contact.transform.rotation=Quaternion.LookRotation((b-a).normalized,Vector3.up);
            var box=contact.AddComponent<BoxCollider>();box.center=new Vector3(0,1.33f,0);box.size=new Vector3(1.12f,2.66f,length+.02f);
        }
        static LaserEmitter Emitter(string name,float x,float z,Vector3 aim)
        {
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/LaserDevice.prefab"),devices);
            g.name=name;g.transform.position=Map(x,z);g.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(aim-g.transform.position,Vector3.up));
            foreach(var t in g.GetComponentsInChildren<TextMesh>())t.gameObject.SetActive(false);
            var laser=g.GetComponent<LaserEmitter>();laser.SetChannel(LightColorChannel.None);laser.MaxDistance=65;
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(laser);
            return laser;
        }
        static LightReceiver Receiver(string name,float x,float z)
        {
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/RedLightReceiver.prefab"),devices);
            g.name=name;g.transform.position=Map(x,z)+Vector3.up*.3f;
            var receiver=g.GetComponentInChildren<LightReceiver>();receiver.Latching=true;receiver.RequiredHitSeconds=.25f;
            Module("柱基","V2_A13_Square_Stepped_Base_2m",g.transform,Map(x,z),Vector3.one*.4f);
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(receiver);
            return receiver;
        }
        public static string Build()
        {
            if(Application.isPlaying)throw new Exception("Stop Play mode before authoring.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.name!="CoastalTemple")throw new Exception("Open CoastalTemple first.");
            Directory.CreateDirectory(Output);AssetDatabase.Refresh();random=new System.Random(26417);
            var old=GameObject.Find("SU_ShoreTutorial");if(old)Undo.DestroyObjectImmediate(old);
            var archive=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="90_Archive_PreSU_Shore_INACTIVE");
            if(!archive)archive=new GameObject("90_Archive_PreSU_Shore_INACTIVE");
            foreach(var path in new[]{"20_Architecture/00_ShoreStart","20_Architecture/02_ShoreRuins_WaterCourt","40_Tutorial/01_BorrowedBridge","40_Tutorial/05_Optional_WaterMirage"})
            {var go=GameObject.Find(path);if(go)go.transform.SetParent(archive.transform,true);}
            archive.SetActive(false);
            var root=new GameObject("SU_ShoreTutorial");root.transform.SetParent(GameObject.Find("20_Architecture").transform,true);
            floor=Group("01_Paving_EDIT_MODULES",root.transform).transform;
            columns=Group("02_FallenRomanColumns_ROUTE_BOUNDARIES",root.transform).transform;
            devices=Group("03_LightPuzzles",root.transform).transform;
            placedColumns.Clear();
            FloorRect("Main flat courtyard",58.6125f,67.5815f,70.2315f,79.3705f);
            FloorRect("Shore entrance",61.2f,65.5f,79.3705f,82.18f);
            FloorRect("Exit to ascent",62.16f,65.32f,68.9f,70.2315f);
            var layout=JObject.Parse(File.ReadAllText("Assets/Objects/LevelGeometry/Tutorial/SU/Layout.json"));
            foreach(var b in layout["bars"])Fence((float)b["a"][0],(float)b["a"][1],(float)b["b"][0],(float)b["b"][1]);
            // Complete the outer route boundary and retain the SU central entrance/exit openings.
            Fence(59.14f,70.23f,59.14f,79.37f);Fence(66.919f,70.23f,66.919f,79.37f);
            Fence(59.14f,79.37f,61.2f,79.37f);Fence(65.5f,79.37f,66.919f,79.37f);
            Fence(61.2f,79.37f,61.2f,82.18f);Fence(65.5f,79.37f,65.5f,82.18f);
            Fence(59.14f,70.05f,62.16f,70.05f);Fence(65.32f,70.05f,66.919f,70.05f);
            Fence(60.725f,72.036f,60.725f,70.23f);
            // The horizontal SU cylinder around the left emitter is attached to the base mesh.
            Fence(60.7249f,73.79545f,62.30895f,73.79545f);
            var right=Receiver("Receiver_A_Direct_Red",66.214f,71.162f);
            var left=Receiver("Receiver_B_Reflection_Red",61.415f,71.083f);
            var emitterA=Emitter("Emitter_A_Direct",66.15f,74.62f,Map(66.214f,71.162f));
            var mirrorAsset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/LightMirror_Rotatable.prefab");
            var positions=new[]{Map(61.276f,76.620f),Map(59.932f,76.620f),Map(59.779f,73.273f),Map(61.506f,73.388f)};
            var emitterB=Emitter("Emitter_B_Reflection",61.47f,74.634f,positions[0]);
            for(int i=0;i<positions.Length;i++)
            {
                var g=(GameObject)PrefabUtility.InstantiatePrefab(mirrorAsset,devices);g.name="Mirror_"+(i+1);g.transform.position=positions[i];
                Vector3 before=i==0?emitterB.transform.position:positions[i-1];Vector3 after=i==3?left.transform.parent.position:positions[i+1];
                Vector3 incoming=(positions[i]-before).normalized, outgoing=(after-positions[i]).normalized;
                var console=g.GetComponent<LightMirrorConsole>();console.pivot.rotation=Quaternion.LookRotation((incoming-outgoing).normalized,Vector3.up);
                // Reflection planes are vertical; all four optical centres share the emitter height.
                var angles=console.pivot.eulerAngles;console.pivot.rotation=Quaternion.Euler(0,angles.y,0);
                if(i==0)console.pivot.Rotate(Vector3.up,-45,Space.Self);
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(console.pivot);
            }
            var curtain=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/RedStoneCurtain.prefab"),devices);
            curtain.name="StoneCurtain_SU";curtain.transform.position=Map(59.913f,71.994f);curtain.transform.localScale=new Vector3(1.102f*Scale/4,1,.276f*Scale/.45f);
            UnityEventTools.AddBoolPersistentListener(left.Activated,curtain.SetActive,false);
            PrefabUtility.RecordPrefabInstancePropertyModifications(left);PrefabUtility.RecordPrefabInstancePropertyModifications(curtain.transform);
            var walker=Object.FindFirstObjectByType<CourtyardWalker>();walker.transform.position=Map(63.2f,81.3f)+Vector3.up*.035f;walker.transform.rotation=Quaternion.Euler(0,180,0);
            walker.water=null;walker.additionalWaters=walker.additionalWaters.Where(w=>w&&w.gameObject.activeInHierarchy).ToArray();EditorUtility.SetDirty(walker);
            var exit=Group("Exit_To_Mountain",root.transform);exit.transform.position=Map(63.7f,69.2f);
            var condition=exit.AddComponent<PlayerReachedCondition>();condition.player=walker.transform;condition.destination=exit.transform;condition.halfExtents=new Vector3(2.8f,2,1.3f);
            var journey=Object.FindFirstObjectByType<TutorialJourney>();
            var lesson=new LessonStation{name="SU start",title="岸边光路 · 同色与反射",marker=root.transform,radius=30,completionCondition=condition,completionMessage="已经穿过岸边遗迹。光会沿镜面改变方向，受光器只识别自己的颜色。",hints=new[]{"靠近发射器按 E 切换颜色。红色受光器需要红光。","镜面可以用 E 转动；沿着实际光束观察下一段会落在哪里。","左侧镜阵需要把光绕过倒柱，送到后面的红色受光器。"}};
            var marker=Group("Lesson centre",root.transform);marker.transform.position=Map(63.2f,75.5f);lesson.marker=marker.transform;
            if(journey.lessons.Length>0)journey.lessons[0]=lesson;else journey.lessons=new[]{lesson};EditorUtility.SetDirty(journey);
            var camera=Object.FindFirstObjectByType<CoastalPlayerCamera>();walker.SendMessage("Awake");if(camera)camera.SnapToTarget();
            foreach(var t in root.GetComponentsInChildren<Transform>())if(PrefabUtility.IsPartOfPrefabInstance(t))PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            PrefabUtility.SaveAsPrefabAsset(root,Output+"/SU_岸边教学关_平地.prefab");
            // Player references are scene-only; the standalone prefab can be reused without them.
            EditorUtility.SetDirty(root);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject=root;if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.Frame(new Bounds(Map(63.2f,75.5f),new Vector3(36,10,34)),false);
            return "Saved shore layout: "+placedColumns.Count+" fallen Roman columns, 2 emitters, 4 mirrors, 2 red receivers, 1 original stone curtain.";
        }
        public static string Capture()
        {
            Directory.CreateDirectory("Documentation/SUTutorial");
            var cameraObject=new GameObject("Temporary authoring capture");var camera=cameraObject.AddComponent<Camera>();
            camera.farClipPlane=650;camera.clearFlags=CameraClearFlags.Skybox;camera.fieldOfView=55;
            try
            {
                void Shot(string name,Vector3 from,Vector3 to,bool top)
                {
                    camera.transform.position=from;camera.transform.LookAt(to);camera.orthographic=top;camera.orthographicSize=18;
                    var rt=RenderTexture.GetTemporary(1600,1200,24);var prior=RenderTexture.active;var tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1200),0,0);tex.Apply();File.WriteAllBytes("Documentation/SUTutorial/"+name+".png",tex.EncodeToPNG());}
                    finally{camera.targetTexture=null;RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
                }
                Shot("Built-Top",Map(63.2f,76)+Vector3.up*48,Map(63.2f,76),true);
                Shot("Built-Overview",new Vector3(-96,28,233),new Vector3(-63,1,194),false);
                Shot("Built-Entrance",Map(63.2f,82.1f)+Vector3.up*2.6f,Map(63.2f,74)+Vector3.up*2,false);
                return "Captured";
            }
            finally{Object.DestroyImmediate(cameraObject);}
        }
    }
}
