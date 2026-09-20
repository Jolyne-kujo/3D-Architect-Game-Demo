using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CoastalTemple.Mechanisms;
using CoastalTemple.Player;
using Courtyard.Water;
using UnityEditor;
using UnityEngine;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class WaterTraversalChecks
    {
        const string Output="Documentation/WaterTraversal";
        public static string Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
            var w=Object.FindFirstObjectByType<CourtyardWalker>();var saved=w.transform.position;float yaw=w.LookYaw;bool enabled=w.enabled;
            var a=w.GetComponentInChildren<Animator>();var driver=w.GetComponentInChildren<RiggedPlayerAnimation>();
            var update=typeof(RiggedPlayerAnimation).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            var swimmer=w.GetComponent<CourtyardSurfaceSwimmer>();var anchor=w.GetComponentInChildren<PlayerBodyAnchor>();
            var lines=new List<string>();Directory.CreateDirectory(Output);
            string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            void Check(bool ok,string message)=>lines.Add((ok?"PASS ":"FAIL ")+message);
            void Step(Vector2 input,bool run=false,bool jump=false,float swim=0,float dt=.02f,bool pose=true)
            {
                Physics.SyncTransforms();w.SimulateMovement(input,run,jump,swim,dt);
                if(pose){update.Invoke(driver,null);a.Update(dt);anchor.SnapToWalker();swimmer.SamplePose(dt);}
            }
            try
            {
                w.enabled=false;
                foreach(var water in Object.FindObjectsByType<WaterVolume>(FindObjectsSortMode.None))
                {
                    water.ResetWater();var preview=water.CreateInitialSurfaceMesh();var runtime=water.GetComponent<MeshFilter>().sharedMesh;
                    var previewData=new List<Vector4>();var runtimeData=new List<Vector4>();preview.GetUVs(1,previewData);runtime.GetUVs(1,runtimeData);
                    Check(preview.vertices.SequenceEqual(runtime.vertices)&&preview.triangles.SequenceEqual(runtime.triangles)&&previewData.SequenceEqual(runtimeData),water.name+": editor preview equals actual runtime initial vertices, indices and water-depth data");
                    Object.DestroyImmediate(preview);
                }
                if(scene=="MechanismPlayground")
                {
                    var mirage=Object.FindFirstObjectByType<WaterMirage>();int oldAim=mirage.AimIndex;
                    try
                    {
                        for(int aim=0;aim<mirage.aimAngles.Length;aim++){mirage.SetAimState(aim);if(mirage.IsSolid)break;}
                        Check(mirage.IsSolid,"real blue-light projection is solid");
                        var steps=mirage.stepColliders.OrderBy(c=>c.bounds.center.z).ToArray();
                        foreach(int fps in new[]{30,60,144,240,500})foreach(bool run in new[]{false,true})
                        {
                            var first=steps[0].bounds;w.RespawnAt(new Vector3(first.center.x,first.max.y+.025f,first.center.z),0);
                            int frames=0;bool mantle=false;
                            for(;frames<fps*3&&w.transform.position.z<steps[3].bounds.center.z;frames++){Step(Vector2.up,run,dt:1f/fps,pose:false);mantle|=w.Climbing;}
                            Check(frames<fps*1.5f&&w.transform.position.y>steps[3].bounds.max.y-.08f&&!mantle,$"{fps} FPS {(run?"run":"walk")} across four projected slabs without jumping: {frames/(float)fps:F3}s, end={w.transform.position}");
                        }
                        foreach(float angle in new[]{-5f,0f,5f})
                        {
                            var first=steps[0].bounds;w.RespawnAt(new Vector3(first.center.x,first.max.y+.025f,first.center.z+.18f),angle);
                            for(int i=0;i<200;i++)Step(Vector2.zero,dt:1f/240,pose:false);
                            int frames=0;for(;frames<360&&w.transform.position.z<steps[3].bounds.center.z;frames++)Step(Vector2.up,dt:1f/240,pose:false);
                            Check(frames<360&&w.transform.position.y>.5f,$"240 FPS restart at blocked edge, approach {angle} degrees: {frames/240f:F3}s");
                        }
                        w.RespawnAt(new Vector3(steps[0].bounds.center.x,steps[0].bounds.max.y+.025f,steps[0].bounds.center.z),0);
                        for(int i=0;i<30;i++)Step(Vector2.up);
                        Capture("ProjectedSteps",w.transform.position+new Vector3(3,1.4f,-3),w.transform.position+Vector3.up*.7f);
                    }
                    finally{mirage.SetAimState(oldAim);}
                }
                var stairs=Object.FindFirstObjectByType<CourtyardStaircase>();
                w.RespawnAt(stairs.transform.TransformPoint(new Vector3(0,stairs.rise+.03f,stairs.run+.7f)),stairs.transform.eulerAngles.y+180);
                bool floated=false,mantled=false;float lowest=1000;
                for(int i=0;i<180;i++){Step(Vector2.up);floated|=w.Swimming;mantled|=w.Climbing;lowest=Mathf.Min(lowest,w.transform.position.y);}
                for(int i=0;i<60;i++)Step(Vector2.zero);
                float clearance=a.GetBoneTransform(HumanBodyBones.Head).position.y-w.WaterSurface;
                Check(floated&&w.Swimming&&!w.Grounded&&!mantled&&clearance>.1f&&clearance<.5f,$"down stairs enters swimming, head above water={clearance:F3}m, lowest root={lowest:F3}, mantle={mantled}");
                Capture(scene+"-SurfaceSwim",w.transform.position+new Vector3(3,2.6f,-3),w.transform.position+Vector3.up);
                w.ApplyLook(new Vector2(180,0));bool stood=false;int up=0;
                for(;up<350;up++){Step(Vector2.up,true);stood|=w.Grounded&&!w.Swimming;if(stairs.transform.InverseTransformPoint(w.transform.position).z>stairs.run+.45f)break;}
                Check(up<350&&stood&&w.Grounded&&!w.Swimming,$"swim back onto ramp and sprint to dry shore without Space: {up*.02f:F2}s end={w.transform.position}");
                float top=w.transform.position.y;Step(Vector2.zero,jump:true);
                Check(!w.Grounded&&w.VerticalSpeed>4&&w.transform.position.y>top,"jump from shore after swimming still works");
                if(scene=="MechanismPlayground")
                {
                    w.RespawnAt(new Vector3(-18,-3.975f,19),0);
                    for(int i=0;i<440;i++)Step(Vector2.zero);
                    clearance=a.GetBoneTransform(HumanBodyBones.Head).position.y-w.WaterSurface;
                    Check(w.Swimming&&!w.Grounded&&clearance>.1f,"seabed contact cannot cancel deep-water flotation: head="+clearance.ToString("F3"));
                    float surfaceFeet=w.transform.position.y;for(int i=0;i<55;i++)Step(Vector2.zero,swim:-1);
                    Check(w.Swimming&&w.Diving&&w.transform.position.y<surfaceFeet-.5f,"Ctrl dive still descends intentionally");
                    for(int i=0;i<220;i++)Step(Vector2.zero);
                    Check(w.Swimming&&!w.Diving&&Mathf.Abs(w.transform.position.y-surfaceFeet)<.2f,"release dive returns to surface");
                    var p=stairs.transform.TransformPoint(new Vector3(0,stairs.rise-.6f,stairs.run-1.1f));w.RespawnAt(p,stairs.transform.eulerAngles.y);
                    for(int i=0;i<35;i++)Step(Vector2.zero);
                    Check(w.Grounded&&!w.Swimming,"shallow supported feet use standing pose");
                    Step(Vector2.zero,jump:true);Check(w.VerticalSpeed>4&&!w.Grounded,"shallow stair support can jump");
                }
            }
            finally{w.RespawnAt(saved,yaw);w.enabled=enabled;}
            string report=string.Join("\n",lines);File.WriteAllText(Output+"/"+scene+"Checks.txt",report);return report;
        }

        public static string CheckEditorPreview()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Exit Play first.");
            var lines=new List<string>();
            foreach(var water in Object.FindObjectsByType<WaterVolume>(FindObjectsSortMode.None))
            {
                WaterSurfacePreviewAssets.Refresh(water,false);var mesh=water.GetComponent<MeshFilter>().sharedMesh;var generated=water.CreateInitialSurfaceMesh();
                bool matches=mesh&&mesh.vertices.SequenceEqual(generated.vertices)&&mesh.triangles.SequenceEqual(generated.triangles);
                var uv=new List<Vector4>();mesh.GetUVs(1,uv);
                lines.Add((matches&&EditorUtility.IsPersistent(mesh)&&water.Grid==null&&uv.Any(v=>v.x>.1f)?"PASS ":"FAIL ")+water.name+": persisted initial surface matches simulation, vertices="+mesh.vertexCount+", no running simulation in Edit");
                Object.DestroyImmediate(generated);
            }
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="MechanismPlayground")
            {
                var water=Object.FindFirstObjectByType<WaterVolume>();var bounds=water.GetComponent<Renderer>().bounds;
                bool aligned=Mathf.Abs(bounds.min.z-GameObject.Find("Main floor").GetComponent<Collider>().bounds.max.z)<.002f&&
                    Mathf.Abs(bounds.min.x-GameObject.Find("Pool west wall").GetComponent<Collider>().bounds.max.x)<.002f&&
                    Mathf.Abs(bounds.max.x-GameObject.Find("Pool east wall").GetComponent<Collider>().bounds.min.x)<.002f&&
                    Mathf.Abs(bounds.max.z-GameObject.Find("Pool far wall").GetComponent<Collider>().bounds.min.z)<.002f;
                lines.Add((aligned?"PASS ":"FAIL ")+"water mesh reaches all four inner pool edges: "+bounds);
                Capture("EditorPool",new Vector3(-2,13,-1),new Vector3(-17,-.3f,15));
            }
            Directory.CreateDirectory(Output);string report=string.Join("\n",lines);File.WriteAllText(Output+"/"+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name+"PreviewChecks.txt",report);return report;
        }

        static void Capture(string name,Vector3 from,Vector3 at)
        {
            // Existing native renderer also bakes the current skinned pose for offscreen capture.
            var camera=new GameObject("Temporary water check camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=48;
            try
            {
                typeof(StairFootSceneChecks).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{camera,"WaterCheck-"+name,from,at});
                Directory.CreateDirectory(Output);File.Copy("Documentation/StairFootPlacement/WaterCheck-"+name+".png",Output+"/"+name+".png",true);File.Delete("Documentation/StairFootPlacement/WaterCheck-"+name+".png");
            }
            finally{Object.DestroyImmediate(camera.gameObject);}
        }
    }
}
