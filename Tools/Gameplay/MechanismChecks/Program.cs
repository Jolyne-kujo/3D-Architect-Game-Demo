using System;
using System.Reflection;

int passed = 0, failed = 0;
void Check(bool good, string label)
{
    if (good) passed++; else failed++;
    Console.WriteLine((good ? "PASS " : "FAIL ") + label);
}
var assembly = Assembly.GetExecutingAssembly();
var math = assembly.GetType("CoastalTemple.Mechanisms.LinearPlatformMotorMath");
var rules = assembly.GetType("CoastalTemple.Mechanisms.MechanismDriveRules");
var lever = assembly.GetType("CoastalTemple.Mechanisms.MotorLeverState");
Check(math != null, "a motor can solve bounded travel without a light or tutorial assembly");
Check(rules != null, "power sampling rejects stale activation after light loss or disable");
Check(lever != null, "a manual lever owns its raise / stop / lower / stop cycle");
if (math != null)
{
    float Step(float current, float bottom, float top, float speed, int direction, float seconds) =>
        (float)math.GetMethod("Advance")!.Invoke(null, new object[] { current, bottom, top, speed, direction, seconds })!;
    Check(Step(2, 0, 8, 1.5f, 1, 1) == 3.5f, "positive command advances at configured speed");
    Check(Step(2, 0, 8, 1.5f, -1, 1) == .5f, "negative command reverses at configured speed");
    Check(Step(2, 0, 8, 1.5f, 0, 100) == 2, "stop holds an arbitrary intermediate position indefinitely");
    Check(Step(7.9f, 0, 8, 1.5f, 1, 1) == 8, "forward endpoint cannot overshoot");
    Check(Step(.1f, 0, 8, 1.5f, -1, 1) == 0, "reverse endpoint cannot overshoot");
    Check(Step(2, 0, 8, 1.5f, 1, 0) == 2, "zero simulation duration holds");
    Check(Step(2, 0, 8, -1, 1, 1) == 2, "negative speed holds");
    Check(Step(2, 0, 8, 1.5f, 90, 1) == 3.5f, "direction magnitude does not multiply speed");
    Check(Step(9, 0, 8, 1.5f, 1, .1f) > 8.8f, "out of range placement recovers without teleporting");
    foreach (float hz in new[] { 30f, 60f, 144f })
    {
        float current = 0;
        for (int i = 0; i < 3 * hz; i++) current = Step(current, 0, 8, 1.5f, 1, 1 / hz);
        float stopped = current;
        for (int i = 0; i < 3 * hz; i++) current = Step(current, 0, 8, 1.5f, 0, 1 / hz);
        Check(Math.Abs(current - 4.5f) < .002f && current == stopped, $"intermediate stop stays fixed at {hz} Hz");
    }
}
if (rules != null)
{
    bool Powered(bool enabled, bool active, bool lit) =>
        (bool)rules.GetMethod("IsPowered")!.Invoke(null, new object[] { enabled, active, lit })!;
    int Direction(bool forward, bool backward) =>
        (int)rules.GetMethod("ResolveDirection")!.Invoke(null, new object[] { forward, backward })!;
    Check(!Powered(true, true, false), "light loss rejects receiver grace or latch state immediately");
    Check(!Powered(false, true, true), "disabled receiver rejects both stale active and illuminated state");
    Check(!Powered(true, false, true), "receiver activation delay is respected");
    Check(Powered(true, true, true), "active illuminated receiver supplies power");
    Check(Direction(true, true) == 0, "opposing drives stop instead of choosing a winner");
    Check(Direction(false, false) == 0, "unpowered drive holds position");
    Check(Direction(true, false) == 1 && Direction(false, true) == -1, "only one requested direction is accepted");
}
if (lever != null)
{
    object state = Activator.CreateInstance(lever)!;
    int Direction() => (int)lever.GetProperty("Direction")!.GetValue(state)!;
    void Cycle() => lever.GetMethod("Cycle")!.Invoke(state, null);
    void Set(int direction) => lever.GetMethod("SetDirection")!.Invoke(state, new object[] { direction });
    Check(Direction() == 0, "new hand lever begins stopped");
    foreach (int expected in new[] { 1, 0, -1, 0, 1, 0, -1, 0 })
    {
        Cycle(); Check(Direction() == expected, "manual cycle selects " + expected);
    }
    Set(-8); Check(Direction() == -1, "direct negative command is normalized");
    Cycle(); Check(Direction() == 0, "using lever after a direct lower command stops it");
    Cycle(); Check(Direction() == 1, "next cycle raises after a lower stop");
    Set(0); Check(Direction() == 0, "direct stop immediately clears direction");
}
Console.WriteLine($"Mechanism checks: {passed} passed, {failed} failed.");
Environment.ExitCode = failed == 0 ? 0 : 1;
