namespace CoastalTemple.LightPuzzles
{
    // Independent timers prevent temporary yellow light from ever contributing to the red latch.
    public sealed class ColorCurtainState
    {
        readonly LightActivationState red = new LightActivationState();
        readonly LightActivationState yellow = new LightActivationState();
        public bool Active => red.Active || yellow.Active;
        public bool Permanent => red.Active;
        public float Progress => red.Progress;

        public void Step(LightColorChannel colors, float deltaTime, float redSeconds, float yellowGraceSeconds)
        {
            red.Step((colors & LightColorChannel.Red) != 0, deltaTime, redSeconds, 0f, true);
            yellow.Step((colors & LightColorChannel.Yellow) != 0, deltaTime, 0f, yellowGraceSeconds, false);
        }

        public void Reset()
        {
            red.Reset();
            yellow.Reset();
        }
    }
}
