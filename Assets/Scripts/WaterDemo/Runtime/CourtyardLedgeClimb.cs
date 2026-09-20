using UnityEngine;

namespace WaterCourtyard
{
    /// <summary>Shoulder-height mantle motor. Animation observes progress; it never moves the controller.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CourtyardWalker), typeof(CharacterController))]
    public sealed class CourtyardLedgeClimb : MonoBehaviour
    {
        [Tooltip("Standing shoulder height relative to a 1.8 m character; scales with the capsule.")]
        [Min(.1f)] public float shoulderHeight = 1.45f;
        [Tooltip("Land mantle threshold; also never lower than the motor's jump height.")]
        [Min(.1f)] public float minimumLedgeHeight = 1.05f;
        [Tooltip("Distance from the front of the capsule at which pushing toward a ledge grabs it.")]
        [Range(.05f, .6f)] public float reach = .26f;
        [Min(.2f)] public float duration = 1.13f;
        [Header("Surface swimming has its own exit rule")]
        [Tooltip("Maximum platform top above sampled water surface. Does not use the swimmer's feet/standing shoulder.")]
        [Min(0)] public float maximumWaterFreeboard = .65f;
        [Min(0)] public float maximumSubmergedLip = .2f;
        public bool IsClimbing { get; private set; }
        public float Progress { get; private set; }
        public bool UsesClimbAnimation { get; private set; }
        public Vector3 LandingPoint => support ? support.TransformPoint(localLanding) : transform.position;
        public Vector3 HandTarget(bool right)
        {
            if (!support) return transform.position;
            Vector3 facing = Vector3.ProjectOnPlane(support.TransformDirection(localForward), Vector3.up).normalized;
            return support.TransformPoint(localGrip) + Vector3.Cross(Vector3.up, facing) * (right ? .23f : -.23f);
        }
        CharacterController body;
        CourtyardWalker walker;
        Collider supportCollider;
        Transform support;
        Vector3 localStart, localLanding, localForward, localGrip;
        float retryDelay, savedStep, climbDuration;
        bool previouslyIgnored;
        // Match the supplied hang-to-crouch clip: pull up, bring knees over, then settle.
        readonly AnimationCurve liftMotion = new AnimationCurve(new Keyframe(0,0),new Keyframe(.25f,.18f),new Keyframe(.5f,.52f),new Keyframe(.78f,1),new Keyframe(1,1));
        readonly AnimationCurve forwardMotion = new AnimationCurve(new Keyframe(0,0),new Keyframe(.3f,.15f),new Keyframe(.6f,.6f),new Keyframe(.9f,1),new Keyframe(1,1));
        readonly CourtyardCharacterQueries query = new CourtyardCharacterQueries();

        void Awake() { body = GetComponent<CharacterController>(); walker = GetComponent<CourtyardWalker>(); }
        void OnDisable() => Cancel();

        public bool TryBegin(Vector3 intent, float seconds = .02f)
        {
            retryDelay = Mathf.Max(0, retryDelay - seconds);
            if (!isActiveAndEnabled || IsClimbing || retryDelay > 0 || intent.sqrMagnitude < .1f) return false;
            if (!body) body = GetComponent<CharacterController>();
            if (!body.enabled) return false;
            if(walker&&walker.Swimming&&!walker.NearWaterSurface)return false;
            Vector3 feet = CourtyardCharacterQueries.Feet(body);
            float maxHeight = shoulderHeight * CourtyardCharacterQueries.Height(body) / 1.8f;
            if (!FindLedge(intent, maxHeight, out var top, out var inward)) return false;
            Vector3 landingFeet = top.point + Vector3.up * .035f;
            supportCollider = top.collider; support = top.collider.transform;
            Vector3 footOffset = feet - transform.position;
            localStart = support.InverseTransformPoint(transform.position);
            localLanding = support.InverseTransformPoint(landingFeet - footOffset);
            localForward = support.InverseTransformDirection(inward);
            localGrip = support.InverseTransformPoint(top.point - inward * (CourtyardCharacterQueries.Radius(body) * .5f - .03f) + Vector3.up * .025f);
            UsesClimbAnimation = true;
            climbDuration = UsesClimbAnimation ? duration : .3f;
            Progress = 0; IsClimbing = true;
            savedStep = body.stepOffset; body.stepOffset = 0;
            // A standing capsule cannot match bent knees. Ignore only the grabbed deck while
            // mantling; retain collisions with every other wall and ceiling, then restore it.
            previouslyIgnored = Physics.GetIgnoreCollision(body, supportCollider);
            Physics.IgnoreCollision(body, supportCollider, true);
            transform.rotation = Quaternion.LookRotation(inward, Vector3.up);
            return true;
        }

        bool FindLedge(Vector3 intent, float maxHeight, out RaycastHit landing, out Vector3 inward)
        {
            landing = default; inward = Vector3.zero;
            Vector3 direction = Vector3.ProjectOnPlane(intent, Vector3.up).normalized;
            Vector3 feet = CourtyardCharacterQueries.Feet(body);
            float radius = CourtyardCharacterQueries.Radius(body);
            bool waterExit=walker&&walker.Swimming;
            float minHeight=waterExit?Mathf.Max(.06f,walker.WaterSurface-maximumSubmergedLip-feet.y)
                :Mathf.Max(minimumLedgeHeight*CourtyardCharacterQueries.Height(body)/1.8f,walker?walker.JumpHeight+.02f:1.05f);
            if(waterExit)maxHeight=walker.WaterSurface+maximumWaterFreeboard-feet.y;
            if(maxHeight<=minHeight)return false;
            // Several rays catch both solid shore walls and thin floating decks.
            for (int i = 0; i < 7; i++)
            {
                float scan = Mathf.Lerp(Mathf.Max(.02f,minHeight - .12f), maxHeight - .025f, i / 6f);
                if (!query.Ray(body, feet + Vector3.up * scan, direction, radius + reach, out var face)) continue;
                if(face.collider.GetComponentInParent<CourtyardStaircase>())continue;
                if (face.normal.y > .4f || Vector3.Dot(face.normal, direction) > -.35f) continue;
                inward = -Vector3.ProjectOnPlane(face.normal, Vector3.up).normalized;
                // Feet can land near the lip; requiring a whole capsule radius plus margin
                // inside the tread incorrectly rejects narrow but walkable pool stairs.
                Vector3 topOrigin = face.point + inward * (radius * .5f + .02f);
                topOrigin.y = feet.y + maxHeight + .08f;
                // Include the lower endpoint: pool lips exactly on the freeboard boundary
                // otherwise miss by float rounding, especially away from the world origin.
                if (!query.Ray(body, topOrigin, Vector3.down, maxHeight - minHeight + .1f, out var top)) continue;
                if(top.collider.GetComponentInParent<CourtyardStaircase>())continue;
                float height = top.point.y - feet.y;
                if (height < minHeight-.002f || height > maxHeight+.002f || top.normal.y < Mathf.Cos(body.slopeLimit * Mathf.Deg2Rad)) continue;
                Vector3 landingFeet = top.point + Vector3.up * .035f;
                Vector3 liftedFeet = new Vector3(feet.x, landingFeet.y + .09f, feet.z);
                if (!query.CanOccupy(body, landingFeet) || !query.CanOccupy(body, liftedFeet)) continue;
                landing = top;
                return true;
            }
            return false;
        }

        public void Step(float seconds)
        {
            if (!IsClimbing) return;
            if (!support || !supportCollider || !supportCollider.enabled || !support.gameObject.activeInHierarchy || !body.enabled)
            { Cancel(); return; }
            Vector3 start = support.TransformPoint(localStart), end = support.TransformPoint(localLanding);
            Vector3 footOffset = CourtyardCharacterQueries.Feet(body) - transform.position;
            if (!query.CanOccupy(body, end + footOffset)) { Cancel(); return; }
            Progress = Mathf.Min(1, Progress + seconds / Mathf.Max(.2f, climbDuration));
            float lift = UsesClimbAnimation ? liftMotion.Evaluate(Progress) : Mathf.SmoothStep(0,1,Mathf.Clamp01(Progress/.6f));
            float over = UsesClimbAnimation ? forwardMotion.Evaluate(Progress) : Mathf.SmoothStep(0,1,Mathf.InverseLerp(.4f,1,Progress));
            Vector3 desired = Vector3.Lerp(start, end, over);
            desired.y = Mathf.Lerp(start.y, end.y + .035f, lift) - .035f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.9f, 1, Progress));
            Vector3 before = transform.position;
            body.Move(desired - before);
            Vector3 facing = Vector3.ProjectOnPlane(support.TransformDirection(localForward), Vector3.up);
            if (facing.sqrMagnitude > .01f) transform.rotation = Quaternion.LookRotation(facing);
            if ((desired - transform.position).sqrMagnitude > .09f) { Cancel(); return; }
            if (Progress >= 1)
            {
                Finish();
                body.Move(Vector3.down * .07f);
            }
        }

        public void Cancel()
        {
            bool wasClimbing = IsClimbing;
            if (wasClimbing && body && body.enabled && body.gameObject.activeInHierarchy && body.bounds.size.sqrMagnitude > .001f
                && support && supportCollider && supportCollider.enabled)
            {
                Vector3 feet = CourtyardCharacterQueries.Feet(body);
                Vector3 safe = support.TransformPoint(localStart);
                if (!query.CanOccupy(body, feet) && query.CanOccupy(body, safe + feet - transform.position)) body.Move(safe - transform.position);
            }
            Finish(); Progress = 0; retryDelay = wasClimbing ? .25f : 0;
        }
        void Finish()
        {
            if (IsClimbing && body)
            {
                body.stepOffset = savedStep;
                if (supportCollider) Physics.IgnoreCollision(body, supportCollider, previouslyIgnored);
            }
            IsClimbing = false; support = null; supportCollider = null;
        }
    }
}
