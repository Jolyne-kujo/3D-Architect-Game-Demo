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
    public sealed class ImportedLocomotionPlayChecks : MonoBehaviour
    {
        const string Output = "Documentation/AssetOrganization/AnimationPreview";
        readonly List<string> lines = new List<string>();
        CourtyardWalker walker;
        CoastalWalkthrough tour;
        RiggedPlayerAnimation body;
        FirstPersonHands hands;
        GameObject floor;
        Camera observer;
        bool cleaned, previousThirdPerson;
        float bodyError, eyeError, hipRadius, kneeChange;
        Quaternion initialKnee;
        Vector3 initialFacing;

        public static string Begin()
        {
            if (!Application.isPlaying) throw new System.InvalidOperationException("Enter Play first.");
            new GameObject("TemporaryImportedLocomotionChecks").AddComponent<ImportedLocomotionPlayChecks>();
            return "Checking supplied animations on actual body and arms, including in-place bones.";
        }
        void Check(bool valid,string label)=>lines.Add((valid?"PASS ":"FAIL ")+label);

        Vector3 BodyForward()
        {
            var left=body.animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            var right=body.animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
            // Bone transforms are readable after animation evaluation; Animator.bodyRotation
            // is an IK-pass API and must not be queried from an ordinary coroutine.
            return Vector3.Cross(right-left,Vector3.up).normalized;
        }

        IEnumerator Move(float seconds, Vector2 input, bool run, bool measure=false)
        {
            float elapsed=0;
            while(elapsed<seconds)
            {
                float step=Mathf.Min(Time.deltaTime,.05f,seconds-elapsed);
                walker.SimulateMovement(input,run,false,0,step);elapsed+=step;yield return null;
                if(!measure)continue;
                bodyError=Mathf.Max(bodyError,Vector3.Distance(body.transform.position,walker.transform.position));
                eyeError=Mathf.Max(eyeError,Vector3.Distance(tour.view.transform.position,walker.eye.position));
                var hip=walker.transform.InverseTransformPoint(body.animator.GetBoneTransform(HumanBodyBones.Hips).position);
                hipRadius=Mathf.Max(hipRadius,new Vector2(hip.x,hip.z).magnitude);
                kneeChange=Mathf.Max(kneeChange,Quaternion.Angle(initialKnee,body.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation));
            }
        }

        void CaptureBody(string name)
        {
            var modes=tour.playerCamera.avatar.bodyRenderers.Select(r=>r.shadowCastingMode).ToArray();
            var rt=RenderTexture.GetTemporary(1280,960,24);var texture=new Texture2D(1280,960,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                foreach(var r in tour.playerCamera.avatar.bodyRenderers)r.shadowCastingMode=ShadowCastingMode.On;
                observer.transform.position=walker.transform.position+new Vector3(3,2.2f,3);
                observer.transform.LookAt(walker.transform.position+Vector3.up*.85f);
                observer.targetTexture=rt;observer.aspect=4f/3;observer.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,1280,960),0,0);texture.Apply();File.WriteAllBytes(Output+"/"+name+"-Body.png",texture.EncodeToPNG());
            }
            finally
            {
                for(int i=0;i<modes.Length;i++)tour.playerCamera.avatar.bodyRenderers[i].shadowCastingMode=modes[i];
                observer.targetTexture=null;RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Destroy(texture);
            }
            ScreenCapture.CaptureScreenshot(Output+"/"+name+"-FirstPerson.png");
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(Output);tour=FindFirstObjectByType<CoastalWalkthrough>();walker=tour.walker;
            previousThirdPerson=tour.playerCamera.thirdPerson;tour.playerCamera.SetThirdPerson(false);
            tour.SetOverview(false);walker.enabled=false;body=walker.GetComponentInChildren<RiggedPlayerAnimation>();hands=tour.playerCamera.hands;
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="TemporaryLocomotionFloor";
            floor.transform.position=new Vector3(-65,149.9f,220);floor.transform.localScale=new Vector3(80,.2f,100);
            observer=new GameObject("TemporaryLocomotionObserver").AddComponent<Camera>();observer.enabled=false;
            observer.cullingMask=~(1<<LayerMask.NameToLayer("FirstPersonHands"));observer.fieldOfView=38;
            observer.nearClipPlane=.1f;observer.farClipPlane=120;Physics.SyncTransforms();
            walker.RespawnAt(new Vector3(-65,150.03f,204),0);yield return Move(.5f,Vector2.zero,false);
            initialFacing=BodyForward();
            foreach(bool running in new[]{false,true})
            {
                string expected=running?"Fast Run":"Slow Run";yield return Move(.8f,Vector2.up,running);
                bodyError=eyeError=hipRadius=kneeChange=0;initialKnee=body.animator.GetBoneTransform(HumanBodyBones.LeftLowerLeg).localRotation;
                CaptureBody(running?"03-FastRun-A":"01-SlowRun-A");
                yield return Move(.22f,Vector2.up,running,true);CaptureBody(running?"04-FastRun-B":"02-SlowRun-B");
                yield return Move(2.2f,Vector2.up,running,true);
                foreach(var animator in new[]{body.animator,hands.animator})
                    Check(animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name==expected&&c.weight>.95f),animator.name+" actually plays "+expected);
                Check(bodyError<.001f&&eyeError<.001f,expected+" keeps body and camera anchored; errors="+bodyError+", "+eyeError);
                Check(hipRadius<.4f,expected+" hips remain inside capsule vicinity over repeated cycles; max XZ radius="+hipRadius);
                Check(kneeChange>10,expected+" animates original mannequin skeleton; knee degrees="+kneeChange);
                var facing=BodyForward();
                facing.y=initialFacing.y=0;
                Check(Vector3.Angle(initialFacing,facing)<35,expected+" keeps same forward direction as idle; angle="+Vector3.Angle(initialFacing,facing));
                Check(!body.animator.applyRootMotion&&!hands.animator.applyRootMotion,expected+" cannot move character using source root motion");
            }
            yield return Move(.6f,Vector2.zero,false);
            Check(body.animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name=="Rig|Idle_Loop"&&c.weight>.95f),"stopping returns to idle");
            File.WriteAllLines("Documentation/AssetOrganization/LocomotionPlay.txt",lines);Cleanup();Destroy(gameObject);
        }
        void Cleanup()
        {
            if(cleaned)return;cleaned=true;if(floor)Destroy(floor);if(observer)Destroy(observer.gameObject);
            if(walker){walker.enabled=true;walker.ResetPosition();tour.playerCamera.SetThirdPerson(previousThirdPerson);tour.SetOverview(false);}
        }
        void OnDestroy()=>Cleanup();
    }
}
#endif
