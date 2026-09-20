using System;

namespace CoastalTemple.Tutorial
{
    // Progress and rewards deliberately are not inputs to interaction permission.
    public static class TutorialInteractionPolicy
    {
        public static bool CanReach(double distanceSquared, float radius, bool obstructed, bool active)
        {
            return active && !obstructed && radius >= 0 && !float.IsNaN(radius) && !float.IsInfinity(radius)
                && distanceSquared >= 0 && !double.IsNaN(distanceSquared) && !double.IsInfinity(distanceSquared)
                && distanceSquared <= (double)radius * radius;
        }

        public static bool CanLightLantern(bool acquired, bool requested) => acquired && requested;
    }
}
