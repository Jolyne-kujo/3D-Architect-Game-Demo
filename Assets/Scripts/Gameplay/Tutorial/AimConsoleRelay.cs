using CoastalTemple.Interaction;
namespace CoastalTemple.Tutorial
{
    public sealed class AimConsoleRelay : TutorialInteractable
    {
        public AimConsole console;
        public override void Use(PlayerInteractor actor) { if(console)console.Use(actor); }
    }
}

