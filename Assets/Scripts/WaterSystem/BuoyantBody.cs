using UnityEngine;

namespace Courtyard.Water
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BuoyantBody : MonoBehaviour
    {
        public WaterVolume water;
        public Vector3 displacementSize=new Vector3(2,.65f,2);
        [Range(0,1)] public float sampleInset=.38f;
        public float waterDensity=1000, linearWaterDrag=2.2f;
        public bool guided;
        Rigidbody body;Vector3 start;Quaternion rotation;
        public float Submersion { get; private set; }
        void Awake(){body=GetComponent<Rigidbody>();start=transform.position;rotation=transform.rotation;body.interpolation=RigidbodyInterpolation.Interpolate;if(guided)body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ;}
        public void ResetBody(){body.position=start;body.rotation=rotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        void FixedUpdate()
        {
            if(!water)return;Submersion=0;float volume=displacementSize.x*displacementSize.y*displacementSize.z;
            for(int i=0;i<4;i++)
            {
                Vector3 p=transform.TransformPoint(new Vector3((i%2==0?-1:1)*displacementSize.x*sampleInset,0,(i<2?-1:1)*displacementSize.z*sampleInset));
                if(!water.Sample(p,out float surface,out Vector3 flow,out _))continue;
                float fraction=HydroMath.SubmergedFraction(p.y,displacementSize.y,surface);Submersion+=fraction*.25f;
                Vector3 force=Vector3.up*(waterDensity*Physics.gravity.magnitude*volume*.25f*fraction);
                force+=(flow-body.GetPointVelocity(p))*(body.mass*.25f*linearWaterDrag*fraction);
                body.AddForceAtPosition(force,p,ForceMode.Force);
            }
        }
    }
}
