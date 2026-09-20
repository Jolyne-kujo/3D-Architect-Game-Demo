using UnityEngine;

namespace CoastalTemple.LightPuzzles
{
    // Single choice in the Inspector; unlike the trace bitmask, a receiver cannot accept several colors.
    public enum ReceiverColor { Red = 1, Yellow = 2, Blue = 4 }

    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class LightReceiver : LightTarget
    {
        [Tooltip("Only matching light charges this receiver and triggers its Activated event.")]
        public ReceiverColor RequiredColor = ReceiverColor.Red;
        public LightColorChannel AcceptedChannel => (LightColorChannel)RequiredColor;
        public bool IsMatchingIlluminated =>
            (RequiredColor == ReceiverColor.Red || RequiredColor == ReceiverColor.Yellow || RequiredColor == ReceiverColor.Blue)
            && (IlluminatedColors & AcceptedChannel) != 0;
        [Min(0)] public float RequiredHitSeconds = .15f;
        [Min(0)] public float ReturnGraceSeconds = .15f;
        public bool Latching;
        public bool TransparentToBeam;
        public bool IsActive => state.Active;
        protected override float ActivationDelay => RequiredHitSeconds;
        protected override float GraceSeconds => ReturnGraceSeconds;
        protected override bool Latch => Latching;
        internal override bool PassesBeam => TransparentToBeam;
        protected override void AdvanceState(float deltaTime) =>
            state.Step(IsMatchingIlluminated, deltaTime, ActivationDelay, GraceSeconds, Latch);
    }
}
