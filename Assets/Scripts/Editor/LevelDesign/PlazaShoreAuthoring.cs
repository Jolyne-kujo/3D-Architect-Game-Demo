using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using Courtyard.Water;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
using UnityEngine.Rendering;

namespace CoastalTemple.Editor
{
    // One-time authoring: the scene and prefab contain normal editable meshes and nested mechanism prefabs.
    public static class PlazaShoreAuthoring
    {
        public const string RootName="01_广场新手关";
        public const string Source="Assets/Objects/Environment/广场.fbx";
        public const string Output="Assets/Objects/LevelGeometry/Plaza20261002";
        public const string Evidence="Documentation/Plaza20261002";
        public const string PrefabPath="Assets/Prefabs/地形建筑/关卡片段/广场_新手关.prefab";
        public const float Enlargement=40;
        public const float SpawnYaw=245;
        public static readonly Vector3 ModelOrigin=new Vector3(-40.6f,-1.7f,267.68f);
        public static Vector3 Map(Vector3 p)=>ModelOrigin+p*Enlargement;
        static Transform devices;
        static readonly List<string> report=new List<string>();
        static void Record(Object o)
        {if(!o)return;EditorUtility.SetDirty(o);if(PrefabUtility.IsPartOfPrefabInstance(o))PrefabUtility.RecordPrefabInstancePropertyModifications(o);}
        static GameObject Group(string name,Transform parent)
        {var g=new GameObject(name);g.transform.SetParent(parent,false);return g;}
        static GameObject Device(string prefab,string name,Vector3 position)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Mechanisms/"+prefab+".prefab");
            if(!asset)throw new InvalidOperationException("Missing mechanism: "+prefab);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,devices);g.name=name;g.transform.position=position;
            foreach(var text in g.GetComponentsInChildren<TextMesh>(true))text.gameObject.SetActive(false);
            return g;
        }
        public static string Build()
        {
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(Application.isPlaying||scene.name!="CoastalTemple")throw new InvalidOperationException("Open CoastalTemple in Edit mode.");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();report.Clear();
            // Source asset and the experiment-scene instance remain untouched.
            foreach(var name in new[]{RootName,"SU_ShoreTutorial","90_Archive_PreSU_Shore_INACTIVE"})
            {var old=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name==name);if(old){report.Add("Removed old shore group: "+name);Undo.DestroyObjectImmediate(old.gameObject);}}
            var shoreRocks=GameObject.Find("12_ShoreTalus_EDIT_ROCKS");
            if(shoreRocks)foreach(Transform t in shoreRocks.transform.Cast<Transform>().ToArray())
            {
                var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)continue;
                var b=rs.Select(r=>r.bounds).Aggregate((a,v)=>{a.Encapsulate(v);return a;});
                if(b.center.z>=169){report.Add("Removed old shore rock: "+t.name);Undo.DestroyObjectImmediate(t.gameObject);}
            }
            var apron=GameObject.Find("13_WeatheredCliffApron_EDIT_ROCKS");
            if(apron)foreach(Transform t in apron.transform.Cast<Transform>().ToArray())
            {var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length>0&&rs[0].bounds.center.z>=174){report.Add("Removed shore apron: "+t.name);Undo.DestroyObjectImmediate(t.gameObject);}}

            var root=new GameObject(RootName);root.transform.SetParent(GameObject.Find("20_Architecture").transform,true);root.transform.position=new Vector3(-68.2f,0,225.84f);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source),root.transform);
            PrefabUtility.UnpackPrefabInstance(model,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
            model.name="01_广场原模型_等比40倍";model.transform.SetPositionAndRotation(ModelOrigin,Quaternion.identity);model.transform.localScale=Vector3.one*Enlargement;
            var filters=model.GetComponentsInChildren<MeshFilter>().ToDictionary(f=>f.name);
            var markers=new[]{29,62,63,64,66,67,68,69}.ToDictionary(n=>n,n=>filters["Mesh"+n].GetComponent<Renderer>().bounds);
            devices=Group("02_机关_可独立复制",root.transform).transform;
            var area=root.AddComponent<MechanismObservationArea>();area.localCenter=root.transform.InverseTransformPoint(new Vector3(-68.2f,2,219.7f));area.localSize=new Vector3(46,10,43);area.yaw=180;
            var guide=root.AddComponent<SceneObjectGuide>();guide.label="广场 · 原模型等比40倍";guide.explanation="原建筑、地面、台阶和倒柱保持FBX布局。蓝色小柱替换为发射器；红色小柱为红受光器；蓝板为固定镜，绿板为可旋转镜。原FBX未修改。";

            LightReceiver Receiver(int index,string name)
            {
                var g=Device("RedLightReceiver",name,markers[index].center);g.transform.localScale=Vector3.one*1.5f;
                var receiver=g.GetComponentInChildren<LightReceiver>();Physics.SyncTransforms();
                g.transform.position+=markers[index].center-receiver.OpticalCollider.bounds.center;Physics.SyncTransforms();
                receiver.Latching=true;receiver.RequiredHitSeconds=.25f;Record(receiver);return receiver;
            }
            var receiverA=Receiver(29,"Receiver_A_直射红光");var receiverB=Receiver(64,"Receiver_B_镜阵红光");
            LaserEmitter Emitter(int index,string name,Vector3 aim)
            {
                var b=markers[index];var g=Device("LaserDevice",name,new Vector3(b.center.x,b.min.y,b.center.z));
                g.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(aim-g.transform.position,Vector3.up));
                var emitter=g.GetComponent<LaserEmitter>();emitter.Origin.rotation=Quaternion.LookRotation(aim-emitter.Origin.position,Vector3.up);
                emitter.MaxDistance=120;emitter.SetChannel(LightColorChannel.None);Record(emitter.Origin);Record(emitter);
                return emitter;
            }
            var opticalY=markers[66].min.y+1.3f;
            var mirrorIndices=new[]{68,62,67,69};
            var points=mirrorIndices.Select(n=>new Vector3(markers[n].center.x,opticalY,markers[n].center.z)).ToArray();
            var emitterA=Emitter(63,"Emitter_A_直射",receiverA.OpticalCollider.bounds.center);
            var emitterB=Emitter(66,"Emitter_B_镜阵",points[0]);
            for(int i=0;i<mirrorIndices.Length;i++)
            {
                bool rotate=i==0||i==2;int n=mirrorIndices[i];
                var g=Device(rotate?"LightMirror_Rotatable":"LightMirror_Fixed",(rotate?"Mirror_Rotatable_":"Mirror_Fixed_")+n,points[i]);
                var mirror=g.GetComponentInChildren<LightMirror>();
                var control=g.GetComponent<LightMirrorConsole>();
                if(rotate)g.transform.position-=Vector3.up*1.3f;
                var before=i==0?emitterB.Origin.position:points[i-1];var after=i==3?receiverB.OpticalCollider.bounds.center:points[i+1];
                var normal=((points[i]-before).normalized-(after-points[i]).normalized).normalized;
                var pivot=rotate?control.pivot:g.transform;pivot.rotation=Quaternion.LookRotation(normal,Vector3.up);
                // Keep both adjustable mirrors deliberately unsolved; one E and two E turns solve the route.
                if(rotate)pivot.Rotate(Vector3.up,i==0?-45:-90,Space.Self);
                mirror.face.transform.localScale=new Vector3(1.8f,1.3f,1);
                Record(pivot);Record(mirror.face.transform);
                report.Add("Replaced Mesh"+n+" with "+(rotate?"rotatable mirror":"fixed mirror")+" at "+points[i]);
            }
            foreach(int index in markers.Keys)Object.DestroyImmediate(filters["Mesh"+index].gameObject);
            // Mesh colliders follow the imported slopes and stairs. Blue water faces stay visual, never solid.
            foreach(var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<Renderer>();var waterSlots=renderer.sharedMaterials.Select((m,i)=>(m,i)).Where(p=>p.m&&p.m.name=="Color_H02").Select(p=>p.i).ToArray();
                Mesh collision=filter.sharedMesh;
                if(waterSlots.Length>0)
                {
                    string path=Output+"/"+filter.name+"_SolidCollision.asset";
                    collision=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if(!collision)
                    {
                        collision=new Mesh{name=filter.name+"_SolidCollision"};collision.vertices=filter.sharedMesh.vertices;
                        var triangles=new List<int>();for(int s=0;s<filter.sharedMesh.subMeshCount;s++)if(!waterSlots.Contains(s))triangles.AddRange(filter.sharedMesh.GetTriangles(s));
                        collision.triangles=triangles.ToArray();collision.RecalculateBounds();AssetDatabase.CreateAsset(collision,path);
                    }
                }
                var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=collision;
            }
            SculptOldShore();ResizeSea();
            var markersGroup=Group("03_出生点和提示",root.transform);
            var spawn=Group("Spawn_海边起点",markersGroup.transform);spawn.transform.position=Map(new Vector3(-.442f,.012f,-.233f));spawn.transform.rotation=Quaternion.Euler(0,SpawnYaw,0);
            var walker=Object.FindFirstObjectByType<WaterCourtyard.CourtyardWalker>();walker.RespawnAt(spawn.transform.position,SpawnYaw);
            var rig=walker.GetComponent<CoastalPlayerCamera>();rig.SetThirdPerson(false);rig.SnapToTarget();Record(walker.transform);Record(walker);Record(rig);
            var journey=Object.FindFirstObjectByType<TutorialJourney>();
            if(journey)
            {
                var lessonMarker=Group("Lesson_广场光路",markersGroup.transform);lessonMarker.transform.position=Map(new Vector3(-.69f,.04f,-1.14f));
                var exit=Group("Exit_神庙阶梯",markersGroup.transform);exit.transform.position=Map(new Vector3(-.61f,.081f,-1.74f));
                var reached=exit.AddComponent<PlayerReachedCondition>();reached.player=walker.transform;reached.destination=exit.transform;reached.halfExtents=new Vector3(3,2,2);
                var lesson=new LessonStation{name="Plaza",title="广场遗迹 · 同色与镜面",marker=lessonMarker.transform,radius=65,completionCondition=reached,completionMessage="抵达广场神庙。红色受光器只接受红光，固定镜与可旋转镜可以共同改变光路。",hints=new[]{"蓝色标记的位置已换成发射器。靠近按 E：黄光、蓝光、红光、关闭。","一侧可直射红色受光器；另一侧需要沿四面镜子反射。绿板对应的镜子可以按 E 转动，蓝板对应固定镜。","操纵时会升高观察光路，继续按 E 操纵同一个机关。WASD / 空格 / Esc 返回第一人称。"}};
                journey.lessons=new[]{lesson}.Concat(journey.lessons.Where(l=>l!=null&&l.marker)).ToArray();
                UnityEventTools.AddStringPersistentListener(receiverA.Activated,journey.NotifyReward,"直射受光器已点亮 · 同色光触发生效");
                UnityEventTools.AddStringPersistentListener(receiverB.Activated,journey.NotifyReward,"镜阵受光器已点亮 · 光已通过四面镜子抵达目标");
                Record(receiverA);Record(receiverB);Record(journey);
            }
            foreach(var component in root.GetComponentsInChildren<Component>(true))if(component)Record(component);
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            // Export without references to the scene's player/HUD; all mechanism references stay within the prefab.
            var portable=Object.Instantiate(root);
            try
            {
                foreach(var c in portable.GetComponentsInChildren<PlayerReachedCondition>()){c.player=null;}
                foreach(var r in portable.GetComponentsInChildren<LightReceiver>())r.Activated=new UnityEngine.Events.UnityEvent();
                PrefabUtility.SaveAsPrefabAsset(portable,PrefabPath);
            }
            finally{Object.DestroyImmediate(portable);}
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            report.Add("Preserved "+model.GetComponentsInChildren<MeshRenderer>().Length+" non-mechanism source meshes at uniform scale 40.");
            report.Add("2 emitters, 2 red receivers, 2 fixed mirrors, 2 rotatable mirrors. All emitters start off.");
            report.Add("Main-map placement: model origin "+ModelOrigin+"; spawn "+spawn.transform.position);
            File.WriteAllLines(Evidence+"/Build.txt",report);Selection.activeGameObject=root;
            if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.Frame(new Bounds(new Vector3(-68,5,223),new Vector3(62,25,92)),false);
            return string.Join("\n",report);
        }
        static void SculptOldShore()
        {
            var terrain=Object.FindFirstObjectByType<Terrain>();const string path=Output+"/PlazaShoreTerrain.asset";
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(path);
            var baseline=AssetDatabase.LoadAssetAtPath<TerrainData>("Assets/Objects/LevelGeometry/RuinCoast29/RuinCoastTerrain.asset");
            if(!data){data=Object.Instantiate(baseline);data.name="PlazaShoreTerrain";AssetDatabase.CreateAsset(data,path);}
            terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;
            int n=data.heightmapResolution;var heights=baseline.GetHeights(0,0,n,n);var origin=terrain.transform.position;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float wx=origin.x+x*data.size.x/(n-1),wz=origin.z+z*data.size.z/(n-1);
                float strength=Mathf.SmoothStep(0,1,Mathf.InverseLerp(174,184,wz))*Mathf.SmoothStep(0,1,Mathf.Min(wx+118,-18-wx)/8);
                if(strength<=0)continue;
                float world=origin.y+heights[z,x]*data.size.y;
                float target=-5;
                // Preserve a compact earth connection from the imported temple back to the existing ascent.
                if(wz<187){float lane=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(2.5f,5.5f,Mathf.Abs(wx+65)));float road=Mathf.Lerp(1.2f,1.52f,Mathf.InverseLerp(174,184,wz));target=Mathf.Lerp(target,road,lane);}
                heights[z,x]=Mathf.Clamp01((Mathf.Lerp(world,target,strength)-origin.y)/data.size.y);
            }
            data.SetHeights(0,0,heights);terrain.Flush();Record(data);Record(terrain);Record(terrain.GetComponent<TerrainCollider>());
        }
        static void ResizeSea()
        {
            var ocean=Object.FindFirstObjectByType<OceanWaves>();if(!ocean)return;
            var water=ocean.water;water.sizeZ=376;Record(water);WaterSurfacePreviewAssets.Refresh(water,true);
            var filter=ocean.farSea.GetComponent<MeshFilter>();const string path=Output+"/FarSeaRing.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=Object.Instantiate(filter.sharedMesh);mesh.name="FarSeaRing_Plaza";var v=mesh.vertices;for(int i=0;i<v.Length;i++){if(Mathf.Abs(v[i].z+88)<.01f)v[i].z=-104;if(Mathf.Abs(v[i].z-256)<.01f)v[i].z=272;}mesh.vertices=v;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);}
            filter.sharedMesh=mesh;Record(filter);
            var buoys=GameObject.Find("BoundaryBuoys_SeamIsLethal");
            if(buoys)foreach(Transform buoy in buoys.transform)
            {
                var p=buoy.position;
                if(Mathf.Abs(p.z-256)<.01f)p.z=272;else if(Mathf.Abs(p.z+88)<.01f)p.z=-104;
                buoy.position=p;Record(buoy);
            }
            report.Add("Near-sea boundary extended to z=272, far-sea ring matched; original 4 m simulation grid retained.");
        }
        public static string Capture()
        {
            var root=GameObject.Find(RootName);if(!root)throw new InvalidOperationException("Build plaza first.");
            var go=new GameObject("TemporaryPlazaCapture");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.farClipPlane=750;camera.nearClipPlane=.05f;camera.fieldOfView=65;camera.clearFlags=CameraClearFlags.Skybox;
            try
            {
                void Shot(string name,Vector3 from,Vector3 to,bool top=false)
                {
                    camera.transform.position=from;camera.transform.LookAt(to);camera.orthographic=top;camera.orthographicSize=50;
                    var rt=RenderTexture.GetTemporary(1600,1200,24);var prior=RenderTexture.active;var tex=new Texture2D(1600,1200,TextureFormat.RGB24,false);
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1200),0,0);tex.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",tex.EncodeToPNG());}
                    finally{camera.targetTexture=null;RenderTexture.active=prior;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
                }
                Shot("Built-Top",new Vector3(-68,130,225),new Vector3(-68,0,225),true);
                Shot("Built-Overview",new Vector3(-137,67,293),new Vector3(-68,3,220));
                var spawn=root.GetComponentsInChildren<Transform>().First(t=>t.name=="Spawn_海边起点");
                Shot("Built-Spawn",spawn.position+Vector3.up*1.62f,spawn.position+Vector3.up*1.62f+spawn.forward*20);
                Shot("Built-Courtyard",Map(new Vector3(-.69f,.15f,-.80f)),Map(new Vector3(-.65f,.1f,-1.65f)));
                return "Saved 4 plaza scene captures.";
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}
