using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CoastalTemple.Mechanisms;
using CoastalTemple.Player;
using UnityEngine;
using WaterCourtyard;
using Object=UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class StairFootSceneChecks
    {
        const string Output="Documentation/StairFootPlacement";
        public static string Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Enter Play first.");
            string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            var w=Object.FindFirstObjectByType<CourtyardWalker>();var saved=w.transform.position;float yaw=w.LookYaw;bool enabled=w.enabled;
            var a=w.GetComponentInChildren<Animator>();var driver=w.GetComponentInChildren<RiggedPlayerAnimation>();var ik=a.GetComponent<GroundFootIK>();
            var update=typeof(RiggedPlayerAnimation).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
            var camera=new GameObject("TemporaryFootObserver").AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.enabled=false;camera.fieldOfView=48;
            var lines=new List<string>();var temporary=new List<GameObject>();Directory.CreateDirectory(Output);
            void Check(bool ok,string message)=>lines.Add((ok?"PASS ":"FAIL ")+message);
            void Pose(){update.Invoke(driver,null);a.Update(.02f);w.GetComponentInChildren<PlayerBodyAnchor>().SnapToWalker();w.GetComponent<CourtyardSurfaceSwimmer>().SamplePose(.02f);}
            void Step(Vector2 input,bool run=false,bool jump=false){Physics.SyncTransforms();w.SimulateMovement(input,run,jump,0,.02f);Pose();}
            try
            {
                w.enabled=false;
                var stair=Object.FindFirstObjectByType<CourtyardStaircase>();
                float Traverse(bool run)
                {
                    w.RespawnAt(stair.transform.TransformPoint(new Vector3(0,.025f,-.75f)),stair.transform.eulerAngles.y);
                    bool mantle=false;int air=0;float speed=0;int moving=0;
                    for(int i=0;i<500;i++)
                    {
                        Step(Vector2.up,run);mantle|=w.Climbing;
                        float z=stair.transform.InverseTransformPoint(w.transform.position).z;
                        // The swimming-to-standing pose can briefly settle onto the submerged ramp.
                        // This assertion concerns loss of contact on the dry running surface.
                        if(z>1&&z<stair.run-.5f){speed+=w.PlanarVelocity.magnitude;moving++;if(!w.Grounded&&!w.Swimming&&w.transform.position.y>=w.WaterSurface)air++;}
                        if(run&&z>stair.run*.55f&&z<stair.run*.55f+.2f)Capture(camera,scene+"-RunRamp",w.transform.position+new Vector3(2.5f,1.1f,1.6f),w.transform.position+Vector3.up*.65f);
                        if(z>stair.run+.5f)
                        {
                            Check(!mantle&&air<3,$"actual {(run?"run":"walk")} stair traversal: {i*.02f:F2}s, speed={speed/Mathf.Max(1,moving):F2}m/s, dry-airborne={air}, mantle={mantle}");
                            return i*.02f;
                        }
                    }
                    Check(false,"stair stuck at "+w.transform.position);return 100;
                }
                float walk=Traverse(false),run=Traverse(true);Check(run<walk*.82f,"Shift accelerates actual scene stairs");
                Check(a.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),"stairs use normal locomotion blend tree");
                w.ApplyLook(new Vector2(180,0));bool jumped=false;int falling=0;
                for(int i=0;i<250;i++){Step(Vector2.up,true);float z=stair.transform.InverseTransformPoint(w.transform.position).z;if(z<.1f)break;if(z>1&&z<stair.run-1&&!w.Grounded&&!w.Swimming)falling++;jumped|=w.Climbing;}
                Check(falling<3&&!jumped,"run down actual stairs: airborne="+falling+", mantle="+jumped);
                if(scene=="MechanismPlayground")
                {
                    var mirage=Object.FindFirstObjectByType<WaterMirage>();int oldAim=mirage.AimIndex;
                    mirage.water.ResetWater();for(int aim=0;aim<mirage.aimAngles.Length;aim++){mirage.SetAimState(aim);if(mirage.IsSolid)break;}
                    Check(mirage.IsSolid,"actual blue-light projection produces solid steps");
                    var first=mirage.stepColliders.OrderBy(c=>c.bounds.center.z).First();
                    w.RespawnAt(new Vector3(first.bounds.center.x,first.bounds.max.y+.025f,first.bounds.center.z),0);
                    int frames=0;bool mantle=false;int air=0;
                    for(;frames<150&&w.transform.position.z<16.4f;frames++){Step(Vector2.up);mantle|=w.Climbing;if(!w.Grounded)air++;if(frames==30)Capture(camera,"Showroom-ProjectedSteps",w.transform.position+new Vector3(2.3f,1.2f,-2),w.transform.position+Vector3.up*.65f);}
                    Check(frames<85&&!mantle&&w.transform.position.y>.5f,$"actual four thin steps without Space: {frames*.02f:F2}s, mantle={mantle}, feet={w.transform.position}");
                    mirage.SetAimState(oldAim);

                    // Split support under the actual imported humanoid, not a synthetic IK goal.
                    var box=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(box);box.name="Temporary left-foot support";
                    box.transform.position=new Vector3(19.6f,.1f,-10);box.transform.localScale=new Vector3(.8f,.2f,1);
                    w.RespawnAt(new Vector3(20,.23f,-10),0);a.Play("Locomotion",0,0);a.SetFloat("Speed",0);
                    for(int i=0;i<65;i++)Step(Vector2.zero);
                    Vector3 left=a.GetBoneTransform(HumanBodyBones.LeftFoot).position,right=a.GetBoneTransform(HumanBodyBones.RightFoot).position;
                    float error=Mathf.Max(Vector3.Distance(left,ik.LeftTarget),Vector3.Distance(right,ik.RightTarget));
                    Check(ik.IsSolving&&left.y-right.y>.15f&&error<.045f,$"independent feet on 0.20m split support: height difference={left.y-right.y:F3}m, IK error={error:F3}m");
                    Capture(camera,"IndependentFeet",w.transform.position+new Vector3(1.4f,.65f,2),w.transform.position+Vector3.up*.55f);
                    Step(Vector2.zero,false,true);Check(!ik.IsSolving&&w.VerticalSpeed>4,"jump immediately releases foot IK");
                    w.RespawnAt(new Vector3(-18,-1.7f,17),0);for(int i=0;i<90;i++)Step(Vector2.zero);
                    Check(w.Swimming&&!ik.IsSolving,"open-water swimming releases foot IK");
                    w.RespawnAt(new Vector3(7,.03f,-.8f),180);for(int i=0;i<20;i++)Step(Vector2.zero);
                    Check(ik.IsSolving&&Mathf.Abs(ik.LeftCorrection)<.08f&&Mathf.Abs(ik.RightCorrection)<.08f,"teleport/respawn clears old foot offsets");
                }
            }
            finally{foreach(var go in temporary)Object.DestroyImmediate(go);w.RespawnAt(saved,yaw);w.enabled=enabled;Object.DestroyImmediate(camera.gameObject);}
            string report=string.Join("\n",lines);File.WriteAllText(Output+"/"+scene+"Checks.txt",report);return report;
        }
        static void Capture(Camera camera,string name,Vector3 from,Vector3 at)
        {
            camera.transform.position=from;camera.transform.LookAt(at);camera.aspect=1.5f;
            var target=RenderTexture.GetTemporary(1200,800,24);var texture=new Texture2D(1200,800,TextureFormat.RGB24,false);var previous=RenderTexture.active;
            var poses=new List<(SkinnedMeshRenderer skin,Mesh mesh,GameObject snapshot)>();
            try
            {
                foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
                {
                    if(!skin.enabled||!skin.gameObject.activeInHierarchy)continue;
                    var mesh=new Mesh();skin.BakeMesh(mesh);var snapshot=new GameObject("TemporaryPoseSnapshot");snapshot.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);snapshot.transform.localScale=skin.transform.lossyScale;
                    snapshot.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=snapshot.AddComponent<MeshRenderer>();renderer.sharedMaterials=skin.sharedMaterials;renderer.shadowCastingMode=skin.shadowCastingMode;
                    var block=new MaterialPropertyBlock();skin.GetPropertyBlock(block);renderer.SetPropertyBlock(block);poses.Add((skin,mesh,snapshot));skin.enabled=false;
                }
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;texture.ReadPixels(new Rect(0,0,1200,800),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+".png",texture.EncodeToPNG());
            }
            finally
            {
                foreach(var pose in poses){if(pose.skin)pose.skin.enabled=true;Object.DestroyImmediate(pose.snapshot);Object.DestroyImmediate(pose.mesh);}
                camera.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);Object.DestroyImmediate(texture);
            }
        }
    }
}
