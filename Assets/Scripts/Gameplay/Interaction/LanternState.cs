namespace CoastalTemple.Interaction
{
    /// <summary>Reusable possession state with no scene or tutorial dependency.</summary>
    public sealed class LanternState
    {
        public bool Acquired { get; private set; }
        public bool IsLit { get; private set; }

        public bool TryAcquire()
        {
            if (Acquired) return false;
            Acquired = true;
            IsLit = true;
            return true;
        }

        public void SetLight(bool requested) => IsLit = Acquired && requested;
    }
}
