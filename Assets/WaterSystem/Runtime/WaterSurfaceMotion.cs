using UnityEngine;
namespace Courtyard.Water
{
    /// <summary>Optional analytic motion on a simulated mesh. Queries use the same displaced triangle vertices.</summary>
    public abstract class WaterSurfaceMotion:MonoBehaviour
    {
        public abstract float MaximumDisplacement { get; }
        public abstract float HeightOffset(Vector3 world,float depth,float time);
    }
}
