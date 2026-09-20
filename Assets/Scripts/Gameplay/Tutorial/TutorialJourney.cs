using System;
using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using CoastalTemple.Player;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Tutorial
{
    /// <summary>Observes authored lessons and publishes hints/rewards. It cannot operate mechanisms.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialJourney : MonoBehaviour
    {
        public PlayerInteractor interactor;
        public PlayerLantern playerLantern;
        public TutorialHud hud;
        public LessonStation[] lessons = Array.Empty<LessonStation>();
        public bool showHud = true;

        // Serialized V8 references remain readable until an explicit editor migration is saved.
        [HideInInspector] public CourtyardWalker walker;
        [HideInInspector] public CoastalPlayerCamera cameraRig;
        [HideInInspector] public Light handLight;
        [HideInInspector] public GameObject carriedLantern;
        [HideInInspector] public LanternPickup lantern;
        [HideInInspector] public RedStoneCurtain firstCurtain, reversibleCurtain, shortcutCurtain;
        [HideInInspector] public LightDrivenLift lift;
        [HideInInspector] public Transform[] stations = Array.Empty<Transform>();
        [HideInInspector] public LayerMask interactionObstructions = ~0;
        [HideInInspector] public AudioSource chimeSource;

        public event Action<string> RewardIssued;
        public bool HasLantern => playerLantern && playerLantern.HasLantern;
        public bool LanternOn => playerLantern && playerLantern.LanternOn;
        public Vector3 InteractorPosition => interactor ? interactor.InteractorPosition : transform.position;
        public TutorialInteractable Nearby => interactor ? interactor.Nearby : null;
        public int CurrentStage => ResolveStage();
        public LessonStation CurrentLesson => CurrentStage >= 0 ? lessons[CurrentStage] : null;
        public string ObservationText { get; private set; }
        public float ObservationUntil { get; private set; }

        LessonProgress progress;

        void Update() => ObserveWorld();

        // Compatibility facades only. New callers use PlayerInteractor / PlayerLantern directly.
        public bool CanInteract(TutorialInteractable target) => interactor && interactor.CanInteract(target);
        public bool TryInteract() => interactor && interactor.TryInteract();
        public bool Interact(TutorialInteractable target) => interactor && interactor.Interact(target);
        public void GiveLantern() { if (playerLantern) playerLantern.GiveLantern(); }
        public void SetLantern(bool value) { if (playerLantern) playerLantern.SetLantern(value); }

        public void NotifyReward(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) RewardIssued?.Invoke(message);
        }

        public void ShowObservation()
        {
            int stage = CurrentStage;
            if (stage < 0) return;
            EnsureProgress();
            string[] hints = lessons[stage].hints;
            int index = progress.NextHintIndex(stage, hints == null ? 0 : hints.Length);
            if (index < 0) return;
            ObservationText = hints[index];
            ObservationUntil = Time.unscaledTime + 12;
        }

        public void ObserveWorld()
        {
            EnsureProgress();
            for (int i = 0; i < lessons.Length; i++)
            {
                var station = lessons[i];
                if (station == null || !station.completionCondition) continue;
                if (!progress.TryComplete(i, station.completionCondition.IsComplete)) continue;
                station.onCompleted?.Invoke();
                NotifyReward(station.completionMessage);
            }
        }

        void EnsureProgress()
        {
            if (lessons == null) lessons = Array.Empty<LessonStation>();
            if (progress == null || progress.Count != lessons.Length) progress = new LessonProgress(lessons.Length);
        }

        int ResolveStage()
        {
            if (lessons == null) return -1;
            Transform player = interactor && interactor.walker ? interactor.walker.transform
                : interactor ? interactor.transform : walker ? walker.transform : null;
            if (!player) return -1;
            float best = float.PositiveInfinity;
            int stage = -1;
            for (int i = 0; i < lessons.Length; i++)
            {
                var station = lessons[i];
                if (station == null || !station.marker) continue;
                float distance = (player.position - station.marker.position).sqrMagnitude;
                if (!TutorialInteractionPolicy.CanReach(distance, station.radius, false, true) || distance >= best) continue;
                best = distance; stage = i;
            }
            return stage;
        }
    }
}
