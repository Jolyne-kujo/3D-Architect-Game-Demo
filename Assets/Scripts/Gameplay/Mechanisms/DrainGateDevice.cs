using CoastalTemple.Interaction;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Tutorial;
using Courtyard.Water;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    /// <summary>A portable drain receiver: local water lookup, light charging, and manual reset.</summary>
    public sealed class DrainGateDevice : TutorialInteractable
    {
        public WaterVolume water;
        public LightReceiver receiver;
        public Transform gate;
        public float gateOpenAngle = 80;
        float nextSearch;
        Quaternion closedRotation;
        bool captured;
        public override bool Available => base.Available && water && water.isActiveAndEnabled;
        public override string DisplayPrompt => water && water.Gate.IsOpen ? "重置水池与排水闸" : "手动打开排水闸";

        void Start() { Capture(); FindWater(); }
        void Capture() { if (!captured && gate) { closedRotation = gate.localRotation; captured = true; } }
        void FindWater()
        {
            // Never replace an explicit reference. A drained pool remains bound so E can refill it.
            if (water) return;
            float closestSurface = float.PositiveInfinity;
            foreach (var candidate in FindObjectsByType<WaterVolume>(FindObjectsInactive.Exclude))
            {
                if (!candidate.isActiveAndEnabled || candidate.Grid == null) continue;
                Vector3 local = transform.position - candidate.transform.position;
                if (Mathf.Abs(local.x) > candidate.sizeX * .5f || Mathf.Abs(local.z) > candidate.sizeZ * .5f) continue;
                bool wet = candidate.Sample(transform.position, out float surface, out _, out float depth);
                float floor = wet ? surface - depth : candidate.transform.position.y + candidate.bottom;
                if (transform.position.y < floor - .15f) continue;
                // Sampling is false for an empty basin, but a control above its floor must still reset it.
                float referenceSurface = wet ? surface : candidate.transform.position.y + candidate.initialLevel;
                float distance = Mathf.Abs(transform.position.y - referenceSurface);
                if (distance >= closestSurface) continue;
                water = candidate; closestSurface = distance;
            }
        }
        void Update()
        {
            if (!water && Time.unscaledTime >= nextSearch) { nextSearch = Time.unscaledTime + 1; FindWater(); }
            if (!water || !water.isActiveAndEnabled) return;
            // Each receiver already aggregates its own beams and owns its activation timer.
            // Opening is idempotent: another unlit control (or the original drain beam) cannot
            // clear its charge or multiply its timer by ticking the shared WaterVolume.Gate again.
            if (receiver && receiver.isActiveAndEnabled && receiver.IsActive && receiver.IsMatchingIlluminated) water.OpenDrain();
            Capture();
            if (gate) gate.localRotation = Quaternion.RotateTowards(gate.localRotation,
                closedRotation * Quaternion.Euler(water.Gate.IsOpen ? gateOpenAngle : 0, 0, 0), 70 * Time.deltaTime);
        }
        public override void Use(PlayerInteractor actor)
        {
            if (!isActiveAndEnabled) return;
            if (!water) FindWater();
            if (!water || !water.isActiveAndEnabled) return;
            if (water.Gate.IsOpen) { water.ResetWater(); if (receiver) receiver.ResetState(); }
            else water.OpenDrain();
        }
    }
}
