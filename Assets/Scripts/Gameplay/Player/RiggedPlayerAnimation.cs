using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    /// <summary>Feeds locomotion into downloaded skeletal animations. Movement stays owned by the walker.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class RiggedPlayerAnimation : MonoBehaviour
    {
        public CourtyardWalker walker;
        public Animator animator;
        public Transform facingRoot;
        [Tooltip("Imported model forward-axis correction: Quaternius uses 180 degrees, Mixamo Red Bot uses 0.")]
        public float modelYawOffset = 180;
        // Kept for older serialized prefabs. Heading is now owned by the walker/body anchor.
        [HideInInspector] public float turnSpeed = 540;
        static readonly int Speed = Animator.StringToHash("Speed");
        static readonly int Swimming = Animator.StringToHash("Swimming");
        static readonly int Grounded = Animator.StringToHash("Grounded");
        static readonly int Climbing = Animator.StringToHash("Climbing");
        static readonly int ClimbProgress = Animator.StringToHash("ClimbProgress");
        static readonly int StairDirection = Animator.StringToHash("StairDirection");
        static readonly int StairSpeed = Animator.StringToHash("StairSpeed");
        RuntimeAnimatorController checkedController;
        bool supportsClimb,supportsStairs;

        void Awake()
        {
            if (!walker) walker = GetComponentInParent<CourtyardWalker>();
            if (!animator) animator = GetComponentInChildren<Animator>();
            if (animator) animator.applyRootMotion = false;
        }

        void Update()
        {
            if (!walker || !animator) return;
            Vector3 velocity = walker.PlanarVelocity;
            float speed = velocity.magnitude;
            animator.SetFloat(Speed, speed, .12f, Time.deltaTime);
            animator.SetBool(Swimming, walker.Swimming);
            bool climbingPose = walker.Climbing && walker.Climber.UsesClimbAnimation;
            animator.SetBool(Grounded, walker.Grounded || (walker.Climbing && !climbingPose));
            if (checkedController != animator.runtimeAnimatorController)
            {
                checkedController = animator.runtimeAnimatorController; supportsClimb = false; supportsStairs=false;
                foreach (var parameter in animator.parameters)
                {
                    if (parameter.nameHash == Climbing) supportsClimb = true;
                    if (parameter.nameHash == StairDirection) supportsStairs=true;
                }
            }
            if(supportsStairs)
            {
                animator.SetInteger(StairDirection,walker.StairDirection);
                animator.SetFloat(StairSpeed,walker.Staircase?Mathf.Clamp(speed/walker.Staircase.animationReferenceSpeed,.4f,1.6f):1);
            }
            if (supportsClimb)
            {
                animator.SetBool(Climbing, climbingPose);
                animator.SetFloat(ClimbProgress, walker.Climber ? walker.Climber.Progress : 0);
            }
            // Strafing and airborne momentum must not rotate the visual away from the player's yaw.
            // PlayerBodyAnchor applies the imported model's fixed axis correction after animation.
        }
    }
}
