using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    /// <summary>Independent sole/toe probes lift each humanoid leg while the capsule owns locomotion.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class GroundFootIK : MonoBehaviour
    {
        [Min(0)] public float maximumLift=.38f;
        [Min(0)] public float maximumDrop=.22f;
        [Min(0)] public float soleClearance=.018f;
        [Min(0)] public float toeReach=.12f;
        [Min(0)] public float settleSpeed=3;
        public float LeftCorrection { get; private set; }
        public float RightCorrection { get; private set; }
        public Vector3 LeftTarget { get; private set; }
        public Vector3 RightTarget { get; private set; }
        public bool IsSolving { get; private set; }
        Animator animator;
        CourtyardWalker walker;
        readonly CourtyardCharacterQueries query=new CourtyardCharacterQueries();
        Vector3 lastRoot;
        float left,right,pelvis;

        void Awake(){animator=GetComponent<Animator>();walker=GetComponentInParent<CourtyardWalker>();lastRoot=transform.position;}
        void OnAnimatorIK(int layer)
        {
            if(layer!=0||!animator||!animator.isHuman||!walker||!walker.Controller)return;
            bool teleported=(transform.position-lastRoot).sqrMagnitude>1;
            lastRoot=transform.position;
            IsSolving=walker.Grounded&&!walker.Swimming&&!walker.Climbing&&walker.VerticalSpeed<=.1f;
            if(!IsSolving||teleported){left=right=pelvis=0;}
            if(!IsSolving)
            {
                animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot,0);animator.SetIKPositionWeight(AvatarIKGoal.RightFoot,0);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftFoot,0);animator.SetIKRotationWeight(AvatarIKGoal.RightFoot,0);
                LeftCorrection=RightCorrection=0;return;
            }
            LeftTarget=Solve(AvatarIKGoal.LeftFoot,animator.leftFeetBottomHeight,ref left);
            RightTarget=Solve(AvatarIKGoal.RightFoot,animator.rightFeetBottomHeight,ref right);
            // Lower the hips when one foot stands below the capsule. Without this the lower
            // leg runs out of reach and hovers above the ground even though its IK goal is correct.
            pelvis=Mathf.MoveTowards(pelvis,Mathf.Min(0,Mathf.Min(left,right)),settleSpeed*Mathf.Min(Time.deltaTime,.05f));
            animator.bodyPosition+=Vector3.up*pelvis;
            LeftCorrection=left;RightCorrection=right;
        }
        Vector3 Solve(AvatarIKGoal goal,float footHeight,ref float offset)
        {
            Vector3 animated=animator.GetIKPosition(goal);
            Quaternion rotation=animator.GetIKRotation(goal);
            Vector3 rootFeet=CourtyardCharacterQueries.Feet(walker.Controller);
            Vector3 forward=Vector3.ProjectOnPlane(transform.forward,Vector3.up).normalized;
            float groundY=float.NegativeInfinity;Vector3 normal=Vector3.up;
            // Sole and toe are sampled separately, so the swing foot clears a vertical tread face.
            for(int i=0;i<2;i++)
            {
                Vector3 point=animated+forward*(i==0?0:toeReach);
                Vector3 origin=new Vector3(point.x,rootFeet.y+maximumLift+.12f,point.z);
                if(!query.Ray(walker.Controller,origin,Vector3.down,maximumLift+maximumDrop+.2f,out var hit))continue;
                if(hit.normal.y<Mathf.Cos(walker.Controller.slopeLimit*Mathf.Deg2Rad))continue;
                float y=hit.point.y;
                var stairs=hit.collider.GetComponentInParent<CourtyardStaircase>();
                if(stairs&&stairs.TryTreadHeight(point,out float treadY))y=Mathf.Max(y,treadY);
                if(y>groundY){groundY=y;normal=stairs?Vector3.up:hit.normal;}
            }
            float wanted=0;
            if(!float.IsNegativeInfinity(groundY))
            {
                // Preserve the authored swing arc; only bring low feet down onto nearby support.
                float sole=animated.y-footHeight;
                float delta=groundY+soleClearance-sole;
                float swing=Mathf.Max(0,sole-rootFeet.y);
                wanted=delta>0?delta:delta*(1-Mathf.SmoothStep(0,1,swing/.18f));
                wanted=Mathf.Clamp(wanted,-maximumDrop,maximumLift);
            }
            // Upward clearance must be immediate; release smoothly without delaying a foot into a step.
            offset=wanted>offset?wanted:Mathf.MoveTowards(offset,wanted,settleSpeed*Mathf.Min(Time.deltaTime,.05f));
            Vector3 target=animated+Vector3.up*offset;
            animator.SetIKPositionWeight(goal,1);animator.SetIKPosition(goal,target);
            float planted=1-Mathf.SmoothStep(0,1,Mathf.Max(0,animated.y-footHeight-rootFeet.y)/.18f);
            animator.SetIKRotationWeight(goal,planted*.65f);
            animator.SetIKRotation(goal,Quaternion.FromToRotation(Vector3.up,normal)*rotation);
            return target;
        }
        void OnDisable(){left=right=pelvis=0;LeftCorrection=RightCorrection=0;IsSolving=false;}
    }
}
