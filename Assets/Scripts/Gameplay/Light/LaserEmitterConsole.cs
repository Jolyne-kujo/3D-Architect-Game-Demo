using CoastalTemple.Interaction;
using CoastalTemple.Tutorial;
using UnityEngine;

namespace CoastalTemple.LightPuzzles
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LaserEmitter))]
    public sealed class LaserEmitterConsole : TutorialInteractable
    {
        public LaserEmitter emitter;
        [Tooltip("Optional lens renderer. Its shared material is never modified.")]
        public Renderer indicator;
        public Renderer currentReadout;
        public Renderer nextIndicator;
        [Tooltip("E cycles through this order, including Off. Empty uses the legacy red/yellow/blue/off order.")]
        public LightColorChannel[] colorCycle = { LightColorChannel.Red, LightColorChannel.Yellow, LightColorChannel.Blue, LightColorChannel.None };
        public Light glow;
        public Color offColor = new Color(.12f, .15f, .18f, 1f);
        [Min(0f)] public float glowIntensity = 1f;
        MaterialPropertyBlock block;
        LightColorChannel displayedCurrent = (LightColorChannel)(-1), displayedNext = (LightColorChannel)(-1);
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int LegacyColor = Shader.PropertyToID("_Color");
        static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

        public LightColorChannel CurrentChannel => emitter && emitter.Powered ? emitter.Channel : LightColorChannel.None;
        public override bool Available => base.Available && emitter && emitter.isActiveAndEnabled;
        public LightColorChannel NextChannel
        {
            get
            {
                if (colorCycle == null || colorCycle.Length == 0) return Next(CurrentChannel);
                int current = System.Array.IndexOf(colorCycle, CurrentChannel);
                return colorCycle[(current + 1) % colorCycle.Length];
            }
        }
        public override string DisplayPrompt => "切换光色 · 当前：" + Label(CurrentChannel) + " · 下次：" + Label(NextChannel);

        void Reset()
        {
            emitter = GetComponent<LaserEmitter>();
            prompt = "切换光色";
        }
        protected override void OnEnable()
        {
            if (!emitter) emitter = GetComponent<LaserEmitter>();
            base.OnEnable();
            if (emitter) emitter.SetChannel(CurrentChannel);
            RefreshVisuals();
        }
        public override void Use(PlayerInteractor actor)
        {
            if (!Available) return;
            emitter.SetChannel(NextChannel);
            RefreshVisuals();
        }
        void LateUpdate()
        {
            if (displayedCurrent != CurrentChannel || displayedNext != NextChannel) RefreshVisuals();
        }

        public static LightColorChannel Next(LightColorChannel current)
        {
            switch (current)
            {
                case LightColorChannel.Red: return LightColorChannel.Yellow;
                case LightColorChannel.Yellow: return LightColorChannel.Blue;
                case LightColorChannel.Blue: return LightColorChannel.None;
                default: return LightColorChannel.Red;
            }
        }
        static string Label(LightColorChannel color)
        {
            switch (color)
            {
                case LightColorChannel.Red: return "红光";
                case LightColorChannel.Yellow: return "黄光";
                case LightColorChannel.Blue: return "蓝光";
                default: return "关闭";
            }
        }
        public void RefreshVisuals()
        {
            bool lit = emitter && emitter.isActiveAndEnabled && CurrentChannel != LightColorChannel.None;
            Color color = lit ? LaserEmitter.ColorForChannel(CurrentChannel) : offColor;
            Paint(indicator, color, lit);
            Paint(currentReadout, color, lit);
            bool nextLit = NextChannel != LightColorChannel.None;
            Paint(nextIndicator, nextLit ? LaserEmitter.ColorForChannel(NextChannel) : offColor, nextLit);
            if (glow)
            {
                glow.color = color / Mathf.Max(1f, color.maxColorComponent);
                glow.intensity = lit ? glowIntensity : 0f;
            }
            displayedCurrent = CurrentChannel; displayedNext = NextChannel;
        }
        void Paint(Renderer target, Color color, bool lit)
        {
            if (!target) return;
            if (block == null) block = new MaterialPropertyBlock();
            target.GetPropertyBlock(block);
            block.SetColor(BaseColor, color); block.SetColor(LegacyColor, color);
            block.SetColor(EmissionColor, lit ? color : Color.black);
            target.SetPropertyBlock(block);
        }
    }
}
