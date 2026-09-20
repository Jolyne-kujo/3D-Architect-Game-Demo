namespace CoastalTemple.Mechanisms
{
    public static class MechanismDriveRules
    {
        // A receiver's activation delay is respected; grace and latching cannot keep a path or motor powered in darkness.
        public static bool IsPowered(bool enabled, bool active, bool illuminated) => enabled && active && illuminated;
        public static int ResolveDirection(bool forward, bool backward) => forward == backward ? 0 : forward ? 1 : -1;
    }
}
