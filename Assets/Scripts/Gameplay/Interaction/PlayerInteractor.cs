using CoastalTemple.Player;
using CoastalTemple.Tutorial;
using UnityEngine;
using UnityEngine.InputSystem;
using WaterCourtyard;

namespace CoastalTemple.Interaction
{
    /// <summary>Owns player reach, visibility, prompt selection and interaction input.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : MonoBehaviour
    {
        public CourtyardWalker walker;
        public CoastalPlayerCamera cameraRig;
        public PlayerLantern lantern;
        public Transform interactionOrigin;
        public LayerMask interactionObstructions = ~0;
        public bool allowInteraction = true;
        public bool readKeyboardInput = true;
        public bool PlayerCanAct => isActiveAndEnabled && allowInteraction
            && (!walker || walker.active) && (!cameraRig || !cameraRig.IsOverview);
        public Vector3 InteractorPosition => interactionOrigin ? interactionOrigin.position
            : walker ? (walker.eye ? walker.eye.position : walker.transform.position + Vector3.up * 1.45f)
            : transform.position;
        public TutorialInteractable Nearby { get; private set; }

        readonly RaycastHit[] hits = new RaycastHit[64];

        void OnDisable() => Nearby = null;
        void Update()
        {
            RefreshNearby();
            if (readKeyboardInput && PlayerCanAct && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                TryInteract();
        }

        public void RefreshNearby() => Nearby = PlayerCanAct ? TutorialInteractable.FindNearest(this) : null;

        public bool CanInteract(TutorialInteractable target)
        {
            if (!target || !PlayerCanAct || !target.CanUse(this)) return false;
            Vector3 displacement = target.InteractionPosition - InteractorPosition;
            if (!TutorialInteractionPolicy.CanReach(displacement.sqrMagnitude, target.useRadius, false, true)) return false;
            float distance = displacement.magnitude;
            if (distance < .01f) return true;
            float castDistance = Mathf.Max(0, distance - .025f);
            int count = Physics.RaycastNonAlloc(InteractorPosition, displacement / distance, hits,
                castDistance, interactionObstructions, QueryTriggerInteraction.Ignore);
            if (count == hits.Length)
            {
                // A full non-alloc buffer can omit a wall behind many allowed colliders.
                foreach (var hit in Physics.RaycastAll(InteractorPosition, displacement / distance,
                    castDistance, interactionObstructions, QueryTriggerInteraction.Ignore))
                    if (BlocksInteraction(hit.collider, target)) return false;
            }
            else for (int i = 0; i < count; i++) if (BlocksInteraction(hits[i].collider, target)) return false;
            return true;
        }

        bool BlocksInteraction(Collider shape, TutorialInteractable target)
        {
            if (!shape) return false;
            Transform body = walker ? walker.transform : transform;
            if (shape.transform.IsChildOf(body) || shape.transform.IsChildOf(target.transform)) return false;
            return shape.GetComponentInParent<TutorialInteractable>() != target;
        }

        public bool TryInteract() => Interact(TutorialInteractable.FindNearest(this));

        public bool Interact(TutorialInteractable target)
        {
            if (!CanInteract(target)) return false;
            target.Use(this);
            RefreshNearby();
            return true;
        }
    }
}
