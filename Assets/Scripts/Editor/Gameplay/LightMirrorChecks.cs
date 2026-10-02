using System;
using System.Collections.Generic;
using CoastalTemple.LightPuzzles;
using UnityEditor;
using UnityEngine;

namespace CoastalTemple.Editor
{
    public static class LightMirrorChecks
    {
        public static string Run()
        {
            var results = new List<string>();
            for (int scenario = 0; scenario < 5; scenario++)
            {
                var root = new GameObject("Mirror regression fixture");
                root.transform.position = new Vector3(16000, 500, 16000);
                try
                {
                    GameObject At(string name, Vector3 p)
                    { var g = new GameObject(name); g.transform.SetParent(root.transform, false); g.transform.localPosition = p; return g; }
                    var emitter = At("Source", Vector3.zero).AddComponent<LaserEmitter>();
                    emitter.RenderBeam = false; emitter.SetChannel(scenario == 4 ? LightColorChannel.Blue : LightColorChannel.Red);
                    var mirror = At("Mirror", new Vector3(0,0,4));
                    mirror.transform.localRotation = Quaternion.Euler(0,-45,0);
                    var box = mirror.AddComponent<BoxCollider>(); box.size = new Vector3(2,2,.06f);
                    var mirrorType = typeof(LaserEmitter).Assembly.GetType("CoastalTemple.LightPuzzles.LightMirror");
                    if (mirrorType != null) mirror.AddComponent(mirrorType);
                    var receiver = At("Receiver", new Vector3(4,0,4)).AddComponent<LightReceiver>();
                    receiver.RequiredHitSeconds = 0; receiver.OpticalSize = Vector3.one;
                    if (scenario == 1) { root.transform.rotation = Quaternion.Euler(0,67,0); root.transform.localScale = Vector3.one * 1.8f; }
                    if (scenario == 2) mirror.transform.localPosition += Vector3.up * 3;
                    if (scenario == 3) { var wall = At("Opaque wall", new Vector3(2,0,4)).AddComponent<BoxCollider>(); wall.size = Vector3.one; }
                    foreach (var c in root.GetComponentsInChildren<MonoBehaviour>())
                    { c.SendMessage("Awake", SendMessageOptions.DontRequireReceiver); c.SendMessage("OnEnable", SendMessageOptions.DontRequireReceiver); }
                    Physics.SyncTransforms(); LightPuzzleWorld.Step(.2f);
                    bool expected = scenario < 2;
                    if (receiver.IsActive != expected || (expected && (emitter.SegmentCount != 2 || receiver.IlluminatedColors != LightColorChannel.Red)))
                        throw new Exception("Expected receiver=" + expected + ", actual=" + receiver.IsActive + ", segments=" + emitter.SegmentCount);
                    results.Add("PASS " + scenario);
                }
                catch (Exception e) { results.Add("FAIL " + scenario + ": " + e.Message); }
                finally
                {
                    foreach (var c in root.GetComponentsInChildren<MonoBehaviour>()) c.SendMessage("OnDisable",SendMessageOptions.DontRequireReceiver);
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
            return string.Join("\n", results);
        }
    }
}
