using System;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using CoastalTemple.Player;
using CoastalTemple.LightPuzzles;
using Object = UnityEngine.Object;

namespace CoastalTemple.Editor
{
    public static class ReusableGameplayAuthoring
    {

        public const string ModelPath = "Assets/Objects/Characters/Quaternius/AnimationLibrary_Unity_Standard.fbx";

        [MenuItem("Coastal Temple/Legacy Quaternius/Prepare mannequin and stone curtain assets")]
        public static void PrepareAssets()
        {
            Directory.CreateDirectory("Assets/Prefabs/Player"); Directory.CreateDirectory("Assets/Prefabs/Light");
            AssetDatabase.Refresh();
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true; importer.importCameras = false; importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                clip.loopTime = clip.name.EndsWith("_Loop", StringComparison.Ordinal);
                clip.loopPose = clip.loopTime;
                clip.lockRootRotation = true; clip.lockRootPositionXZ = true; clip.lockRootHeightY = true;
                clip.keepOriginalOrientation = true; clip.keepOriginalPositionY = true;
                clip.keepOriginalPositionXZ = true;
            }
            importer.clipAnimations = clips; importer.SaveAndReimport();
            BuildMannequin(); BuildCurtain(CurtainMode.Permanent); BuildCurtain(CurtainMode.WhileIlluminated);
            AssetDatabase.SaveAssets();
        }

        static Material Material(string path, Color color)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", .2f); m.enableInstancing = true;
            EditorUtility.SetDirty(m); return m;
        }

        static void BuildMannequin()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var root = new GameObject("Quaternius_Mannequin");
            try
            {
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(model); visual.transform.SetParent(root.transform, false);
                visual.name = "Imported_Model_And_Skeleton";
                var animator = visual.GetComponent<Animator>();
                if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid)
                    throw new InvalidOperationException("Imported mannequin has no valid Humanoid avatar.");
                var skins = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
                visual.transform.localRotation = Quaternion.Euler(0,180,0);
                CalibrateMannequin(visual.transform);
                var body = Material("Assets/Materials/Player/Mannequin_Ivory.mat", new Color(.87f,.85f,.74f));
                foreach (var r in skins) { r.sharedMaterials = Enumerable.Repeat(body, r.sharedMaterials.Length).ToArray(); r.updateWhenOffscreen = true; }
                animator.runtimeAnimatorController = BuildController(); animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var visibility = root.AddComponent<CoastalPlayerAvatar>(); visibility.bodyRenderers = skins.Cast<Renderer>().ToArray();
                // No procedural poseRoot or joints: every visible pose is an imported skeletal animation.
                var driver = root.AddComponent<RiggedPlayerAnimation>(); driver.animator = animator; driver.facingRoot = visual.transform;
                root.AddComponent<PlayerBodyAnchor>().animationDriver = driver;
                PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Player/QuaterniusMannequin.prefab");
            }
            finally { Object.DestroyImmediate(root); }
        }

        public static void CalibrateMannequin(Transform visual)
        {
            // Skinned renderer bounds include animation/culling padding. Use actual bind-pose
            // vertices so the 1.8 m body stands on its soles instead of floating above the pivot.
            visual.localPosition = Vector3.zero;
            visual.localScale = Vector3.one;
            var bounds = new Bounds(); bool first = true;
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                // Imported mesh vertices are in bind pose; do not measure a sampled idle or swim pose.
                foreach (var vertex in skin.sharedMesh.vertices)
                {
                    var p = visual.parent.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                    if (first) { bounds = new Bounds(p, Vector3.zero); first = false; }
                    else bounds.Encapsulate(p);
                }
            }
            if (first || bounds.size.y <= 0) throw new InvalidOperationException("Mannequin contains no valid skin geometry.");
            float scale = 1.8f / bounds.size.y;
            visual.localScale = Vector3.one * scale;
            visual.localPosition = Vector3.up * (-bounds.min.y * scale);
        }

        static AnimatorController BuildController()
        {
            string path = "Assets/Animations/Player/MannequinLocomotion.controller";
            var existing = AssetDatabase.LoadAssetAtPath<AnimatorController>(path); if (existing) return PlayerLocomotionAuthoring.ApplyTo(existing);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Swimming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
            var clips = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
            AnimationClip Clip(string suffix) => clips.Single(c => c.name == "Rig|" + suffix);
            var machine = controller.layers[0].stateMachine;
            var land = machine.AddState("Locomotion"); var swim = machine.AddState("Swimming"); var air = machine.AddState("Airborne");
            BlendTree Tree(string name)
            {
                var tree = new BlendTree { name=name, blendType=BlendTreeType.Simple1D, blendParameter="Speed", useAutomaticThresholds=false };
                AssetDatabase.AddObjectToAsset(tree,controller); return tree;
            }
            var ground = Tree("Idle Walk Run"); ground.AddChild(Clip("Idle_Loop"),0); ground.AddChild(Clip("Walk_Loop"),3.4f); ground.AddChild(Clip("Jog_Fwd_Loop"),5.3f); land.motion=ground;
            var water = Tree("Float Swim"); water.AddChild(Clip("Swim_Idle_Loop"),0); water.AddChild(Clip("Swim_Fwd_Loop"),1.87f); swim.motion=water;
            air.motion=Clip("Jump_Loop"); machine.defaultState=land;
            var toSwim=machine.AddAnyStateTransition(swim); Setup(toSwim); toSwim.AddCondition(AnimatorConditionMode.If,0,"Swimming");
            var toLand=machine.AddAnyStateTransition(land); Setup(toLand); toLand.AddCondition(AnimatorConditionMode.IfNot,0,"Swimming"); toLand.AddCondition(AnimatorConditionMode.If,0,"Grounded");
            var toAir=machine.AddAnyStateTransition(air); Setup(toAir); toAir.AddCondition(AnimatorConditionMode.IfNot,0,"Swimming"); toAir.AddCondition(AnimatorConditionMode.IfNot,0,"Grounded");
            EditorUtility.SetDirty(controller); return PlayerLocomotionAuthoring.ApplyTo(controller);
        }
        static void Setup(AnimatorStateTransition t) { t.hasExitTime=false; t.hasFixedDuration=true; t.duration=.18f; t.canTransitionToSelf=false; }

        static void BuildCurtain(CurtainMode mode)
        {
            bool permanent=mode==CurtainMode.Permanent;
            var root=new GameObject(permanent?"RedStone_Permanent":"RedStone_WhileLit");
            try
            {
                var shape=root.AddComponent<BoxCollider>(); shape.size=new Vector3(4,3,.45f); shape.center=new Vector3(0,1.5f,0);
                var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tutorial/V8/StoneCurtain_Red.mat");
                var stripe=Material("Assets/Materials/Light/StoneRuleMark.mat",new Color(.95f,.77f,.46f));
                void Part(string name, Vector3 p, Vector3 size, Material m)
                {
                    var part=GameObject.CreatePrimitive(PrimitiveType.Cube);part.name=name;part.transform.SetParent(root.transform,false);
                    part.transform.localPosition=p;part.transform.localScale=size;Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=m;
                }
                Part("Replaceable_Art",shape.center,shape.size,material);
                for(int side=-1;side<=1;side+=2)
                {
                    if(permanent) Part("Single_Permanent_Mark",new Vector3(0,1.5f,side*.23f),new Vector3(.09f,2.5f,.018f),stripe);
                    else for(int band=-1;band<=1;band+=2)Part("Double_Returning_Mark",new Vector3(band*.25f,1.5f,side*.23f),new Vector3(.09f,2.5f,.018f),stripe);
                }
                var c=root.AddComponent<RedStoneCurtain>();c.Mode=mode;c.PhysicalCollider=shape;c.OpticalCollider=shape;c.Visuals=root.GetComponentsInChildren<Renderer>();
                c.ContinuousHitSeconds=.35f;c.ReturnGraceSeconds=.15f;
                PrefabUtility.SaveAsPrefabAsset(root,"Assets/Prefabs/Light/"+root.name+".prefab");
            }
            finally{Object.DestroyImmediate(root);}
        }

        // Historical V10 authoring only. Current scene binding is FirstPersonAuthoring.Build.
        public static string BindScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play before editing authored objects.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();var roots=scene.GetRootGameObjects();
            var tour=roots.SelectMany(r=>r.GetComponentsInChildren<CoastalWalkthrough>(true)).Single();
            var player=tour.walker;var rig=tour.playerCamera;
            var lantern=player.GetComponent<PlayerLantern>();
            var carried=lantern ? lantern.carriedLantern : null;
            // Preserve scene-owned accessories before replacing the old model hierarchy.
            if(carried)carried.transform.SetParent(player.transform,true);
            foreach(var old in player.GetComponentsInChildren<CoastalPlayerAvatar>(true))Object.DestroyImmediate(old.gameObject);
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/QuaterniusMannequin.prefab"),player.transform);
            model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;
            var avatar=model.GetComponent<CoastalPlayerAvatar>();avatar.walker=player;
            model.GetComponent<RiggedPlayerAnimation>().walker=player;
            if(lantern)
            {
                if(!carried)
                {
                    carried=GameObject.CreatePrimitive(PrimitiveType.Cube);carried.name="Lantern_Carried";
                    Object.DestroyImmediate(carried.GetComponent<Collider>());
                    carried.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Tutorial/V8/Sunlight_Gold.mat");
                }
                var hand=model.GetComponentInChildren<Animator>().GetBoneTransform(HumanBodyBones.RightHand);
                carried.transform.SetParent(hand,false);carried.transform.localPosition=new Vector3(0,-.07f,0);
                carried.transform.localRotation=Quaternion.identity;
                var handScale=hand.lossyScale;
                carried.transform.localScale=new Vector3(.12f/Mathf.Abs(handScale.x),.18f/Mathf.Abs(handScale.y),.12f/Mathf.Abs(handScale.z));
                carried.SetActive(false);lantern.carriedLantern=carried;
                if(tour.tutorial){tour.tutorial.carriedLantern=carried;EditorUtility.SetDirty(tour.tutorial);}
                EditorUtility.SetDirty(lantern);
            }
            var anchor=player.transform.Find("CameraFollow_稳定跟随点");if(!anchor){anchor=new GameObject("CameraFollow_稳定跟随点").transform;anchor.SetParent(player.transform,false);}
            rig.walker=player;rig.avatar=avatar;rig.followAnchor=anchor;rig.overviewRoot=player.transform.parent;rig.thirdPerson=true;
            rig.SnapToTarget();PrefabUtility.RecordPrefabInstancePropertyModifications(avatar);PrefabUtility.RecordPrefabInstancePropertyModifications(model.GetComponent<RiggedPlayerAnimation>());
            var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            Transform Find(string name)=>all.Single(t=>t.name==name);
            void Note(Transform target,string label,string explanation,bool marker=false)
            {
                var note=target.GetComponent<SceneObjectGuide>();if(!note)note=target.gameObject.AddComponent<SceneObjectGuide>();
                note.label=label;note.explanation=explanation;note.drawMarker=marker;EditorUtility.SetDirty(note);
            }
            var sun=Find("Sun");var tone=Find("Courtyard tone");
            // Directional illumination and Global Volume are translation-invariant. Keep their icons near the shore for editing.
            sun.position=player.transform.position+Vector3.up*12;tone.position=player.transform.position;
            Note(sun,"全局太阳光 · 改旋转才改变光向","Directional Light 模拟无限远太阳，位置不影响照明。Rotation 决定光照和阴影方向；Intensity / Color 调亮度和颜色。它不是关卡激光，不会消除红石幕。图标原来在世界原点，并非藏在山后布光。");
            Note(tone,"全局画面色调 · 位置无效","Global Volume，使用 CoastalTone 配置中的 Tonemapping 和 ColorAdjustments。影响全屏色调、曝光、对比度、饱和度。勾选 Is Global 时没有空间边界，移动不会改变效果。通过 Profile 编辑效果。");
            var landmarks=Find("11_CoastalLandmarks");landmarks.gameObject.tag="EditorOnly";
            Note(landmarks,"编辑定位标记 · 不控制地形","以下三个空对象只是关卡规划位置。当前无运行时组件引用它们；移动不会带动山体、沙滩或神庙。组已标为 EditorOnly，不进入游戏构建。");
            foreach(Transform landmark in landmarks)
            {
                landmark.gameObject.tag="EditorOnly";
                string label=landmark.name.StartsWith("Halfway")?"半山腰规划位置":landmark.name.StartsWith("HiddenBeach")?"隐藏海滩关卡规划位置":"山顶规划位置";
                Note(landmark,label+" · 仅标记","这是空的编辑定位点，不是模型和地形控制器。选中可看位置，拖动只移动标记。实际修改地形请选 10_CompactMountain，修改建筑请选 20_Architecture。",true);
            }
            for(int i=0;i<tour.viewpoints.Length;i++)Note(tour.viewpoints[i],"全景摄影机预设 F"+(i+1),"此点保存全景取景的坐标和朝向。移动或旋转后，按相应 F 键重新取景才会应用，不会改变地形或主视角跟随点。");
            Note(anchor,"玩家摄像机跟随点","主摄像机在行走 / 游泳时绑定在此点下。该点跟随角色根节点，不跟随动画头骨，避免动作抖动。C 切换第一 / 第三人称；全景模式暂时解绑，回到角色时重新绑定。");
            EditorUtility.SetDirty(rig);EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
            return "Imported Humanoid and follow camera bound; lighting and guide objects annotated; reusable curtain prefabs ready.";
        }
    }
}
