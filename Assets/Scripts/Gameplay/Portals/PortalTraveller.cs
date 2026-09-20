using UnityEngine;
using WaterCourtyard;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;

namespace CoastalTemple.Portals
{
    [DisallowMultipleComponent, DefaultExecutionOrder(175)]
    public sealed class PortalTraveller : MonoBehaviour
    {
        [Tooltip("Optional crossing point. Defaults to character centre or rigidbody centre of mass.")]
        public Transform TransitAnchor;
        [Tooltip("Rigid bodies opt in by adding this component. Players are registered automatically.")]
        public bool TransferEnabled = true;
        public int TransferCount { get; private set; }
        public LightPortal LastEntrance { get; private set; }
        CourtyardWalker walker;
        Rigidbody body;
        CharacterController controller;
        CoastalPlayerCamera cameraRig;
        Vector3 previous;
        bool initialized;

        void Awake() => Resolve();
        void Start()
        {
            if (Application.isPlaying && !GetComponent<PortalTravellerVisual>()) gameObject.AddComponent<PortalTravellerVisual>();
        }
        void OnEnable() { Resolve(); ResetTracking(); }
        void Resolve()
        {
            walker = GetComponent<CourtyardWalker>(); body = GetComponent<Rigidbody>();
            controller = GetComponent<CharacterController>(); cameraRig = GetComponent<CoastalPlayerCamera>();
        }
        public Vector3 CrossingPoint => TransitAnchor ? TransitAnchor.position :
            controller ? transform.TransformPoint(controller.center) : body ? body.worldCenterOfMass : transform.position;
        public void ResetTracking() { previous = CrossingPoint; initialized = true; }
        void LateUpdate() => EvaluateNow();

        public bool EvaluateNow()
        {
            if (!TransferEnabled || !initialized) { ResetTracking(); return false; }
            Vector3 current = CrossingPoint;
            LightPortal nearest = null; float first = float.PositiveInfinity;
            foreach (var surface in PortalSurface.Active)
            {
                if (!surface || !surface.TransportTravellers || !surface.isActiveAndEnabled) continue;
                var portal = surface.Portal;
                if (portal && portal.TryCrossing(previous, current, out float fraction, out _) && fraction < first)
                { nearest = portal; first = fraction; }
            }
            if (nearest) WarpThrough(nearest,Mathf.Sign(Vector3.Dot(previous-nearest.transform.position,nearest.PlaneNormal)));
            previous = CrossingPoint;
            return nearest;
        }

        public bool WarpThrough(LightPortal entrance,float entrySide=0)
        {
            if (!TransferEnabled || !entrance || !entrance.CanTransfer) return false;
            Matrix4x4 map = entrance.TransferMatrix;
            Vector3 position = map.MultiplyPoint3x4(transform.position);
            Quaternion rotation = PortalSpace.MapRotation(map, transform.rotation);
            // Preserve the full frame's overshoot. A fixed epsilon would erase fast motion.
            Vector3 reference = map.MultiplyPoint3x4(CrossingPoint);
            float depth = Vector3.Dot(reference - entrance.Paired.transform.position, entrance.Paired.PlaneNormal);
            float exitSide=entrySide==0?(depth<0?-1:1):Mathf.Sign(entrySide);
            if (depth*exitSide < .002f) position += entrance.Paired.PlaneNormal * (exitSide*.002f-depth);
            if (walker) walker.WarpThroughPortal(position, rotation, map);
            else if (body)
            {
                Vector3 velocity = map.MultiplyVector(body.linearVelocity);
                Quaternion turn = rotation * Quaternion.Inverse(body.rotation);
                Vector3 angular = turn * body.angularVelocity;
                body.position = position; body.rotation = rotation;
                transform.SetPositionAndRotation(position, rotation);
                if (!body.isKinematic) { body.linearVelocity = velocity; body.angularVelocity = angular; }
            }
            else
            {
                bool wasEnabled = controller && controller.enabled;
                if (wasEnabled) controller.enabled = false;
                transform.SetPositionAndRotation(position, rotation);
                if (wasEnabled) controller.enabled = true;
            }
            if (cameraRig) cameraRig.WarpThroughPortal(map, entrance, exitSide);
            Physics.SyncTransforms();
            LastEntrance = entrance; TransferCount++; ResetTracking();
            return true;
        }
    }
}
