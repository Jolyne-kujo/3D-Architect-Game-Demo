namespace CoastalTemple.Mechanisms
{
    // This solver deliberately has no Unity, light-system, or tutorial dependency.
    public static class LinearPlatformMotorMath
    {
        public static float Advance(float current, float bottom, float top, float speed, int direction, float seconds)
        {
            if (direction == 0 || seconds <= 0 || speed <= 0) return current;
            float target = direction > 0 ? top : bottom;
            float remaining = target - current;
            float travel = System.Math.Min(System.Math.Abs(remaining), speed * seconds);
            return current + (remaining < 0 ? -travel : travel);
        }
    }
}
