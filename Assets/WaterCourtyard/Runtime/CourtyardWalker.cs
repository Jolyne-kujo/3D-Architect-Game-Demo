using Courtyard.Water;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterCourtyard
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class CourtyardWalker : MonoBehaviour
    {
        public WaterVolume water;
        public WaterVolume[] additionalWaters=System.Array.Empty<WaterVolume>();
        public Transform eye;
        public bool active;
        public bool Swimming { get; private set; }
        public CharacterController Controller { get; private set; }
        float yaw,pitch,vertical,disturbTime,startYaw;Vector3 start;
        Rigidbody groundBody;Vector3 groundPosition;
        void Awake(){Controller=GetComponent<CharacterController>();start=transform.position;startYaw=yaw=transform.eulerAngles.y;}
        public void ResetPosition(){Controller.enabled=false;transform.position=start;transform.rotation=Quaternion.Euler(0,startYaw,0);eye.localRotation=Quaternion.identity;Controller.enabled=true;yaw=startYaw;pitch=0;vertical=0;Swimming=false;groundBody=null;}
        public WaterVolume SampleWater(Vector3 position,out float surface,out Vector3 flow,out float depth)
        {
            surface=0;flow=Vector3.zero;depth=0;
            if(water&&water.isActiveAndEnabled&&water.Sample(position,out surface,out flow,out depth))return water;
            foreach(var candidate in additionalWaters)
                if(candidate&&candidate.isActiveAndEnabled&&candidate.Sample(position,out surface,out flow,out depth))return candidate;
            surface=0;flow=Vector3.zero;depth=0;return null;
        }
        public void SetActive(bool value){active=value;Cursor.lockState=value?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!value;}
        void Update()
        {
            if(!active)return;var kb=Keyboard.current;var mouse=Mouse.current;if(kb==null||mouse==null)return;
            Vector2 look=mouse.delta.ReadValue()*.08f;yaw+=look.x;pitch=Mathf.Clamp(pitch-look.y,-85,85);transform.rotation=Quaternion.Euler(0,yaw,0);eye.localRotation=Quaternion.Euler(pitch,0,0);
            Vector2 input=new Vector2((kb.dKey.isPressed?1:0)-(kb.aKey.isPressed?1:0),(kb.wKey.isPressed?1:0)-(kb.sKey.isPressed?1:0));input=Vector2.ClampMagnitude(input,1);
            Vector3 move=(transform.right*input.x+transform.forward*input.y)*(kb.leftShiftKey.isPressed?5.3f:3.4f);
            var currentWater=SampleWater(transform.position+Vector3.up*.8f,out float surface,out Vector3 flow,out float depth);
            Swimming=depth>.01f&&surface>transform.position.y+1.1f;
            if(Swimming){move*=.55f;move+=Vector3.ClampMagnitude(flow,.5f);float target=(kb.spaceKey.isPressed?1.8f:kb.leftCtrlKey.isPressed?-1.8f:Mathf.Clamp(surface-(transform.position.y+1.5f),-.4f,.8f));vertical=Mathf.MoveTowards(vertical,target,5*Time.deltaTime);}
            else {if(Controller.isGrounded&&vertical<0)vertical=-2;if(kb.spaceKey.wasPressedThisFrame&&Controller.isGrounded)vertical=4.6f;vertical+=Physics.gravity.y*Time.deltaTime;}
            if(groundBody){Vector3 carry=groundBody.position-groundPosition;if(carry.magnitude<1)Controller.Move(carry);groundBody=null;}
            Controller.Move((move+Vector3.up*vertical)*Time.deltaTime);
            if(transform.position.y<-12)ResetPosition();
            if(Swimming&&currentWater&&input.sqrMagnitude>.1f&&Time.time>disturbTime){currentWater.Disturb(transform.position,currentWater.swimmerDisplacement);disturbTime=Time.time+.16f;}
        }
        void OnControllerColliderHit(ControllerColliderHit hit){if(hit.normal.y>.6f&&hit.rigidbody){groundBody=hit.rigidbody;groundPosition=groundBody.position;}}
    }
}
