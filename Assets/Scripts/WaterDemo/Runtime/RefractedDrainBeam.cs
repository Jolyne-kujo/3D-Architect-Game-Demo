using Courtyard.Water;
using UnityEngine;

namespace WaterCourtyard
{
    public sealed class RefractedDrainBeam : MonoBehaviour
    {
        public WaterVolume water;
        public Transform source,receiver;
        public LineRenderer airLine,waterLine;
        public Renderer receiverLens;
        public Light receiverLight;
        public Vector3 incidentDirection=new Vector3(0,-.7f,.7141428f);
        public bool powered;
        public float receiverRadius=.38f;
        public Vector3 Entry { get; private set; }
        public Vector3 Hit { get; private set; }
        public bool HitsReceiver { get; private set; }
        MaterialPropertyBlock properties;
        void Awake(){properties=new MaterialPropertyBlock();}
        void Update()
        {
            Trace();water.Illuminate(powered&&HitsReceiver,Time.deltaTime);
            airLine.enabled=powered;waterLine.enabled=powered;
            if(powered){airLine.SetPosition(0,source.position);airLine.SetPosition(1,Entry);waterLine.SetPosition(0,Entry);waterLine.SetPosition(1,Hit);}
            Color c=water.Gate.IsOpen?new Color(.24f,1,.67f):(HitsReceiver&&powered?new Color(1,.72f,.2f):new Color(.11f,.28f,.3f));
            properties.SetColor("_BaseColor",c);properties.SetColor("_EmissionColor",c*(water.Gate.IsOpen?1.3f:.4f));receiverLens.SetPropertyBlock(properties);
            if(receiverLight){receiverLight.color=c;receiverLight.intensity=water.Gate.IsOpen?4:1;}
        }
        public void Trace()
        {
            Vector3 ray=incidentDirection.normalized;float floor=receiver.position.y;
            float enter=(water.Level-source.position.y)/ray.y;
            Vector3 point=source.position+ray*Mathf.Max(0,enter);
            // Solve intersection with the simulated, non-planar free surface.
            bool wet=false;
            for(int i=0;i<6;i++){wet=water.Sample(point,out float height,out _,out _);if(!wet)break;enter=(height-source.position.y)/ray.y;point=source.position+ray*Mathf.Max(0,enter);}
            Entry=point;
            Vector3 inside=ray;
            if(wet&&point.y>floor)
            {
                Vector3 normal=water.SurfaceNormal(point);float eta=1/Mathf.Max(1,water.refractiveIndex),cos=-Vector3.Dot(normal,ray);float k=1-eta*eta*(1-cos*cos);
                inside=k>=0?eta*ray+(eta*cos-Mathf.Sqrt(k))*normal:Vector3.Reflect(ray,normal);
            }
            else Entry=source.position;
            float distance=(floor-Entry.y)/inside.y;
            Hit=Entry+inside*Mathf.Max(0,distance);
            // Receiver and displayed beam share this exact endpoint, independent of scene lighting.
            HitsReceiver=Vector2.Distance(new Vector2(Hit.x,Hit.z),new Vector2(receiver.position.x,receiver.position.z))<receiverRadius;
        }
    }
}
