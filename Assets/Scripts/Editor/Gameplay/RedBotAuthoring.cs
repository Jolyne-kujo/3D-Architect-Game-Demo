using System;
using System.IO;
using System.Linq;
using CoastalTemple.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    /// <summary>Explicit, repeatable import and scene binding for the supplied Mixamo bot.</summary>
    public static class RedBotAuthoring
    {
        public const string SourceFolder = "Assets/Animations/Character";
        public const string ControllerPath = "Assets/Animations/Player/RedBot.controller";
        public const string PrefabPath = "Assets/Prefabs/Player/RedBot.prefab";
        public const string HoldPath = "Assets/Animations/Player/LiftHold.anim";
        public static string Source(string name) => SourceFolder + "/X Bot@" + name + ".fbx";
        public static AnimationClip Clip(string name) => AssetDatabase.LoadAllAssetsAtPath(Source(name))
            .OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__") && c.humanMotion);

        [MenuItem("Coastal Temple/Player/Apply red bot and supplied animations")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before replacing the authored player.");
            ConfigureSources(); BuildPrefab(); BindScene();
        }

        public static void ConfigureSources()
        {
            Avatar avatar = null;
            foreach (var name in new[] { "Slow Run", "Fast Run", "Idle", "Jump", "Lifting", "Swimming", "Treading Water", "Ascending Stairs", "Descending Stairs", "Braced Hang To Crouch" })
            {
                var path = Source(name); var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!importer) throw new InvalidOperationException("Missing supplied FBX: " + path);
                importer.animationType = ModelImporterAnimationType.Human;
                importer.avatarSetup = name == "Slow Run" ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.CopyFromOther;
                if (name != "Slow Run") importer.sourceAvatar = avatar;
                importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
                var clips = importer.defaultClipAnimations;
                if (clips.Length != 1) throw new InvalidOperationException("Expected one animation in " + path);
                foreach (var clip in clips)
                {
                    clip.name = name;
                    clip.loopTime = name != "Jump" && name != "Lifting" && name != "Braced Hang To Crouch";
                    clip.loopPose = clip.loopTime;
                    clip.lockRootRotation = true; clip.keepOriginalOrientation = false; clip.rotationOffset = 0;
                    clip.lockRootPositionXZ = false; clip.keepOriginalPositionXZ = false;
                    clip.lockRootHeightY = name != "Jump"; clip.keepOriginalPositionY = true;
                    // Space launches immediately: use takeoff through landing from the supplied
                    // 65-frame clip, without its long stationary anticipation/recovery sections.
                    if (name == "Jump") { clip.firstFrame = 21; clip.lastFrame = 45; }
                    if (name == "Braced Hang To Crouch")
                    {
                        clip.lockRootHeightY = false; clip.heightFromFeet = true;
                        clip.keepOriginalPositionY = false; clip.keepOriginalOrientation = true;
                    }
                }
                importer.clipAnimations = clips; importer.SaveAndReimport();
                var a = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<Animator>();
                // Animation-only FBX files do not get an Animator component when
                // copying an avatar. Validate the referenced avatar and imported clip.
                var importedAvatar = name == "Slow Run" ? (a ? a.avatar : null) : importer.sourceAvatar;
                if (!importedAvatar || !importedAvatar.isValid || !importedAvatar.isHuman || !Clip(name).humanMotion)
                    throw new InvalidOperationException("Invalid humanoid skeleton in " + path);
                if (name == "Slow Run") avatar = importedAvatar;
            }
        }

        static AnimationClip BuildHold()
        {
            var source = Clip("Lifting");
            var pose = AssetDatabase.LoadAssetAtPath<AnimationClip>(HoldPath);
            if (!pose) { pose = new AnimationClip(); AssetDatabase.CreateAsset(pose, HoldPath); }
            pose.ClearCurves(); pose.name = "Lift Hold"; pose.frameRate = 30;
            // At 1.4 seconds the source's RIGHT hand is raised at chest height.
            // Freeze this pose so locomotion cannot replay the pickup/drop sections.
            foreach (var binding in AnimationUtility.GetCurveBindings(source))
            {
                float value = AnimationUtility.GetEditorCurve(source, binding).Evaluate(1.4f);
                // The run clips lean the torso farther forward than Lifting. Raise
                // the held upper arm and flex the elbow so the lamp stays at chest height.
                if (binding.propertyName == "Right Arm Down-Up") value += .18f;
                if (binding.propertyName == "Right Forearm Stretch") value -= .4f;
                AnimationUtility.SetEditorCurve(pose, binding, AnimationCurve.Constant(0, 1, value));
            }
            var settings = AnimationUtility.GetAnimationClipSettings(pose); settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(pose, settings); EditorUtility.SetDirty(pose);
            return pose;
        }

        public static AnimatorController BuildController()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.layers = Array.Empty<AnimatorControllerLayer>(); controller.parameters = Array.Empty<AnimatorControllerParameter>();
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(ControllerPath)) if (sub != controller) Object.DestroyImmediate(sub, true);
            var machine = new AnimatorStateMachine { name = "Base Layer", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(machine, controller);
            controller.layers = new[] { new AnimatorControllerLayer { name = "Base Layer", stateMachine = machine, defaultWeight = 1, iKPass = true } };
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Swimming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Climbing", AnimatorControllerParameterType.Bool);
            controller.AddParameter("ClimbProgress", AnimatorControllerParameterType.Float);
            var land = machine.AddState("Locomotion"); land.writeDefaultValues = false;
            var tree = new BlendTree { name = "Red Bot Idle Slow Fast", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(Clip("Idle"), 0); tree.AddChild(Clip("Slow Run"), 3.4f); tree.AddChild(Clip("Fast Run"), 5.3f);
            land.motion = tree; machine.defaultState = land;
            var air = machine.AddState("Airborne"); air.motion = Clip("Jump"); air.writeDefaultValues = false;
            var swim = machine.AddState("Swimming"); swim.writeDefaultValues = false;
            var water = new BlendTree { name = "Red Bot Tread Swim", blendType = BlendTreeType.Simple1D, blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(water, controller);
            water.AddChild(Clip("Treading Water"), 0);
            water.AddChild(Clip("Swimming"), 1.87f); swim.motion = water;
            AnimatorStateTransition Transition(AnimatorState state)
            {
                var t = machine.AddAnyStateTransition(state); t.hasExitTime = false; t.hasFixedDuration = true;
                t.duration = .08f; t.canTransitionToSelf = false; return t;
            }
            var climb = machine.AddState("Ledge Climb"); climb.motion = Clip("Braced Hang To Crouch"); climb.writeDefaultValues = false;
            climb.timeParameter = "ClimbProgress"; climb.timeParameterActive = true;
            var climbing = Transition(climb); climbing.AddCondition(AnimatorConditionMode.If, 0, "Climbing");
            var wet = Transition(swim); wet.AddCondition(AnimatorConditionMode.If, 0, "Swimming");
            var grounded = Transition(land); grounded.AddCondition(AnimatorConditionMode.IfNot, 0, "Swimming"); grounded.AddCondition(AnimatorConditionMode.If, 0, "Grounded");
            var jumping = Transition(air); jumping.AddCondition(AnimatorConditionMode.IfNot, 0, "Swimming"); jumping.AddCondition(AnimatorConditionMode.IfNot, 0, "Grounded");
            foreach (var transition in new[] { wet, grounded, jumping }) transition.AddCondition(AnimatorConditionMode.IfNot, 0, "Climbing");
            var mask = new AvatarMask { name = "Right lantern arm only", hideFlags = HideFlags.HideInHierarchy };
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++) mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true); mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            AssetDatabase.AddObjectToAsset(mask, controller);
            var carryMachine = new AnimatorStateMachine { name = "Carried Lantern", hideFlags = HideFlags.HideInHierarchy };
            AssetDatabase.AddObjectToAsset(carryMachine, controller);
            var hold = carryMachine.AddState("Carry"); hold.motion = BuildHold(); hold.writeDefaultValues = false;
            carryMachine.defaultState = hold;
            controller.AddLayer(new AnimatorControllerLayer { name = "Carried Lantern", stateMachine = carryMachine, avatarMask = mask, defaultWeight = 0, blendingMode = AnimatorLayerBlendingMode.Override });
            EditorUtility.SetDirty(controller); return controller;
        }

        static Material Material(string name, Color tint)
        {
            string path = "Assets/Materials/Player/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
            material.name = name; material.SetColor("_BaseColor", tint); material.SetFloat("_Smoothness", .32f);
            material.SetFloat("_Metallic", .12f); material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }

        static void BuildPrefab()
        {
            var root = new GameObject("RedBot");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Source("Slow Run")), root.transform);
                visual.name = "RedBot_ModelAndSkeleton";
                ReusableGameplayAuthoring.CalibrateMannequin(visual.transform);
                var a = visual.GetComponent<Animator>(); a.runtimeAnimatorController = BuildController(); a.applyRootMotion = false; a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                visual.AddComponent<LedgeClimbHandIK>();
                visual.AddComponent<GroundFootIK>();
                var red = Material("RedBot_Surface", new Color(.837f, .302308f, .263655f));
                var joints = Material("RedBot_Joints", new Color(.333333f, .124529f, .101015f));
                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                foreach (var skin in skins) { skin.sharedMaterials = Enumerable.Repeat(skin.name.Contains("Joints") ? joints : red, skin.sharedMaterials.Length).ToArray(); skin.updateWhenOffscreen = true; }
                var avatar = root.AddComponent<CoastalPlayerAvatar>(); avatar.bodyRenderers = skins.Cast<Renderer>().ToArray(); avatar.keepShadowsWhenHidden = true;
                var driver = root.AddComponent<RiggedPlayerAnimation>(); driver.animator = a; driver.facingRoot = visual.transform; driver.modelYawOffset = 0;
                root.AddComponent<PlayerBodyAnchor>().animationDriver = driver;
                root.AddComponent<PlayerCarryAnimation>().animator = a;
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void BindScene()
        {
            var tour = Object.FindFirstObjectByType<CoastalWalkthrough>();
            if (!tour || !tour.playerCamera) throw new InvalidOperationException("Open CoastalTemple scene before binding.");
            var player = tour.walker; var rig = tour.playerCamera; var lantern = player.GetComponent<PlayerLantern>();
            if (!player.GetComponent<WaterCourtyard.CourtyardLedgeClimb>()) player.gameObject.AddComponent<WaterCourtyard.CourtyardLedgeClimb>();
            if (!player.GetComponent<WaterCourtyard.CourtyardSurfaceSwimmer>()) player.gameObject.AddComponent<WaterCourtyard.CourtyardSurfaceSwimmer>();
            string backup = "Assets/Scenes/Archive/CoastalTemple_BeforeRedBot.unity";
            if (!File.Exists(backup)) EditorSceneManager.SaveScene(tour.gameObject.scene, backup, true);
            if (lantern && lantern.carriedLantern) lantern.carriedLantern.transform.SetParent(player.transform, true);
            if (rig.hands)
            {
                if (rig.hands.overlayCamera) rig.view.GetUniversalAdditionalCameraData().cameraStack.Remove(rig.hands.overlayCamera);
                Object.DestroyImmediate(rig.hands.gameObject); rig.hands = null;
            }
            foreach (var old in player.GetComponentsInChildren<CoastalPlayerAvatar>(true)) Object.DestroyImmediate(old.gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), player.transform);
            model.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            var avatar = model.GetComponent<CoastalPlayerAvatar>(); avatar.walker = player;
            var driver = model.GetComponent<RiggedPlayerAnimation>(); driver.walker = player;
            var anchor = model.GetComponent<PlayerBodyAnchor>(); anchor.walker = player; anchor.SnapToWalker();
            var carry = model.GetComponent<PlayerCarryAnimation>(); carry.walker = player; carry.lantern = lantern;
            rig.avatar = avatar; rig.lantern = lantern;
            if (lantern)
            {
                lantern.firstPersonHand = null; lantern.worldHand = driver.animator.GetBoneTransform(carry.handBone);
                lantern.SetPerspective(true);
                if (lantern.handLight && lantern.carriedLantern)
                {
                    // The full body is visible now: light comes from the carried prop,
                    // rather than from the old first-person eye inside the bot's head.
                    lantern.handLight.transform.SetParent(lantern.carriedLantern.transform, false);
                    lantern.handLight.transform.localPosition = Vector3.zero;
                }
                EditorUtility.SetDirty(lantern);
            }
            rig.SetThirdPerson(true);
            player.ApplyLook(new Vector2(Mathf.DeltaAngle(player.LookYaw, player.transform.eulerAngles.y), 0));
            rig.SnapToTarget();
            foreach (var component in model.GetComponents<Component>()) PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(tour.gameObject.scene); EditorSceneManager.SaveScene(tour.gameObject.scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = model;
        }
    }
}
