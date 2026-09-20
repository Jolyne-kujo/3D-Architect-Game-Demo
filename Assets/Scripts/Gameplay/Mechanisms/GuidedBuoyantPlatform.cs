using Courtyard.Water;
using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    public enum GuidedPlatformMode { VerticalBuoyancy, HorizontalFerry }

    /// <summary>
    /// An ideal guided float: integrates Archimedes force and gravity only along its allowed guide,
    /// and presents that motion through a kinematic deck Rigidbody for collision and rider carry.
    /// Coordinates belong to this stationary frame, never to the moving deck.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GuidedBuoyantPlatform : MonoBehaviour
    {
        public GuidedPlatformMode mode;
        [Header("Owned moving deck (a direct child of this frame)")]
        public Transform platform;
        public Rigidbody platformBody;
        [Header("Path in frame-local coordinates")]
        public Vector3 pathStart = new Vector3(0, .5f, 0);
        public Vector3 pathEnd = new Vector3(0, 5, 0);
        [Range(0, 1)] public float initialTravel;
        [Header("Water and displaced hull in deck-local metres")]
        [Tooltip("Optional. Leave empty to sample enabled WaterVolumes automatically.")]
        public WaterVolume water;
        public Vector3 displacementCenter;
        public Vector3 displacementSize = new Vector3(3, .6f, 3);
        [Min(1), Tooltip("Dry mass / displaced hull volume. Values below water density float; scaling preserves this density.")]
        public float bodyDensity = 350;
        [Min(1)] public float waterDensity = 1000;
        [Tooltip("Use the density of each sampled WaterVolume; disable for a per-platform override.")]
        public bool useWaterDensity = true;
        [Min(0)] public float waterDrag = 3;
        [Min(0)] public float railDamping = .4f;
        [Min(.05f)] public float maxBuoyancySpeed = 3;
        [Header("Horizontal ferry: drive and water-following slide")]
        [Min(0), Tooltip("World metres per second along the transformed path.")]
        public float travelSpeed = 1.5f;
        [Tooltip("Second guide axis in frame-local coordinates. Default up permits the ferry to follow water level.")]
        public Vector3 floatAxis = Vector3.up;
        public float floatMin = -2;
        public float floatMax = 2;

        public int Direction => isActiveAndEnabled && mode == GuidedPlatformMode.HorizontalFerry ? cycle.Direction : 0;
        public float NormalizedTravel => travel;
        public float FloatOffset => floatOffset;
        public float BuoyancySpeed => buoyancySpeed;
        public float Submersion { get; private set; }
        public bool IsMoving { get; private set; }
        public Vector3 StartWorld => transform.TransformPoint(pathStart);
        public Vector3 EndWorld => transform.TransformPoint(pathEnd);
        public Vector3 FloatAxisWorld => transform.TransformVector(SafeFloatAxis).normalized;
        public float WorldVolume
        {
            get
            {
                if (!platform) return 0;
                Vector3 x = platform.TransformVector(Vector3.right * displacementSize.x);
                Vector3 y = platform.TransformVector(Vector3.up * displacementSize.y);
                Vector3 z = platform.TransformVector(Vector3.forward * displacementSize.z);
                return Mathf.Abs(Vector3.Dot(x, Vector3.Cross(y, z)));
            }
        }

        readonly MotorLeverState cycle = new MotorLeverState();
        Quaternion deckLocalRotation;
        float travel, floatOffset, buoyancySpeed;
        bool initialized;
        Vector3 SafeFloatAxis => floatAxis.sqrMagnitude > .000001f ? floatAxis.normalized : Vector3.up;

        void Awake() => Initialize();
        void Reset()
        {
            if (!platformBody) platformBody = GetComponentInChildren<Rigidbody>();
            if (platformBody && platformBody.transform != transform) platform = platformBody.transform;
        }

        public bool Initialize()
        {
            if (initialized) return platform && platformBody;
            if (!platform && platformBody) platform = platformBody.transform;
            if (!platformBody && platform) platformBody = platform.GetComponent<Rigidbody>();
            if (!platformBody)
            {
                platformBody = GetComponentInChildren<Rigidbody>();
                if (platformBody) platform = platformBody.transform;
            }
            if (!platform || !platformBody || platformBody.transform != platform || platform.parent != transform)
            {
                Debug.LogError("GuidedBuoyantPlatform needs an owned direct-child deck with a Rigidbody; keep the rail frame separate.", this);
                return false;
            }
            if (platform.GetComponent<BuoyantBody>() || platform.GetComponent<LinearPlatformMotor>())
            {
                Debug.LogError("The guided deck must have a single motion owner. Remove other buoyancy/motor components from its Rigidbody.", this);
                return false;
            }
            platformBody.isKinematic = true;
            platformBody.useGravity = false;
            platformBody.constraints = RigidbodyConstraints.None;
            platformBody.interpolation = RigidbodyInterpolation.Interpolate;
            platformBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            deckLocalRotation = platform.localRotation;
            initialized = true;
            ResetState();
            return true;
        }

        public void ResetState()
        {
            if (!initialized && !Initialize()) return;
            travel = Mathf.Clamp01(initialTravel);
            floatOffset = Mathf.Clamp(0, Mathf.Min(floatMin, floatMax), Mathf.Max(floatMin, floatMax));
            buoyancySpeed = 0; Submersion = 0; IsMoving = false;
            cycle.SetDirection(-1); cycle.SetDirection(0);
            platformBody.position = ResolvePosition();
            platformBody.rotation = transform.rotation * deckLocalRotation;
            platformBody.mass = Mathf.Max(.001f, bodyDensity * WorldVolume);
        }

        public void SetDirection(int direction) => cycle.SetDirection(mode == GuidedPlatformMode.HorizontalFerry ? direction : 0);
        public void Stop() => SetDirection(0);
        public void CycleDirection()
        {
            if (mode != GuidedPlatformMode.HorizontalFerry || !isActiveAndEnabled) return;
            cycle.Cycle();
        }

        void FixedUpdate() => Simulate(Time.fixedDeltaTime);
        public void Simulate(float seconds)
        {
            IsMoving = false;
            if (!isActiveAndEnabled || seconds <= 0 || float.IsNaN(seconds) || float.IsInfinity(seconds) || (!initialized && !Initialize())) return;
            Vector3 before = platformBody.position;
            // Bounded substeps keep buoyancy stable when callers advance at a lower simulation rate.
            float remaining = Mathf.Min(seconds, .5f);
            while (remaining > .000001f)
            {
                float dt = Mathf.Min(.02f, remaining);
                Advance(dt);
                remaining -= dt;
            }
            Vector3 next = ResolvePosition();
            IsMoving = (next - before).sqrMagnitude > .00000001f;
            platformBody.mass = Mathf.Max(.001f, bodyDensity * WorldVolume);
            platformBody.MovePosition(next);
            platformBody.MoveRotation(transform.rotation * deckLocalRotation);
        }

        void Advance(float seconds)
        {
            Vector3 path = EndWorld - StartWorld;
            float pathLength = path.magnitude;
            if (mode == GuidedPlatformMode.HorizontalFerry)
            {
                int direction = Direction;
                if (pathLength > .0001f && direction != 0)
                {
                    travel = Mathf.Clamp01(travel + direction * Mathf.Max(0, travelSpeed) * seconds / pathLength);
                    if ((direction > 0 && travel >= 1) || (direction < 0 && travel <= 0)) cycle.SetDirection(0);
                }
            }
            Vector3 axis = mode == GuidedPlatformMode.VerticalBuoyancy ? path.normalized : FloatAxisWorld;
            float axisScale = mode == GuidedPlatformMode.VerticalBuoyancy ? pathLength : transform.TransformVector(SafeFloatAxis).magnitude;
            if (axisScale < .0001f) { buoyancySpeed = 0; return; }

            SampleHull(ResolvePosition(), out float fraction, out Vector3 flow, out float displacedDensity);
            Submersion = fraction;
            // Archimedes force / mass: density * displaced volume * g / (bodyDensity * hullVolume).
            // The frame supplies all reaction forces perpendicular to the allowed slide.
            Vector3 acceleration = Physics.gravity + Vector3.up * (Physics.gravity.magnitude * displacedDensity / Mathf.Max(1, bodyDensity));
            float damping = Mathf.Max(0, railDamping) + Mathf.Max(0, waterDrag) * fraction;
            float force = Vector3.Dot(acceleration, axis) + Mathf.Max(0, waterDrag) * fraction * Vector3.Dot(flow, axis);
            if (damping > .00001f)
            {
                float decay = Mathf.Exp(-damping * seconds);
                buoyancySpeed = buoyancySpeed * decay + force * (1 - decay) / damping;
            }
            else buoyancySpeed += force * seconds;
            buoyancySpeed = Mathf.Clamp(buoyancySpeed, -Mathf.Max(.05f, maxBuoyancySpeed), Mathf.Max(.05f, maxBuoyancySpeed));
            if (mode == GuidedPlatformMode.VerticalBuoyancy)
            {
                travel = Mathf.Clamp01(travel + buoyancySpeed * seconds / axisScale);
                if ((travel <= 0 && buoyancySpeed < 0) || (travel >= 1 && buoyancySpeed > 0)) buoyancySpeed = 0;
            }
            else
            {
                float low = Mathf.Min(floatMin, floatMax), high = Mathf.Max(floatMin, floatMax);
                floatOffset = Mathf.Clamp(floatOffset + buoyancySpeed * seconds / axisScale, low, high);
                if ((floatOffset <= low && buoyancySpeed < 0) || (floatOffset >= high && buoyancySpeed > 0)) buoyancySpeed = 0;
            }
        }

        Vector3 ResolvePosition() => Vector3.Lerp(StartWorld, EndWorld, travel)
            + (mode == GuidedPlatformMode.HorizontalFerry ? transform.TransformVector(SafeFloatAxis) * floatOffset : Vector3.zero);

        void SampleHull(Vector3 position, out float fraction, out Vector3 flow, out float displacedDensity)
        {
            fraction = 0; flow = Vector3.zero; displacedDensity = 0;
            const int count = 4;
            Vector3 size = new Vector3(Mathf.Abs(displacementSize.x), Mathf.Abs(displacementSize.y), Mathf.Abs(displacementSize.z));
            Vector3 x = platform.TransformVector(Vector3.right * size.x);
            Vector3 y = platform.TransformVector(Vector3.up * size.y);
            Vector3 z = platform.TransformVector(Vector3.forward * size.z);
            // Each column samples its own live surface. The projected cell span also handles tilted hulls.
            float verticalSpan = Mathf.Max(.001f, Mathf.Abs(y.y) + (Mathf.Abs(x.y) + Mathf.Abs(z.y)) / count);
            Vector3 center = position + platform.TransformVector(displacementCenter);
            for (int ix = 0; ix < count; ix++) for (int iz = 0; iz < count; iz++)
            {
                Vector3 p = center + x * ((ix + .5f) / count - .5f) + z * ((iz + .5f) / count - .5f);
                var sampled = water;
                float surface; Vector3 localFlow;
                if (sampled)
                {
                    if (!WaterVolume.SampleColumn(sampled, p, verticalSpan * .5f, out surface, out localFlow, out _)) continue;
                }
                else sampled = WaterVolume.FindAt(p, gameObject.scene, out surface, out localFlow, out _, verticalSpan * .5f);
                if (!sampled) continue;
                float submerged = Mathf.Clamp01((surface - p.y) / verticalSpan + .5f) / (count * count);
                fraction += submerged; flow += localFlow * submerged;
                displacedDensity += submerged * Mathf.Max(1, useWaterDensity ? sampled.density : waterDensity);
            }
            if (fraction > .00001f) flow /= fraction;
        }

        void OnDisable() { cycle.SetDirection(0); buoyancySpeed = 0; IsMoving = false; }
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(.25f, .8f, 1);
            Gizmos.DrawLine(StartWorld, EndWorld); Gizmos.DrawWireSphere(StartWorld, .15f); Gizmos.DrawWireSphere(EndWorld, .15f);
            if (mode == GuidedPlatformMode.HorizontalFerry)
            {
                Vector3 axis = transform.TransformVector(SafeFloatAxis);
                Gizmos.DrawLine(StartWorld + axis * floatMin, StartWorld + axis * floatMax);
                Gizmos.DrawLine(EndWorld + axis * floatMin, EndWorld + axis * floatMax);
            }
        }
    }
}
