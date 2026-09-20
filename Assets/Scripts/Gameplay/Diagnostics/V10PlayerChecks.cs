#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CoastalTemple.Player;
using WaterCourtyard;

namespace CoastalTemple.Editor
{
    [AddComponentMenu("")]
    public sealed class V10PlayerChecks : MonoBehaviour
    {
        readonly List<string> checks = new List<string>();
        CourtyardWalker walker;
        CoastalWalkthrough tour;
        RiggedPlayerAnimation driver;
        GameObject floor;
        bool finished;
        public static string Begin()
        {
            if (!Application.isPlaying) throw new System.InvalidOperationException("Enter Play first");
            new GameObject("TemporaryImportedPlayerChecks").AddComponent<V10PlayerChecks>();
            return "Running real movement, imported bones, camera and swimming checks.";
        }
        void Check(bool value, string name) { checks.Add((value ? "PASS " : "FAIL ") + name); }
        IEnumerator Start()
        {
            tour=Object.FindAnyObjectByType<CoastalWalkthrough>(); walker=tour.walker;
            driver=walker.GetComponentInChildren<RiggedPlayerAnimation>(); tour.SetOverview(false);
            walker.enabled=false;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="TemporaryAnimationCheckFloor";
            floor.transform.position=new Vector3(-65,149.9f,204);floor.transform.localScale=new Vector3(35,.2f,35);
            Physics.SyncTransforms();walker.RespawnAt(new Vector3(-65,150.05f,204),90);
            yield return Frames(.7f,Vector2.zero);
            var animator=driver.animator;
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"),"grounded idle uses downloaded locomotion state");
            Check(!animator.applyRootMotion,"animation never moves the physical player root");
            Vector3 start=walker.transform.position;var leg=animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);var rest=leg.localRotation;
            yield return Frames(1.1f,Vector2.up);
            Check(Vector3.Distance(start,walker.transform.position)>2,"real walking moves the character");
            Check(animator.GetFloat("Speed")>3,"actual velocity drives walking blend");
            Check(Quaternion.Angle(rest,leg.localRotation)>2,"downloaded animation moves skeletal leg bones");
            var foot=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var toe=animator.GetBoneTransform(HumanBodyBones.LeftToes);
            Check(Vector3.Dot((toe.position-foot.position).normalized,walker.transform.forward)>.2f,"model faces movement rather than walking backwards");
            yield return Frames(.75f,Vector2.up,true);
            Check(animator.GetFloat("Speed")>4.8f,"running selects faster imported animation blend");
            walker.SimulateMovement(Vector2.zero,false,true,0,Time.deltaTime);
            yield return Frames(.25f,Vector2.zero);
            Check(!walker.Controller.isGrounded&&animator.GetCurrentAnimatorStateInfo(0).IsName("Airborne"),"real jump selects imported airborne animation");
            Check(tour.view.transform.IsChildOf(walker.transform),"third person camera stays attached during movement");
            tour.playerCamera.SetThirdPerson(false);
            Check(driver.GetComponentsInChildren<Renderer>().All(r=>!r.enabled),"first person hides the local avatar");
            tour.playerCamera.SetThirdPerson(true);
            Check(driver.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"third person restores skinned avatar");
            tour.SetOverview(true);tour.SetOverview(false);
            Check(tour.view.transform.IsChildOf(walker.transform),"overview exit restores player binding");
            tour.ResetWater();walker.RespawnAt(tour.water.transform.position+new Vector3(0,-.8f,0),160);
            yield return Frames(3,Vector2.zero);
            Check(walker.Swimming&&animator.GetCurrentAnimatorStateInfo(0).IsName("Swimming"),"actual water selects imported swim-idle animation");
            walker.SampleWater(walker.transform.position+Vector3.up*.8f,out float surface,out _,out _);
            Check(tour.view.transform.position.y>surface+.35f,"swimming follow camera stays above water");
            Capture("02-SwimmingIdle");
            yield return Frames(.55f,Vector2.up);
            Check(animator.GetFloat("Speed")>.5f&&animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name.EndsWith("Swim_Fwd_Loop")&&c.weight>.1f),"swimming forward uses downloaded stroke animation");
            Capture("03-SwimmingForward");
            File.WriteAllText("Documentation/CoastalV10Verification/PlayerAnimationChecks.txt",string.Join("\n",checks));
            finished=true;Cleanup();
            yield return new WaitForSeconds(.5f);
            Capture("01-PlayerFollow");
            Destroy(gameObject);
        }
        IEnumerator Frames(float seconds,Vector2 input,bool run=false)
        {
            float end=Time.time+seconds;
            while(Time.time<end){walker.SimulateMovement(input,run,false,0,Time.deltaTime);yield return null;}
        }
        void Capture(string name)
        {
            var cam=tour.view;var previous=cam.targetTexture;var aspect=cam.aspect;var active=RenderTexture.active;
            var rt=RenderTexture.GetTemporary(1600,900,24);var tx=new Texture2D(1600,900,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.aspect=16f/9;cam.Render();RenderTexture.active=rt;tx.ReadPixels(new Rect(0,0,1600,900),0,0);tx.Apply();File.WriteAllBytes("Documentation/CoastalV10Verification/"+name+".png",tx.EncodeToPNG());}
            finally{cam.targetTexture=previous;cam.aspect=aspect;RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);Destroy(tx);}
        }
        void Cleanup()
        {
            if(floor)Destroy(floor);
            if(walker){walker.enabled=true;walker.ResetPosition();tour.SetOverview(false);}
        }
        void OnDestroy(){if(!finished)Cleanup();}
    }
}
#endif
