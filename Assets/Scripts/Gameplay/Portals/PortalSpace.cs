using UnityEngine;

namespace CoastalTemple.Portals
{
    /// <summary>The same affine map is used for rays, velocity, travellers and virtual cameras.</summary>
    public static class PortalSpace
    {
        static readonly Matrix4x4 HalfTurn = Matrix4x4.Rotate(Quaternion.Euler(0, 180, 0));
        public static Matrix4x4 Mapping(Transform entrance, Transform exit) =>
            exit.localToWorldMatrix * HalfTurn * entrance.worldToLocalMatrix;

        public static bool IsUsable(Transform value)
        {
            var scale = value.lossyScale;
            return scale.x > .0001f && scale.y > .0001f && scale.z > .0001f &&
                   value.localToWorldMatrix.determinant > .00000001f;
        }

        public static Vector3 Normal(Transform plane) =>
            plane.worldToLocalMatrix.transpose.MultiplyVector(Vector3.forward).normalized;

        public static Quaternion MapRotation(Matrix4x4 mapping, Quaternion rotation)
        {
            Vector3 forward = mapping.MultiplyVector(rotation * Vector3.forward).normalized;
            Vector3 up = mapping.MultiplyVector(rotation * Vector3.up).normalized;
            // A Transform cannot store shear. Orthonormalize orientation while preserving mapped forward.
            up = Vector3.ProjectOnPlane(up, forward).normalized;
            return Quaternion.LookRotation(forward, up);
        }

        public static bool Crosses(Transform plane, Vector2 aperture, Vector3 previous,
            Vector3 current, out float fraction, out Vector3 intersection, bool twoSided=false)
        {
            fraction = 0; intersection = current;
            Vector3 a = plane.InverseTransformPoint(previous), b = plane.InverseTransformPoint(current);
            bool front=a.z>0&&b.z<=0;
            bool back=twoSided&&a.z<0&&b.z>=0;
            if ((!front&&!back)||Mathf.Abs(a.z-b.z)<.000001f) return false;
            fraction = a.z / (a.z - b.z);
            Vector3 local = Vector3.LerpUnclamped(a, b, fraction);
            if (Mathf.Abs(local.x) > aperture.x * .5f || Mathf.Abs(local.y) > aperture.y * .5f) return false;
            intersection = Vector3.LerpUnclamped(previous, current, fraction);
            return true;
        }
    }
}
