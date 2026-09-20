namespace CoastalTemple.Mechanisms
{
    public sealed class MotorLeverState
    {
        int phase = 3;
        public int Direction => phase == 0 ? 1 : phase == 2 ? -1 : 0;
        public void Cycle() { phase = (phase + 1) % 4; }
        public void SetDirection(int direction)
        {
            if (direction > 0) phase = 0;
            else if (direction < 0) phase = 2;
            else phase = phase == 0 ? 1 : phase == 2 ? 3 : phase;
        }
    }
}
