using CoastalTemple.Interaction;
using CoastalTemple.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CoastalTemple.Tutorial
{
    /// <summary>Optional presentation shortcuts; mechanisms never consume these keys.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialInput : MonoBehaviour
    {
        public PlayerInteractor interactor;
        public TutorialJourney journey;
        public CoastalPlayerCamera cameraRig;
        public bool readKeyboardInput = true;

        void Update()
        {
            var keyboard = Keyboard.current;
            if (!readKeyboardInput || !interactor || !interactor.PlayerCanAct || keyboard == null) return;
            if (keyboard.hKey.wasPressedThisFrame && journey && journey.isActiveAndEnabled) journey.ShowObservation();
        }
    }
}
