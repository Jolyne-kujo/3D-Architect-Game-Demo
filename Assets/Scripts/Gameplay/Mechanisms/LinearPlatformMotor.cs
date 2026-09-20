using UnityEngine;

namespace CoastalTemple.Mechanisms
{
    /// <summary>Moves an authored rigidbody along one straight path. A zero drive holds any intermediate position.</summary>
    [DisallowMultipleComponent]
    public class LinearPlatformMotor : MonoBehaviour
    {
        public Transform platform;
        public Rigidbody platformBody;
        [Tooltip("When enabled, bottom/top are coordinates in the platform parent's space; otherwise they are world positions.")]
        public bool positionsAreLocal = true;
        public Vector3 bottom = Vector3.zero;
        public Vector3 top = new Vector3(0, 8, 0);
        [Min(0)] public float speed = 1.5f;
        [Tooltip("Optional authored counterweight. Its local position follows the platform inversely.")]
        public Transform counterweight;
        public Vector3 counterweightTravel = new Vector3(0, -8, 0);
        public bool IsMoving { get; private set; }
        public int ManualDirection { get; private set; }
        public int Direction => isActiveAndEnabled ? SampleDirection() : 0;
        public float NormalizedHeight => NormalizedPosition(platform ? platform.position : BottomWorld);
        public float NormalizedTravel => NormalizedHeight;
        public Vector3 BottomWorld => ToWorld(bottom);
        public Vector3 TopWorld => ToWorld(top);
        Vector3 counterweightRest;
        bool initialized;

        protected virtual void Awake() => Initialize();
        protected virtual void Reset() { platform = transform; platformBody = GetComponent<Rigidbody>(); }

        public bool Initialize()
        {
            if (initialized) return platformBody;
            if (!platform && platformBody) platform = platformBody.transform;
            if (!platform) platform = transform;
            if (!platformBody) platformBody = platform.GetComponent<Rigidbody>();
            if (!platformBody)
            {
                Debug.LogError("LinearPlatformMotor needs an authored Rigidbody on its platform.", this);
                return false;
            }
            platform = platformBody.transform;
            platformBody.isKinematic = true;
            platformBody.useGravity = false;
            platformBody.interpolation = RigidbodyInterpolation.Interpolate;
            platformBody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            if (counterweight) counterweightRest = counterweight.localPosition - counterweightTravel * NormalizedHeight;
            initialized = true;
            return true;
        }

        public void SetDirection(int direction)
        {
            ManualDirection = System.Math.Sign(direction);
            if (ManualDirection == 0) IsMoving = false;
        }
        public void SetDrive(int direction) => SetDirection(direction);
        public void SetManualDirection(int direction) => SetDirection(direction);
        public void StopManual() => SetDirection(0);
        public void ReturnToBottom() => SetDirection(-1);
        protected virtual int SampleDirection() => ManualDirection;

        // All adapters sample their drive here; no adapter owns a second physics update.
        protected void FixedUpdate() => Simulate(Time.fixedDeltaTime);
        public void Simulate(float seconds)
        {
            IsMoving = false;
            if (!isActiveAndEnabled || seconds <= 0 || (!initialized && !Initialize())) return;
            int direction = SampleDirection();
            Vector3 current = platformBody.position;
            Vector3 next = NextPosition(current, BottomWorld, TopWorld, speed, direction, seconds);
            IsMoving = (next - current).sqrMagnitude > .00000001f;
            if (IsMoving) platformBody.MovePosition(next);
            if (ManualDirection != 0 && direction != 0 && !IsMoving) ManualDirection = 0;
            if (counterweight) counterweight.localPosition = counterweightRest + counterweightTravel * NormalizedPosition(next);
        }

        public static Vector3 NextPosition(Vector3 current, Vector3 bottom, Vector3 top, float speed, int direction, float seconds)
        {
            if (direction == 0 || seconds <= 0 || speed <= 0) return current;
            Vector3 target = direction > 0 ? top : bottom;
            float remaining = Vector3.Distance(current, target);
            float after = LinearPlatformMotorMath.Advance(remaining, 0, remaining, speed, -1, seconds);
            return Vector3.MoveTowards(current, target, remaining - after);
        }

        // Preserves LightDrivenLift.NextPosition callers from authored scenes and existing checks.
        public static Vector3 NextPosition(Vector3 current, Vector3 bottom, Vector3 top, float speed, bool raise, bool lower, float seconds) =>
            NextPosition(current, bottom, top, speed, MechanismDriveRules.ResolveDirection(raise, lower), seconds);

        float NormalizedPosition(Vector3 worldPosition)
        {
            Vector3 path = TopWorld - BottomWorld;
            return path.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector3.Dot(worldPosition - BottomWorld, path) / path.sqrMagnitude);
        }
        Vector3 ToWorld(Vector3 value) => positionsAreLocal && platform && platform.parent ? platform.parent.TransformPoint(value) : value;
        protected virtual void OnDisable() { IsMoving = false; ManualDirection = 0; }
        protected void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1, .72f, .16f);
            Gizmos.DrawLine(BottomWorld, TopWorld);
            Gizmos.DrawWireSphere(BottomWorld, .2f);
            Gizmos.DrawWireSphere(TopWorld, .2f);
        }
    }
}
