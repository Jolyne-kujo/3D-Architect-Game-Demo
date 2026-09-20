using CoastalTemple.Interaction;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace CoastalTemple.Player
{
    /// <summary>Portable light inventory and visuals; usable without any lesson system.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerLantern : MonoBehaviour
    {
        public Light handLight;
        public GameObject carriedLantern;
        public Transform firstPersonHand;
        public Transform worldHand;
        public PlayerInteractor interactor;
        public bool readKeyboardInput = true;
        public UnityEvent pickedUp = new UnityEvent();
        public bool HasLantern => state.Acquired;
        public bool LanternOn => state.IsLit && handLight && handLight.enabled;

        readonly LanternState state = new LanternState();

        void Awake() => ApplyVisuals();
        void OnEnable() => ApplyVisuals();
        void OnDisable()
        {
            if (handLight) handLight.enabled = false;
            if (carriedLantern) carriedLantern.SetActive(false);
        }
        void Update()
        {
            if (readKeyboardInput && (!interactor || interactor.PlayerCanAct)
                && Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame) Toggle();
        }

        public bool GiveLantern()
        {
            if (!state.TryAcquire()) return false;
            ApplyVisuals();
            pickedUp.Invoke();
            return true;
        }

        public void SetLantern(bool value) { state.SetLight(value); ApplyVisuals(); }
        public void Toggle() => SetLantern(!state.IsLit);

        public void SetPerspective(bool thirdPerson)
        {
            var hand = thirdPerson ? worldHand : firstPersonHand;
            if (!hand || !carriedLantern || carriedLantern.transform.parent == hand) return;
            var held = carriedLantern.transform;
            held.SetParent(hand, false);
            held.localPosition = new Vector3(0, -.07f, 0);
            held.localRotation = Quaternion.identity;
            var scale = hand.lossyScale;
            held.localScale = new Vector3(.12f / Mathf.Max(.001f, Mathf.Abs(scale.x)),
                .18f / Mathf.Max(.001f, Mathf.Abs(scale.y)), .12f / Mathf.Max(.001f, Mathf.Abs(scale.z)));
            foreach (var node in held.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = hand.gameObject.layer;
            foreach (var renderer in held.GetComponentsInChildren<Renderer>(true))
                renderer.shadowCastingMode = thirdPerson ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void ApplyVisuals()
        {
            if (handLight) handLight.enabled = isActiveAndEnabled && state.IsLit;
            if (carriedLantern) carriedLantern.SetActive(isActiveAndEnabled && state.Acquired);
        }
    }
}
