using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    /// <summary>An optional authored fall zone. Recovery moves only the player and preserves world mechanisms.</summary>
    [DisallowMultipleComponent]
    public sealed class FallRecoveryRegion : MonoBehaviour
    {
        public CourtyardWalker walker;
        public Transform safePoint;
        public Vector3 center = Vector3.zero;
        public Vector3 size = new Vector3(12, 8, 12);
        public int RecoveryCount { get; private set; }

        readonly FallRecoveryState state = new FallRecoveryState();

        void LateUpdate() => EvaluateNow();

        public bool EvaluateNow()
        {
            if (!isActiveAndEnabled || !walker || !walker.isActiveAndEnabled || !safePoint
                || !walker.Controller || !walker.Controller.enabled) return false;
            Vector3 local = transform.InverseTransformPoint(walker.transform.position);
            if (!state.ShouldRecover(FallRecoveryState.ContainsLocal(local, center, size))) return false;
            walker.RespawnAt(safePoint.position, safePoint.eulerAngles.y);
            RecoveryCount++;
            return true;
        }
    }

    // Pure entry policy: a destination accidentally authored inside a region cannot cause per-frame jitter.
    internal sealed class FallRecoveryState
    {
        bool handledEntry;
        public FallRecoveryState() { }
        public bool ShouldRecover(bool inside)
        {
            if (!inside) { handledEntry = false; return false; }
            if (handledEntry) return false;
            handledEntry = true;
            return true;
        }

        public static bool ContainsLocal(Vector3 point, Vector3 center, Vector3 size)
        {
            return System.Math.Abs(point.x - center.x) <= size.x * .5f
                && System.Math.Abs(point.y - center.y) <= size.y * .5f
                && System.Math.Abs(point.z - center.z) <= size.z * .5f;
        }
    }
}
