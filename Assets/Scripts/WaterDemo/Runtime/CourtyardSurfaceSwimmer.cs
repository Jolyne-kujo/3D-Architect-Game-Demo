using UnityEngine;

namespace WaterCourtyard
{
    /// <summary>Keep the animated head at the waterline; the motor still owns all world movement.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CourtyardWalker)), DefaultExecutionOrder(110)]
    public sealed class CourtyardSurfaceSwimmer : MonoBehaviour
    {
        public Transform head;
        [Min(0)] public float headClearance = .24f;
        [Min(.02f)] public float poseSmoothing = .12f;
        public float CurrentDraft { get; private set; } = CourtyardSwimMotion.SurfaceDraft;
        CourtyardWalker walker;
        float smoothingVelocity, pendingPoseCorrection;
        void Awake()
        {
            walker=GetComponent<CourtyardWalker>();
            if(!head){var animator=GetComponentInChildren<Animator>();if(animator&&animator.isHuman)head=animator.GetBoneTransform(HumanBodyBones.Head);}
        }
        void LateUpdate() => SamplePose(Time.deltaTime);
        public void SamplePose(float seconds)
        {
            if(!walker)walker=GetComponent<CourtyardWalker>();
            if(!head||!walker.Swimming){pendingPoseCorrection=0;return;}
            float target=Mathf.Clamp(head.position.y-transform.position.y-headClearance,.25f,1.6f);
            float previous=CurrentDraft;
            CurrentDraft=Mathf.SmoothDamp(CurrentDraft,target,ref smoothingVelocity,poseSmoothing,8,seconds);
            // Feed the change of posture forward to the collision motor. Buoyancy alone
            // responds too slowly when a vertical tread blends into a horizontal stroke.
            pendingPoseCorrection+=previous-CurrentDraft;
        }
        public float ConsumePoseCorrection(){float correction=pendingPoseCorrection;pendingPoseCorrection=0;return correction;}
        public void ResetDraft(){CurrentDraft=CourtyardSwimMotion.SurfaceDraft;smoothingVelocity=0;pendingPoseCorrection=0;}
    }
}
