using UnityEngine;

namespace CoastalTemple.LightPuzzles
{
    public enum CurtainMode { Permanent = 0, WhileIlluminated = 1, ByLightColor = 2 }

    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class RedStoneCurtain : LightTarget
    {
        public CurtainMode Mode = CurtainMode.Permanent;
        [Min(0)] public float ContinuousHitSeconds = .35f;
        [Min(0)] public float ReturnGraceSeconds = .15f;
        public BoxCollider PhysicalCollider;
        public Renderer[] Visuals;
        [Tooltip("Player occupancy layers. Zero uses CharacterController, Rigidbody, or Player tag detection.")]
        public LayerMask OccupantMask;
        public bool IsOpen { get; private set; }
        public bool RestorePending { get; private set; }
        public bool IsPermanent => Mode == CurtainMode.ByLightColor ? colorState.Permanent : Mode == CurtainMode.Permanent && state.Active;
        public override float Progress => Mode == CurtainMode.ByLightColor ? colorState.Progress : base.Progress;
        readonly ColorCurtainState colorState = new ColorCurtainState();
        readonly Collider[] occupants = new Collider[32];
        protected override bool ActivationActive => Mode == CurtainMode.ByLightColor ? colorState.Active : base.ActivationActive;
        protected override float ActivationDelay => Mode == CurtainMode.Permanent ? ContinuousHitSeconds : 0f;
        protected override float GraceSeconds => ReturnGraceSeconds;
        protected override bool Latch => Mode == CurtainMode.Permanent;
        protected override BoxCollider Shape => OpticalCollider ? OpticalCollider : PhysicalCollider;
        internal override bool PassesBeam => IsOpen;
        internal override bool OpticallyPresent => !IsPermanent;
        internal override bool OwnsCollider(Collider value) => value == PhysicalCollider || base.OwnsCollider(value);

        protected override void Awake()
        {
            base.Awake();
            if (!PhysicalCollider) PhysicalCollider = GetComponent<BoxCollider>();
            if (Visuals == null || Visuals.Length == 0) Visuals = GetComponentsInChildren<Renderer>();
        }
        protected override void ApplyState()
        {
            bool open = ActivationActive;
            RestorePending = !open && IsOpen && HasOccupant();
            if (RestorePending) open = true;
            if (PhysicalCollider) PhysicalCollider.enabled = !open;
            if (Visuals != null)
                for (int i = 0; i < Visuals.Length; i++) if (Visuals[i]) Visuals[i].enabled = !open;
            IsOpen = open;
        }
        protected override void AdvanceState(float deltaTime)
        {
            if (Mode == CurtainMode.ByLightColor)
                colorState.Step(IlluminatedColors, deltaTime, ContinuousHitSeconds, ReturnGraceSeconds);
            else base.AdvanceState(deltaTime);
        }
        protected override void ClearState()
        {
            base.ClearState();
            colorState.Reset();
        }
        bool HasOccupant()
        {
            var box = PhysicalCollider ? PhysicalCollider : Shape;
            var basis = box ? box.transform : transform;
            var center = basis.TransformPoint(box ? box.center : OpticalCenter);
            var size = box ? box.size : OpticalSize;
            var scale = basis.lossyScale;
            var half = new Vector3(Mathf.Abs(size.x * scale.x), Mathf.Abs(size.y * scale.y), Mathf.Abs(size.z * scale.z)) * .5f + Vector3.one * .03f;
            int mask = OccupantMask.value == 0 ? ~0 : OccupantMask.value;
            int count = Physics.OverlapBoxNonAlloc(center, half, occupants, basis.rotation, mask, QueryTriggerInteraction.Ignore);
            // A full buffer is conservatively occupied; never spawn collision onto a player.
            if (count == occupants.Length) return true;
            for (int i = 0; i < count; i++)
            {
                var occupant = occupants[i];
                if (!occupant || occupant.transform.IsChildOf(transform)) continue;
                if (OccupantMask.value != 0 || occupant is CharacterController || occupant.attachedRigidbody || occupant.CompareTag("Player")) return true;
            }
            return false;
        }
    }
}
