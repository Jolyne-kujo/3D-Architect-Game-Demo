using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    /// <summary>Camera-space presentation only; no input, camera rotation or actor movement is owned here.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class FirstPersonHands : MonoBehaviour
    {
        public CourtyardWalker walker;
        public PlayerLantern lantern;
        public Animator animator;
        public Transform model;
        public Camera overlayCamera;
        public Vector3 restingPosition = new Vector3(0,-1.55f,.58f);
        public Vector3 swimmingPosition = new Vector3(0,-1.5f,.4f);
        [Tooltip("Camera-space shoulder anchor; mirrored for the left arm. Keeps the cut shoulder outside the view.")]
        public Vector3 shoulderAnchor = new Vector3(.27f,-.38f,.035f);
        public Vector3 carryingAim = new Vector3(.21f,-.13f,.65f);
        public Vector3 swimmingAim = new Vector3(.22f,-.06f,.55f);
        public Vector3 restingAim = new Vector3(.33f,-.40f,.65f);
        public Vector3 walkingAim = new Vector3(.32f,-.28f,.65f);
        public Vector3 movingAim = new Vector3(.27f,-.10f,.50f);
        public Vector3 airborneAim = new Vector3(.21f,.05f,.52f);
        [Tooltip("Retain imported walk/run arm swing when bringing the arms into first-person framing.")]
        [Range(0,1)] public float importedSwing=.85f;
        [Tooltip("Roll empty walking hands away from a palms-up guard pose.")]
        public float relaxedWristRoll=55;
        [Min(0)] public float swayAmount = .007f;
        static readonly int Swimming=Animator.StringToHash("Swimming");
        static readonly int Grounded=Animator.StringToHash("Grounded");
        static readonly int Speed=Animator.StringToHash("Speed");
        float phase, carryWeight,relaxedWeight,groundPoseWeight;
        int carryLayer=-1,relaxedLayer=-1;
        bool rigReady;
        Transform leftArm, rightArm, leftHand, rightHand;

        public void SetVisible(bool visible)
        {
            if(overlayCamera)overlayCamera.enabled=visible;
            if(model)model.gameObject.SetActive(visible);
        }
        void Awake()
        {
            if(!walker)walker=GetComponentInParent<CourtyardWalker>();
            if(!lantern&&walker)lantern=walker.GetComponent<PlayerLantern>();
            if(animator)animator.applyRootMotion=false;
        }
        void InitializeVisibleRig()
        {
            // The first-person model starts inactive in third-person scenes. Its animator
            // does not have a playable graph until enabled, so defer layer/bone lookup.
            if(!rigReady&&animator&&animator.isHuman&&animator.isInitialized)
            {
                carryLayer=animator.GetLayerIndex("Carried Lantern");
                relaxedLayer=animator.GetLayerIndex("Relaxed Hands");
                leftArm=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                rightArm=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                leftHand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
                rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                rigReady=true;
            }
        }
        void Update()
        {
            if(!animator||!walker||!animator.isActiveAndEnabled||!animator.isInitialized)return;
            InitializeVisibleRig();
            // Feed movement before Unity evaluates the skeleton, then frame its result in LateUpdate.
            animator.SetBool(Swimming,walker.Swimming);
            animator.SetBool(Grounded,walker.Controller&&walker.Controller.isGrounded);
            animator.SetFloat(Speed,walker.PlanarVelocity.magnitude,.12f,Time.deltaTime);
            carryWeight=Mathf.MoveTowards(carryWeight,lantern&&lantern.HasLantern&&!walker.Swimming?1:0,Time.deltaTime*6);
            if(carryLayer>=0)animator.SetLayerWeight(carryLayer,carryWeight);
            relaxedWeight=Mathf.MoveTowards(relaxedWeight,walker.Swimming?0:1,Time.deltaTime*6);
            if(relaxedLayer>=0)animator.SetLayerWeight(relaxedLayer,relaxedWeight);
            groundPoseWeight=Mathf.MoveTowards(groundPoseWeight,walker.Controller&&walker.Controller.isGrounded&&!walker.Swimming?1:0,Time.deltaTime*6);
        }
        void LateUpdate()
        {
            if(!animator||!walker||!model||!animator.isActiveAndEnabled||!rigReady)return;
            float speed=walker.PlanarVelocity.magnitude;
            phase+=Time.deltaTime*8;
            Vector3 target=walker.Swimming?swimmingPosition:restingPosition;
            if(!walker.Swimming)target+=new Vector3(Mathf.Sin(phase)*.5f,Mathf.Sin(phase*2),0)*swayAmount*Mathf.Clamp01(speed/3.4f);
            model.localPosition=Vector3.Lerp(model.localPosition,target,1-Mathf.Exp(-12*Time.deltaTime));
            // Retarget only the arm roots of downloaded full-body locomotion clips.
            // Elbow bends, wrist poses and finger animation remain from the source asset.
            // Shoulder cuts stay below the viewport while the original stroke extends/retracts.
            Vector3 groundAim=speed<=walker.walkSpeed
                ?Vector3.Lerp(restingAim,walkingAim,Mathf.InverseLerp(.1f,walker.walkSpeed,speed))
                :Vector3.Lerp(walkingAim,movingAim,Mathf.InverseLerp(walker.walkSpeed,walker.runSpeed,speed));
            Vector3 aim=walker.Swimming?swimmingAim:walker.Controller&&walker.Controller.isGrounded?groundAim:airborneAim;
            float relaxedGround=groundPoseWeight;
            FrameArm(leftArm,leftHand,-1,Vector3.Lerp(aim,carryingAim,carryWeight),relaxedGround*(1-carryWeight));
            FrameArm(rightArm,rightHand,1,aim,relaxedGround);
        }

        void FrameArm(Transform arm,Transform hand,float side,Vector3 aim,float relaxedGround)
        {
            if(!arm||!hand)return;
            Vector3 originalDirection=hand.position-arm.position;
            // A fixed aim alone cancels the imported forward/backward arm swing.
            // Map that displacement into the visible wrist arc while preserving elbow and wrist poses.
            var source=transform.InverseTransformVector(originalDirection);
            if(!walker.Swimming){aim.y+=source.z*Mathf.Lerp(.4f,importedSwing,relaxedGround);aim.z+=source.z*.12f;}
            Vector3 shoulder=shoulderAnchor;shoulder.x*=side;aim.x*=side;
            arm.position=transform.TransformPoint(shoulder);
            Quaternion target=Quaternion.FromToRotation(originalDirection,transform.TransformPoint(aim)-arm.position)*arm.rotation;
            arm.rotation=target;
            if(relaxedGround>0)
                hand.rotation=Quaternion.AngleAxis(side*relaxedWristRoll*relaxedGround,(hand.position-hand.parent.position).normalized)*hand.rotation;
        }
    }
}
