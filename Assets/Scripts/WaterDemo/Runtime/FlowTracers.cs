using Courtyard.Water;
using UnityEngine;
using UnityEngine.Rendering;

namespace WaterCourtyard
{
    [RequireComponent(typeof(MeshFilter),typeof(MeshRenderer))]
    public sealed class FlowTracers : MonoBehaviour
    {
        public WaterVolume water;
        public Material material;
        const int Count=160;
        Vector3[] positions,vertices;int[] indices;Mesh mesh;
        System.Random random=new System.Random(7321);
        void Start()
        {
            positions=new Vector3[Count];vertices=new Vector3[Count*3];indices=new int[Count*3];for(int i=0;i<indices.Length;i++)indices[i]=i;
            mesh=new Mesh{name="Advected flow tracers"};mesh.MarkDynamic();mesh.vertices=vertices;mesh.triangles=indices;
            GetComponent<MeshFilter>().sharedMesh=mesh;var r=GetComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;
            for(int i=0;i<Count;i++)Reseed(i);
        }
        void Reseed(int i){positions[i]=water.transform.position+new Vector3(((float)random.NextDouble()-.5f)*water.sizeX*.98f,0,((float)random.NextDouble()-.5f)*water.sizeZ*.98f);}
        void LateUpdate()
        {
            if(mesh==null)return;
            for(int i=0;i<Count;i++)
            {
                Vector3 p=positions[i];bool wet=water.Sample(p,out float height,out Vector3 flow,out float depth);
                if(!wet||Vector2.Distance(new Vector2(p.x,p.z),water.drainPosition)<.65f&&water.Gate.IsOpen){Reseed(i);p=positions[i];water.Sample(p,out height,out flow,out depth);}
                p+=Vector3.ClampMagnitude(flow,4)*Time.deltaTime;p.y=height+.014f;positions[i]=p;
                float visible=(water.Gate.IsOpen||water.showFlow)&&depth>.025f?1:0;
                Vector3 dir=flow.sqrMagnitude>.0001f?flow.normalized:Vector3.forward;
                float length=visible*Mathf.Clamp(flow.magnitude*.18f,.035f,.26f),width=visible*.012f;
                Vector3 side=Vector3.Cross(Vector3.up,dir)*width;
                vertices[i*3]=p+dir*length;vertices[i*3+1]=p-dir*length+side;vertices[i*3+2]=p-dir*length-side;
            }
            mesh.vertices=vertices;mesh.RecalculateBounds();
        }
        void OnDestroy(){if(mesh)Destroy(mesh);}
    }
}
