using System;
using System.Collections.Generic;
using System.Reflection;
using CoastalTemple.LightPuzzles;
using CoastalTemple.Mechanisms;
using Courtyard.Water;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CoastalTemple.Editor
{
    public static class MechanismWaterDeviceChecks
    {
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Enter Play mode to check the reusable water devices.");
            var lines = new List<string>();
            void Check(bool good, string label)
            {
                lines.Add((good ? "PASS " : "FAIL ") + label);
                if (!good) throw new InvalidOperationException(string.Join("\n", lines));
            }
            var scene = SceneManager.CreateScene("TemporaryWaterDeviceChecks", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var holder = new GameObject("WaterDeviceChecks"); SceneManager.MoveGameObjectToScene(holder, scene);
            const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
            WaterVolume Basin(string name, float y, float bottom)
            {
                var obj = new GameObject(name); obj.SetActive(false); obj.transform.SetParent(holder.transform, false);
                obj.transform.position = new Vector3(7000, y, 7000);
                var value = obj.AddComponent<WaterVolume>(); value.sizeX = value.sizeZ = 40; value.cellSize = 1;
                value.bottom = bottom; value.initialLevel = 0; obj.SetActive(true); value.Initialize(); return value;
            }
            Transform Anchor(Transform parent, string name, Vector3 local)
            {
                var obj = new GameObject(name); obj.transform.SetParent(parent, false); obj.transform.localPosition = local; return obj.transform;
            }
            void SetLight(LightReceiver receiver, bool lit)
            {
                typeof(LightTarget).GetMethod("BeginTrace", hidden).Invoke(receiver, null);
                if (lit) typeof(LightTarget).GetMethod("Illuminate", hidden).Invoke(receiver, new object[] { LightColorChannel.Red });
                typeof(LightTarget).GetMethod("Resolve", hidden).Invoke(receiver, new object[] { .3f });
            }
            void DrainUpdate(DrainGateDevice value) => typeof(DrainGateDevice).GetMethod("Update", hidden).Invoke(value, null);
            try
            {
                var upper = Basin("OverlappingUpperBasin", 40, -5);
                var water = Basin("OccupiedLowerBasin", 30, -10);
                var frame = Anchor(holder.transform, "RotatedScaledMirage", new Vector3(7000, 30, 7000)).gameObject;
                frame.SetActive(false); frame.transform.rotation = Quaternion.Euler(0, 31, 0); frame.transform.localScale = new Vector3(1.2f, 1.4f, .9f);
                var mirage = frame.AddComponent<WaterMirage>(); mirage.aimReference = frame.transform;
                mirage.source = Anchor(frame.transform, "Source", new Vector3(0, 2, -2)); mirage.source.localRotation = Quaternion.Euler(50, 0, 0);
                mirage.sampleObject = Anchor(frame.transform, "Sample", new Vector3(0, -1, 0));
                mirage.projectionPlane = Anchor(frame.transform, "Plane", new Vector3(0, -1, 1));
                mirage.target = Anchor(frame.transform, "ManualTarget", new Vector3(0, -2.401f, 1));
                var binding = frame.AddComponent<MirageWaterBinding>(); binding.mirage = mirage;
                frame.SetActive(true); binding.Resolve(); mirage.EvaluateNow();
                Check(mirage.water == water, "automatic mirage binding selects the occupied vertical water layer");
                Check(mirage.HasProjection && mirage.IsSolid && Vector3.Distance(mirage.target.position, mirage.ProjectedPoint) < .005f,
                    "rotated nonuniformly scaled mirage calibrates its initial target to a real refracted hit");
                Vector3 target = mirage.target.position;
                mirage.CycleAim(); binding.Resolve();
                Check(Vector3.Distance(mirage.target.position, target) < .00001f, "using E and resolving again cannot move the calibrated target");
                water.initialLevel = -.3f; water.ResetWater(); binding.Resolve();
                Check(Vector3.Distance(mirage.target.position, target) < .00001f, "changing water level cannot continuously recalibrate the puzzle target");
                var manual = Anchor(holder.transform, "UnprojectableMirage", new Vector3(7005, 30, 7000)).gameObject;
                manual.SetActive(false); var broken = manual.AddComponent<WaterMirage>();
                broken.water = water; broken.aimReference = manual.transform;
                broken.source = Anchor(manual.transform, "SourceUnderwater", new Vector3(0, -2, -2));
                broken.sampleObject = Anchor(manual.transform, "Sample", Vector3.down);
                broken.projectionPlane = Anchor(manual.transform, "Plane", Vector3.forward);
                broken.target = Anchor(manual.transform, "ManualTarget", new Vector3(0, -2, 1));
                var badBinding = manual.AddComponent<MirageWaterBinding>(); badBinding.mirage = broken;
                manual.SetActive(true); Vector3 manualTarget = broken.target.position; badBinding.Resolve();
                Check(broken.water == water && Vector3.Distance(broken.target.position, manualTarget) < .00001f,
                    "failed physical projection keeps the explicit water and manually authored target");

                var a = Anchor(holder.transform, "LitDrain", new Vector3(7000, 29, 7000)).gameObject;
                a.SetActive(false); var lit = a.AddComponent<LightReceiver>(); var drainA = a.AddComponent<DrainGateDevice>();
                drainA.water = water; drainA.receiver = lit; a.SetActive(true);
                var b = Anchor(holder.transform, "UnlitDrain", new Vector3(7001, 29, 7000)).gameObject;
                b.SetActive(false); var dark = b.AddComponent<LightReceiver>(); var drainB = b.AddComponent<DrainGateDevice>();
                drainB.water = water; drainB.receiver = dark; b.SetActive(true);
                water.ResetWater(); SetLight(lit, true); SetLight(dark, false);
                for (int i = 0; i < 100; i++) { DrainUpdate(drainA); DrainUpdate(drainB); }
                Check(water.Gate.IsOpen, "a second unlit drain cannot cancel the illuminated receiver's completed activation");
                water.ResetWater(); lit.enabled = false;
                for (int i = 0; i < 100; i++) DrainUpdate(drainA);
                Check(!water.Gate.IsOpen, "disabled receiver residual illumination cannot open its drain");
                lit.enabled = true; SetLight(lit, false); DrainUpdate(drainA);
                Check(!water.Gate.IsOpen, "an unlit receiver does not supply a drain command");
                var automatic = Anchor(holder.transform, "AutoDrain", new Vector3(7002, 29, 7000)).gameObject.AddComponent<DrainGateDevice>();
                automatic.Use(null);
                Check(automatic.water == water, "automatic drain binding rejects a basin whose floor is above the device");
                water.enabled = false; water.ResetWater(); drainA.Use(null);
                Check(drainA.water == water && !water.Gate.IsOpen, "an explicit disabled water is preserved but cannot be operated");
                water.enabled = true;
                return string.Join("\n", lines) + "\n" + lines.Count + " reusable water device checks passed.";
            }
            finally
            {
                if (holder) UnityEngine.Object.DestroyImmediate(holder);
                if (scene.IsValid() && scene.isLoaded) SceneManager.UnloadSceneAsync(scene);
            }
        }
    }
}
