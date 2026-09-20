#if UNITY_EDITOR
using CoastalTemple.Interaction;
using CoastalTemple.Player;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

namespace CoastalTemple.Tutorial.Editor
{
    /// <summary>Explicit editor-only wiring. Runtime code never creates missing scene modules.</summary>
    public static class TutorialModuleMigration
    {
        [MenuItem("Coastal Temple/Tutorial/Bind Independent Player Modules")]
        public static void BindActiveScene()
        {
            var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (var journey in Object.FindObjectsByType<TutorialJourney>(FindObjectsInactive.Include))
                if (journey.gameObject.scene == activeScene) Bind(journey);
        }

        public static PlayerInteractor Bind(TutorialJourney journey)
        {
            if (!journey) throw new System.ArgumentNullException(nameof(journey));
            if (!journey.walker && (!journey.interactor || !journey.interactor.walker))
                throw new System.InvalidOperationException("Assign the player walker before binding tutorial modules.");
            var walker = journey.interactor && journey.interactor.walker ? journey.interactor.walker : journey.walker;
            var player = walker.gameObject;
            T GetOrAdd<T>(GameObject owner) where T : Component
            {
                var component = owner.GetComponent<T>();
                return component ? component : owner.AddComponent<T>();
            }
            var actor = journey.interactor ? journey.interactor : GetOrAdd<PlayerInteractor>(player);
            actor.walker = walker;
            if (journey.cameraRig) actor.cameraRig = journey.cameraRig;
            actor.interactionObstructions = journey.interactionObstructions;
            var lantern = journey.playerLantern ? journey.playerLantern : GetOrAdd<PlayerLantern>(player);
            if (journey.handLight) lantern.handLight = journey.handLight;
            if (journey.carriedLantern) lantern.carriedLantern = journey.carriedLantern;
            lantern.interactor = actor;
            actor.lantern = lantern;
            journey.interactor = actor;
            journey.playerLantern = lantern;
            var hud = journey.hud ? journey.hud : GetOrAdd<TutorialHud>(journey.gameObject);
            hud.journey = journey; hud.interactor = actor; hud.lantern = lantern;
            var audio = journey.chimeSource ? journey.chimeSource : hud.chimeSource ? hud.chimeSource : GetOrAdd<AudioSource>(hud.gameObject);
            audio.playOnAwake = false; audio.spatialBlend = 0; audio.volume = .18f;
            hud.chimeSource = audio;
            journey.hud = hud;
            var input = GetOrAdd<TutorialInput>(player);
            input.interactor = actor; input.cameraRig = actor.cameraRig; input.journey = journey;
            bool pickupBound = false;
            for (int i = 0; i < lantern.pickedUp.GetPersistentEventCount(); i++)
                pickupBound |= lantern.pickedUp.GetPersistentTarget(i) == journey && lantern.pickedUp.GetPersistentMethodName(i) == nameof(TutorialJourney.NotifyReward);
            if (!pickupBound) UnityEventTools.AddStringPersistentListener(lantern.pickedUp, journey.NotifyReward, "获得提灯  ·  L 开关照明\n可以随身照亮暗处，再回看机关留下的变化。");
            foreach (var item in new Object[] { actor, lantern, journey, hud, input, audio }) EditorUtility.SetDirty(item);
            if (journey.gameObject.scene.IsValid()) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(journey.gameObject.scene);
            return actor;
        }
    }
}
#endif
