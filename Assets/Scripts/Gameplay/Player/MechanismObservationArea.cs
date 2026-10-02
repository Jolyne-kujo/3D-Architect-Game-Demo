using UnityEngine;

namespace CoastalTemple.Player
{
    /// <summary>Optional authored framing bounds shared by all controls in a puzzle area.</summary>
    [DisallowMultipleComponent]
    public sealed class MechanismObservationArea : MonoBehaviour
    {
        public Vector3 localCenter = Vector3.up * 2;
        public Vector3 localSize = new Vector3(20, 6, 20);
        public bool fixedYaw = true;
        public float yaw = 180;

        public Bounds WorldBounds
        {
            get
            {
                var bounds=new Bounds(transform.TransformPoint(localCenter),Vector3.zero);
                var extents=localSize*.5f;
                for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
                    bounds.Encapsulate(transform.TransformPoint(localCenter+Vector3.Scale(extents,new Vector3(x,y,z))));
                return bounds;
            }
        }

        void OnDrawGizmosSelected()
        {
            var previous=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;
            Gizmos.color=new Color(.45f,.8f,1,.7f);Gizmos.DrawWireCube(localCenter,localSize);Gizmos.matrix=previous;
        }
    }
}
