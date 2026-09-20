using System.Collections.Generic;
using UnityEngine;

namespace CoastalTemple.LightPuzzles
{
    // Registry lifetime follows components. One pass aggregates all sources before any target resolves.
    public static class LightPuzzleWorld
    {
        static readonly List<LaserEmitter> sources = new List<LaserEmitter>(16);
        static readonly List<LightTarget> targets = new List<LightTarget>(32);
        static readonly List<LightTarget> resolvingTargets = new List<LightTarget>(32);
        static readonly List<LightTarget> resettingTargets = new List<LightTarget>(32);
        static readonly List<LightPortal> portals = new List<LightPortal>(16);
        static readonly LightTarget[] passedTargets = new LightTarget[LaserEmitter.MaximumSegments];
        static readonly RaycastHit[] hits = new RaycastHit[64];
        static readonly RaycastHit[] crowdedHits = new RaycastHit[256];
        static int frame = -1;
        static bool resolving;
        static bool resetting;

        static void Add<T>(List<T> list, T value) where T : Object
        {
            if (list.Contains(value)) return;
            int index = 0;
            while (index < list.Count && list[index] && list[index].GetEntityId().GetHashCode() < value.GetEntityId().GetHashCode()) index++;
            list.Insert(index, value);
        }
        internal static void Register(LaserEmitter value) { Add(sources, value); }
        internal static void Unregister(LaserEmitter value) { sources.Remove(value); }
        internal static void Register(LightTarget value) { Add(targets, value); }
        internal static void Unregister(LightTarget value) { targets.Remove(value); }
        internal static void Register(LightPortal value) { Add(portals, value); }
        internal static void Unregister(LightPortal value) { portals.Remove(value); }

        internal static void Tick()
        {
            if (!Application.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            Step(Time.deltaTime);
        }

        public static void Step(float deltaTime)
        {
            if (resolving) return;
            resolving = true;
            try
            {
                // Callbacks may disable or destroy components; retain a reusable snapshot for this pass.
                resolvingTargets.Clear();
                resolvingTargets.AddRange(targets);
                for (int i = 0; i < resolvingTargets.Count; i++) if (resolvingTargets[i]) resolvingTargets[i].BeginTrace();
                for (int i = 0; i < sources.Count; i++) if (sources[i]) Trace(sources[i]);
                for (int i = 0; i < resolvingTargets.Count; i++)
                    if (resolvingTargets[i] && resolvingTargets[i].isActiveAndEnabled) resolvingTargets[i].Resolve(Mathf.Max(0f, deltaTime));
            }
            finally { resolving = false; }
        }

        public static void ResetAll()
        {
            if (resetting) return;
            resetting = true;
            try
            {
                resettingTargets.Clear();
                resettingTargets.AddRange(targets);
                for (int i = 0; i < resettingTargets.Count; i++) if (resettingTargets[i]) resettingTargets[i].ResetState();
            }
            finally { resetting = false; }
        }

        static void Trace(LaserEmitter source)
        {
            source.BeginTrace();
            if (!source.Powered || source.Channel == LightColorChannel.None || !source.isActiveAndEnabled) { source.DisplayTrace(); return; }
            var basis = source.Origin ? source.Origin : source.transform;
            Vector3 origin = basis.position;
            Vector3 direction = basis.forward.normalized;
            float remaining = Mathf.Max(0f, source.MaxDistance);
            int hops = 0;
            int passedCount = 0;
            for (int step = 0; step < LaserEmitter.MaximumSegments && remaining > .001f; step++)
            {
                float nearest = PhysicalDistance(source, origin, direction, remaining);
                LightTarget target = null;
                LightPortal portal = null;
                for (int i = 0; i < targets.Count; i++)
                {
                    var candidate = targets[i];
                    if (!candidate || !candidate.isActiveAndEnabled || !candidate.OpticallyPresent) continue;
                    bool passed = false;
                    for (int seen = 0; seen < passedCount; seen++) if (passedTargets[seen] == candidate) { passed = true; break; }
                    if (passed) continue;
                    if (candidate.Intersect(origin, direction, nearest, out float distance) && distance <= nearest)
                    { nearest = distance; target = candidate; }
                }
                for (int i = 0; i < portals.Count; i++)
                {
                    var candidate = portals[i];
                    if (!candidate || !candidate.isActiveAndEnabled) continue;
                    if (candidate.Intersect(origin, direction, nearest, out float distance) && distance < nearest)
                    { nearest = distance; portal = candidate; target = null; }
                }
                Vector3 end = origin + direction * nearest;
                source.AddSegment(origin, end);
                remaining -= nearest;
                if (portal)
                {
                    if (hops >= Mathf.Clamp(source.MaxPortalHops, 0, 16)) { source.TraceLimited = true; break; }
                    if (!portal.TryTransfer(end, direction, out origin, out direction)) break;
                    // Exit offset is part of the physical distance budget, while portal gap is not.
                    remaining -= portal.ExitOffset;
                    hops++;
                    passedCount = 0;
                    continue;
                }
                if (!target) break;
                target.Illuminate(source.Channel);
                if (!target.PassesBeam) break;
                passedTargets[passedCount++] = target;
                const float epsilon = .005f;
                origin = end + direction * epsilon;
                remaining -= epsilon;
                if (step == LaserEmitter.MaximumSegments - 1) source.TraceLimited = true;
            }
            source.PortalHopCount = hops;
            source.DisplayTrace();
        }

        static float PhysicalDistance(LaserEmitter source, Vector3 origin, Vector3 direction, float range)
        {
            var buffer = hits;
            int count = Physics.RaycastNonAlloc(origin, direction, buffer, range, source.OccluderMask, QueryTriggerInteraction.Ignore);
            if (count == buffer.Length)
            {
                buffer = crowdedHits;
                count = Physics.RaycastNonAlloc(origin, direction, buffer, range, source.OccluderMask, QueryTriggerInteraction.Ignore);
                if (count == buffer.Length) return 0f;
            }
            float nearest = range;
            var ignoreRoot = source.IgnoreRoot ? source.IgnoreRoot : source.transform;
            for (int i = 0; i < count; i++)
            {
                var hit = buffer[i];
                var collider = hit.collider;
                if (!collider || hit.distance >= nearest || collider.transform.IsChildOf(ignoreRoot)) continue;
                if (source.IgnorePlayer && (collider is CharacterController || collider.CompareTag("Player"))) continue;
                var target = collider.GetComponentInParent<LightTarget>();
                if (target && target.isActiveAndEnabled && target.OwnsCollider(collider) && target.Intersect(origin,direction,range,out _)) continue;
                var portal = collider.GetComponent<LightPortal>();
                if (portal && portal.isActiveAndEnabled && portal.Intersect(origin, direction, range, out _)) continue;
                nearest = hit.distance;
            }
            return nearest;
        }
    }
}
