using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using UnityEngine;

namespace CoastalTemple.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class AimConsole : TutorialInteractable
    {
        public LaserEmitter emitter;
        public Transform[] aimPoints = System.Array.Empty<Transform>();
        public int selected;
        public override bool Available => base.Available && emitter && aimPoints != null && aimPoints.Length > 0;
        public override string DisplayPrompt => prompt + "  ·  方位 " + (selected + 1) + "/" + (aimPoints == null ? 0 : aimPoints.Length);

        void Start() => Apply();
        public override void Use(PlayerInteractor actor) => Select(selected + 1);
        public void Select(int index)
        {
            if (aimPoints == null || aimPoints.Length == 0) return;
            selected = ((index % aimPoints.Length) + aimPoints.Length) % aimPoints.Length;
            Apply();
        }
        public void Apply()
        {
            if (!emitter || aimPoints == null || aimPoints.Length == 0) return;
            selected = Mathf.Clamp(selected, 0, aimPoints.Length - 1);
            if (!aimPoints[selected]) return;
            Transform basis = emitter.Origin ? emitter.Origin : emitter.transform;
            Vector3 direction = aimPoints[selected].position - basis.position;
            if (direction.sqrMagnitude < .000001f) return;
            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > .999f ? Vector3.forward : Vector3.up;
            basis.rotation = Quaternion.LookRotation(direction, up);
        }
    }
}

