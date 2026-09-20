using UnityEngine;

namespace WaterCourtyard
{
    /// <summary>Stair semantics shared by the movement motor, mantle exclusion and animation driver.</summary>
    [DisallowMultipleComponent]
    public sealed class CourtyardStaircase : MonoBehaviour
    {
        [Min(.1f)] public float run=7.7f;
        [Min(.1f)] public float rise=4.2f;
        [Min(.1f)] public float width=2.2f;
        [Min(.1f)] public float movementSpeed=2;
        [Min(.1f)] public float animationReferenceSpeed=2;
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
