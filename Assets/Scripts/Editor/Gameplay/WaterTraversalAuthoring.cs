using Courtyard.Water;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    // Scoped migration: retain all placed mechanisms and hand-edited terrain.
    public static class WaterTraversalAuthoring
    {
        public static string ApplyCurrentScene()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Exit Play first.");
            var scene=SceneManager.GetActiveScene();
            foreach(var walker in Object.FindObjectsByType<CourtyardWalker>(FindObjectsSortMode.None))
            {
                var cc=walker.GetComponent<CharacterController>();cc.minMoveDistance=0;
                EditorUtility.SetDirty(cc);PrefabUtility.RecordPrefabInstancePropertyModifications(cc);
            }
            if(scene.path==MechanismPlaygroundAuthoring.ScenePath)AlignShowroomPool();
            foreach(var water in Object.FindObjectsByType<WaterVolume>(FindObjectsSortMode.None))WaterSurfacePreviewAssets.Refresh(water,true);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return "Saved water preview and traversal settings: "+scene.path;
        }

        public static void AlignShowroomPool()
        {
            var water=GameObject.Find("Showroom water - use dimensions to resize").GetComponent<WaterVolume>();
            // Front rim ends at Z=6, rear inner wall at Z=26.5. Preserve the drain in world space.
            float shift=water.transform.position.z-16.25f;
            water.drainPosition+=new Vector2(0,shift);
            if(water.bedBlocks!=null)for(int i=0;i<water.bedBlocks.Length;i++)water.bedBlocks[i].center+=new Vector2(0,shift);
            water.transform.position=new Vector3(-16,0,16.25f);water.sizeX=21;water.sizeZ=20.5f;
            var material=GameObject.Find("Pool bottom").GetComponent<Renderer>().sharedMaterial;
            Box("Pool bottom",new Vector3(-16,-4.2f,16.25f),new Vector3(22,.4f,21.5f),material);
            Box("Pool west wall",new Vector3(-26.7f,-2,16.5f),new Vector3(.4f,4,21),material);
            Box("Pool east wall",new Vector3(-5.3f,-2,16.5f),new Vector3(.4f,4,21),material);
            Box("Pool near wall",new Vector3(-16,-2,5.8f),new Vector3(22,4,.4f),material);
            Box("Pool east walk",new Vector3(-2.9f,-.25f,16.5f),new Vector3(5.2f,.5f,21),material);
            Box("Pool rear walk",new Vector3(-16,-.25f,29),new Vector3(30,.5f,4.2f),material);
            EditorUtility.SetDirty(water);EditorUtility.SetDirty(water.transform);
        }

        static void Box(string name,Vector3 position,Vector3 scale,Material material)
        {
            var box=GameObject.Find(name);
            if(!box){box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;}
            box.transform.position=position;box.transform.localScale=scale;box.GetComponent<Renderer>().sharedMaterial=material;
            EditorUtility.SetDirty(box.transform);
        }
    }
}
