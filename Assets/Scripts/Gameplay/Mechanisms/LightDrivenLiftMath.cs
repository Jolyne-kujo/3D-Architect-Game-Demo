namespace CoastalTemple.Mechanisms
{
    public static class LightDrivenLiftMath
    {
        public static float Advance(float current, float bottom, float top, float speed, bool raise, bool lower, float seconds)
        {
            if (raise == lower || seconds <= 0 || speed <= 0) return current;
            float target = raise ? top : bottom;
            float remaining = target - current;
            float travel = System.Math.Min(System.Math.Abs(remaining), speed * seconds);
            return current + (remaining < 0 ? -travel : travel);
        }
    }
}
