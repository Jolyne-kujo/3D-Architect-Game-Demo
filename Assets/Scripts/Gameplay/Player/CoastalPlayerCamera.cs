using UnityEngine;
using WaterCourtyard;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Portals;

namespace CoastalTemple.Player
{
    [DisallowMultipleComponent, DefaultExecutionOrder(150)]
    public sealed class CoastalPlayerCamera : MonoBehaviour
    {
        public CourtyardWalker walker;
        public Camera view;
        public CoastalPlayerAvatar avatar;
        [Tooltip("Stable eye anchor for the optional first-person view, never an animated bone.")]
        public Transform followAnchor;
        [Tooltip("Optional parent for detached overview shots.")]
        public Transform overviewRoot;
        public FirstPersonHands hands;
        public PlayerLantern lantern;
        [Header("Third-person follow")]
        public bool thirdPerson;
        [Min(.5f)] public float distance = 4.2f;
        public float pivotHeight = 1.48f;
        public float shoulderOffset = .35f;
        public float pitchBias = 10;
        [Tooltip("A higher swimming pivot keeps the follow camera above the surface and small floating props.")]
        public float swimmingPivotHeight = 2.1f;
        public float swimmingPitchBias = 15;
        [Min(.04f)] public float collisionRadius = .19f;
        [Min(0)] public float collisionPadding = .08f;
        [Min(.1f)] public float recoverySpeed = 6;
        public LayerMask obstructionLayers = ~0;
        public bool IsOverview { get; private set; }
        public float CurrentDistance { get; private set; }
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly Collider[] overlaps = new Collider[32];
        LightPortal portalBridge;
        float portalBridgeSide=1;

        // Keep an orbit camera on its original side until the boom itself crosses the aperture.
        // The teleported avatar is then seen through the live window, without a one-frame orbit snap.
        public void WarpThroughPortal(Matrix4x4 mapping, LightPortal entrance,float entrySide=1)
        {
            if (!view || IsOverview) return;
            if (!thirdPerson) { SnapToTarget(); return; }
            if (portalBridge && portalBridge.CanTransfer)
            {
                Matrix4x4 prior = portalBridge.TransferMatrix;
                view.transform.SetPositionAndRotation(prior.MultiplyPoint3x4(view.transform.position),
                    PortalSpace.MapRotation(prior, view.transform.rotation));
            }
            portalBridge = null;
            view.transform.SetPositionAndRotation(mapping.MultiplyPoint3x4(view.transform.position),
                PortalSpace.MapRotation(mapping, view.transform.rotation));
            portalBridge = entrance;
            portalBridgeSide=entrySide<0?-1:1;
            ApplyPortalBridge();
        }

        void ApplyPortalBridge()
        {
            if (!portalBridge || !portalBridge.CanTransfer || !thirdPerson) { portalBridge = null; return; }
            var exit = portalBridge.Paired;
            float side = Vector3.Dot(view.transform.position - exit.transform.position, exit.PlaneNormal);
            if (side*portalBridgeSide >= .025f) { portalBridge = null; return; }
            Matrix4x4 inverse = portalBridge.TransferMatrix.inverse;
            view.transform.SetPositionAndRotation(inverse.MultiplyPoint3x4(view.transform.position),
                PortalSpace.MapRotation(inverse, view.transform.rotation));
        }

        void Start()
        {
            if (!walker) walker = GetComponent<CourtyardWalker>();
            if (!lantern && walker) lantern = walker.GetComponent<PlayerLantern>();
            SnapToTarget();
        }

        public void SetOverview(bool value)
        {
            IsOverview = value;
            if (value && view) view.transform.SetParent(overviewRoot, true);
            UpdateAvatarVisibility();
            if (!value) SnapToTarget();
        }

        public void SetThirdPerson(bool value)
        {
            thirdPerson = value;
            if (walker)
            {
                walker.rotateBodyWithLook = !value;
                if (!value) walker.ApplyLook(Vector2.zero);
            }
            UpdateAvatarVisibility();
            if (!IsOverview) SnapToTarget();
        }

        public void SnapToTarget()
        {
            if (!walker || !view || IsOverview) return;
            portalBridge = null;
            walker.rotateBodyWithLook = !thirdPerson;
            if (thirdPerson)
            {
                UpdateFollowCamera(0, true);
                UpdateAvatarVisibility();
                return;
            }
            var eye = walker.eye ? walker.eye : followAnchor;
            if (!eye) return;
            view.transform.SetParent(eye, false);
            view.transform.localPosition = Vector3.zero;
            view.transform.localRotation = Quaternion.identity;
            CurrentDistance = 0;
            UpdateAvatarVisibility();
        }

        void UpdateAvatarVisibility()
        {
            if (avatar) avatar.SetVisible(IsOverview || (thirdPerson && CurrentDistance > .55f));
            var heldItem = lantern ? lantern : hands ? hands.lantern : null;
            if (heldItem) heldItem.SetPerspective(thirdPerson || IsOverview);
            if (hands)
            {
                hands.SetVisible(!IsOverview && !thirdPerson);
            }
        }

        void LateUpdate()
        {
            if (IsOverview || !walker || !view) return;
            if (thirdPerson)
            {
                UpdateFollowCamera(Time.deltaTime, false);
                UpdateAvatarVisibility();
            }
            else if (view.transform.parent != walker.eye) SnapToTarget();
        }

        void UpdateFollowCamera(float seconds, bool snap)
        {
            // The orbit uses look yaw, not the body's movement-facing rotation or animated head.
            if (view.transform.parent != overviewRoot) view.transform.SetParent(overviewRoot, true);
            float height = walker.Swimming ? swimmingPivotHeight : pivotHeight;
            float bias = walker.Swimming ? swimmingPitchBias : pitchBias;
            var rotation = Quaternion.Euler(Mathf.Clamp(walker.LookPitch + bias, -30, 65), walker.LookYaw, 0);
            var pivot = walker.transform.position + Vector3.up * height;
            var offset = rotation * new Vector3(shoulderOffset, 0, -distance);
            float desired = offset.magnitude;
            float hit = NearestObstruction(pivot, offset.normalized, desired);
            CurrentDistance = PlayerCameraMath.ResolveDistance(desired, hit, collisionPadding,
                snap ? desired : CurrentDistance, recoverySpeed, seconds);
            view.transform.SetPositionAndRotation(pivot + offset.normalized * CurrentDistance, rotation);
            ApplyPortalBridge();
        }

        public float NearestObstruction(Vector3 origin, Vector3 direction, float range)
        {
            // Sphere casts do not report colliders already overlapping their starting sphere.
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, collisionRadius, overlaps,
                obstructionLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
                if (BlocksCamera(overlaps[i])) return 0;
            if (overlapCount == overlaps.Length)
                foreach (var shape in Physics.OverlapSphere(origin, collisionRadius, obstructionLayers, QueryTriggerInteraction.Ignore))
                    if (BlocksCamera(shape)) return 0;
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, direction, hits, range,
                obstructionLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (!BlocksCamera(hit.collider)) continue;
                nearest = Mathf.Min(nearest, hit.distance);
            }
            if (count == hits.Length)
                foreach (var hit in Physics.SphereCastAll(origin, collisionRadius, direction, range, obstructionLayers, QueryTriggerInteraction.Ignore))
                    if (BlocksCamera(hit.collider)) nearest = Mathf.Min(nearest, hit.distance);
            return nearest;
        }

        bool BlocksCamera(Collider shape) => shape && !(walker && shape.transform.IsChildOf(walker.transform));
    }
}
