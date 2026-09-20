#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using CoastalTemple.Player;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    [AddComponentMenu("")]
    public sealed class FirstPersonPlayChecks : MonoBehaviour
    {
        readonly List<string> records=new List<string>();
        CoastalWalkthrough tour;CourtyardWalker walker;FirstPersonHands hands;
        GameObject floor,wall;bool finished,previousThirdPerson;
        string directory="Documentation/CoastalV12Verification";
        public static string Begin(string outputDirectory="Documentation/CoastalV12Verification")
        {
            if(!Application.isPlaying)throw new System.InvalidOperationException("Enter Play first.");
            var probe=new GameObject("TemporaryFirstPersonRegression").AddComponent<FirstPersonPlayChecks>();
            probe.directory=outputDirectory;Directory.CreateDirectory(outputDirectory);
            return "First-person rotation, collision, viewmodel and swimming checks running.";
        }
        void Check(bool ok,string label)=>records.Add((ok?"PASS ":"FAIL ")+label);
        IEnumerator Frames(float seconds,Vector2 input,float dive=0)
        {
            // Match the walker's maximum step. Editor stalls must not consume test duration
            // without actually simulating the corresponding movement (especially the dive).
            float elapsed=0;
            while(elapsed<seconds)
            {
                float step=Mathf.Min(Time.deltaTime,.05f,seconds-elapsed);
                walker.SimulateMovement(input,false,false,dive,step);elapsed+=step;yield return null;
            }
        }
        void Capture(string name)=>ScreenCapture.CaptureScreenshot(directory+"/Regression-"+name+".png");
        bool ShouldersOutsideFrame()
        {
            return new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm}.All(b=>
            {
                var p=hands.overlayCamera.WorldToViewportPoint(hands.animator.GetBoneTransform(b).position);
                return p.z<hands.overlayCamera.nearClipPlane||p.y<-.1f;
            });
        }
        IEnumerator Start()
        {
            tour=Object.FindAnyObjectByType<CoastalWalkthrough>();walker=tour.walker;hands=tour.playerCamera.hands;
            previousThirdPerson=tour.playerCamera.thirdPerson;
            tour.playerCamera.SetThirdPerson(false);tour.SetOverview(false);walker.enabled=false;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="TemporaryFPSFloor";
            floor.transform.position=new Vector3(-65,149.9f,204);floor.transform.localScale=new Vector3(30,.2f,30);
            Physics.SyncTransforms();walker.RespawnAt(new Vector3(-65,150.03f,204),0);
            // Respawning onto the test floor can first play Airborne -> Landing -> Locomotion.
            // Let the imported landing clip and its transitions finish before asserting idle.
            yield return Frames(.8f,Vector2.zero);
            Vector3 root=walker.transform.position,eye=tour.view.transform.position;
            float maxEyeDrift=0,maxRootDrift=0;
            foreach(var delta in new[]{new Vector2(90,0),new Vector2(90,-80),new Vector2(90,160),new Vector2(90,-80)})
            {
                walker.ApplyLook(delta);yield return null;
                maxEyeDrift=Mathf.Max(maxEyeDrift,Vector3.Distance(eye,tour.view.transform.position));
                maxRootDrift=Mathf.Max(maxRootDrift,Vector3.Distance(root,walker.transform.position));
                Check(Vector3.Dot(walker.transform.up,Vector3.up)>.99999f,"look pitch keeps the collision capsule upright");
            }
            Check(maxEyeDrift<.001f,"360-degree yaw and extreme pitch do not orbit camera; drift="+maxEyeDrift);
            Check(maxRootDrift<.001f,"turning does not translate the character; drift="+maxRootDrift);
            Check(tour.view.transform.parent==walker.eye&&tour.view.transform.localPosition.sqrMagnitude<.000001f,"camera remains exactly at the eye pivot");
            Check(tour.playerCamera.avatar.bodyRenderers.All(r=>r.shadowCastingMode==ShadowCastingMode.ShadowsOnly),"full head, torso and legs only cast world shadows");
            var skin=hands.animator.GetComponentInChildren<SkinnedMeshRenderer>();var weights=skin.sharedMesh.boneWeights;
            bool Arm(int i){string n=skin.bones[i].name;return n.Contains("upper_arm")||n.Contains("forearm")||n.Contains("hand.")||n.Contains("f_")||n.Contains("thumb");}
            Check(weights.All(w=>(Arm(w.boneIndex0)?w.weight0:0)+(Arm(w.boneIndex1)?w.weight1:0)+(Arm(w.boneIndex2)?w.weight2:0)+(Arm(w.boneIndex3)?w.weight3:0)>.99f),"viewmodel contains only downloaded arm and hand skin weights");
            Check(hands.GetComponentsInChildren<Collider>(true).Length==0,"visual hands cannot collide with or move their player");
            Check(!hands.animator.applyRootMotion,"viewmodel animation cannot translate player or camera");
            Check(hands.animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion")&&hands.animator.GetCurrentAnimatorStateInfo(0).loop,"resting hands keep an active looping idle animation");
            var palm=hands.animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var point=hands.overlayCamera.WorldToViewportPoint(palm.position);
            Check(point.z>hands.overlayCamera.nearClipPlane&&point.x>0&&point.x<1&&point.y<.35f&&point.y>-.5f,"relaxed idle hands stay low instead of blocking the centre of the viewport");
            Check(Vector3.Distance(palm.position,tour.view.transform.position)<.4f,"camera-space hand geometry is kept close to capsule center");
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="TemporaryFPSWall";wall.transform.position=root+new Vector3(0,1.5f,1.4f);wall.transform.localScale=new Vector3(8,3,.2f);
            Physics.SyncTransforms();yield return Frames(1,Vector2.up);
            float front=wall.transform.position.z-.1f;
            Check(walker.transform.position.z<front-walker.Controller.radius*.7f,"real CharacterController stops at wall");
            Check(tour.view.transform.position.z+tour.view.nearClipPlane<front-.1f,"world camera near plane stays on player side of wall");
            Check(Vector3.Distance(wall.GetComponent<Collider>().ClosestPoint(tour.view.transform.position),tour.view.transform.position)>.15f,"eye remains outside solid geometry");
            Capture("02-NearWall");yield return null;
            walker.ApplyLook(new Vector2(0,-75));Capture("03-LookDown");yield return null;
            Check(tour.view.transform.localPosition.sqrMagnitude<.000001f,"looking down does not push the eye through the wall");
            walker.ApplyLook(new Vector2(0,75));
            tour.playerCamera.SetThirdPerson(true);Check(tour.playerCamera.thirdPerson&&!hands.overlayCamera.enabled,"explicit third-person switch hides viewmodel");
            tour.playerCamera.SetThirdPerson(false);
            tour.SetOverview(true);Check(!hands.overlayCamera.enabled,"author overview hides local first-person hands");
            tour.SetOverview(false);Check(hands.overlayCamera.enabled&&tour.view.transform.parent==walker.eye,"return from author inspection restores eye and hands");
            Check(!tour.inspectionShortcuts,"normal gameplay has no overview keyboard shortcuts");
            tour.ResetWater();walker.RespawnAt(tour.water.transform.position+new Vector3(0,-.8f,0),160);
            yield return Frames(3,Vector2.zero);
            walker.SampleWater(walker.transform.position+Vector3.up*.8f,out float surface,out _,out _);
            Check(walker.Swimming,"actual courtyard water still enters swimming");
            float height=tour.view.transform.position.y-surface;
            Check(height>.1f&&height<.4f,"floating eye stays just above water, not several metres behind player; clearance="+height);
            Check(hands.animator.GetCurrentAnimatorStateInfo(0).IsName("Swimming"),"downloaded swimming animations drive first-person arms");
            Check(ShouldersOutsideFrame(),"floating animation keeps cut shoulders outside viewport");
            Capture("04-Floating");yield return null;
            yield return Frames(.65f,Vector2.up);Capture("05-SwimForward");yield return null;
            bool strokeFraming=true;
            for(int i=0;i<6;i++){yield return Frames(.25f,Vector2.up);strokeFraming&=ShouldersOutsideFrame();}
            Check(strokeFraming,"shoulder cuts stay outside viewport through swimming stroke");
            yield return Frames(1,Vector2.zero,-1);
            walker.SampleWater(walker.transform.position+Vector3.up*.8f,out surface,out _,out _);
            Check(tour.view.transform.position.y<surface,"deliberate dive carries eye below water");
            Check(tour.view.transform.parent==walker.eye&&hands.overlayCamera.enabled,"diving never detaches camera or disables arms");
            Capture("06-Underwater");yield return null;
            walker.ResetPosition();yield return Frames(.5f,Vector2.zero);
            var lantern=walker.GetComponent<PlayerLantern>();lantern.GiveLantern();yield return Frames(.5f,Vector2.zero);
            Check(lantern.carriedLantern.transform.IsChildOf(hands.model),"carried lantern belongs to first-person rig");
            Check(lantern.carriedLantern.activeInHierarchy&&lantern.LanternOn,"pickup keeps held lantern and illumination working");
            int carryLayer=hands.animator.GetLayerIndex("Carried Lantern");
            Check(carryLayer>=0&&hands.animator.GetLayerWeight(carryLayer)>.95f&&hands.animator.GetCurrentAnimatorStateInfo(carryLayer).IsName("Carry"),"downloaded left-arm torch-holding layer follows pickup");
            Check(lantern.carriedLantern.transform.parent==hands.animator.GetBoneTransform(HumanBodyBones.LeftHand),"lantern uses imported animation's left holding hand");
            Check(ShouldersOutsideFrame(),"carrying animation keeps cut shoulders outside viewport");
            var heldPoint=hands.overlayCamera.WorldToViewportPoint(lantern.carriedLantern.transform.position);
            Check(heldPoint.z>hands.overlayCamera.nearClipPlane&&heldPoint.x>0&&heldPoint.x<1&&heldPoint.y>0&&heldPoint.y<1,"held lantern appears within first-person viewport");
            Capture("07-CarriedLantern");yield return null;
            File.WriteAllText(directory+"/FirstPersonRegression.txt",string.Join("\n",records));
            finished=true;Cleanup();Destroy(gameObject);
        }
        void Cleanup(){if(floor)Destroy(floor);if(wall)Destroy(wall);if(walker){walker.enabled=true;walker.ResetPosition();tour.playerCamera.SetThirdPerson(previousThirdPerson);tour.SetOverview(false);}}
        void OnDestroy(){if(!finished)Cleanup();}
    }
}
#endif
