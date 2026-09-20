// Test-only pre-implementation baseline: the feature does not activate yet.
namespace CoastalTemple.LightPuzzles {
 public sealed class LightActivationState {
  public bool Active { get; private set; }
  public float Progress { get; private set; }
  public void Step(bool illuminated, float deltaTime, float requiredSeconds, float graceSeconds, bool latching) { }
  public void Reset() { }
 }
}
