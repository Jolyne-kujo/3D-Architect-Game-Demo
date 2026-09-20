using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using Courtyard.Water;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    // Gameplay prototype: Snell ray placement is physical; its solid projected steps are a fantasy rule.
    [DisallowMultipleComponent]
    public sealed class WaterMirage : MonoBehaviour
    {
        public WaterVolume water;
        public LightReceiver receiver;
        public Transform source;
        [Tooltip("Authored underwater statue/sample anchor used to seed the local water-surface query.")]
        public Transform sampleObject;
        [Tooltip("Local +Z is the normal of the authored projection plane.")]
        public Transform projectionPlane;
        public Transform projectionMarker;
        public Renderer markerRenderer;
        public Transform target;
        [Min(.01f)] public float targetRadius=.4f;
        public Transform[] solidSteps;
        public Renderer[] stepRenderers;
        public Collider[] stepColliders;
        [Min(.1f)] public float maxDistance=20f;
        [Min(.01f)] public float minimumDepth=.15f;
        [Range(1f,2f)] public float waterRefractiveIndex=1.333f;
        public bool powered=true;
        public Transform aimReference;
        [Tooltip("Exact local pitch/yaw states. Positive pitch aims downward.")]
        public Vector2[] aimAngles={new Vector2(50,0),new Vector2(65,0)};
        public int AimIndex { get; private set; }
        public bool HasProjection { get; private set; }
        public bool IsSolid { get; private set; }
        public Vector3 EntryPoint { get; private set; }
        public Vector3 ProjectedPoint { get; private set; }
        public Vector3 RefractedDirection { get; private set; }
        public string Status { get; private set; }="等待光源";
        float nextEvaluation;
        bool initialized;
        readonly RaycastHit[] obstructionHits=new RaycastHit[32];

        void Awake()=>Initialize();
        public void Initialize()
        {
            if(initialized)return;initialized=true;
            if(!markerRenderer&&projectionMarker)markerRenderer=projectionMarker.GetComponentInChildren<Renderer>();
            if(solidSteps!=null)
            {
                if(stepRenderers==null||stepRenderers.Length==0){var list=new List<Renderer>();foreach(var step in solidSteps)if(step)list.AddRange(step.GetComponentsInChildren<Renderer>());stepRenderers=list.ToArray();}
                if(stepColliders==null||stepColliders.Length==0){var list=new List<Collider>();foreach(var step in solidSteps)if(step)list.AddRange(step.GetComponentsInChildren<Collider>());stepColliders=list.ToArray();}
            }
            Apply(false,false);
        }
        void Update(){if(Time.unscaledTime<nextEvaluation)return;nextEvaluation=Time.unscaledTime+.05f;EvaluateNow();}
        void OnDisable()=>Apply(false,false);
        public void SetAimState(int index)
        {
            if(!source||aimAngles==null||aimAngles.Length==0)return;
            AimIndex=((index%aimAngles.Length)+aimAngles.Length)%aimAngles.Length;
            var angles=aimAngles[AimIndex];source.rotation=(aimReference?aimReference.rotation:Quaternion.identity)*Quaternion.Euler(angles.x,angles.y,0);
            EvaluateNow();
        }
        public void CycleAim()=>SetAimState(AimIndex+1);
        public void SetSourceTarget(Transform value){if(value)SetSourceTarget(value.position);}
        public void SetSourceTarget(Vector3 point)
        {
            if(!source||(point-source.position).sqrMagnitude<.0001f)return;
            source.rotation=Quaternion.LookRotation(point-source.position,Vector3.up);EvaluateNow();
        }
        public bool EvaluateNow()
        {
            Initialize();
            if(!powered||(receiver&&!receiver.IsActive))return Fail("光路尚未供能");
            if(!water||!source||!sampleObject||!projectionPlane||!target)return Fail("等待布置投影组件");
            if(!water.Sample(sampleObject.position,out float surface,out _,out float depth)||depth<minimumDepth)return Fail("水深不足");
            if(!WaterMirageMath.InWater(sampleObject.position.y,surface,depth,minimumDepth))return Fail("样本未浸入水中");
            MirageVector entry=default;double entryDistance=0;
            // Three bounded surface samples follow the incoming ray over gently varying simulated water.
            for(int i=0;i<3;i++)
            {
                if(!WaterMirageMath.EnterWater(ToMath(source.position),ToMath(source.forward),surface,maxDistance,out entry,out entryDistance))return Fail("光源须从水面上方照入");
                if(!water.Sample(ToUnity(entry),out surface,out _,out depth)||depth<minimumDepth)return Fail("入射位置没有足够的水");
            }
            if(!WaterMirageMath.EnterWater(ToMath(source.position),ToMath(source.forward),surface,maxDistance,out entry,out entryDistance))return Fail("无法求得水面交点");
            EntryPoint=ToUnity(entry);
            if(!WaterMirageMath.Refract(ToMath(source.forward),ToMath(water.SurfaceNormal(EntryPoint)),1,waterRefractiveIndex,out var refracted))return Fail("无透射光线");
            if(!WaterMirageMath.IntersectPlane(entry,refracted,ToMath(projectionPlane.position),ToMath(projectionPlane.forward),maxDistance-entryDistance,out var hit,out double projectedDistance))return Fail("折射光线未命中投影墙");
            ProjectedPoint=ToUnity(hit);RefractedDirection=ToUnity(refracted);
            if(!water.Sample(ProjectedPoint,out float hitSurface,out _,out float hitDepth)||!WaterMirageMath.InWater(ProjectedPoint.y,hitSurface,hitDepth,minimumDepth))return Fail("投影点超出单层水体光路");
            if(Blocked(source.position,source.forward,(float)entryDistance)||Blocked(EntryPoint+RefractedDirection*.015f,RefractedDirection,(float)projectedDistance-.03f))return Fail("折射光路受到遮挡");
            if(projectionMarker){projectionMarker.position=ProjectedPoint+projectionPlane.forward*.012f;projectionMarker.rotation=projectionPlane.rotation;}
            bool aligned=(ProjectedPoint-target.position).sqrMagnitude<=targetRadius*targetRadius;
            Status=aligned?"折射投影对齐：幻影台阶实体化":"投影未对齐：调整照射角度";
            Apply(true,aligned);return aligned;
        }
        bool Blocked(Vector3 origin,Vector3 direction,float distance)
        {
            if(distance<=0)return false;
            int count=Physics.RaycastNonAlloc(origin,direction,obstructionHits,distance,~0,QueryTriggerInteraction.Ignore);
            if(count==obstructionHits.Length)return true;
            for(int i=0;i<count;i++)
            {
                var collider=obstructionHits[i].collider;
                if(!collider||collider is CharacterController||collider.transform.IsChildOf(source)||collider.transform.IsChildOf(sampleObject))continue;
                bool ownStep=false;
                if(stepColliders!=null)for(int j=0;j<stepColliders.Length;j++)if(collider==stepColliders[j]){ownStep=true;break;}
                if(!ownStep)return true;
            }
            return false;
        }
        bool Fail(string reason){Status=reason;Apply(false,false);return false;}
        void Apply(bool visible,bool solid)
        {
            HasProjection=visible;IsSolid=solid;
            if(markerRenderer)markerRenderer.enabled=visible;
            if(stepRenderers!=null)for(int i=0;i<stepRenderers.Length;i++)if(stepRenderers[i])stepRenderers[i].enabled=solid;
            if(stepColliders!=null)for(int i=0;i<stepColliders.Length;i++)if(stepColliders[i])stepColliders[i].enabled=solid;
        }
        static MirageVector ToMath(Vector3 value)=>new MirageVector(value.x,value.y,value.z);
        static Vector3 ToUnity(MirageVector value)=>new Vector3((float)value.X,(float)value.Y,(float)value.Z);
    }
}
