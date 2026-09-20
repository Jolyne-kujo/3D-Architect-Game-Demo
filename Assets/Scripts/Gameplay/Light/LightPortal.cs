using UnityEngine;
using CoastalTemple.Portals;

namespace CoastalTemple.LightPuzzles
{
    [DisallowMultipleComponent]
    public sealed class LightPortal : MonoBehaviour
    {
        public LightPortal Paired;
        [Tooltip("Full local width and height. Forward points toward incoming light.")]
        public Vector2 Aperture = new Vector2(1.6f, 2.4f);
        public bool AllowEntry = true;
        [Tooltip("Render and traverse from either side of the aperture. Disable only for a deliberately one-sided gate.")]
        public bool TwoSided = true;
        [Min(.001f)] public float ExitOffset = .015f;
        public bool CanTransfer => isActiveAndEnabled && AllowEntry && Paired && Paired.isActiveAndEnabled &&
            PortalSpace.IsUsable(transform) && PortalSpace.IsUsable(Paired.transform);
        public Matrix4x4 TransferMatrix => Paired ? PortalSpace.Mapping(transform, Paired.transform) : Matrix4x4.identity;
        public Vector3 PlaneNormal => PortalSpace.Normal(transform);
        public Vector3 MapPoint(Vector3 point) => TransferMatrix.MultiplyPoint3x4(point);
        public Vector3 MapVector(Vector3 vector) => TransferMatrix.MultiplyVector(vector);
        public Quaternion MapRotation(Quaternion rotation) => PortalSpace.MapRotation(TransferMatrix, rotation);
        void OnEnable() { LightPuzzleWorld.Register(this); }
        void OnDisable() { LightPuzzleWorld.Unregister(this); }

        internal bool Intersect(Vector3 origin, Vector3 direction, float maximum, out float distance)
        {
            distance = 0;
            if (!CanTransfer) return false;
            float denominator = Vector3.Dot(direction, PlaneNormal);
            if (TwoSided ? Mathf.Abs(denominator)<.0001f : denominator>=-.0001f) return false;
            distance = Vector3.Dot(transform.position - origin, PlaneNormal) / denominator;
            if (distance < .0001f || distance > maximum) return false;
            var point = transform.InverseTransformPoint(origin + direction * distance);
            return Mathf.Abs(point.x) <= Aperture.x * .5f && Mathf.Abs(point.y) <= Aperture.y * .5f;
        }

        public bool TryTransfer(Vector3 incomingPoint, Vector3 incomingDirection, out Vector3 exitPoint, out Vector3 exitDirection)
        {
            exitPoint = incomingPoint; exitDirection = incomingDirection;
            float directionAlongNormal=Vector3.Dot(incomingDirection,PlaneNormal);
            if (!CanTransfer || (TwoSided ? Mathf.Abs(directionAlongNormal)<.0001f : directionAlongNormal>=-.0001f)) return false;
            var localPoint = transform.InverseTransformPoint(incomingPoint);
            if (Mathf.Abs(localPoint.x) > Aperture.x * .5f || Mathf.Abs(localPoint.y) > Aperture.y * .5f) return false;
            localPoint.z = 0f;
            var mappedPoint = new Vector3(-localPoint.x, localPoint.y, 0);
            if (Mathf.Abs(mappedPoint.x) > Paired.Aperture.x * .5f || Mathf.Abs(mappedPoint.y) > Paired.Aperture.y * .5f) return false;
            exitPoint = Paired.transform.TransformPoint(mappedPoint);
            exitDirection = MapVector(incomingDirection).normalized;
            exitPoint += exitDirection * ExitOffset;
            return true;
        }

        public bool TryCrossing(Vector3 previous, Vector3 current, out float fraction, out Vector3 point)
        {
            fraction = 0; point = current;
            if (!CanTransfer || !PortalSpace.Crosses(transform, Aperture, previous, current, out fraction, out point, TwoSided)) return false;
            Vector3 atExit = Paired.transform.InverseTransformPoint(MapPoint(point));
            return Mathf.Abs(atExit.x) <= Paired.Aperture.x * .5f && Mathf.Abs(atExit.y) <= Paired.Aperture.y * .5f;
        }
    }
}
