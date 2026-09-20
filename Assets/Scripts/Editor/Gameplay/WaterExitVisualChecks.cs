using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Player;
using UnityEngine;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class WaterExitVisualChecks
    {
        const string Output="Documentation/WaterExit";
        public static string Run()
        {
            if(!Application.isPlaying||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="MechanismPlayground")
                throw new InvalidOperationException("Play the MechanismPlayground scene first.");
            var w=Object.FindFirstObjectByType<CourtyardWalker>();var saved=w.transform.position;float savedYaw=w.LookYaw;bool wasEnabled=w.enabled;
            var driver=w.GetComponentInChildren<RiggedPlayerAnimation>();var animator=driver.animator;
            var camera=new GameObject("TemporaryWaterExitObserver").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=48;
            var savedSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            var rails=Object.FindObjectsByType<GuidedBuoyantPlatform>(FindObjectsSortMode.None);
            var lines=new List<string>();Directory.CreateDirectory(Output);
            void Pose(){driver.SendMessage("Update");animator.Update(.02f);w.GetComponentInChildren<PlayerBodyAnchor>().SnapToWalker();}
            void Step(Vector2 input,bool jump=false,float swim=0){Physics.SyncTransforms();foreach(var rail in rails)rail.Simulate(.02f);Physics.Simulate(.02f);w.SimulateMovement(input,false,jump,swim,.02f);Pose();}
            try
            {
                w.enabled=false;
                for(int i=0;i<300;i++){foreach(var settling in rails)settling.Simulate(.02f);Physics.Simulate(.02f);}
                w.RespawnAt(new Vector3(-24,-2.975f,10.9f),180);Step(Vector2.zero);
                bool startedStanding=w.Grounded&&!w.Swimming;
                float jumpSpeed=0;int steps=0;
                for(;steps<550;steps++)
                {
                    Step(Vector2.up,steps%40==0,1);if(steps==0)jumpSpeed=w.VerticalSpeed;
                    if(w.transform.position.z<7.5f&&w.transform.position.y>-.05f)break;
                }
                bool exited=steps<550;
                lines.Add((startedStanding&&jumpSpeed>3&&exited?"PASS ":"FAIL ")+"actual showroom stairs: grounded="+startedStanding+", jump speed="+jumpSpeed+", exit="+w.transform.position);
                Capture(camera,"01-StairsExit",w.transform.position+new Vector3(3.2f,2,-3),w.transform.position+Vector3.up*.9f);
                var rail=Object.FindObjectsByType<GuidedBuoyantPlatform>(FindObjectsSortMode.None).First(r=>r.mode==GuidedPlatformMode.VerticalBuoyancy);
                Vector3 p=rail.platform.position;
                w.water.Sample(p,out float surface,out _,out _);
                w.RespawnAt(new Vector3(p.x,surface-CourtyardSwimMotion.SurfaceDraft,p.z-2.05f),0);
                bool grabbed=false;int shot=0;bool animation=false;
                for(int i=0;i<240;i++)
                {
                    Step(grabbed?Vector2.zero:Vector2.up,false,1);grabbed|=w.Climbing;
                    if(w.Climbing)
                    {
                        animation|=animator.GetCurrentAnimatorStateInfo(0).IsName("Ledge Climb");
                        float threshold=shot==0?.08f:shot==1?.52f:.94f;
                        if(shot<3&&w.Climber.Progress>=threshold)
                        {
                            Capture(camera,"02-Climb-"+shot,p+new Vector3(3,1.4f,-3.6f),p+new Vector3(0,.7f,-1));shot++;
                        }
                    }
                    if(grabbed&&!w.Climbing){for(int j=0;j<14;j++)Step(Vector2.zero);break;}
                }
                bool boarded=w.Grounded&&!w.Swimming&&w.transform.position.y>p.y+.25f;
                lines.Add((grabbed&&animation&&boarded?"PASS ":"FAIL ")+"actual showroom floating deck: grabbed="+grabbed+", native climb state="+animation+", stood="+boarded+", exit="+w.transform.position);
                Capture(camera,"03-OnFloatingDeck",p+new Vector3(3.8f,2.5f,-4.2f),w.transform.position+Vector3.up*.9f);
                var console=Object.FindObjectsByType<LaserEmitterConsole>(FindObjectsSortMode.None).OrderBy(c=>c.transform.position.x).First();
                var wall=Object.FindObjectsByType<RedStoneCurtain>(FindObjectsSortMode.None).OrderBy(c=>c.transform.position.x).First();
                var oldChannel=console.CurrentChannel;
                try
                {
                    wall.ResetState();console.emitter.SetChannel(LightColorChannel.None);console.Use(null);
                    for(int i=0;i<30;i++){Physics.SyncTransforms();LightPuzzleWorld.Step(.02f);}
                    bool yellow=wall.IsOpen&&!wall.IsPermanent;
                    Capture(camera,"04-LaserReadout",console.transform.position+new Vector3(.65f,1.85f,-1.9f),console.transform.position+new Vector3(0,1.3f,-.42f));
                    console.Use(null);for(int i=0;i<20;i++)LightPuzzleWorld.Step(.02f);
                    bool blue=!wall.IsOpen&&!wall.IsPermanent;
                    console.Use(null);for(int i=0;i<30;i++)LightPuzzleWorld.Step(.02f);
                    bool red=wall.IsOpen&&wall.IsPermanent;
                    lines.Add((yellow&&blue&&red?"PASS ":"FAIL ")+"actual showroom E sequence: yellow temporary="+yellow+", blue restores="+blue+", red permanent="+red);
                }
                finally{console.emitter.SetChannel(oldChannel);console.RefreshVisuals();wall.ResetState();}
            }
            finally
            {
                w.RespawnAt(saved,savedYaw);w.enabled=wasEnabled;Physics.simulationMode=savedSimulation;Object.DestroyImmediate(camera.gameObject);
            }
            var result=string.Join("\n",lines);File.WriteAllText(Output+"/ShowroomChecks.txt",result);return result;
        }

        static void Capture(Camera camera,string name,Vector3 from,Vector3 at)
        {
            camera.transform.position=from;camera.transform.LookAt(at);camera.aspect=1.5f;
            var target=RenderTexture.GetTemporary(1200,800,24);var texture=new Texture2D(1200,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var poses=new List<(SkinnedMeshRenderer skin,Mesh mesh,GameObject snapshot)>();
            try
            {
                // Multiple test poses are evaluated in one Editor callback. GPU skinning can
                // otherwise reuse the first frame's buffers; bake the evaluated native pose.
                foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
                {
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh);
                    var snapshot=new GameObject("TemporaryPoseSnapshot");snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);snapshot.transform.localScale=skin.transform.lossyScale;
                    snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=snapshot.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;renderer.shadowCastingMode=skin.shadowCastingMode;
                    var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
                    poses.Add((skin,mesh,snapshot));skin.enabled=false;
                }
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,800),0,0);texture.Apply();
                File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally
            {
                foreach(var pose in poses){if(pose.skin)pose.skin.enabled=true;Object.DestroyImmediate(pose.snapshot);Object.DestroyImmediate(pose.mesh);}
                camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);
            }
        }
    }
}
