using System;
using CoastalTemple.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CoastalTemple.Editor
{
    public static class ThirdPersonAuthoring
    {
        [MenuItem("Coastal Temple/Player/Use third-person preview")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play before saving camera settings.");
            var tour = UnityEngine.Object.FindFirstObjectByType<CoastalWalkthrough>();
            if (!tour || !tour.playerCamera) throw new InvalidOperationException("Open CoastalTemple scene first.");
            var rig = tour.playerCamera; var walker = tour.walker;
            Undo.RecordObjects(new UnityEngine.Object[] { rig, walker, tour.view, tour.view.transform }, "Use third-person preview");
            rig.distance = 4.2f; rig.pivotHeight = 1.48f; rig.shoulderOffset = .35f; rig.pitchBias = 10;
            rig.swimmingPivotHeight = 2.1f; rig.swimmingPitchBias = 15;
            rig.overviewRoot = walker.transform.parent;
            var body = walker.GetComponentInChildren<RiggedPlayerAnimation>(true);
            var lantern = walker.GetComponent<PlayerLantern>();
            rig.lantern = lantern;
            if (lantern && body)
            {
                var carry = body.GetComponent<PlayerCarryAnimation>();
                lantern.worldHand = body.animator.GetBoneTransform(carry ? carry.handBone : HumanBodyBones.LeftHand);
                lantern.firstPersonHand = rig.hands ? rig.hands.animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
                EditorUtility.SetDirty(lantern);
            }
            rig.SetThirdPerson(true);
            walker.ApplyLook(new Vector2(Mathf.DeltaAngle(walker.LookYaw, walker.transform.eulerAngles.y), 0));
            rig.SnapToTarget();
            tour.view.fieldOfView = 65; tour.view.nearClipPlane = .05f;
            tour.inspectionShortcuts = false;
            var guide = walker.eye.GetComponent<SceneObjectGuide>();
            if (guide)
            {
                guide.label = "角色眼部 · 交互与第一人称备用";
                guide.explanation = "当前为第三人称：主镜头绕角色稳定支点跟随，鼠标观察不移动身体；WASD 按镜头水平朝向移动，角色朝运动方向转身。Eye 保留近身交互与提灯照明位置。调整镜头请选 Player_ShoreStart 上的 CoastalPlayerCamera。";
                EditorUtility.SetDirty(guide);
            }
            foreach (var renderer in rig.avatar.bodyRenderers) PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            if (rig.hands && rig.hands.model) PrefabUtility.RecordPrefabInstancePropertyModifications(rig.hands.model.gameObject);
            if (rig.hands && rig.hands.overlayCamera) PrefabUtility.RecordPrefabInstancePropertyModifications(rig.hands.overlayCamera);
            EditorUtility.SetDirty(rig); EditorUtility.SetDirty(walker); EditorUtility.SetDirty(tour);
            EditorSceneManager.MarkSceneDirty(tour.gameObject.scene);
            EditorSceneManager.SaveScene(tour.gameObject.scene); AssetDatabase.SaveAssets();
            Selection.activeGameObject = walker.gameObject;
        }
    }
}
