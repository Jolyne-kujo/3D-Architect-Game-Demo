using UnityEngine;
using UnityEngine.SceneManagement;

namespace WaterCourtyard
{
    /// <summary>Bound native capsule auto-stepping by the actual support plane, not its skin-offset feet.</summary>
    public sealed class CourtyardStepGuard
    {
        readonly RaycastHit[] hits=new RaycastHit[24];

        public Vector3 Constrain(CharacterController body,RaycastHit support,Vector3 displacement,float maximumStep,ref Vector3 velocity)
        {
            float distance=displacement.magnitude;
            if(distance<.000001f)return displacement;
            Vector3 feet=CourtyardCharacterQueries.Feet(body),direction=displacement/distance;
            float radius=CourtyardCharacterQueries.Radius(body);
            float supportY=feet.y-body.skinWidth;
            if(support.collider&&support.normal.y>.1f)
                supportY=support.point.y-(support.normal.x*(feet.x-support.point.x)+support.normal.z*(feet.z-support.point.z))/support.normal.y;
            // Probe just above the maximum eligible tread. A valid lower tread is left to
            // native Move and HasLowStep; a steep face above this band cannot lift the capsule.
            const float probeRadius=.006f;
            Vector3 origin=new Vector3(feet.x,supportY+Mathf.Max(0,maximumStep)+probeRadius+.002f,feet.z);
            Vector3 across=Vector3.Cross(Vector3.up,direction)*(radius+body.skinWidth-probeRadius);
            // A continuous thin band covers the capsule width: sparse rays miss narrow posts
            // and the outer crescent while rounding a corner.
            int count=body.gameObject.scene.GetPhysicsScene().CapsuleCast(origin-across,origin+across,probeRadius,direction,hits,
                radius+distance+body.skinWidth+.025f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            if(count==hits.Length){velocity=Vector3.zero;return Vector3.zero;}
            float walkable=Mathf.Cos(body.slopeLimit*Mathf.Deg2Rad);
            for(int i=0;i<count;i++)
            {
                var hit=hits[i];
                if(!hit.collider||hit.collider==body||hit.collider.transform.IsChildOf(body.transform)||hit.normal.y>=walkable)continue;
                Vector3 normal=new Vector3(hit.normal.x,0,hit.normal.z).normalized;
                float into=Vector3.Dot(displacement,normal);
                if(into>=0)continue;
                float clearance=Mathf.Max(0,Vector3.Dot(feet-hit.point,normal)-radius-body.skinWidth);
                if(-into<=clearance)continue;
                // Keep the allowed approach and tangential slide. Never project onto the
                // tilted 3D face: that would introduce an upward component while pushing.
                displacement+=normal*(-clearance-into);
                float speedInto=Vector3.Dot(velocity,normal);
                if(speedInto<0)velocity-=normal*speedInto;
            }
            return displacement;
        }
    }
}
