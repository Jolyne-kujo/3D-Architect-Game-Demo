using System;

namespace CoastalTemple.LightPuzzles
{
    // Shared by curtains and receivers; independent of rendering and physics.
    public sealed class LightActivationState
    {
        float continuousSeconds;
        float darkSeconds;
        public bool Active { get; private set; }
        public float Progress { get; private set; }

        public void Step(bool illuminated, float deltaTime, float requiredSeconds, float graceSeconds, bool latching)
        {
            deltaTime = Math.Max(0f, deltaTime);
            requiredSeconds = Math.Max(0f, requiredSeconds);
            graceSeconds = Math.Max(0f, graceSeconds);
            if (illuminated)
            {
                darkSeconds = 0f;
                continuousSeconds += deltaTime;
                Progress = requiredSeconds <= 0f ? 1f : Math.Min(1f, continuousSeconds / requiredSeconds);
                if (continuousSeconds + 0.000001f >= requiredSeconds) Active = true;
            }
            else
            {
                continuousSeconds = 0f;
                darkSeconds += deltaTime;
                if (!latching && darkSeconds >= graceSeconds) Active = false;
                Progress = Active ? 1f : 0f;
            }
            if (Active && latching) Progress = 1f;
        }

        public void Reset()
        {
            Active = false;
            Progress = continuousSeconds = darkSeconds = 0f;
        }
    }
}
