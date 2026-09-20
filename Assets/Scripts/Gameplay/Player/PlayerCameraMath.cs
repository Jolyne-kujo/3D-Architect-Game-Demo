namespace CoastalTemple.Player
{
    public static class PlayerCameraMath
    {
        public static float ResolveDistance(float desired, float hitDistance, float padding, float current, float recoverySpeed, float seconds)
        {
            float limit = System.Math.Max(0, System.Math.Min(desired, hitDistance - padding));
            // Obstructions retract immediately; clearance is restored gradually so doorways do not pop the view.
            return limit < current ? limit : System.Math.Min(limit, current + System.Math.Max(0, recoverySpeed * seconds));
        }
    }
}
