using CoastalTemple.Interaction;
using CoastalTemple.Tutorial;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    /// <summary>A reusable E control; state belongs to the ferry, so multiple landing handles agree.</summary>
    [DisallowMultipleComponent]
    public sealed class GuidedBuoyantPlatformControl : TutorialInteractable
    {
        public GuidedBuoyantPlatform platform;
        public Transform handle;
        public Vector3 rotationAxis = Vector3.right;
        [Range(0, 80)] public float handleAngle = 28;
        Quaternion handleRest;
        bool hasRest;
        public override bool Available => base.Available && platform && platform.isActiveAndEnabled
            && platform.mode == GuidedPlatformMode.HorizontalFerry;
        public override string DisplayPrompt => prompt + " · " + (platform && platform.Direction > 0 ? "前进" : platform && platform.Direction < 0 ? "返回" : "停止");

        protected override void OnEnable()
        {
            if (!platform) platform = GetComponentInParent<GuidedBuoyantPlatform>();
            base.OnEnable();
        }
        public override void Use(PlayerInteractor actor)
        {
            if (!Available) return;
            platform.CycleDirection(); RefreshHandle();
        }
        void LateUpdate() => RefreshHandle();
        public void RefreshHandle()
        {
            if (!handle) return;
            if (!hasRest) { handleRest = handle.localRotation; hasRest = true; }
            handle.localRotation = handleRest * Quaternion.AngleAxis(handleAngle * (platform ? platform.Direction : 0), rotationAxis);
        }
    }
}
