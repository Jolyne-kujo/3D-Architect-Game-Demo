using CoastalTemple.Interaction;
using UnityEngine;
namespace CoastalTemple.Tutorial
{
    // A second reachable handle operates the same rail, so the lift can always be recalled.
    public sealed class PortalConsoleRelay : TutorialInteractable
    {
        public PortalRailConsole rail;
        public override void Use(PlayerInteractor actor) { if (rail) rail.Use(actor); }
    }
}

