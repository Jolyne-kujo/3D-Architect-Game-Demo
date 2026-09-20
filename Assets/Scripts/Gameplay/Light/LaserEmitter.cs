using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CoastalTemple.LightPuzzles
{
    [Serializable]
    public struct BeamSegment
    {
        public Vector3 Start;
        public Vector3 End;
        public BeamSegment(Vector3 start, Vector3 end) { Start = start; End = end; }
    }

    [DisallowMultipleComponent, DefaultExecutionOrder(400)]
    public sealed class LaserEmitter : MonoBehaviour
    {
        public const int MaximumSegments = 32;
        public bool Powered = true;
        [Tooltip("Gameplay color. Use SetChannel when switching color at runtime so the beam appearance follows it.")]
        public LightColorChannel Channel = LightColorChannel.Red;
        public Transform Origin;
        public Transform IgnoreRoot;
        [Min(0)] public float MaxDistance = 45f;
        [Range(0,16)] public int MaxPortalHops = 6;
        public LayerMask OccluderMask = ~0;
        public bool IgnorePlayer = true;
        public bool RenderBeam = true;
        [Min(.001f)] public float BeamRadius = .018f;
        [ColorUsage(true,true)] public Color BeamColor = new Color(4f,.12f,.035f,1f);
        public Material BeamMaterial;
        public int SegmentCount { get; private set; }
        public int PortalHopCount { get; internal set; }
        public bool TraceLimited { get; internal set; }
        readonly BeamSegment[] segments = new BeamSegment[MaximumSegments];
        readonly LineRenderer[] lines = new LineRenderer[MaximumSegments];
        Material ownedMaterial;
        Material copiedMaterial;

        void OnEnable() { LightPuzzleWorld.Register(this); }
        void OnDisable()
        {
            LightPuzzleWorld.Unregister(this);
            for (int i = 0; i < lines.Length; i++) if (lines[i]) lines[i].enabled = false;
        }
        void OnDestroy()
        {
            ReleaseMaterial();
        }
        void LateUpdate() { LightPuzzleWorld.Tick(); }
        public void TraceNow() { LightPuzzleWorld.Step(0f); }
        public void SetChannel(LightColorChannel channel)
        {
            Channel = channel;
            Powered = channel != LightColorChannel.None;
            BeamColor = ColorForChannel(channel);
        }
        public static Color ColorForChannel(LightColorChannel channel)
        {
            switch (channel)
            {
                case LightColorChannel.Red: return new Color(4f, .12f, .035f, 1f);
                case LightColorChannel.Yellow: return new Color(4f, 3f, .06f, 1f);
                case LightColorChannel.Blue: return new Color(.08f, .65f, 4f, 1f);
                default: return Color.clear;
            }
        }
        public BeamSegment GetSegment(int index)
        {
            if (index < 0 || index >= SegmentCount) throw new ArgumentOutOfRangeException(nameof(index));
            return segments[index];
        }
        internal void BeginTrace() { SegmentCount = 0; PortalHopCount = 0; TraceLimited = false; }
        internal void AddSegment(Vector3 start, Vector3 end)
        {
            if (SegmentCount < segments.Length) segments[SegmentCount++] = new BeamSegment(start,end);
        }
        internal void DisplayTrace()
        {
            if (RenderBeam && SegmentCount > 0) PrepareMaterial();
            for (int i = 0; i < lines.Length; i++)
            {
                bool visible = RenderBeam && i < SegmentCount;
                if (visible && !lines[i]) lines[i] = CreateLine(i);
                if (!lines[i]) continue;
                lines[i].enabled = visible;
                if (!visible) continue;
                lines[i].startWidth = lines[i].endWidth = BeamRadius * 2f;
                // Apply tint once, on a private material. Red source assets cannot multiply away blue or yellow.
                lines[i].sharedMaterial = ownedMaterial;
                lines[i].startColor = lines[i].endColor = Color.white;
                lines[i].SetPosition(0, segments[i].Start);
                lines[i].SetPosition(1, segments[i].End);
            }
        }
        LineRenderer CreateLine(int index)
        {
            var child = new GameObject("Laser segment " + index);
            child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.sharedMaterial = ownedMaterial;
            return line;
        }
        void PrepareMaterial()
        {
            if (!ownedMaterial || copiedMaterial != BeamMaterial)
            {
                ReleaseMaterial();
                copiedMaterial = BeamMaterial;
                if (BeamMaterial) ownedMaterial = new Material(BeamMaterial) { name = "Runtime laser" };
                else
                {
                    var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (!shader) shader = Shader.Find("Sprites/Default");
                    ownedMaterial = new Material(shader) { name = "Runtime laser" };
                }
            }
            if (ownedMaterial.HasProperty("_BaseColor")) ownedMaterial.SetColor("_BaseColor", BeamColor);
            if (ownedMaterial.HasProperty("_Color")) ownedMaterial.SetColor("_Color", BeamColor);
            if (ownedMaterial.HasProperty("_EmissionColor")) ownedMaterial.SetColor("_EmissionColor", BeamColor);
        }
        void ReleaseMaterial()
        {
            if (!ownedMaterial) return;
            if (Application.isPlaying) Destroy(ownedMaterial); else DestroyImmediate(ownedMaterial);
            ownedMaterial = null;
        }
    }
}
