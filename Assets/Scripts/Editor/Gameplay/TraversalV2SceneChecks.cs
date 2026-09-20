using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using WaterCourtyard;
using Courtyard.Water;
using CoastalTemple.Player;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    // Exercise the authored main map and its real player, not a substitute test courtyard.
    public static class TraversalV2SceneChecks
    {
        const string Output="Documentation/TraversalV2";
        public static string Run()
        {
            if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="CoastalTemple")throw new InvalidOperationException("Play CoastalTemple first.");
            var w=Object.FindFirstObjectByType<CourtyardWalker>();var saved=w.transform.position;float yaw=w.LookYaw;bool enabled=w.enabled;
            var driver=w.GetComponentInChildren<RiggedPlayerAnimation>();var a=driver.animator;var swimmer=w.GetComponent<CourtyardSurfaceSwimmer>();
            var anchor=w.GetComponentInChildren<PlayerBodyAnchor>();var head=a.GetBoneTransform(HumanBodyBones.Head);
            var camera=new GameObject("Traversal proof camera").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=48;
            var simulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var floaters=Object.FindObjectsByType<BuoyantBody>(FindObjectsSortMode.None);var lines=new List<string>();Directory.CreateDirectory(Output);
            void Pose(){driver.SendMessage("Update");a.Update(.02f);anchor.SnapToWalker();swimmer.SamplePose(.02f);}
            void Step(Vector2 input){Physics.SyncTransforms();foreach(var f in floaters)f.SendMessage("FixedUpdate");Physics.Simulate(.02f);w.SimulateMovement(input,false,false,0,.02f);Pose();}
            void Check(bool pass,string text)=>lines.Add((pass?"PASS ":"FAIL ")+text);
            try
            {
                w.enabled=false;
                var overview=Object.FindFirstObjectByType<CoastalWalkthrough>().viewpoints[0];
                Capture(camera,"01-MainMap",overview.position,overview.position+overview.forward*100);
                Physics.SyncTransforms();
                if(!Physics.Raycast(new Vector3(-54.65f,-3.7f,182.2f),Vector3.down,out var bottom,2,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Pool bottom missing.");
                w.RespawnAt(bottom.point+Vector3.up*.025f,180);Step(Vector2.zero);
                bool startedSwimming=w.Swimming&&!w.Grounded,climbed=false,up=false;int stairFrames=0;
                for(int i=0;i<420;i++)
                {
                    Step(Vector2.up);climbed|=w.Climbing;up|=a.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");if(w.StairDirection==1)stairFrames++;
                    if(i==160)Capture(camera,"02-MainPoolStairs",w.transform.position+new Vector3(3,2.4f,-3),w.transform.position+Vector3.up*.8f);
                    if(w.transform.position.z<173&&w.transform.position.y>-.05f)break;
                }
                Check(startedSwimming&&up&&!climbed&&w.transform.position.z<173&&w.transform.position.y>-.05f,"main pool stairs: deep start swimming="+startedSwimming+", normal locomotion="+up+", mantle="+climbed+", stair frames="+stairFrames+", exit="+w.transform.position);
                w.RespawnAt(new Vector3(-49,-2,179),0);for(int i=0;i<160;i++)Step(Vector2.zero);
                float idle=head.position.y-w.WaterSurface;
                Capture(camera,"03-TreadingWater",w.transform.position+new Vector3(3,2.7f,2),w.transform.position+Vector3.up*1.25f);
                float moving=100,transition=100;bool continuous=true;
                for(int i=0;i<190;i++){Step(Vector2.up);continuous&=w.Swimming;transition=Mathf.Min(transition,head.position.y-w.WaterSurface);if(i>90)moving=Mathf.Min(moving,head.position.y-w.WaterSurface);}
                Check(idle>.08f&&moving>.07f&&transition>0&&continuous,"main pool swimming: idle head="+idle.ToString("F3")+"m, moving min="+moving.ToString("F3")+"m, transition min="+transition.ToString("F3")+"m, continuous swim="+continuous);
                Capture(camera,"04-SurfaceSwimming",w.transform.position+new Vector3(3,2.3f,2),w.transform.position+Vector3.up*.6f);
                var lift=floaters.First(f=>f.name=="Guided buoyant lift");Vector3 p=lift.transform.position;
                w.water.Sample(p,out float surface,out _,out _);w.RespawnAt(new Vector3(p.x,surface-1.4f,p.z+3.5f),180);
                bool grabbed=false,native=false,shot=false;
                for(int i=0;i<320;i++)
                {
                    Step(grabbed?Vector2.zero:Vector2.up);grabbed|=w.Climbing;
                    if(w.Climbing){native|=a.GetCurrentAnimatorStateInfo(0).IsName("Ledge Climb");if(!shot&&w.Climber.Progress>.5f){Capture(camera,"05-MainPoolGrab",p+new Vector3(3,1.8f,4),p+new Vector3(0,.7f,.7f));shot=true;}}
                    if(grabbed&&!w.Climbing&&w.Grounded)break;
                }
                Check(grabbed&&native&&w.Grounded&&!w.Swimming,"main pool existing buoyant lift without Space: grabbed="+grabbed+", native clip="+native+", stood="+w.Grounded+", feet="+w.transform.position);
                for(int i=0;i<20;i++)Step(Vector2.zero);
                Capture(camera,"06-OnMainPoolLift",p+new Vector3(3,2.5f,4),w.transform.position+Vector3.up*.8f);
            }
            finally{w.RespawnAt(saved,yaw);w.enabled=enabled;Physics.simulationMode=simulation;Object.DestroyImmediate(camera.gameObject);}
            var report=string.Join("\n",lines);File.WriteAllText(Output+"/MainSceneChecks.txt",report);return report;
        }
        public static string RunShowroom()
        {
            if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MechanismPlayground")throw new InvalidOperationException("Play MechanismPlayground first.");
            var w=Object.FindFirstObjectByType<CourtyardWalker>();var saved=w.transform.position;float yaw=w.LookYaw;bool enabled=w.enabled;
            var driver=w.GetComponentInChildren<RiggedPlayerAnimation>();var a=driver.animator;var swimmer=w.GetComponent<CourtyardSurfaceSwimmer>();
            var simulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            try
            {
                w.enabled=false;Physics.SyncTransforms();
                if(!Physics.Raycast(new Vector3(-24,-3.3f,12.3f),Vector3.down,out var bottom,3,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))throw new InvalidOperationException("Showroom pool bottom missing.");
                w.RespawnAt(bottom.point+Vector3.up*.025f,180);bool climb=false,up=false,down=false;
                void Step(){Physics.SyncTransforms();Physics.Simulate(.02f);w.SimulateMovement(Vector2.up,false,false,0,.02f);driver.SendMessage("Update");a.Update(.02f);swimmer.SamplePose(.02f);climb|=w.Climbing;}
                for(int i=0;i<430;i++){Step();up|=a.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");if(w.transform.position.z<6.3f&&w.transform.position.y>-.05f)break;}
                bool exited=w.transform.position.z<6.3f&&w.transform.position.y>-.05f;
                w.ApplyLook(new Vector2(180,0));for(int i=0;i<250;i++){Step();down|=a.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");if(w.transform.position.z>10.5f)break;}
                string result=(exited&&up&&down&&!climb?"PASS ":"FAIL ")+"actual scaled showroom ramp stairs: exited="+exited+", up clip="+up+", down clip="+down+", mantle="+climb+", end="+w.transform.position;
                File.WriteAllText(Output+"/ShowroomStairChecks.txt",result);return result;
            }
            finally{w.RespawnAt(saved,yaw);w.enabled=enabled;Physics.simulationMode=simulation;}
        }
        static void Capture(Camera camera,string name,Vector3 from,Vector3 at)
        {
            // Reuse the pose-baking camera capture, then keep this pass's evidence together.
            var method=typeof(WaterExitVisualChecks).GetMethod("Capture",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);
            method.Invoke(null,new object[]{camera,"TraversalV2-"+name,from,at});
            string source="Documentation/WaterExit/TraversalV2-"+name+".png";
            File.Copy(source,Output+"/"+name+".png",true);File.Delete(source);
        }
    }
}
