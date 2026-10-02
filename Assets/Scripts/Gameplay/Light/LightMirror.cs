using UnityEngine;

namespace CoastalTemple.LightPuzzles
{
    /// <summary>A finite, two-sided reflector. Local XY is the face; local Z is its normal.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class LightMirror : MonoBehaviour
    {
        public BoxCollider face;
        void Awake() { if (!face) face = GetComponent<BoxCollider>(); }
        void OnEnable() { Awake(); LightPuzzleWorld.Register(this); }
        void OnDisable() => LightPuzzleWorld.Unregister(this);
        internal bool OwnsCollider(Collider value) => value == face;
        internal bool Intersect(Vector3 origin, Vector3 direction, float maximum, out float distance)
        {
            distance = 0;
            if (!face) return false;
            var basis = face.transform;
            var localOrigin = basis.InverseTransformPoint(origin) - face.center;
            var localDirection = basis.InverseTransformVector(direction);
            if (Mathf.Abs(localDirection.z) < .000001f) return false;
            distance = -localOrigin.z / localDirection.z;
            if (distance < .001f || distance > maximum) return false;
            var point = localOrigin + localDirection * distance;
            return Mathf.Abs(point.x) <= face.size.x * .5f && Mathf.Abs(point.y) <= face.size.y * .5f;
        }
        internal Vector3 Reflect(Vector3 incoming)
        {
            // Correct for transformed parents, including nonuniform scale.
            var normal = face.transform.worldToLocalMatrix.transpose.MultiplyVector(Vector3.forward).normalized;
            return Vector3.Reflect(incoming, normal).normalized;
        }
    }
}
