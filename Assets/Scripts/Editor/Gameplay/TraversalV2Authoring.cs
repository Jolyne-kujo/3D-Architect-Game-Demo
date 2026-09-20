using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class TraversalV2Authoring
    {
        public const string StairPrefab="Assets/Prefabs/Architecture/Staircase.prefab";
        const string RampAsset="Assets/Objects/Mechanisms/StaircaseRamp.asset";
        public static void BuildStairPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(StairPrefab));Directory.CreateDirectory(Path.GetDirectoryName(RampAsset));AssetDatabase.Refresh();
            var root=new GameObject("Staircase");
            try
            {
                var marker=root.AddComponent<CourtyardStaircase>();
                var art=new GameObject("Editable visual steps");art.transform.SetParent(root.transform,false);
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mechanisms/Ivory.mat");
                for(int i=0;i<14;i++)Box(art.transform,"Step "+(i+1),new Vector3(0,(i+1)*.15f,(i+.5f)*.55f),new Vector3(2.2f,(i+1)*.3f,.55f),material,false);
                Box(root.transform,"Top landing",new Vector3(0,4.1f,8.65f),new Vector3(2.2f,.2f,1.9f),material,true);
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(RampAsset);
                if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,RampAsset);}else mesh.Clear();
                mesh.name="Staircase smooth collision ramp";
                var profile=new[]{new Vector2(0,0),new Vector2(.3f,0),new Vector2(4.2f,7.15f),new Vector2(4.2f,7.7f),new Vector2(0,7.7f)};
                int n=profile.Length;var vertices=new Vector3[n*2];var triangles=new List<int>();
                for(int i=0;i<n;i++){vertices[i]=new Vector3(-1.1f,profile[i].x,profile[i].y);vertices[i+n]=new Vector3(1.1f,profile[i].x,profile[i].y);}
                for(int i=1;i<n-1;i++){triangles.AddRange(new[]{0,i+1,i,n,n+i,n+i+1});}
                for(int i=0;i<n;i++){int j=(i+1)%n;triangles.AddRange(new[]{i,j,j+n,i,j+n,i+n});}
                mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
                var ramp=new GameObject("Smooth walk collision - keep with stairs");ramp.transform.SetParent(root.transform,false);ramp.AddComponent<MeshCollider>().sharedMesh=mesh;
                PrefabUtility.SaveAsPrefabAsset(root,StairPrefab);AssetDatabase.SaveAssets();
            }
            finally{Object.DestroyImmediate(root);}
        }
        static void Box(Transform parent,string name,Vector3 p,Vector3 size,Material material,bool collision)
        {
            var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);box.transform.localPosition=p;box.transform.localScale=size;
            box.GetComponent<Renderer>().sharedMaterial=material;if(!collision)Object.DestroyImmediate(box.GetComponent<Collider>());
        }
        public static GameObject PlaceStair(Transform parent,Vector3 position,Quaternion rotation,Vector3 scale)
        {
            var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(StairPrefab));
            obj.transform.SetParent(parent,true);obj.transform.SetPositionAndRotation(position,rotation);obj.transform.localScale=scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(obj.transform);return obj;
        }
        public static void UpgradePlayers(Scene scene)
        {
            foreach(var root in scene.GetRootGameObjects())foreach(var walker in root.GetComponentsInChildren<CourtyardWalker>(true))ConfigurePlayer(walker);
        }
        static void ConfigurePlayer(CourtyardWalker walker)
        {
            if(!walker.GetComponent<CourtyardSurfaceSwimmer>())walker.gameObject.AddComponent<CourtyardSurfaceSwimmer>();
            var climb=walker.GetComponent<CourtyardLedgeClimb>();if(!climb)climb=walker.gameObject.AddComponent<CourtyardLedgeClimb>();
            climb.minimumLedgeHeight=1.05f;climb.maximumWaterFreeboard=.65f;
            EditorUtility.SetDirty(climb);EditorUtility.SetDirty(walker.gameObject);
            PrefabUtility.RecordPrefabInstancePropertyModifications(climb);
        }
        public static void Apply()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play first.");
            BuildStairPrefab();RedBotAuthoring.BuildController();
            const string playerPath="Assets/Prefabs/Player/ExplorerThirdPerson.prefab";
            var player=PrefabUtility.LoadPrefabContents(playerPath);
            try{foreach(var w in player.GetComponentsInChildren<CourtyardWalker>(true))ConfigurePlayer(w);PrefabUtility.SaveAsPrefabAsset(player,playerPath);}
            finally{PrefabUtility.UnloadPrefabContents(player);}
            var main=SceneManager.GetActiveScene();if(main.path!="Assets/Scenes/CoastalTemple.unity")throw new InvalidOperationException("Open CoastalTemple first.");
            Directory.CreateDirectory("Logs/TraversalBackup");if(!File.Exists("Logs/TraversalBackup/CoastalTemple_BeforeStairPrefab.unity"))File.Copy(main.path,"Logs/TraversalBackup/CoastalTemple_BeforeStairPrefab.unity");
            var stairs=main.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).Where(t=>System.Text.RegularExpressions.Regex.IsMatch(t.name,@"^Stair \d+$")).ToArray();
            if(stairs.Length==14)
            {
                var parent=stairs[0].parent;
                PlaceStair(parent,new Vector3(-54.65f,-4.5f,181.7f),Quaternion.Euler(0,180,0),Vector3.one).name="Courtyard staircase - reusable";
                foreach(var stair in stairs)Object.DestroyImmediate(stair.gameObject);
            }
            UpgradePlayers(main);EditorSceneManager.MarkSceneDirty(main);EditorSceneManager.SaveScene(main);
            var showroom=EditorSceneManager.OpenScene(MechanismPlaygroundAuthoring.ScenePath,OpenSceneMode.Additive);
            var old=showroom.GetRootGameObjects().Where(r=>r.name.StartsWith("Pool stair ")).ToArray();
            if(old.Length==8)
            {
                var stair=PlaceStair(null,new Vector3(-24,-4,11.71f),Quaternion.Euler(0,180,0),new Vector3(3/2.2f,4/4.2f,5/7.7f));
                SceneManager.MoveGameObjectToScene(stair,showroom);stair.name="Pool staircase - reusable";
                foreach(var step in old)Object.DestroyImmediate(step);
            }
            UpgradePlayers(showroom);EditorSceneManager.MarkSceneDirty(showroom);EditorSceneManager.SaveScene(showroom);EditorSceneManager.CloseScene(showroom,true);
            AssetDatabase.SaveAssets();
        }
    }
}
