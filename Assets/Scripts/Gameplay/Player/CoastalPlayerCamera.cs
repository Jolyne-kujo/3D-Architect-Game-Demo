using UnityEngine;
using WaterCourtyard;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Portals;
using CoastalTemple.Tutorial;
using UnityEngine.InputSystem;

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
        [Header("First-person presentation")]
        public bool showFirstPersonHands;
        [Tooltip("Optional item-only overlay. It renders the lantern after pickup without a hand model.")]
        public Camera heldItemCamera;
        [Header("Mechanism observation")]
        [Range(45,80)] public float mechanismPitch = 65;
        [Range(60,95)] public float mechanismFieldOfView = 80;
        [Min(4)] public float fallbackObservationRadius = 9;
        public bool IsMechanismView { get; private set; }
        public TutorialInteractable MechanismTarget { get; private set; }
        MechanismObservationArea observationArea;
        float gameplayFieldOfView, mechanismYaw;
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
            EndMechanismView();
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
            EndMechanismView();
            IsOverview = value;
            if (value && view) view.transform.SetParent(overviewRoot, true);
            UpdateAvatarVisibility();
            if (!value) SnapToTarget();
        }

        public void SetThirdPerson(bool value)
        {
            EndMechanismView();
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
            if (!walker || !view || IsOverview || IsMechanismView) return;
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
            if (heldItem)
            {
                heldItem.SetPerspective(thirdPerson || IsOverview);
                heldItem.SetPresentationVisible(!IsMechanismView);
            }
            if(heldItemCamera)heldItemCamera.enabled=!IsOverview&&!thirdPerson&&!IsMechanismView&&heldItem&&heldItem.HasLantern;
            if (hands)
            {
                hands.SetVisible(showFirstPersonHands&&!IsOverview&&!thirdPerson&&!IsMechanismView);
            }
        }

        public void BeginMechanismView(TutorialInteractable target)
        {
            if(!walker||!view||IsOverview||!target||!target.UsesObservationView)return;
            if(!IsMechanismView)gameplayFieldOfView=view.fieldOfView;
            MechanismTarget=target;observationArea=target.GetComponentInParent<MechanismObservationArea>();
            mechanismYaw=observationArea&&observationArea.fixedYaw?observationArea.yaw:walker.LookYaw;
            IsMechanismView=true;walker.cameraInputSuspended=true;
            view.transform.SetParent(overviewRoot,true);
            UpdateMechanismView();UpdateAvatarVisibility();
        }

        public void EndMechanismView()
        {
            if(!IsMechanismView)return;
            IsMechanismView=false;MechanismTarget=null;observationArea=null;
            if(walker)walker.cameraInputSuspended=false;
            if(view)view.fieldOfView=gameplayFieldOfView;
            UpdateAvatarVisibility();SnapToTarget();
        }

        void UpdateMechanismView()
        {
            var bounds=observationArea?observationArea.WorldBounds:
                new Bounds(MechanismTarget.InteractionPosition+Vector3.up,new Vector3(fallbackObservationRadius*2,4,fallbackObservationRadius*2));
            var rotation=Quaternion.Euler(mechanismPitch,mechanismYaw,0);
            var inverse=Quaternion.Inverse(rotation);
            float vertical=Mathf.Tan(mechanismFieldOfView*.5f*Mathf.Deg2Rad);
            float horizontal=vertical*Mathf.Max(.3f,view.aspect),distance=10;
            for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            {
                var corner=inverse*Vector3.Scale(bounds.extents,new Vector3(x,y,z));
                distance=Mathf.Max(distance,Mathf.Max(Mathf.Abs(corner.x)/horizontal-corner.z,Mathf.Abs(corner.y)/vertical-corner.z));
            }
            view.fieldOfView=mechanismFieldOfView;
            view.transform.SetPositionAndRotation(bounds.center-rotation*Vector3.forward*(distance+1.5f),rotation);
        }

        void Update()
        {
            if(!IsMechanismView)return;
            var keys=Keyboard.current;
            if(keys!=null&&(keys.wKey.isPressed||keys.aKey.isPressed||keys.sKey.isPressed||keys.dKey.isPressed
                ||keys.spaceKey.wasPressedThisFrame||keys.escapeKey.wasPressedThisFrame))EndMechanismView();
        }

        void OnDisable()=>EndMechanismView();

        void LateUpdate()
        {
            if(IsMechanismView)
            {
                if(!MechanismTarget||!MechanismTarget.Available)EndMechanismView();
                else{UpdateMechanismView();UpdateAvatarVisibility();return;}
            }
            if (IsOverview || !walker || !view) return;
            if (thirdPerson)
            {
                UpdateFollowCamera(Time.deltaTime, false);
            }
            else if (view.transform.parent != walker.eye) SnapToTarget();
            UpdateAvatarVisibility();
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
