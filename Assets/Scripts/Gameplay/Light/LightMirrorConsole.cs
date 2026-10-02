using CoastalTemple.Interaction;
using CoastalTemple.Tutorial;
using UnityEngine;

namespace CoastalTemple.LightPuzzles
{
    public sealed class LightMirrorConsole : TutorialInteractable
    {
        public Transform pivot;
        [Range(5, 90)] public float stepDegrees = 45;
        public override string DisplayPrompt => "旋转镜面 · 每次 " + stepDegrees + "°";
        public override void Use(PlayerInteractor actor)
        { if (pivot) pivot.Rotate(Vector3.up, stepDegrees, Space.Self); }
    }
}
