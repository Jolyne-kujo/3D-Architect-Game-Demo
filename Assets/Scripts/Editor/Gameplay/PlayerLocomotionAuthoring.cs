using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoastalTemple.Editor
{
    /// <summary>Retarget the supplied locomotion onto the existing world body and first-person arms.</summary>
    public static class PlayerLocomotionAuthoring
    {
        public const string SlowPath = "Assets/Animations/Player/Locomotion/SlowRun.fbx";
        public const string FastPath = "Assets/Animations/Player/Locomotion/FastRun.fbx";

        [MenuItem("Coastal Temple/Legacy Quaternius/Apply old slow and fast run")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before changing animation assets.");
            ConfigureModel(SlowPath); ConfigureModel(FastPath);
            ApplyTo(AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/MannequinLocomotion.controller"));
            ApplyTo(AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Player/FirstPersonHands.controller"));
            AssetDatabase.SaveAssets();
        }

        static void ConfigureModel(string path)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            if (!importer) throw new InvalidOperationException("Missing animation source: " + path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.name = path == SlowPath ? "Slow Run" : "Fast Run";
                clip.loopTime = true; clip.loopPose = true;
                clip.lockRootRotation = true; clip.keepOriginalOrientation = false;
                // Quaternius' authored clips face -Z; match that convention inside the pose.
                // Do not rotate/move the world body's anchor to compensate for a source clip.
                clip.rotationOffset = 180;
                clip.lockRootHeightY = true; clip.keepOriginalPositionY = true;
                // Extract the source trajectory rather than baking forward travel into the pose.
                // The target Animators discard root motion; only the CharacterController moves.
                clip.lockRootPositionXZ = false; clip.keepOriginalPositionXZ = false;
            }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            var animator = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("Source cannot be retargeted as Humanoid: " + path);
        }

        public static AnimatorController ApplyTo(AnimatorController controller)
        {
            if (!controller) throw new InvalidOperationException("Missing player Animator Controller.");
            if (AssetDatabase.GetAssetPath(controller) == RedBotAuthoring.ControllerPath) return controller;
            AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Single(c => !c.name.StartsWith("__preview__") && c.humanMotion);
            var tree = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Locomotion").state.motion as BlendTree;
            if (!tree) throw new InvalidOperationException("Locomotion must retain its speed blend tree.");
            var children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (Mathf.Approximately(children[i].threshold, 3.4f)) children[i].motion = Clip(SlowPath);
                if (Mathf.Approximately(children[i].threshold, 5.3f)) children[i].motion = Clip(FastPath);
            }
            tree.children = children; tree.name = "Idle Slow Run Fast Run";
            EditorUtility.SetDirty(tree); EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
