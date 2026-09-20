using System;
using System.Reflection;
using CoastalTemple.Tutorial;

int passed = 0, failed = 0;
void Check(bool good, string name) { Console.WriteLine((good ? "PASS " : "FAIL ") + name); if (good) passed++; else failed++; }
Check(TutorialInteractionPolicy.CanReach(4, 2, false, true), "exact interaction radius is included");
Check(!TutorialInteractionPolicy.CanReach(4.001, 2, false, true), "outside real distance is rejected");
Check(!TutorialInteractionPolicy.CanReach(1, 2, true, true), "an obstruction forbids interaction");
Check(!TutorialInteractionPolicy.CanReach(1, 2, false, false), "inactive players cannot interact");
Check(!TutorialInteractionPolicy.CanReach(double.NaN, 2, false, true), "invalid distance is rejected");
Check(!TutorialInteractionPolicy.CanReach(1, float.PositiveInfinity, false, true), "invalid radius is rejected");
var assembly = Assembly.GetExecutingAssembly();
var lantern = assembly.GetType("CoastalTemple.Interaction.LanternState");
Check(lantern != null, "lantern possession is callable independently from a tutorial");
if (lantern != null)
{
    object state = Activator.CreateInstance(lantern);
    bool Flag(string name) => (bool)lantern.GetProperty(name).GetValue(state);
    bool Acquire() => (bool)lantern.GetMethod("TryAcquire").Invoke(state, null);
    void Light(bool value) => lantern.GetMethod("SetLight").Invoke(state, new object[] { value });
    Light(true); Check(!Flag("Acquired") && !Flag("IsLit"), "uncollected lantern refuses light");
    Check(Acquire() && Flag("Acquired") && Flag("IsLit"), "pickup grants possession and illumination");
    Light(false); Check(Flag("Acquired") && !Flag("IsLit"), "switching off retains possession");
    Check(!Acquire() && !Flag("IsLit"), "repeat pickup neither awards nor toggles an acquired lantern");
    Light(true); Check(Flag("IsLit"), "acquired lantern switches on without tutorial state");
}
var progress = assembly.GetType("CoastalTemple.Tutorial.LessonProgress");
Check(progress != null, "configurable lesson progression is independent of mechanism types");
if (progress != null)
{
    object state = Activator.CreateInstance(progress, new object[] { 3 });
    int Hint(int station, int count) => (int)progress.GetMethod("NextHintIndex").Invoke(state, new object[] { station, count });
    bool Complete(int station, bool actual) => (bool)progress.GetMethod("TryComplete").Invoke(state, new object[] { station, actual });
    Check(Hint(0, 3) == 0 && Hint(0, 3) == 1 && Hint(0, 3) == 2 && Hint(0, 3) == 2, "station hints advance and stop at the authored final hint");
    Check(Hint(1, 2) == 0, "each lesson has independent hint progression");
    Check(Hint(2, 0) == -1 && Hint(-1, 3) == -1, "absent or invalid lesson hints have no invented content");
    Check(!Complete(0, false), "proximity or hint progress cannot complete an unsolved world state");
    Check(Complete(0, true), "actual completion emits one first success");
    Check(!Complete(0, true) && !Complete(0, false) && !Complete(0, true), "reversible world changes do not repeat an earned reward");
    Check(Complete(2, true), "another independently solved lesson still completes");
}
Console.WriteLine($"Interaction core checks: {passed} passed, {failed} failed.");
Environment.ExitCode = failed == 0 ? 0 : 1;
