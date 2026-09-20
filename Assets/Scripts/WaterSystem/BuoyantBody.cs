using UnityEngine;

namespace Courtyard.Water
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class BuoyantBody : MonoBehaviour
    {
        [Tooltip("Optional. Leave empty to discover water at each buoyancy sample.")]
        public WaterVolume water;
        public Vector3 displacementSize=new Vector3(2,.65f,2);
        [Range(0,1)] public float sampleInset=.38f;
        public float waterDensity=1000, linearWaterDrag=2.2f;
        [Tooltip("Read density from the sampled WaterVolume; disable to use the legacy Water Density override.")]
        public bool useWaterDensity=true;
        public bool guided;
        Rigidbody body;Vector3 start;Quaternion rotation;
        public float Submersion { get; private set; }
        void Awake(){body=GetComponent<Rigidbody>();start=transform.position;rotation=transform.rotation;body.interpolation=RigidbodyInterpolation.Interpolate;if(guided)body.constraints=RigidbodyConstraints.FreezeRotation|RigidbodyConstraints.FreezePositionX|RigidbodyConstraints.FreezePositionZ;}
        public void ResetBody(){body.position=start;body.rotation=rotation;body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        void FixedUpdate()
        {
            Submersion=0;
            Vector3 x=transform.TransformVector(Vector3.right*displacementSize.x);
            Vector3 y=transform.TransformVector(Vector3.up*displacementSize.y);
            Vector3 z=transform.TransformVector(Vector3.forward*displacementSize.z);
            float volume=Mathf.Abs(Vector3.Dot(x,Vector3.Cross(y,z)));
            float span=Mathf.Max(.001f,Mathf.Abs(y.y)+(Mathf.Abs(x.y)+Mathf.Abs(z.y))*.5f);
            for(int i=0;i<4;i++)
            {
                Vector3 p=transform.TransformPoint(new Vector3((i%2==0?-1:1)*displacementSize.x*sampleInset,0,(i<2?-1:1)*displacementSize.z*sampleInset));
                WaterVolume sampled=water;float surface;Vector3 flow;
                if(sampled){if(!WaterVolume.SampleColumn(sampled,p,span*.5f,out surface,out flow,out _))continue;}
                else sampled=WaterVolume.FindAt(p,gameObject.scene,out surface,out flow,out _,span*.5f);
                if(!sampled)continue;
                float fraction=HydroMath.SubmergedFraction(p.y,span,surface);Submersion+=fraction*.25f;
                float density=useWaterDensity?sampled.density:waterDensity;
                Vector3 force=Vector3.up*(Mathf.Max(1,density)*Physics.gravity.magnitude*volume*.25f*fraction);
                force+=(flow-body.GetPointVelocity(p))*(body.mass*.25f*linearWaterDrag*fraction);
                body.AddForceAtPosition(force,p,ForceMode.Force);
            }
        }
    }
}
