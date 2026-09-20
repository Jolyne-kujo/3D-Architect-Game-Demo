using UnityEngine;

namespace WaterCourtyard
{
    /// <summary>Passive stair geometry for mantle exclusion and visual foot placement. Does not drive speed or animation.</summary>
    [DisallowMultipleComponent]
    public sealed class CourtyardStaircase : MonoBehaviour
    {
        [Min(.1f)] public float run=7.7f;
        [Min(.1f)] public float rise=4.2f;
        [Min(.1f)] public float width=2.2f;
        [HideInInspector] public float movementSpeed=2; // Legacy scene serialization only.
        [HideInInspector] public float animationReferenceSpeed=2;
        Transform[] treads;
        public bool TryTreadHeight(Vector3 worldPoint,out float height)
        {
            if(treads==null)
            {
                var art=transform.Find("Editable visual steps");
                treads=new Transform[art?art.childCount:0];
                for(int i=0;i<treads.Length;i++)treads[i]=art.GetChild(i);
            }
            height=float.NegativeInfinity;
            foreach(var tread in treads)
            {
                if(!tread||!tread.gameObject.activeInHierarchy)continue;
                Vector3 p=tread.InverseTransformPoint(worldPoint);
                if(Mathf.Abs(p.x)>.5f||Mathf.Abs(p.z)>.5f)continue;
                height=Mathf.Max(height,tread.TransformPoint(new Vector3(p.x,.5f,p.z)).y);
            }
            return !float.IsNegativeInfinity(height);
        }
        public Vector3 UpDirection => Vector3.ProjectOnPlane(transform.TransformVector(Vector3.forward),Vector3.up).normalized;
        public bool IsFlight(Vector3 point)
        {
            var local=transform.InverseTransformPoint(point);
            return local.z>.03f&&local.z<run-.03f;
        }
        void OnDrawGizmosSelected()
        {
            var old=Gizmos.matrix;Gizmos.matrix=transform.localToWorldMatrix;Gizmos.color=Color.yellow;
            Gizmos.DrawLine(Vector3.zero,new Vector3(0,rise,run));Gizmos.matrix=old;
        }
    }
}
