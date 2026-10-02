using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    // These are authoring operations only. All output is saved as normal, editable scene objects.
    public static class RuinCoastAuthoring
    {
        public const string Output = "Assets/Objects/LevelGeometry/RuinCoast29";
        public const string Evidence = "Documentation/RuinCoast29";
        const string Kit = "Assets/Prefabs/地形建筑/单件模块/";
        const string OriginalTerrain = "Assets/Objects/LevelGeometry/Tutorial/SU/ShoreTerrain.asset";
        public static readonly Vector3[] Corridor = {
            new Vector3(-65,1.2f,174), new Vector3(-65,8.2f,160),
            new Vector3(-57.39f,15.3f,147.84f), new Vector3(-42.05f,21.2f,144.70f)
        };
        public static readonly Vector3[] Route = {
            new Vector3(-65,-.06f,178), new Vector3(-65,1.2f,174), new Vector3(-65,8.2f,160),
            new Vector3(-57.39f,15.3f,147.84f), new Vector3(-42.05f,21.2f,144.70f), new Vector3(-22.35f,38,119.58f), new Vector3(-3,40.95f,121)
        };
        static System.Random random;
        static float R(float a,float b) => Mathf.Lerp(a,b,(float)random.NextDouble());
        static float Ease(float a,float b,float x) => Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        public static GameObject Group(string name,Transform parent)
        { var g=new GameObject(name);g.transform.SetParent(parent,false);return g; }
        static Vector3 Enlarge(Vector3 p) => new Vector3(-65+(p.x+65)*1.5f,p.y,194+(p.z-194)*1.5f);
        static Vector3 Map(float x,float z) => Enlarge(SUShoreAuthoring.Map(x,z));
        static void RequireScene()
        {
            if(Application.isPlaying || UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="CoastalTemple")
                throw new InvalidOperationException("Open CoastalTemple in Edit mode first.");
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();
        }
        static GameObject Module(string category,string name,Transform parent,Vector3 p,Vector3 scale,Quaternion rot)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Kit+category+"/"+name+".prefab");
            if(!prefab)throw new InvalidOperationException("Missing ruin module "+name);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            g.transform.SetPositionAndRotation(p,rot);g.transform.localScale=scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);return g;
        }
        static void Wall(Transform parent,Vector3 a,Vector3 b,float h,int seed)
        {
            var delta=b-a;delta.y=0;float length=delta.magnitude;
            string[] types={"V2_W04_Straight_4m_Light_Damage","V2_W06_Straight_4m_Heavy_Collapse","V2_W05_Straight_4m_Breach"};
            int n=Mathf.CeilToInt(length/4.2f);
            for(int i=0;i<n;i++)
            {
                var p=Vector3.Lerp(a,b,(i+.5f)/n);p.y=-.16f;
                float height=h*(.70f+.30f*Mathf.Sin((i+seed)*1.73f)*Mathf.Sin((i+seed)*1.73f));
                Module("墙体",types[(i+seed)%3],parent,p,new Vector3(length/n/4, height/4,1.25f),Quaternion.FromToRotation(Vector3.right,delta.normalized));
                if((i+seed)%3==0)
                    Module("碎石","V2_G28_Debris_4m_Massive_Fallen_Stone",parent,p+Quaternion.AngleAxis(90,Vector3.up)*delta.normalized*1.05f,new Vector3(.8f,.38f,.75f),Quaternion.Euler(0,R(0,360),0));
            }
        }
        public static string Shore()
        {
            RequireScene();random=new System.Random(2926);
            var root=GameObject.Find("SU_ShoreTutorial");if(!root)throw new Exception("Existing shore puzzle is required.");
            if(PrefabUtility.IsAnyPrefabInstanceRoot(root))PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
            var devices=root.transform.Find("03_LightPuzzles");
            string positions="Logs/RuinCoast29/device-positions.json";
            if(!File.Exists(positions))File.WriteAllText(positions,JsonConvert.SerializeObject(devices.Cast<Transform>().ToDictionary(t=>t.name,t=>new[]{t.position.x,t.position.y,t.position.z})));
            var baseline=JObject.Parse(File.ReadAllText(positions));
            foreach(Transform t in devices)
            {
                var q=baseline[t.name];if(q==null)continue;
                t.position=Enlarge(new Vector3((float)q[0],(float)q[1],(float)q[2]));
                PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            }
            foreach(var e in devices.GetComponentsInChildren<LaserEmitter>())
            {e.MaxDistance=110;e.SetChannel(LightColorChannel.None);PrefabUtility.RecordPrefabInstancePropertyModifications(e);}
            foreach(var t in root.transform.Cast<Transform>().ToArray())
                if(t!=devices && t.name!="Exit_To_Mountain" && t.name!="Lesson centre")Undo.DestroyObjectImmediate(t.gameObject);
            var paving=Group("01_BrokenPaving_EDIT_MODULES",root.transform).transform;
            var walls=Group("02_RuinedWalls_NOT_COLUMNS",root.transform).transform;
            var guides=Group("04_SingleFallenColumns_Guidance",root.transform).transform;
            var trim=Group("05_CollapsedRooms_And_Debris",root.transform).transform;
            string[] floors={"V2_G03_Floor_4m_Through_Fissure","V2_G04_Floor_4m_Missing_Corner","V2_G05_Floor_4m_Collapse_Pit"};
            // Loose archaeological paving, with sand visible in the joints and lost corners.
            for(int iz=0;iz<10;iz++)for(int ix=0;ix<10;ix++)
            {
                bool edge=ix==0||ix==9||iz==0||iz==9;
                if(random.NextDouble()<(edge?.4:.16))continue;
                var p=new Vector3(-81.3f+ix*3.65f+R(-.5f,.5f),R(-.21f,-.14f),180+iz*3.55f+R(-.45f,.45f));
                Module("地板",floors[random.Next(3)],paving,p,new Vector3(R(.73f,.92f),.72f,R(.74f,.92f)),Quaternion.Euler(0,random.Next(4)*90+R(-11,11),0));
            }
            for(int i=0;i<8;i++)
            {
                var p=new Vector3(-65+R(-3,3),-.21f,214+i*1.1f);
                Module("碎石","V2_G30_Debris_4m_Shattered_Flagstones",paving,p,new Vector3(.8f,.19f,.8f),Quaternion.Euler(0,R(0,360),0));
            }
            // Perimeter reads as the remnant of rooms, never stacked column fences.
            Wall(walls,new Vector3(-83,0,209),new Vector3(-83,0,188),3.5f,0);
            Wall(walls,new Vector3(-83,0,188),new Vector3(-79,0,178),2.6f,2);
            Wall(walls,new Vector3(-79,0,178),new Vector3(-69,0,178),4.5f,1);
            Wall(walls,new Vector3(-61,0,178),new Vector3(-46,0,178),3.2f,0);
            Wall(walls,new Vector3(-46,0,178),new Vector3(-46,0,190),3.9f,2);
            Wall(walls,new Vector3(-46,0,196),new Vector3(-46,0,207),2.3f,0);
            Wall(walls,new Vector3(-83,0,209),new Vector3(-72,0,213),2.2f,1);
            Wall(walls,new Vector3(-58,0,213),new Vector3(-46,0,207),1.7f,2);
            Module("门洞","V2_W17_Gate_Broken_Lintel",walls,new Vector3(-65,-.1f,214),new Vector3(1.55f,1.45f,1.5f),Quaternion.identity);
            Module("门洞","V2_W16_Gate_4m_Clear_6m_Module",walls,new Vector3(-65,-.15f,178),new Vector3(1.5f,1.5f,1.6f),Quaternion.identity);
            // Two incomplete side chambers explain why these walls existed.
            Wall(trim,new Vector3(-83,0,204),new Vector3(-90,0,204),2.1f,2);
            Wall(trim,new Vector3(-90,0,204),new Vector3(-90,0,194),1.8f,1);
            Wall(trim,new Vector3(-90,0,194),new Vector3(-83,0,194),2.5f,0);
            Wall(trim,new Vector3(-46,0,185),new Vector3(-39,0,185),2.5f,2);
            Wall(trim,new Vector3(-39,0,185),new Vector3(-39,0,195),1.4f,0);
            Wall(trim,new Vector3(-39,0,195),new Vector3(-46,0,195),1.9f,1);
            var bars=JObject.Parse(File.ReadAllText("Assets/Objects/LevelGeometry/Tutorial/SU/Layout.json"))["bars"];
            int index=0;
            foreach(var bar in bars)
            {
                var a=Map((float)bar["a"][0],(float)bar["a"][1]);var b=Map((float)bar["b"][0],(float)bar["b"][1]);
                var d=(b-a).normalized;float length=Vector3.Distance(a,b);
                // A single fallen shaft at each SU guide line. No duplicate layer or invisible perimeter.
                var col=Module("柱子","V2_A01_Stout_Doric_4m",guides,a+Vector3.up*.39f,new Vector3(.68f,length/4,.68f),Quaternion.FromToRotation(Vector3.up,d));
                col.name="单根倒柱_"+(++index);
                if(index%4==0)Module("碎石","V2_G28_Debris_4m_Massive_Fallen_Stone",trim,a-d*.8f+new Vector3(0,-.3f,0),new Vector3(.42f,.23f,.42f),Quaternion.Euler(0,R(0,360),0));
            }
            // Small remains outside the plan ease the transition from architecture to beach.
            foreach(var p in new[]{new Vector3(-88,0,211),new Vector3(-42,0,206),new Vector3(-82,0,217),new Vector3(-52,0,218)})
                Module("碎石","V2_G28_Debris_4m_Massive_Fallen_Stone",trim,p+Vector3.down*.3f,new Vector3(1.2f,.35f,1.1f),Quaternion.Euler(0,R(0,360),0));
            var exit=root.transform.Find("Exit_To_Mountain");exit.position=new Vector3(-65,.1f,176);
            var marker=root.transform.Find("Lesson centre");marker.position=new Vector3(-65,.1f,196);
            var journey=Object.FindFirstObjectByType<TutorialJourney>();if(journey&&journey.lessons.Length>0){journey.lessons[0].radius=44;EditorUtility.SetDirty(journey);}
            var walker=Object.FindFirstObjectByType<WaterCourtyard.CourtyardWalker>();
            walker.transform.SetPositionAndRotation(new Vector3(-65,.25f,220),Quaternion.Euler(0,180,0));walker.SendMessage("Awake");
            var cam=Object.FindFirstObjectByType<CoastalPlayerCamera>();if(cam)cam.SnapToTarget();
            PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/地形建筑/关卡片段/SU_岸边教学关_平地.prefab");
            Save();return "Expanded shore to 36 x 36 m; 12 single fallen guides; broken perimeter walls and rooms.";
        }
        public static void Nearest(float x,float z,out float distance,out float height)
        {
            distance=float.MaxValue;height=0;float weightSum=0;
            var distances=new float[Route.Length-1];var grades=new float[Route.Length-1];
            for(int i=0;i<Route.Length-1;i++)
            {
                var a=Route[i];var b=Route[i+1];var delta=new Vector2(b.x-a.x,b.z-a.z);
                float t=Mathf.Clamp01(Vector2.Dot(new Vector2(x-a.x,z-a.z),delta)/delta.sqrMagnitude);
                float d=Vector2.Distance(new Vector2(x,z),new Vector2(a.x,a.z)+delta*t);
                distance=Mathf.Min(distance,d);distances[i]=d;grades[i]=Mathf.Lerp(a.y,b.y,t);
            }
            // Continuous interpolation across the bisectors of the two bends. A nearest-segment
            // winner alone creates metre-high discontinuities across the inside of each turn.
            for(int i=0;i<distances.Length;i++)
            {float w=Mathf.Exp(-(distances[i]*distances[i]-distance*distance)/18f);weightSum+=w;height+=grades[i]*w;}
            height/=weightSum;
        }
        public static string Landscape()
        {
            RequireScene();var terrain=Object.FindFirstObjectByType<Terrain>();
            var original=AssetDatabase.LoadAssetAtPath<TerrainData>(OriginalTerrain);
            string terrainPath=Output+"/RuinCoastTerrain.asset";
            var data=AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);
            if(!data){AssetDatabase.CopyAsset(OriginalTerrain,terrainPath);data=AssetDatabase.LoadAssetAtPath<TerrainData>(terrainPath);}
            var origin=terrain.transform.position;int n=original.heightmapResolution;var h=original.GetHeights(0,0,n,n);
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float wx=origin.x+x*data.size.x/(n-1),wz=origin.z+z*data.size.z/(n-1),old=origin.y+h[z,x]*data.size.y;
                float region=Ease(155,176,wz)*Ease(-128,-116,wx)*(1-Ease(-18,1,wx));
                float dx=(wx+65)/44,dz=(wz-193)/40,angle=Mathf.Atan2(dz,dx);
                float radius=Mathf.Sqrt(dx*dx+dz*dz)*(1+.023f*Mathf.Sin(3*angle)+.016f*Mathf.Sin(5*angle+.6f));
                float sand=Mathf.Lerp(-.06f,-6, Ease(.65f,1.25f,radius));
                float height=Mathf.Lerp(old,sand,region);
                Nearest(wx,wz,out float d,out float grade);
                float front=Ease(131,144,wz);
                float support=1-Ease(Mathf.Lerp(4.2f,6.6f,front),Mathf.Lerp(10,14,front),d);
                // Compact building bench at the front; the upper ascent remains open for future levels.
                height=Mathf.Lerp(height,grade-.09f+.10f*Ease(2,6,d),support);
                h[z,x]=Mathf.Clamp01((height-origin.y)/data.size.y);
            }
            data.SetHeights(0,0,h);
            // Fill only old tutorial holes underneath the new shore and corridor footprint.
            int hr=data.holesResolution;var holes=original.GetHoles(0,0,hr,hr);
            for(int z=0;z<hr;z++)for(int x=0;x<hr;x++)
            {
                float wx=origin.x+(x+.5f)*data.size.x/hr,wz=origin.z+(z+.5f)*data.size.z/hr;
                Nearest(wx,wz,out float d,out _);if(d<18||(wz>172&&wx<-20))holes[z,x]=true;
            }
            data.SetHoles(0,0,holes);terrain.terrainData=data;terrain.GetComponent<TerrainCollider>().terrainData=data;
            var mountain=GameObject.Find("10_CompactMountain");
            string mapping=Output+"/OriginalMountainMeshes.json";
            if(!File.Exists(mapping))File.WriteAllText(mapping,JsonConvert.SerializeObject(mountain.GetComponentsInChildren<MeshFilter>().ToDictionary(m=>m.name,m=>AssetDatabase.GetAssetPath(m.sharedMesh))));
            var paths=JsonConvert.DeserializeObject<Dictionary<string,string>>(File.ReadAllText(mapping));int changed=0;
            foreach(var filter in mountain.GetComponentsInChildren<MeshFilter>())
            {
                var baseline=AssetDatabase.LoadAssetAtPath<Mesh>(paths[filter.name]);
                var source=RuinRouteMeshCut.Prepare(baseline,filter.transform);var verts=source.vertices;bool edited=false;
                for(int i=0;i<verts.Length;i++)
                {
                    var world=filter.transform.TransformPoint(verts[i]);Nearest(world.x,world.z,out float d,out float grade);
                    float front=Ease(131,144,world.z);
                    float influence=1-Ease(Mathf.Lerp(4.5f,6.9f,front),Mathf.Lerp(10,14,front),d);if(influence<=0||world.y<grade-.45f)continue;
                    world.y=Mathf.Lerp(world.y,grade-.45f,influence);verts[i]=filter.transform.InverseTransformPoint(world);edited=true;
                }
                if(!edited){Object.DestroyImmediate(source);continue;}
                string asset=Output+"/"+filter.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(asset);
                if(!mesh){mesh=Object.Instantiate(source);AssetDatabase.CreateAsset(mesh,asset);}else EditorUtility.CopySerialized(source,mesh);
                mesh.vertices=verts;mesh.RecalculateNormals();SmoothCutNormals(mesh,filter.transform);mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                filter.sharedMesh=mesh;var colliders=filter.GetComponents<MeshCollider>();
                if(colliders.Length>0){colliders[0].sharedMesh=null;colliders[0].sharedMesh=mesh;}
                // Earlier terrain revisions left duplicate colliders carrying the old, uncut mountain.
                foreach(var duplicate in colliders.Skip(1))Undo.DestroyObjectImmediate(duplicate);
                changed++;
                Object.DestroyImmediate(source);
            }
            int aw=data.alphamapWidth,ah=data.alphamapHeight;var alpha=original.GetAlphamaps(0,0,aw,ah);
            for(int z=0;z<ah;z++)for(int x=0;x<aw;x++)
            {
                float wx=origin.x+x*data.size.x/(aw-1),wz=origin.z+z*data.size.z/(ah-1);
                Nearest(wx,wz,out float d,out float level);
                float coast=Ease(158,176,wz)*Ease(-128,-115,wx)*(1-Ease(-18,1,wx));
                float path=(1-Ease(9,18,d))*(1-coast);float weight=Mathf.Max(coast,path);
                if(weight<.001f)continue;
                for(int layer=0;layer<data.alphamapLayers;layer++)
                {float target=layer==1?coast:layer==2?(1-coast)*.72f:layer==0?(1-coast)*.28f:0;alpha[z,x,layer]=Mathf.Lerp(alpha[z,x,layer],target,weight);}
            }
            data.SetAlphamaps(0,0,alpha);data.SetBaseMapDirty();terrain.Flush();EditorUtility.SetDirty(data);
            Physics.SyncTransforms();Save();return "Saved new TerrainData and "+changed+" separately editable mountain mesh copies.";
        }
        static void SmoothCutNormals(Mesh mesh,Transform transform)
        {
            var v=mesh.vertices;var n=mesh.normals;var sums=new Dictionary<Vector3Int,Vector3>();
            Vector3Int Key(Vector3 p)=>new Vector3Int(Mathf.RoundToInt(p.x*40),Mathf.RoundToInt(p.y*40),Mathf.RoundToInt(p.z*40));
            var tris=mesh.triangles;
            for(int i=0;i<tris.Length;i+=3)
            {
                int a=tris[i],b=tris[i+1],c=tris[i+2];var area=Vector3.Cross(v[b]-v[a],v[c]-v[a]);
                foreach(int id in new[]{a,b,c}){var key=Key(v[id]);sums.TryGetValue(key,out var sum);sums[key]=sum+area;}
            }
            for(int i=0;i<v.Length;i++)
            {var w=transform.TransformPoint(v[i]);Nearest(w.x,w.z,out float d,out _);if(d<22&&sums.TryGetValue(Key(v[i]),out var sum)&&sum.sqrMagnitude>1e-8f)n[i]=sum.normalized;}
            mesh.normals=n;
        }
        public static string CorridorAndTalus()
        {
            RequireScene();random=new System.Random(20260929);
            var architecture=GameObject.Find("20_Architecture").transform;
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var archive=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="91_Archive_PreCorridor_INACTIVE");
            if(!archive)archive=new GameObject("91_Archive_PreCorridor_INACTIVE");
            foreach(var path in new[]{"40_Tutorial/02_StoneWorkshop","40_Tutorial/03_FoldedBridge"})
            {var g=GameObject.Find(path);if(g)g.transform.SetParent(archive.transform,true);}
            archive.SetActive(false);
            var journey=Object.FindFirstObjectByType<TutorialJourney>();
            if(journey){journey.lessons=journey.lessons.Where(l=>l!=null&&l.marker&&l.marker.gameObject.activeInHierarchy).ToArray();EditorUtility.SetDirty(journey);}
            var corridorRoot=RuinCorridorBuilder.Build(architecture,Corridor);
            var old=architecture.Find("Mountain_Corridor_Paving");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var paving=Group("Mountain_Corridor_Paving",corridorRoot.transform).transform;
            // The terrain is the smooth movement surface. Thin broken stones visually follow its slope.
            for(int j=0;j<4;j++)
            {
                var a=Route[j];var b=Route[j+1];var forward=(b-a).normalized;
                var right=Vector3.Cross(Vector3.up,forward).normalized;var up=Vector3.Cross(forward,right).normalized;
                float length=Vector3.Distance(a,b);int n=Mathf.CeilToInt(length/3.5f);
                for(int k=0;k<n;k++)for(int side=-1;side<=1;side+=2)
                {
                    if(random.NextDouble()<.19)continue;
                    var p=Vector3.Lerp(a,b,(k+.5f)/n)+right*(side*.70f+R(-.08f,.08f))-Vector3.up*.18f;
                    var g=Module("地板",k%3==0?"V2_G03_Floor_4m_Through_Fissure":"V2_G04_Floor_4m_Missing_Corner",paving,p,new Vector3(.32f,.45f,length/n/4*.94f),Quaternion.LookRotation(forward,up)*Quaternion.Euler(0,R(-5,5),0));
                    foreach(var c in g.GetComponentsInChildren<Collider>())c.enabled=false;
                }
            }
            var geo=GameObject.Find("10_Geography_EDIT_TERRAIN_HERE").transform;
            var rocksOld=geo.Find("12_ShoreTalus_EDIT_ROCKS");if(rocksOld)Undo.DestroyObjectImmediate(rocksOld.gameObject);
            var rocks=Group("12_ShoreTalus_EDIT_ROCKS",geo).transform;
            var terrain=Object.FindFirstObjectByType<Terrain>();
            var rockMat=GameObject.Find("North_AscentBody").GetComponent<Renderer>().sharedMaterials[0];
            var points=new[]{new Vector3(-91,0,179),new Vector3(-87,0,175),new Vector3(-81,0,174),new Vector3(-48,0,169),new Vector3(-42,0,172),new Vector3(-34,0,181),new Vector3(-32,0,184),new Vector3(-98,0,201),new Vector3(-97,0,205),new Vector3(-99,0,197),new Vector3(-32,0,202),new Vector3(-30,0,198),new Vector3(-79,0,160),new Vector3(-81,0,148),new Vector3(-73,0,131),new Vector3(-46,0,110)};
            for(int i=0;i<points.Length;i++)
            {
                string path="Assets/Objects/Environment/PureNature/Rocks/Rock"+(i%6+1).ToString("00")+".fbx";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(!prefab)continue;
                var g=(GameObject)PrefabUtility.InstantiatePrefab(prefab,rocks);g.name="风化崩石_"+(i+1);
                var renderers=g.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                float width=R(4,7.5f);g.transform.localScale=new Vector3(width/bounds.size.x,R(2.3f,4.8f)/bounds.size.y,width/bounds.size.z);
                g.transform.rotation=Quaternion.Euler(R(-8,8),R(0,360),R(-8,8));
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                var p=points[i];float ground=terrain.SampleHeight(p)+terrain.transform.position.y;
                g.transform.position=new Vector3(p.x-bounds.center.x,ground-.7f-bounds.min.y,p.z-bounds.center.z);
                foreach(var r in renderers)r.sharedMaterial=rockMat;
                // Border dressing stays outside the authored walking route.
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
                foreach(var r in renderers)PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            }
            var sun=RenderSettings.sun;if(sun){sun.transform.rotation=Quaternion.Euler(43,222,0);sun.intensity=1.02f;sun.color=new Color(1,.98f,.93f);}
            RenderSettings.ambientSkyColor=new Color(.57f,.65f,.71f);RenderSettings.ambientEquatorColor=new Color(.49f,.50f,.47f);RenderSettings.ambientGroundColor=new Color(.30f,.29f,.26f);
            Save();return "Built mountain buildings, sloped paving and coastal rock transition.";
        }
        public static string Polish()
        {
            RequireScene();var geo=GameObject.Find("10_Geography_EDIT_TERRAIN_HERE").transform;
            var old=geo.Find("13_WeatheredCliffApron_EDIT_ROCKS");if(old)Undo.DestroyObjectImmediate(old.gameObject);
            var root=Group("13_WeatheredCliffApron_EDIT_ROCKS",geo).transform;
            var mat=GameObject.Find("North_AscentBody").GetComponent<Renderer>().sharedMaterials[0];
            var centers=new[]{new Vector3(-88,6,176),new Vector3(-86,10,164),new Vector3(-84,13,150),new Vector3(-43,6,171),new Vector3(-23,10,165)};
            var sizes=new[]{new Vector3(21,17,18),new Vector3(19,23,18),new Vector3(18,26,18),new Vector3(17,18,20),new Vector3(18,24,20)};
            for(int i=0;i<centers.Length;i++)
            {
                string file=i%2==0?"Cliff03":"Cliff02";
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Objects/Environment/PureNature/Cliffs/"+file+".fbx");
                var g=(GameObject)PrefabUtility.InstantiatePrefab(asset,root);g.name="风化岩脚_"+(i+1);
                g.transform.rotation=Quaternion.Euler(0,35+i*67,0);
                var renderers=g.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                // Use world-space bounds after rotation so the apron cannot spill into the six-metre route.
                g.transform.localScale=Vector3.one;
                var meshes=g.GetComponentsInChildren<MeshFilter>();
                var meshRoot=new GameObject("Proportions").transform;meshRoot.SetParent(root,false);
                g.transform.SetParent(meshRoot,true);
                meshRoot.localScale=new Vector3(sizes[i].x/bounds.size.x,sizes[i].y/bounds.size.y,sizes[i].z/bounds.size.z);
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                meshRoot.position=centers[i]-bounds.center;meshRoot.name=g.name;
                foreach(var r in renderers){r.sharedMaterials=r.sharedMaterials.Select(_=>mat).ToArray();PrefabUtility.RecordPrefabInstancePropertyModifications(r);}
                foreach(var f in meshes){var c=f.gameObject.GetComponent<MeshCollider>();if(!c)c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;}
                PrefabUtility.RecordPrefabInstancePropertyModifications(g.transform);
            }
            string skyPath="Assets/Materials/Environment/RuinCoastSky.mat";
            var sky=AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if(!sky){sky=new Material(RenderSettings.skybox);AssetDatabase.CreateAsset(sky,skyPath);}
            sky.SetColor("_SkyTint",new Color(.5f,.52f,.54f));sky.SetColor("_GroundColor",new Color(.34f,.39f,.41f));sky.SetFloat("_Exposure",.85f);
            RenderSettings.skybox=sky;EditorUtility.SetDirty(sky);
            RenderSettings.ambientSkyColor=new Color(.66f,.70f,.74f);RenderSettings.ambientEquatorColor=new Color(.58f,.60f,.59f);RenderSettings.ambientGroundColor=new Color(.37f,.35f,.31f);
            Physics.SyncTransforms();Save();return "Added five weathered cliff aprons and a separate scene sky material.";
        }
        public static void Save()
        {AssetDatabase.SaveAssets();var s=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(s);EditorSceneManager.SaveScene(s);}
        public static string Capture()
        {
            Directory.CreateDirectory(Evidence);var go=new GameObject("Temporary_Ruin29_Camera");var camera=go.AddComponent<Camera>();camera.farClipPlane=750;camera.fieldOfView=55;
            var rendering=go.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            rendering.renderPostProcessing=true;rendering.volumeLayerMask=~0;
            rendering.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
            try
            {
                void Shot(string name,Vector3 from,Vector3 to,bool ortho=false,float size=70)
                {
                    camera.transform.position=from;camera.transform.LookAt(to);camera.orthographic=ortho;camera.orthographicSize=size;
                    var rt=RenderTexture.GetTemporary(1700,1100,24);var before=RenderTexture.active;var tex=new Texture2D(1700,1100,TextureFormat.RGB24,false);
                    try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1700,1100),0,0);tex.Apply();File.WriteAllBytes(Evidence+"/"+name+".png",tex.EncodeToPNG());}
                    finally{camera.targetTexture=null;RenderTexture.active=before;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(tex);}
                }
                Shot("01-CoastalComposition",new Vector3(-126,84,267),new Vector3(-40,24,154));
                Shot("02-ShoreCourtyard",new Vector3(-104,27,234),new Vector3(-64,1,195));
                Shot("03-PlayerArrival",new Vector3(-65,2.9f,225),new Vector3(-65,7,180));
                Shot("04-MountainCorridor",new Vector3(-102,52,207),new Vector3(-53,12,157));
                Shot("05-Plan",new Vector3(-45,200,164),new Vector3(-45,0,164),true,72);
                Shot("06-CorridorEye",new Vector3(-65,5.6f,169),new Vector3(-64,9,157));
                return "Saved 6 native Unity views.";
            }finally{Object.DestroyImmediate(go);}
        }
    }
}
