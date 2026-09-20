using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CoastalTemple.Editor
{
    /// <summary>Author a finger-only relaxed pose using the downloaded rig's own closed and open poses.</summary>
    public static class RelaxedHandsAuthoring
    {
        public static AnimatorController Configure(AnimatorController controller)
        {
            if(controller.layers.Any(l=>l.name=="Relaxed Hands"))return controller;
            var clips=AssetDatabase.LoadAllAssetsAtPath(ReusableGameplayAuthoring.ModelPath).OfType<AnimationClip>().ToArray();
            var closed=clips.Single(c=>c.name=="Rig|Idle_Loop");
            var open=clips.Single(c=>c.name=="Rig|A_TPose");
            string path="Assets/Animations/Player/RelaxedHands.anim";
            var pose=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(!pose)
            {
                pose=new AnimationClip{name="Relaxed Hands",frameRate=30};
                foreach(var binding in AnimationUtility.GetCurveBindings(closed))
                {
                    if(!binding.propertyName.StartsWith("LeftHand.")&&!binding.propertyName.StartsWith("RightHand."))continue;
                    var a=AnimationUtility.GetEditorCurve(closed,binding);
                    var b=AnimationUtility.GetEditorCurve(open,binding);if(b==null)continue;
                    // Preserve the rig's calibrated thumb/spread axes, relaxing the grip without a rigid flat palm.
                    float value=Mathf.Lerp(a.Evaluate(0),b.Evaluate(0),.78f);
                    AnimationUtility.SetEditorCurve(pose,binding,AnimationCurve.Constant(0,1,value));
                }
                var settings=AnimationUtility.GetAnimationClipSettings(pose);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(pose,settings);
                AssetDatabase.CreateAsset(pose,path);
            }
            var mask=new AvatarMask{name="Relaxed fingers only",hideFlags=HideFlags.HideInHierarchy};
            for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers,true);mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers,true);
            AssetDatabase.AddObjectToAsset(mask,controller);
            var machine=new AnimatorStateMachine{name="Relaxed Hands",hideFlags=HideFlags.HideInHierarchy};AssetDatabase.AddObjectToAsset(machine,controller);
            var state=machine.AddState("Relaxed Fingers");state.motion=pose;machine.defaultState=state;
            var layers=controller.layers.ToList();
            // The later torch layer retains the left-hand grip; the free hand stays relaxed and animated.
            layers.Insert(1,new AnimatorControllerLayer{name="Relaxed Hands",stateMachine=machine,avatarMask=mask,defaultWeight=1,blendingMode=AnimatorLayerBlendingMode.Override});
            controller.layers=layers.ToArray();EditorUtility.SetDirty(controller);return controller;
        }
    }
}
