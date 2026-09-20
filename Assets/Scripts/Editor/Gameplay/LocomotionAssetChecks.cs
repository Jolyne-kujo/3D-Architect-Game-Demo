using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoastalTemple.Editor
{
    public static class LocomotionAssetChecks
    {
        public static string Run(string report = "Documentation/AssetOrganization/LocomotionAssets.txt")
        {
            var lines = new List<string>();
            void Check(bool valid, string message) => lines.Add((valid ? "PASS " : "FAIL ") + message);
            var slowPath = AssetDatabase.GUIDToAssetPath("69ad0ce074ca89b49bfa418b53bb956d");
            var fastPath = AssetDatabase.GUIDToAssetPath("30653e724fe41ba4fba96997785689f9");
            foreach (var path in new[] { slowPath, fastPath })
            {
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = model ? model.GetComponent<Animator>() : null;
                var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Single(c => !c.name.StartsWith("__preview__"));
                Check(animator && animator.avatar && animator.avatar.isHuman && animator.avatar.isValid, path + " has valid Humanoid avatar");
                Check(clip.humanMotion && clip.isLooping, clip.name + " is a looping retargetable humanoid clip");
                Check(AnimationUtility.GetCurveBindings(clip).Any(b => b.type == typeof(Animator) && b.propertyName.Contains("Leg")), clip.name + " contains retargeted leg muscle animation");
            }
            foreach (var guid in new[] { "1a3a88ce336c17642930e94694af5e85", "4bd7c89f7ce89bb4b9409be4a6ceb444" })
            {
                var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
                var state = controller.layers[0].stateMachine.states.Single(s => s.state.name == "Locomotion").state;
                var tree = state.motion as BlendTree;
                Check(tree && tree.children.Any(c => Mathf.Approximately(c.threshold, 3.4f) && AssetDatabase.GetAssetPath(c.motion) == slowPath), controller.name + " uses user Slow Run at normal movement speed");
                Check(tree && tree.children.Any(c => Mathf.Approximately(c.threshold, 5.3f) && AssetDatabase.GetAssetPath(c.motion) == fastPath), controller.name + " uses user Fast Run at sprint speed");
                Check(tree && tree.children.Any(c => Mathf.Approximately(c.threshold, 0) && c.motion.name == "Rig|Idle_Loop"), controller.name + " retains idle");
                Check(controller.layers[0].stateMachine.states.Any(s => s.state.name == "Swimming") && controller.layers[0].stateMachine.states.Any(s => s.state.name == "Airborne"), controller.name + " retains swimming and jumping");
            }
            Directory.CreateDirectory(Path.GetDirectoryName(report)); File.WriteAllLines(report, lines);
            return string.Join("\n", lines);
        }
    }
}
