using CoastalTemple.Interaction;
using CoastalTemple.Tutorial;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    [DisallowMultipleComponent]
    public sealed class MotorLever : TutorialInteractable
    {
        public LinearPlatformMotor motor;
        public Transform handle;
        public Vector3 rotationAxis = Vector3.right;
        [Range(0, 80)] public float handleAngle = 28;
        public string forwardLabel = "升起";
        public string backwardLabel = "降下";
        // A stopped motor has two possible next directions. Share only that cycle memory;
        // all live direction, prompt and pose information comes from the actual motor.
        static readonly ConditionalWeakTable<LinearPlatformMotor, MotorLeverState> cycles = new ConditionalWeakTable<LinearPlatformMotor, MotorLeverState>();
        Quaternion handleRest;
        bool handleInitialized;

        public int Direction => motor ? motor.Direction : 0;
        public override bool Available => base.Available && motor && motor.isActiveAndEnabled;
        public override string DisplayPrompt => prompt + " · " + (Direction > 0 ? forwardLabel : Direction < 0 ? backwardLabel : "停止");

        public override void Use(PlayerInteractor actor)
        {
            if (!Available) return;
            var state = cycles.GetOrCreateValue(motor);
            state.SetDirection(Direction);
            state.Cycle();
            motor.SetDirection(state.Direction);
            RefreshHandle();
        }
        public void SetDirection(int direction)
        {
            if (!motor) return;
            cycles.GetOrCreateValue(motor).SetDirection(direction);
            motor.SetDirection(direction);
            RefreshHandle();
        }
        void LateUpdate() => RefreshHandle();

        public void RefreshHandle()
        {
            int direction = Direction;
            if (motor) cycles.GetOrCreateValue(motor).SetDirection(direction);
            if (!handle) return;
            if (!handleInitialized) { handleRest = handle.localRotation; handleInitialized = true; }
            handle.localRotation = handleRest * Quaternion.AngleAxis(handleAngle * direction, rotationAxis);
        }
    }
}
