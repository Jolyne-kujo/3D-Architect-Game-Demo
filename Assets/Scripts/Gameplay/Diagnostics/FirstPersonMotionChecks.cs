#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using CoastalTemple.Player;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    [AddComponentMenu("")]
    public sealed class FirstPersonMotionChecks : MonoBehaviour
    {
        readonly List<string> records=new List<string>();
        CourtyardWalker walker;FirstPersonHands hands;CoastalWalkthrough tour;
        GameObject floor,wall;bool finished,sawLanding;string report;
        string directory="Documentation/CoastalV12Verification";
        readonly Vector3 start=new Vector3(-65,150.03f,204);
        public static string Begin(string reportName="MotionChecks.txt",string outputDirectory="Documentation/CoastalV12Verification")
        {
            if(!Application.isPlaying)throw new System.InvalidOperationException("Enter Play first.");
            var probe=new GameObject("TemporaryFirstPersonMotionChecks").AddComponent<FirstPersonMotionChecks>();
            probe.report=reportName;probe.directory=outputDirectory;return "Testing actual hand skeleton, airborne momentum and collisions.";
        }
        void Check(bool ok,string name)=>records.Add((ok?"PASS ":"FAIL ")+name);
        IEnumerator Move(float seconds,Vector2 input,bool run=false)
        {
            float end=Time.time+seconds;
            while(Time.time<end)
            {
                walker.SimulateMovement(input,run,false,0,Time.deltaTime);yield return null;
                sawLanding|=hands.animator.GetCurrentAnimatorStateInfo(0).IsName("Landing");
            }
        }
        void Capture(string name)=>ScreenCapture.CaptureScreenshot(directory+"/"+name+".png");
        IEnumerator Start()
        {
            tour=Object.FindAnyObjectByType<CoastalWalkthrough>();walker=tour.walker;hands=tour.playerCamera.hands;
            tour.SetOverview(false);walker.enabled=false;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="TemporaryMotionFloor";
            floor.transform.position=start-new Vector3(0,.13f,0);floor.transform.localScale=new Vector3(70,.2f,70);
            Physics.SyncTransforms();walker.RespawnAt(start,0);yield return Move(.4f,Vector2.zero);
            Directory.CreateDirectory(directory);Capture("01-Idle");yield return null;
            yield return Move(.45f,Vector2.up);
            var elbow=hands.animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var upper=hands.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var initialElbow=elbow.localRotation;var initialUpper=upper.localRotation;
            float armChange=0;
            for(int i=0;i<12;i++)
            {
                yield return Move(.08f,Vector2.up);
                armChange=Mathf.Max(armChange,Quaternion.Angle(initialElbow,elbow.localRotation),Quaternion.Angle(initialUpper,upper.localRotation));
                if(i==3)Capture("02-Walk-A");if(i==9)Capture("03-Walk-B");
            }
            Check(armChange>3,"walking changes actual arm bones, not just whole-model sway; degrees="+armChange);
            Check(hands.animator.GetCurrentAnimatorStateInfo(0).speed>0,"walking hand animation is not frozen at playback speed zero");
            Check(tour.view.transform.localPosition.sqrMagnitude<.000001f,"hand animation cannot move the eye camera");
            yield return Move(.4f,Vector2.up,true);
            Capture("04-Run-A");yield return Move(.22f,Vector2.up,true);Capture("05-Run-B");yield return null;
            float takeoffSpeed=walker.PlanarVelocity.magnitude;
            walker.SimulateMovement(Vector2.up,true,true,0,1f/60);yield return null;
            yield return Move(.1f,Vector2.up,true);
            Check(!walker.Controller.isGrounded&&walker.VerticalSpeed>0,"test performs a real grounded jump");
            Vector3 releasePosition=walker.transform.position, releaseVelocity=walker.PlanarVelocity;
            yield return Move(.2f,Vector2.zero);
            Capture("06-Airborne");yield return null;
            Check(walker.PlanarVelocity.magnitude>takeoffSpeed*.8f,"releasing input in air preserves most launch speed; before="+takeoffSpeed+", after="+walker.PlanarVelocity.magnitude);
            Check((walker.transform.position-releasePosition).z>.6f,"released jump continues travelling forward");
            Vector3 beforeTurn=walker.PlanarVelocity;walker.ApplyLook(new Vector2(90,0));yield return Move(.1f,Vector2.zero);
            Check(beforeTurn.magnitude>1&&Vector3.Angle(beforeTurn,walker.PlanarVelocity)<2,"turning camera in air does not rotate momentum");
            Check(!hands.animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"),"airborne hands use an active movement pose");
            yield return Move(1,Vector2.zero);
            Check(walker.Controller.isGrounded&&walker.PlanarVelocity.magnitude<.1f,"landing applies ground braking and comes to rest");
            Check(sawLanding,"touchdown plays the imported landing animation");
            walker.RespawnAt(start,0);yield return Move(.3f,Vector2.zero);yield return Move(.4f,Vector2.up,true);
            walker.SimulateMovement(Vector2.up,true,true,0,1f/60);yield return null;yield return Move(.1f,Vector2.up,true);
            float oldForward=walker.PlanarVelocity.z;yield return Move(.15f,Vector2.down);
            Check(walker.PlanarVelocity.z>oldForward*.5f,"opposite air input steers gradually instead of reversing instantly");
            walker.RespawnAt(start,0);yield return Move(.3f,Vector2.zero);
            Check(walker.PlanarVelocity.magnitude<.01f,"respawn clears previous momentum");
            wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="TemporaryMotionWall";
            wall.transform.position=start+new Vector3(0,2,2);wall.transform.localScale=new Vector3(12,4,.2f);Physics.SyncTransforms();
            yield return Move(.8f,Vector2.up,true);
            Check(walker.transform.position.z<wall.transform.position.z-.3f,"inertial controller still stops before wall");
            Check(walker.PlanarVelocity.magnitude<.05f,"blocked character reports zero actual speed for animation");
            yield return Move(.2f,Vector2.zero);
            var stopped=walker.transform.position;Object.Destroy(wall);wall=null;yield return null;
            yield return Move(.15f,Vector2.zero);
            Check(Vector3.Distance(walker.transform.position,stopped)<.04f,"removing wall does not release stored blocked velocity");
            Directory.CreateDirectory(directory);
            File.WriteAllText(directory+"/"+report,string.Join("\n",records));
            finished=true;Cleanup();Destroy(gameObject);
        }
        void Cleanup(){if(floor)Destroy(floor);if(wall)Destroy(wall);if(walker){walker.enabled=true;walker.ResetPosition();tour.SetOverview(false);}}
        void OnDestroy(){if(!finished)Cleanup();}
    }
}
#endif
