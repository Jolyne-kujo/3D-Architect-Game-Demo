using CoastalTemple.LightPuzzles;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    // Evaluate after the light world's ordinary LateUpdate so moving a beam away removes the path in the same frame.
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class LightPathDriver : MonoBehaviour
    {
        public LightReceiver receiver;
        public SolidPath path;
        public bool IsPowered { get; private set; }

        void OnEnable() => EvaluateNow();
        void LateUpdate() => EvaluateNow();
        void OnDisable()
        {
            IsPowered = false;
            if (path) path.SetSolid(false);
        }
        public bool EvaluateNow()
        {
            IsPowered = isActiveAndEnabled && receiver && receiver.RequiredColor == ReceiverColor.Blue &&
                MechanismDriveRules.IsPowered(receiver.isActiveAndEnabled, receiver.IsActive, receiver.IsMatchingIlluminated);
            if (path) path.SetSolid(IsPowered);
            return IsPowered;
        }
    }
}
