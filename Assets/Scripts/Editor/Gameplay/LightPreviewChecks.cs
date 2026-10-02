using System.Collections.Generic;
using System.Linq;
using CoastalTemple.LightPuzzles;
using UnityEngine;

namespace CoastalTemple.Editor
{
    public static class LightPreviewChecks
    {
        public static string Run()
        {
            var root = new GameObject("Read-only preview fixture");
            root.transform.position = new Vector3(16500,500,16500);
            try
            {
                GameObject At(string name,Vector3 p)
                { var go=new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=p;return go; }
                var source=At("Disabled source",Vector3.zero).AddComponent<LaserEmitter>();
                source.RenderBeam=false;source.SetChannel(LightColorChannel.None);
                var mirror=At("Mirror",new Vector3(0,0,4)).AddComponent<LightMirror>();
                mirror.face=mirror.GetComponent<BoxCollider>();mirror.face.size=new Vector3(2,2,.05f);
                mirror.transform.localRotation=Quaternion.Euler(0,-45,0);
                var receiver=At("Latching receiver",new Vector3(4,0,4)).AddComponent<LightReceiver>();
                receiver.Latching=true;receiver.RequiredHitSeconds=0;
                var path=new List<BeamSegment>();var reached=new List<LightTarget>();int events=0;
                receiver.Activated.AddListener(()=>events++);
                var preview=typeof(LightPuzzleWorld).GetMethod("Preview");
                Physics.SyncTransforms();
                preview?.Invoke(null,new object[]{source,path,new LightTarget[]{receiver},new[]{mirror},new LightPortal[0],reached});
                if(path.Count!=2||reached.Count!=1||reached[0]!=receiver)
                    return "FAIL disabled emitter preview must reflect and reach receiver without enabling the source";
                if(source.Powered||source.Channel!=LightColorChannel.None||source.SegmentCount!=0||receiver.IsActive||receiver.IsIlluminated||events!=0)
                    return "FAIL preview mutated gameplay state";
                var wall=At("Blocker",new Vector3(2,0,4)).AddComponent<BoxCollider>();Physics.SyncTransforms();
                preview.Invoke(null,new object[]{source,path,new LightTarget[]{receiver},new[]{mirror},new LightPortal[0],reached});
                if(reached.Count!=0||path.Count!=2||path.Last().End.x-root.transform.position.x>1.51f)
                    return "FAIL preview ignored physical occlusion";
                return "PASS disabled source reflection; PASS no activation or source mutation; PASS physical occlusion";
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
