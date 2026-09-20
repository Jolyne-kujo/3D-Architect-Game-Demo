using Courtyard.Water;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterCourtyard
{
    // Kept beside the shared walker so the standalone courtyard has no dependency on CoastalTemple.
    public static class CourtyardSwimMotion
    {
        public const float SurfaceDraft = 1.4f;

        public static bool ShouldSwim(bool hasWater, float feet, float surface, float depth, bool wasSwimming)
        {
            // Retain swim mode as the horizontal animation raises the capsule root.
            // Shallow supported feet or leaving water end swimming in the walker.
            return hasWater && depth > 1.28f && surface - feet > (wasSwimming ? .12f : 1.18f);
        }

        public static float VerticalDisplacement(float feet, float surface, ref float velocity, float input, float seconds, float surfaceDraft = SurfaceDraft)
        {
            if (seconds <= 0) return 0;
            float error = surface - surfaceDraft - feet;
            float target = input < -.1f ? -1.8f : input > .1f && error > 0
                ? System.Math.Min(1.8f, error * 6)
                : System.Math.Max(-1.1f, System.Math.Min(1.5f, error * 3));
            float change = 5.5f * seconds;
            velocity += System.Math.Max(-change, System.Math.Min(change, target - velocity));
            float displacement = velocity * seconds;
            // Only bound upward travel. Entering water from above or falling water levels must not teleport the body down.
            if (displacement > 0)
            {
                displacement = System.Math.Min(displacement, System.Math.Max(0, error));
                velocity = displacement / seconds;
            }
            return displacement;
        }
    }

    [RequireComponent(typeof(CharacterController))]
    public sealed class CourtyardWalker : MonoBehaviour
    {
        public WaterVolume water;
        public WaterVolume[] additionalWaters=System.Array.Empty<WaterVolume>();
        public Transform eye;
        public bool active;
        [Tooltip("Overview can stop player input while retaining gravity and moving-platform contact.")]
        public bool simulateWhileInactive;
        [Tooltip("First-person yaw turns the body; third-person orbit leaves heading to movement.")]
        public bool rotateBodyWithLook = true;
        [Min(0)] public float movementTurnSpeed = 720;
        [Header("Horizontal movement / metres per second")]
        [Min(0)] public float walkSpeed=3.4f;
        [Min(0)] public float runSpeed=5.3f;
        [Tooltip("Ground acceleration in metres per second squared.")]
        [Min(0)] public float groundAcceleration=22;
        [Tooltip("Ground braking in metres per second squared; does not apply in the air.")]
        [Min(0)] public float groundBraking=28;
        [Tooltip("Maximum change of horizontal velocity while steering in the air, in metres per second squared.")]
        [Min(0)] public float airAcceleration=4;
        [Tooltip("Fractional horizontal damping per second when airborne with no input. Zero preserves momentum exactly.")]
        [Min(0)] public float airDrag=.1f;
        [Min(0)] public float swimAcceleration=7;
        [Tooltip("Maximum water height above supported feet for standing/wading. Deeper water wins over floor contact.")]
        [Range(.3f,1.4f)] public float maximumWadingDepth=1.1f;
        [Min(0)] public float jumpSpeed=4.6f;
        public float JumpHeight => jumpSpeed*jumpSpeed/(2*Mathf.Max(.1f,Mathf.Abs(Physics.gravity.y)));
        public float WaterSurface { get; private set; }
        public bool Diving { get; private set; }
        public float SurfaceDraft => surfaceSwimmer ? surfaceSwimmer.CurrentDraft : CourtyardSwimMotion.SurfaceDraft;
        public bool NearWaterSurface => Swimming&&!Diving&&transform.position.y>=WaterSurface-SurfaceDraft-.3f;
        public CourtyardStaircase Staircase { get; private set; }
        public int StairDirection => Grounded&&Staircase&&PlanarVelocity.sqrMagnitude>.02f
            ? (Vector3.Dot(PlanarVelocity,Staircase.UpDirection)>.1f?1:Vector3.Dot(PlanarVelocity,Staircase.UpDirection)<-.1f?-1:0) : 0;
        public bool Swimming { get; private set; }
        public bool Grounded { get; private set; }
        public CourtyardLedgeClimb Climber { get; private set; }
        public bool Climbing => Climber && Climber.IsClimbing;
        public CharacterController Controller { get; private set; }
        public Vector3 PlanarVelocity { get; private set; }
        public float VerticalSpeed => vertical;
        public float LookYaw => yaw;
        public float LookPitch => pitch;
        public Vector3 WorldVelocity => horizontalVelocity + Vector3.up * vertical;
        float yaw,pitch,vertical,disturbTime,startYaw,landStepOffset,waterJumpGrace;Vector3 start;
        readonly CourtyardCharacterQueries contacts = new CourtyardCharacterQueries();
        readonly CourtyardStepGuard stepGuard = new CourtyardStepGuard();
        CourtyardSurfaceSwimmer surfaceSwimmer;
        Rigidbody groundBody;Vector3 groundPosition,horizontalVelocity;
        void Awake(){Controller=GetComponent<CharacterController>();Controller.minMoveDistance=0;Climber=GetComponent<CourtyardLedgeClimb>();surfaceSwimmer=GetComponent<CourtyardSurfaceSwimmer>();landStepOffset=Controller.stepOffset;start=transform.position;startYaw=yaw=transform.eulerAngles.y;}
        public void ResetPosition() => RespawnAt(start, startYaw);
        // Matrix supplied by the portal package avoids an assembly dependency from this shared walker.
        public void WarpThroughPortal(Vector3 position, Quaternion rotation, Matrix4x4 mapping)
        {
            if(Climber)Climber.Cancel();Grounded=false;waterJumpGrace=0;
            if(!Controller)Controller=GetComponent<CharacterController>();
            Vector3 velocity=mapping.MultiplyVector(WorldVelocity);
            Vector3 looking=mapping.MultiplyVector(Quaternion.Euler(pitch,yaw,0)*Vector3.forward).normalized;
            yaw=Mathf.Atan2(looking.x,looking.z)*Mathf.Rad2Deg;
            pitch=Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(looking.y,-1,1))*Mathf.Rad2Deg,-85,85);
            bool wasEnabled=Controller&&Controller.enabled;
            if(wasEnabled)Controller.enabled=false;
            // This walker has world-Y gravity. Rigid-body travellers retain the complete mapped roll.
            Vector3 heading=Vector3.ProjectOnPlane(rotation*Vector3.forward,Vector3.up);
            transform.SetPositionAndRotation(position,heading.sqrMagnitude>.00001f?Quaternion.LookRotation(heading):Quaternion.Euler(0,yaw,0));
            if(eye)eye.localRotation=Quaternion.Euler(pitch,0,0);
            if(wasEnabled)Controller.enabled=true;
            horizontalVelocity=new Vector3(velocity.x,0,velocity.z);vertical=velocity.y;
            PlanarVelocity=horizontalVelocity;groundBody=null;groundPosition=Vector3.zero;Swimming=false;
        }
        public void RespawnAt(Vector3 position, float yaw)
        {
            Staircase=null;Diving=false;if(surfaceSwimmer)surfaceSwimmer.ResetDraft();
            if(Climber)Climber.Cancel();Grounded=false;waterJumpGrace=0;
            if(!Controller){Controller=GetComponent<CharacterController>();if(Controller)landStepOffset=Controller.stepOffset;}
            if(Controller)Controller.enabled=false;
            transform.SetPositionAndRotation(position,Quaternion.Euler(0,yaw,0));
            if(eye)eye.localRotation=Quaternion.identity;
            if(Controller){Controller.enabled=true;Controller.stepOffset=landStepOffset;}
            this.yaw=yaw;pitch=0;vertical=0;Swimming=false;groundBody=null;groundPosition=Vector3.zero;PlanarVelocity=Vector3.zero;horizontalVelocity=Vector3.zero;disturbTime=0;
        }
        public WaterVolume SampleWater(Vector3 position,out float surface,out Vector3 flow,out float depth)
        {
            surface=0;flow=Vector3.zero;depth=0;
            WaterVolume selected=null;
            if(TryEligibleWater(water,position,out float primarySurface,out Vector3 primaryFlow,out float primaryDepth))
            {
                selected=water;surface=primarySurface;flow=primaryFlow;depth=primaryDepth;
            }
            if(additionalWaters!=null)for(int i=0;i<additionalWaters.Length;i++)
            {
                var candidate=additionalWaters[i];
                if(candidate==water||!TryEligibleWater(candidate,position,out float candidateSurface,out Vector3 candidateFlow,out float candidateDepth))continue;
                // Highest occupied basin wins. Equal surfaces keep the explicit primary/earlier sample stable.
                if(selected&&candidateSurface<=surface+.0001f)continue;
                selected=candidate;surface=candidateSurface;flow=candidateFlow;depth=candidateDepth;
            }
            return selected;
        }
        static bool TryEligibleWater(WaterVolume candidate,Vector3 position,out float surface,out Vector3 flow,out float depth)
        {
            surface=0;flow=Vector3.zero;depth=0;
            if(!candidate||!candidate.isActiveAndEnabled||!candidate.Sample(position,out surface,out flow,out depth))return false;
            // A raised basin must not capture a swimmer travelling under its physical floor.
            return position.y>=surface-depth-.05f;
        }
        public void SetActive(bool value){active=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;}
        void Update()
        {
            if(!active){if(simulateWhileInactive)SimulateMovement(Vector2.zero,false,false,0,Time.deltaTime);return;}var kb=Keyboard.current;var mouse=Mouse.current;if(kb==null)return;
            if(mouse!=null)ApplyLook(mouse.delta.ReadValue()*.08f);
            Vector2 input=new Vector2((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0));input=Vector2.ClampMagnitude(input,1);
            SimulateMovement(input,kb.leftShiftKey.isPressed,kb.spaceKey.wasPressedThisFrame,(kb.spaceKey.isPressed?1:0)-(kb.leftCtrlKey.isPressed?1:0),Time.deltaTime);
        }
        public void ApplyLook(Vector2 delta)
        {
            yaw+=delta.x;pitch=Mathf.Clamp(pitch-delta.y,-85,85);
            if(rotateBodyWithLook)transform.rotation=Quaternion.Euler(0,yaw,0);
            if(eye)eye.localRotation=Quaternion.Euler(pitch,0,0);
        }
        // Shared by live input and scene-level movement checks. Input values are intent, not world displacements.
        public void SimulateMovement(Vector2 input,bool running,bool jumpPressed,float swimInput,float seconds)
        {
            if(!Controller||!Controller.enabled||seconds<=0)return;
            seconds=Mathf.Min(seconds,.05f);input=Vector2.ClampMagnitude(input,1);
            if(!Climber)Climber=GetComponent<CourtyardLedgeClimb>();
            if(!surfaceSwimmer)surfaceSwimmer=GetComponent<CourtyardSurfaceSwimmer>();
            if(Climbing)
            {
                Vector3 climbBefore=transform.position;
                Climber.Step(seconds);
                PlanarVelocity=Vector3.ProjectOnPlane(transform.position-climbBefore,Vector3.up)/seconds;
                horizontalVelocity=Vector3.zero;vertical=0;Swimming=false;groundBody=null;
                Grounded=!Climbing&&contacts.FindGround(Controller,out _);
                return;
            }
            // Carry first so the foot probe sees this frame's moving support.
            if(groundBody){Vector3 carry=groundBody.position-groundPosition;if(carry.magnitude<1)Controller.Move(carry);groundBody=null;}
            RaycastHit ground=default;
            Grounded=vertical<=.1f&&(contacts.FindGround(Controller,out ground)||Controller.isGrounded);
            UpdateStairs(ground);
            waterJumpGrace=Mathf.Max(0,waterJumpGrace-seconds);
            // Input describes a desired velocity, not an instantaneous replacement for momentum.
            // This vector stays in world space so turning the camera cannot rotate an airborne trajectory.
            var inputFrame=Quaternion.Euler(0,yaw,0);
            Vector3 target=inputFrame*new Vector3(input.x,0,input.y)*(running?runSpeed:walkSpeed);
            var currentWater=SampleWater(transform.position+Vector3.up*.8f,out float surface,out Vector3 flow,out float depth);
            WaterSurface=surface;Diving=swimInput<-.1f;
            bool wasSwimming=Swimming;
            bool canWade=Grounded&&(!currentWater||WithinWadingDepth(surface));
            Swimming=!canWade&&waterJumpGrace<=0&&CourtyardSwimMotion.ShouldSwim(currentWater,transform.position.y,surface,depth,wasSwimming);
            if(Swimming)Grounded=false;
            bool shallowStep=Swimming&&NearWaterSurface&&contacts.HasLowStep(Controller,target,landStepOffset);
            Controller.stepOffset=Swimming&&!shallowStep?0:landStepOffset;
            if(Climber&&!(jumpPressed&&Grounded)&&Climber.TryBegin(target,seconds))
            {
                horizontalVelocity=Vector3.zero;vertical=0;Swimming=false;Grounded=false;groundBody=null;
                return;
            }
            float verticalDisplacement;
            if(Swimming)
            {
                target*=.55f;target+=Vector3.ClampMagnitude(flow,.5f);target.y=0;
                horizontalVelocity=Vector3.MoveTowards(horizontalVelocity,target,swimAcceleration*seconds);
                if(!wasSwimming)vertical=Mathf.Clamp(vertical,-2.4f,1.8f);
                verticalDisplacement=CourtyardSwimMotion.VerticalDisplacement(transform.position.y,surface,ref vertical,swimInput,seconds,
                    SurfaceDraft);
                if(surfaceSwimmer)verticalDisplacement+=surfaceSwimmer.ConsumePoseCorrection();
            }
            else
            {
                if(Grounded)
                    horizontalVelocity=Vector3.MoveTowards(horizontalVelocity,target,(input.sqrMagnitude>.0001f?groundAcceleration:groundBraking)*seconds);
                else if(input.sqrMagnitude>.0001f)
                    horizontalVelocity=Vector3.MoveTowards(horizontalVelocity,target,airAcceleration*seconds);
                else
                    horizontalVelocity*=Mathf.Exp(-airDrag*seconds);
                // Follow descending ramps at sprint speed without briefly switching to an airborne pose.
                if(Grounded&&vertical<=0)vertical=ground.collider
                    ?-Mathf.Max(2,horizontalVelocity.magnitude*Mathf.Sqrt(Mathf.Max(0,1-ground.normal.y*ground.normal.y))/Mathf.Max(.1f,ground.normal.y)):-2;
                if(jumpPressed&&Grounded){vertical=jumpSpeed;waterJumpGrace=.5f;Grounded=false;}
                vertical+=Physics.gravity.y*seconds;verticalDisplacement=vertical*seconds;
            }
            Vector3 before=transform.position;
            Vector3 horizontalDisplacement=horizontalVelocity*seconds;
            if(Grounded&&!Swimming&&vertical<=0)
                horizontalDisplacement=stepGuard.Constrain(Controller,ground,horizontalDisplacement,landStepOffset,ref horizontalVelocity);
            CollisionFlags collision=Controller.Move(horizontalDisplacement+Vector3.up*verticalDisplacement);
            Vector3 displacement=transform.position-before;PlanarVelocity=new Vector3(displacement.x,0,displacement.z)/seconds;
            if(!rotateBodyWithLook&&input.sqrMagnitude>.0001f&&PlanarVelocity.sqrMagnitude>.01f)
                transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(PlanarVelocity),movementTurnSpeed*seconds);
            if((collision&CollisionFlags.Above)!=0&&vertical>0)vertical=0;
            if((collision&CollisionFlags.Below)!=0&&vertical<0)vertical=Swimming?0:-2;
            ground=default;Grounded=vertical<=.1f&&(contacts.FindGround(Controller,out ground)||((collision&CollisionFlags.Below)!=0));
            // Touching a deep stair/seabed does not cancel buoyancy. Stand only when the
            // supported feet reach a shallow enough level to put the standing torso above water.
            if(Swimming&&!WithinWadingDepth(surface))Grounded=false;
            UpdateStairs(ground);
            if(Grounded){Swimming=false;Controller.stepOffset=landStepOffset;}
            if(transform.position.y<-12)ResetPosition();
            if(Swimming&&currentWater&&input.sqrMagnitude>.1f&&Time.time>disturbTime){currentWater.Disturb(transform.position,currentWater.swimmerDisplacement);disturbTime=Time.time+.16f;}
        }
        bool WithinWadingDepth(float surface)
        {
            // Native step contact can leave the capsule feet one skin-width below the tread.
            // Account for that contact margin when deciding whether shallow support can stand.
            return surface-CourtyardCharacterQueries.Feet(Controller).y<=maximumWadingDepth+Controller.skinWidth+.005f;
        }
        void UpdateStairs(RaycastHit ground)
        {
            Staircase=Grounded&&ground.collider?ground.collider.GetComponentInParent<CourtyardStaircase>():null;
            if(Staircase&&!Staircase.IsFlight(ground.point))Staircase=null;
        }
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.normal.y>.6f&&hit.rigidbody){groundBody=hit.rigidbody;groundPosition=groundBody.position;}
            // A wall consumes only momentum into its face; tangential movement can slide along it.
            // Do not retain a hidden push that launches the player if the obstruction disappears.
            if(hit.normal.y<Mathf.Cos(Controller.slopeLimit*Mathf.Deg2Rad))
            {
                // A low tread is an intended step, not a wall. Cancelling its velocity every
                // callback made progress depend on frame time and deadlocked at 240+ FPS.
                if(((Grounded&&vertical<=0)||(Swimming&&NearWaterSurface))&&Controller.stepOffset>0&&contacts.HasLowStep(Controller,horizontalVelocity,landStepOffset))return;
                Vector3 normal=new Vector3(hit.normal.x,0,hit.normal.z).normalized;
                float into=Vector3.Dot(horizontalVelocity,normal);
                if(into<0)horizontalVelocity-=normal*into;
            }
        }
    }
}
