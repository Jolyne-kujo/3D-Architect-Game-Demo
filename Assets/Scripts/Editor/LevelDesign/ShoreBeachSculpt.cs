using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoastalTemple.Editor
{
    // One-time editor sculpt; saved TerrainData remains editable with normal Terrain tools.
    public static class ShoreBeachSculpt
    {
        public static string Apply()
        {
            if(Application.isPlaying)throw new System.InvalidOperationException("Stop Play mode first.");
            var terrain=Object.FindFirstObjectByType<Terrain>();
            if(!terrain||terrain.gameObject.scene.name!="CoastalTemple")throw new System.InvalidOperationException("Open CoastalTemple.");
            var data=terrain.terrainData;
            if(AssetDatabase.GetAssetPath(data)!="Assets/Objects/LevelGeometry/Tutorial/SU/ShoreTerrain.asset")throw new System.InvalidOperationException("Expected the shore terrain copy.");
            Undo.RegisterCompleteObjectUndo(data,"Sculpt natural shore beach");
            var origin=terrain.transform.position;int n=data.heightmapResolution;
            var h=data.GetHeights(0,0,n,n);int edited=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++)
            {
                float wx=origin.x+x*data.size.x/(n-1),wz=origin.z+z*data.size.z/(n-1);
                float blend=Region(wx,wz);if(blend<=0)continue;
                float original=origin.y+h[z,x]*data.size.y;
                if(wz<179.5f&&original>1)continue;
                float dx=(wx+65)/37, dz=(wz-188)/36;
                float angle=Mathf.Atan2(dz,dx),radius=Mathf.Sqrt(dx*dx+dz*dz);
                float inside=(1-radius)*34+1.8f*Mathf.Sin(angle*3+.8f)+1.1f*Mathf.Sin(angle*5-1.2f)+.55f*Mathf.Sin(angle*8+.4f);
                // A small eastern inlet breaks the convex outline.
                inside-=3.2f*Mathf.Exp(-((wx+34)*(wx+34)/100+(wz-201)*(wz-201)/100));
                float beach;
                if(inside>=0)beach=Mathf.Lerp(-1.7f,-.04f,Mathf.SmoothStep(0,1,Mathf.Clamp01(inside/11)));
                else beach=Mathf.Lerp(-1.7f,-6,Mathf.SmoothStep(0,1,Mathf.Clamp01(-inside/11)));
                // The authored playable footprint stays flat and at its original elevation.
                float outX=Mathf.Max(-77.5f-wx,0,wx+53),outZ=Mathf.Max(179.5f-wz,0,wz-213.5f);
                float protect=1-Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Sqrt(outX*outX+outZ*outZ)/3));
                beach=Mathf.Lerp(beach,-.04f,protect);
                h[z,x]=Mathf.Lerp(h[z,x],Mathf.Clamp01((beach-origin.y)/data.size.y),blend);edited++;
            }
            data.SetHeights(0,0,h);
            // Sand follows the sculpted shoreline; mountain terrain layers outside this region are untouched.
            int aw=data.alphamapWidth,ah=data.alphamapHeight;
            var alpha=data.GetAlphamaps(0,0,aw,ah);
            for(int z=0;z<ah;z++)for(int x=0;x<aw;x++)
            {
                float wx=origin.x+x*data.size.x/(aw-1),wz=origin.z+z*data.size.z/(ah-1),blend=Region(wx,wz);
                if(wz<179.5f&&terrain.SampleHeight(new Vector3(wx,0,wz))+origin.y>1)continue;
                if(blend<=0)continue;
                for(int layer=0;layer<data.alphamapLayers;layer++)alpha[z,x,layer]=Mathf.Lerp(alpha[z,x,layer],layer==1?1:0,blend);
            }
            data.SetAlphamaps(0,0,alpha);terrain.Flush();EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(terrain.gameObject.scene);
            return "Sculpted "+edited+" shore samples; preserved playable footprint and mountain terrain.";
        }
        static float Region(float x,float z)
        {
            float edge=Mathf.Min(x+119,-11-x,239-z);
            return Mathf.SmoothStep(0,1,Mathf.Clamp01(edge/8))*Mathf.SmoothStep(0,1,Mathf.Clamp01((z-163)/13));
        }
    }
}
