using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    /// <summary>Keep both hands on the grabbed edge while the downloaded clip pulls the body up.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Animator))]
    public sealed class LedgeClimbHandIK : MonoBehaviour
    {
        Animator animator;
        CourtyardWalker walker;
        void Awake(){animator=GetComponent<Animator>();walker=GetComponentInParent<CourtyardWalker>();}
        void OnAnimatorIK(int layer)
        {
            if(layer!=0||!animator||!animator.isHuman)return;
            var climb=walker?walker.Climber:null;
            float weight=climb&&climb.IsClimbing&&climb.UsesClimbAnimation
                ? Mathf.SmoothStep(0,1,climb.Progress/.1f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.62f,.9f,climb.Progress))) : 0;
            animator.SetIKPositionWeight(AvatarIKGoal.LeftHand,weight);animator.SetIKPositionWeight(AvatarIKGoal.RightHand,weight);
            if(weight>0)
            {
                animator.SetIKPosition(AvatarIKGoal.LeftHand,climb.HandTarget(false));
                animator.SetIKPosition(AvatarIKGoal.RightHand,climb.HandTarget(true));
            }
        }
    }
}
