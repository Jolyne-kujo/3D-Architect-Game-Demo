using Courtyard.Water;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    /// <summary>Optional local water discovery for a reusable projection device.</summary>
    [DefaultExecutionOrder(-50)]
    public sealed class MirageWaterBinding : MonoBehaviour
    {
        public WaterMirage mirage;
        [Tooltip("Once a water layer is bound, set the target to the default aim's real refracted hit. Disable to keep an authored target unchanged.")]
        public bool calibrateTargetOnBind = true;
        [Min(0), Tooltip("Aim used only for initial calibration; the current aim and source pose are restored afterwards.")]
        public int calibrationAimState;
        public bool TargetCalibrated { get; private set; }
        float nextSearch;
        bool calibrationAttempted;
        void Start() => Resolve();
        void Update()
        {
            if (mirage && mirage.water && calibrationAttempted) return;
            if (Time.unscaledTime < nextSearch) return;
            nextSearch = Time.unscaledTime + 1; Resolve();
        }
        public bool Resolve()
        {
            if (!mirage) mirage = GetComponent<WaterMirage>();
            if (!mirage || !mirage.isActiveAndEnabled) return false;
            // Explicit references are authoritative, even when temporarily disabled.
            if (!mirage.water)
            {
                Vector3 point = mirage.sampleObject ? mirage.sampleObject.position : transform.position;
                float highestSurface = float.NegativeInfinity;
                foreach (var candidate in FindObjectsByType<WaterVolume>(FindObjectsInactive.Exclude))
                {
                    if (!candidate.isActiveAndEnabled || !candidate.Sample(point, out float surface, out _, out float depth)
                        || !WaterMirageMath.InWater(point.y, surface, depth, mirage.minimumDepth) || surface <= highestSurface) continue;
                    mirage.water = candidate; highestSurface = surface;
                }
            }
            if (!mirage.water || !mirage.water.isActiveAndEnabled) return false;
            if (!calibrationAttempted)
            {
                calibrationAttempted = true;
                CalibrateInitialTarget();
            }
            return true;
        }

        void CalibrateInitialTarget()
        {
            if (!calibrateTargetOnBind || !mirage.target || !mirage.source) return;
            int previousAim = mirage.AimIndex;
            Quaternion previousRotation = mirage.source.rotation;
            mirage.SetAimState(calibrationAimState);
            // HasProjection includes immersion, finite path, basin floor, and obstruction checks.
            // If an extreme transform cannot produce such a ray, retain the manual target.
            if (mirage.HasProjection)
            {
                mirage.target.position = mirage.ProjectedPoint;
                TargetCalibrated = true;
            }
            mirage.SetAimState(previousAim);
            mirage.source.rotation = previousRotation;
            mirage.EvaluateNow();
        }
    }
}
