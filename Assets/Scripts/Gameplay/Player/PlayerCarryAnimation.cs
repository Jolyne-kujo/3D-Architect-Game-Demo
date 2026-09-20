using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    /// <summary>Blends a masked carrying pose without taking ownership of locomotion or the actor transform.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(50)]
    public sealed class PlayerCarryAnimation : MonoBehaviour
    {
        public Animator animator;
        public CourtyardWalker walker;
        public PlayerLantern lantern;
        [Tooltip("The hand used by the carrying pose and lantern attachment.")]
        public HumanBodyBones handBone = HumanBodyBones.RightHand;
        public string layerName = "Carried Lantern";
        [Min(0)] public float blendSpeed = 6;

        Animator cachedAnimator;
        RuntimeAnimatorController cachedController;
        string cachedLayerName;
        int layerIndex = -1;
        float weight;

        void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>(true);
            if (!walker) walker = GetComponentInParent<CourtyardWalker>();
            if (!lantern) lantern = GetComponentInParent<PlayerLantern>();
        }

        void Update()
        {
            if (!ResolveLayer()) return;
            float target = lantern && lantern.HasLantern && (!walker || (!walker.Swimming && !walker.Climbing)) ? 1 : 0;
            weight = Mathf.MoveTowards(weight, target, Mathf.Max(0, blendSpeed) * Time.deltaTime);
            animator.SetLayerWeight(layerIndex, weight);
        }

        void OnDisable()
        {
            weight = 0;
            if (ResolveLayer()) animator.SetLayerWeight(layerIndex, 0);
        }

        bool ResolveLayer()
        {
            // Inactive presentation models have no playable graph yet. Querying their
            // layer names before initialization produces Animator errors.
            if (!animator || !animator.isActiveAndEnabled || !animator.isInitialized || !animator.runtimeAnimatorController)
                return false;

            var controller = animator.runtimeAnimatorController;
            if (cachedAnimator != animator || cachedController != controller || cachedLayerName != layerName)
            {
                cachedAnimator = animator;
                cachedController = controller;
                cachedLayerName = layerName;
                layerIndex = string.IsNullOrEmpty(layerName) ? -1 : animator.GetLayerIndex(layerName);
                weight = 0;
            }

            // A missing/misnamed overlay must never alter the locomotion base layer.
            return layerIndex > 0 && layerIndex < animator.layerCount;
        }
    }
}
