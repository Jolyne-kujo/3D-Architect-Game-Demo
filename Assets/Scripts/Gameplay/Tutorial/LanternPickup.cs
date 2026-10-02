using CoastalTemple.Interaction;
using UnityEngine;

namespace CoastalTemple.Tutorial
{
    [DisallowMultipleComponent]
    public sealed class LanternPickup : TutorialInteractable
    {
        public Renderer[] visuals = System.Array.Empty<Renderer>();
        public bool Collected { get; private set; }
        public override bool UsesObservationView => false;
        public override bool Available => base.Available && !Collected;
        public override bool CanUse(PlayerInteractor actor) => Available && actor && actor.lantern
            && actor.lantern.isActiveAndEnabled && !actor.lantern.HasLantern;

        public override void Use(PlayerInteractor actor)
        {
            if (!CanUse(actor) || !actor.lantern.GiveLantern()) return;
            Collected = true;
            if (visuals != null) foreach (var visual in visuals) if (visual) visual.enabled = false;
            foreach (var shape in GetComponentsInChildren<Collider>()) shape.enabled = false;
        }
    }
}

