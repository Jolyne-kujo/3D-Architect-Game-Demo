using UnityEngine;
using UnityEngine.Events;

namespace CoastalTemple.LightPuzzles
{
    [DefaultExecutionOrder(400)]
    public abstract class LightTarget : MonoBehaviour
    {
        [Tooltip("Optional collider defining the optical box. It remains optically detectable when disabled.")]
        public BoxCollider OpticalCollider;
        public Vector3 OpticalSize = Vector3.one;
        public Vector3 OpticalCenter;
        public UnityEvent Activated = new UnityEvent();
        public UnityEvent Deactivated = new UnityEvent();
        public LightColorChannel IlluminatedColors { get; private set; }
        public bool IsIlluminated => IlluminatedColors != LightColorChannel.None;
        public virtual float Progress => state.Progress;
        protected readonly LightActivationState state = new LightActivationState();
        protected virtual bool ActivationActive => state.Active;
        protected abstract float ActivationDelay { get; }
        protected abstract float GraceSeconds { get; }
        protected abstract bool Latch { get; }
        internal virtual bool PassesBeam => false;
        internal virtual bool OpticallyPresent => true;
        protected virtual BoxCollider Shape => OpticalCollider;
        internal BoxCollider OpticalShape => Shape;
        internal virtual bool OwnsCollider(Collider value) => value == Shape;

        protected virtual void Awake() { if (!OpticalCollider) OpticalCollider = GetComponent<BoxCollider>(); }
        protected virtual void OnEnable() { LightPuzzleWorld.Register(this); }
        protected virtual void OnDisable() { LightPuzzleWorld.Unregister(this); }
        protected virtual void LateUpdate() { LightPuzzleWorld.Tick(); }
        internal void BeginTrace() { IlluminatedColors = LightColorChannel.None; }
        internal void Illuminate(LightColorChannel color = LightColorChannel.Red) { IlluminatedColors |= color; }
        internal void Resolve(float deltaTime)
        {
            bool previous = ActivationActive;
            AdvanceState(deltaTime);
            ApplyState();
            if (ActivationActive != previous) { if (ActivationActive) Activated.Invoke(); else Deactivated.Invoke(); }
        }
        protected virtual void AdvanceState(float deltaTime) => state.Step(IsIlluminated, deltaTime, ActivationDelay, GraceSeconds, Latch);
        protected virtual void ApplyState() { }
        public virtual void ResetState()
        {
            bool wasActive = ActivationActive;
            ClearState(); IlluminatedColors = LightColorChannel.None; ApplyState();
            if (wasActive) Deactivated.Invoke();
        }
        protected virtual void ClearState() => state.Reset();
        internal bool Intersect(Vector3 origin, Vector3 direction, float maximum, out float distance)
        {
            var box = Shape;
            var basis = box ? box.transform : transform;
            Vector3 center = box ? box.center : OpticalCenter;
            Vector3 half = (box ? box.size : OpticalSize) * .5f;
            var localOrigin = basis.InverseTransformPoint(origin) - center;
            var localDirection = basis.InverseTransformVector(direction);
            float near = 0f, far = maximum;
            for (int axis = 0; axis < 3; axis++)
            {
                float component = localDirection[axis];
                if (Mathf.Abs(component) < .0000001f)
                {
                    if (Mathf.Abs(localOrigin[axis]) > half[axis]) { distance = 0; return false; }
                    continue;
                }
                float a = (-half[axis] - localOrigin[axis]) / component;
                float b = (half[axis] - localOrigin[axis]) / component;
                if (a > b) { float temporary = a; a = b; b = temporary; }
                near = Mathf.Max(near, a); far = Mathf.Min(far, b);
                if (near > far) { distance = 0; return false; }
            }
            distance = near;
            return far >= .0001f && near <= maximum;
        }
    }
}
