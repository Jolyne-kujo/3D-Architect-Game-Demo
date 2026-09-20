using CoastalTemple.LightPuzzles;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    // Keep this script identity and inherited field names so existing lift components retain their serialized data.
    [DisallowMultipleComponent]
    public sealed class LightDrivenLift : LinearPlatformMotor
    {
        public LightReceiver receiverRaise;
        public LightReceiver receiverLower;

        protected override int SampleDirection()
        {
            bool raising = Powered(receiverRaise) || ManualDirection > 0;
            bool lowering = Powered(receiverLower) || ManualDirection < 0;
            return MechanismDriveRules.ResolveDirection(raising, lowering);
        }

        static bool Powered(LightReceiver receiver) => receiver &&
            MechanismDriveRules.IsPowered(receiver.isActiveAndEnabled, receiver.IsActive, receiver.IsMatchingIlluminated);
    }
}
