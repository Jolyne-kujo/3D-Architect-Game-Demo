using CoastalTemple.Interaction;
using UnityEngine;

namespace CoastalTemple.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class PortalRailConsole : TutorialInteractable
    {
        public Transform movingPortal;
        public Transform[] docks = System.Array.Empty<Transform>();
        public int selected;
        [Min(0)] public float speed = 3f;
        public bool IsMoving { get; private set; }
        public override bool Available => base.Available && movingPortal && docks != null && docks.Length > 0;
        public override string DisplayPrompt => prompt + "  ·  轨位 " + (selected + 1) + "/" + (docks == null ? 0 : docks.Length);

        void Start() => ApplyInitialPose();
        void Update() => Simulate(Time.deltaTime);
        public override void Use(PlayerInteractor actor) => Select(selected + 1);
        public void Select(int index)
        {
            if (docks == null || docks.Length == 0) return;
            selected = ((index % docks.Length) + docks.Length) % docks.Length;
            IsMoving = movingPortal && docks[selected];
        }
        public void ApplyInitialPose()
        {
            if (!movingPortal || docks == null || docks.Length == 0) return;
            selected = Mathf.Clamp(selected, 0, docks.Length - 1);
            if (!docks[selected]) return;
            movingPortal.SetPositionAndRotation(docks[selected].position, docks[selected].rotation);
            IsMoving = false;
        }
        public void Simulate(float seconds)
        {
            if (!movingPortal || docks == null || docks.Length == 0 || seconds <= 0) return;
            selected = Mathf.Clamp(selected, 0, docks.Length - 1);
            Transform target = docks[selected];
            if (!target) { IsMoving = false; return; }
            float step = Mathf.Max(0, speed) * seconds;
            movingPortal.SetPositionAndRotation(
                Vector3.MoveTowards(movingPortal.position, target.position, step),
                Quaternion.RotateTowards(movingPortal.rotation, target.rotation, step * 90));
            IsMoving = (movingPortal.position - target.position).sqrMagnitude > .000001f
                || Quaternion.Angle(movingPortal.rotation, target.rotation) > .01f;
        }
    }
}

