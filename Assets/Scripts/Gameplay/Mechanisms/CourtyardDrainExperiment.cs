using Courtyard.Water;
using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Mechanisms
{
    // An authored water experiment that can run without a player, tutorial or walkthrough.
    [DisallowMultipleComponent]
    public sealed class CourtyardDrainExperiment : MonoBehaviour
    {
        public WaterVolume water;
        public RefractedDrainBeam beam;
        public Transform drainGate;
        public BuoyantBody[] floaters = System.Array.Empty<BuoyantBody>();
        public bool Powered => beam && beam.powered;

        float gateAngle;

        public void StartDrain()
        {
            if (beam && water) beam.powered = true;
        }

        public void ResetExperiment()
        {
            if (beam) beam.powered = false;
            if (water) water.ResetWater();
            if (floaters != null)
                foreach (var floater in floaters)
                    if (floater) floater.ResetBody();
            gateAngle = 0;
            if (drainGate) drainGate.localRotation = Quaternion.Euler(0, 0, 0);
        }

        public void Toggle()
        {
            if (Powered) ResetExperiment();
            else StartDrain();
        }

        void Update() => SimulateGate(Time.deltaTime);

        public void SimulateGate(float seconds)
        {
            if (!drainGate || !water || seconds <= 0) return;
            gateAngle = Mathf.MoveTowards(gateAngle, water.Gate.IsOpen ? 82 : 0, seconds * 35);
            drainGate.localRotation = Quaternion.Euler(gateAngle, 0, 0);
        }
    }
}
