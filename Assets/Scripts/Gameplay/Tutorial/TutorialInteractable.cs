using CoastalTemple.Interaction;
using System.Collections.Generic;
using UnityEngine;

namespace CoastalTemple.Tutorial
{
    public abstract class TutorialInteractable : MonoBehaviour
    {
        public string prompt = "操作";
        [Min(0)] public float useRadius = 2.8f;
        [Tooltip("Optional authored point used for distance and line-of-sight checks.")]
        public Transform interactionPoint;
        static readonly List<TutorialInteractable> registry = new List<TutorialInteractable>(16);
        public static IReadOnlyList<TutorialInteractable> Registry => registry;
        public virtual bool Available => isActiveAndEnabled;
        public virtual string DisplayPrompt => prompt;
        public Vector3 InteractionPosition => interactionPoint ? interactionPoint.position : transform.position;

        protected virtual void OnEnable() { if (!registry.Contains(this)) registry.Add(this); }
        protected virtual void OnDisable() { registry.Remove(this); }
        public virtual bool CanUse(PlayerInteractor actor) => Available;
        public abstract void Use(PlayerInteractor actor);

        public static TutorialInteractable FindNearest(PlayerInteractor actor)
        {
            if (!actor) return null;
            TutorialInteractable nearest = null;
            float best = float.PositiveInfinity;
            for (int i = 0; i < registry.Count; i++)
            {
                var candidate = registry[i];
                if (!candidate || !candidate.Available) continue;
                float distance = (candidate.InteractionPosition - actor.InteractorPosition).sqrMagnitude;
                if (distance >= best || !actor.CanInteract(candidate)) continue;
                nearest = candidate;
                best = distance;
            }
            return nearest;
        }
    }
}

