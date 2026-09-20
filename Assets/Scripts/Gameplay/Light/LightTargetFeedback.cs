using UnityEngine;

namespace CoastalTemple.LightPuzzles
{
    // Visual feedback only. The target remains the sole authority for puzzle state.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(600)]
    public sealed class LightTargetFeedback : MonoBehaviour
    {
        public LightTarget target;
        public Renderer indicator;
        public Light glow;
        [Tooltip("Optional authored pointer. Charge rotates it around its original local Z axis.")]
        public Transform dial;
        public float dialSweepDegrees=270f;
        [Min(.01f)] public float responseSpeed=12f;
        public Color idleColor=new Color(.68f,.62f,.44f,1f);
        public Color illuminatedColor=new Color(.15f,.95f,1f,1f);
        public Color latchedColor=new Color(1f,.68f,.12f,1f);
        [Min(0)] public float idleEmission=.04f;
        [Min(0)] public float litEmission=1.6f;
        [Min(0)] public float glowIntensity=.7f;
        static readonly int BaseColor=Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor=Shader.PropertyToID("_Color");
        static readonly int EmissionColor=Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock block;
        Renderer capturedIndicator;
        Light capturedGlow;
        Transform capturedDial;
        bool captured,hasBaseColor,hasLegacyColor,hasEmission;
        Color originalBase,originalLegacy,originalEmission,originalGlowColor,currentColor;
        float originalGlowIntensity,currentEmission,currentProgress;
        Quaternion originalDialRotation;

        void OnEnable()=>Capture();
        void LateUpdate()=>Refresh(Time.deltaTime);
        void OnDisable()=>ResetMaterial();

        void Capture()
        {
            if(captured)return;
            if(block==null)block=new MaterialPropertyBlock();
            capturedIndicator=indicator;capturedGlow=glow;capturedDial=dial;
            if(capturedIndicator)
            {
                var material=capturedIndicator.sharedMaterial;
                capturedIndicator.GetPropertyBlock(block);
                hasBaseColor=material&&material.HasProperty(BaseColor);
                hasLegacyColor=material&&material.HasProperty(LegacyColor);
                hasEmission=material&&material.HasProperty(EmissionColor);
                if(hasBaseColor)originalBase=block.HasProperty(BaseColor)?block.GetColor(BaseColor):material.GetColor(BaseColor);
                if(hasLegacyColor)originalLegacy=block.HasProperty(LegacyColor)?block.GetColor(LegacyColor):material.GetColor(LegacyColor);
                if(hasEmission)originalEmission=block.HasProperty(EmissionColor)?block.GetColor(EmissionColor):material.GetColor(EmissionColor);
            }
            if(capturedGlow){originalGlowColor=capturedGlow.color;originalGlowIntensity=capturedGlow.intensity;}
            if(capturedDial)originalDialRotation=capturedDial.localRotation;
            currentColor=idleColor;currentEmission=idleEmission;currentProgress=0;captured=true;
        }

        public void Refresh(float seconds)
        {
            if(!captured||capturedIndicator!=indicator||capturedGlow!=glow||capturedDial!=dial){ResetMaterial();Capture();}
            var colorReceiver=target as LightReceiver;
            bool lit=target&&target.isActiveAndEnabled&&(colorReceiver?colorReceiver.IsMatchingIlluminated:target.IsIlluminated);
            bool active=false,latched=false;
            if(target&&target.isActiveAndEnabled)
            {
                if(target is RedStoneCurtain curtain){active=curtain.IsOpen;latched=curtain.IsPermanent;}
                else if(target is LightReceiver receiver){active=receiver.IsActive;latched=active&&receiver.Latching;}
            }
            float progress=target&&target.isActiveAndEnabled?Mathf.Clamp01(target.Progress):0f;
            if(active)progress=1f;
            Color desired=latched?latchedColor:(lit||active?illuminatedColor:idleColor);
            if(colorReceiver)
            {
                Color identity=LaserEmitter.ColorForChannel(colorReceiver.AcceptedChannel);
                identity/=Mathf.Max(1,identity.maxColorComponent);identity.a=1;
                desired=lit||active?identity:Color.Lerp(identity*.7f,Color.white,.08f);desired.a=1;
            }
            float emission=lit||active?Mathf.Lerp(litEmission*.35f,litEmission,progress):idleEmission;
            float blend=1f-Mathf.Exp(-Mathf.Max(.01f,responseSpeed)*Mathf.Max(0f,seconds));
            currentColor=Color.Lerp(currentColor,desired,blend);currentEmission=Mathf.Lerp(currentEmission,emission,blend);currentProgress=Mathf.Lerp(currentProgress,progress,blend);
            if(capturedIndicator)
            {
                // Read the current block so properties owned by other effects survive both update and reset.
                capturedIndicator.GetPropertyBlock(block);
                if(hasBaseColor)block.SetColor(BaseColor,currentColor);
                if(hasLegacyColor)block.SetColor(LegacyColor,currentColor);
                if(hasEmission)block.SetColor(EmissionColor,currentColor*currentEmission);
                capturedIndicator.SetPropertyBlock(block);
            }
            if(capturedGlow){capturedGlow.color=currentColor;capturedGlow.intensity=glowIntensity*Mathf.Clamp01(currentEmission/Mathf.Max(.001f,litEmission));}
            if(capturedDial)capturedDial.localRotation=originalDialRotation*Quaternion.AngleAxis(-dialSweepDegrees*currentProgress,Vector3.forward);
        }

        public void ResetMaterial()
        {
            if(!captured)return;
            if(capturedIndicator)
            {
                capturedIndicator.GetPropertyBlock(block);
                if(hasBaseColor)block.SetColor(BaseColor,originalBase);
                if(hasLegacyColor)block.SetColor(LegacyColor,originalLegacy);
                if(hasEmission)block.SetColor(EmissionColor,originalEmission);
                capturedIndicator.SetPropertyBlock(block);
            }
            if(capturedGlow){capturedGlow.color=originalGlowColor;capturedGlow.intensity=originalGlowIntensity;}
            if(capturedDial)capturedDial.localRotation=originalDialRotation;
            captured=false;
        }
    }
}
