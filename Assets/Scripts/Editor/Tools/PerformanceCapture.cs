#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;

namespace CoastalTemple.Editor
{
    /// <summary>Allocation-free sampling of real Play Mode frames, not a tight simulation loop.</summary>
    public static class PerformanceCapture
    {
        [Serializable] public sealed class Metric
        {
            public string name, unit;
            public bool available;
            public double mean, median, p95, maximum;
        }
        [Serializable] public sealed class Report
        {
            public string scene, cpu, gpu, unityVersion, cameraStart, cameraEnd, quality;
            public int width, height, frames, targetFrameRate;
            public float lodBias;
            public Metric[] metrics;
        }

        static readonly string[] CounterNames = {
            "CPU Main Thread Frame Time", "CPU Render Thread Frame Time",
            "SRP Batcher Draw Calls Count", "Standard Draw Calls Count",
            "BRG Draw Calls Count", "BRG Indirect Draw Calls Count",
            "SetPass Calls Count", "Triangles Count"
        };
        static ProfilerRecorder[] recorders;
        static double[,] samples;
        static readonly FrameTiming[] timings = new FrameTiming[1];
        static double started;
        static int count, lastFrame;
        static string outputPath;
        static Report report;
        public static bool IsCapturing => samples != null;

        [MenuItem("Coastal Temple/Diagnostics/Capture performance (10 seconds in Play Mode)")]
        public static void CaptureMenu() => Begin("Logs/Performance/Manual-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".json");

        public static void Begin(string path)
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play Mode first.");
            if (IsCapturing) throw new InvalidOperationException("A performance capture is already running.");
            var camera = Camera.main;
            report = new Report {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                unityVersion = Application.unityVersion, cameraStart = Pose(camera),
                quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                width = camera ? camera.pixelWidth : Screen.width,
                height = camera ? camera.pixelHeight : Screen.height,
                targetFrameRate = Application.targetFrameRate, lodBias = QualitySettings.lodBias
            };
            outputPath = path;
            samples = new double[24000, CounterNames.Length + 2];
            recorders = CounterNames.Select(n => ProfilerRecorder.StartNew(ProfilerCategory.Render, n, 1)).ToArray();
            count = 0; lastFrame = -1; started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            AssemblyReloadEvents.beforeAssemblyReload += Cancel;
        }

        static string Pose(Camera camera) => camera ? camera.transform.position + " / " + camera.transform.eulerAngles : "none";

        static void Tick()
        {
            if (!EditorApplication.isPlaying) { Cancel(); return; }
            double elapsed = EditorApplication.timeSinceStartup - started;
            if (elapsed >= 13 || count >= samples.GetLength(0)) { Finish(); return; }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            FrameTimingManager.CaptureFrameTimings();
            if (elapsed < 3) return;
            samples[count, 0] = Time.unscaledDeltaTime * 1000;
            samples[count, 1] = FrameTimingManager.GetLatestTimings(1, timings) > 0 ? timings[0].gpuFrameTime : -1;
            for (int i = 0; i < recorders.Length; i++)
                samples[count, i + 2] = recorders[i].Valid ? recorders[i].LastValue : -1;
            count++;
        }

        static void Finish()
        {
            try
            {
                report.frames = count; report.cameraEnd = Pose(Camera.main);
                report.metrics = new Metric[CounterNames.Length + 2];
                for (int i = 0; i < report.metrics.Length; i++)
                {
                    int column = i;
                    double divisor = i == 2 || i == 3 ? 1e6 : 1;
                    var values = Enumerable.Range(0, count).Select(row => samples[row, column])
                        .Where(x => x >= 0).Select(x => x / divisor).OrderBy(x => x).ToArray();
                    var metric = new Metric {
                        name = i == 0 ? "Observed frame interval" : i == 1 ? "GPU frame time" : CounterNames[i - 2],
                        unit = i < 4 ? "ms" : "count", available = values.Length > 0
                    };
                    if (metric.available) {
                        metric.mean = values.Average(); metric.median = values[values.Length / 2];
                        metric.p95 = values[Math.Min(values.Length - 1, (int)(values.Length * .95))];
                        metric.maximum = values[values.Length - 1];
                    }
                    report.metrics[i] = metric;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)));
                File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
                Debug.Log("Performance capture saved: " + outputPath + " (" + count + " frames)");
            }
            finally { Cancel(); }
        }

        public static void Cancel()
        {
            EditorApplication.update -= Tick;
            AssemblyReloadEvents.beforeAssemblyReload -= Cancel;
            if (recorders != null) foreach (var recorder in recorders) recorder.Dispose();
            recorders = null; samples = null;
        }
    }
}
#endif
