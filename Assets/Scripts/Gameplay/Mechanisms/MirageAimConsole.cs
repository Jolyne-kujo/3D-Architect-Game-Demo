using CoastalTemple.Interaction;
using CoastalTemple.Tutorial;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    [DisallowMultipleComponent]
    public sealed class MirageAimConsole : TutorialInteractable
    {
        public WaterMirage mirage;
        public override bool Available => base.Available && mirage && mirage.source;
        public override string DisplayPrompt => prompt + "  ·  " + (mirage && mirage.AimIndex == 0 ? "50°" : "65°") + (mirage && mirage.IsSolid ? "  ·  投影已实体化" : "  ·  投影未对齐");
        public override void Use(PlayerInteractor actor) { if (mirage) mirage.CycleAim(); }
    }
}

