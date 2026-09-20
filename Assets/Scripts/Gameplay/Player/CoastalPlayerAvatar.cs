using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    [DisallowMultipleComponent, DefaultExecutionOrder(100)]
    public sealed class CoastalPlayerAvatar : MonoBehaviour
    {
        public CourtyardWalker walker;
        public Transform poseRoot;
        public Transform leftShoulder, rightShoulder, leftElbow, rightElbow;
        public Transform leftHip, rightHip, leftKnee, rightKnee;
        public Renderer[] bodyRenderers;
        public bool keepShadowsWhenHidden;
        float phase, locomotion, swimBlend;
        Vector3 poseRest;

        void Awake()
        {
            if (!walker) walker = GetComponentInParent<CourtyardWalker>();
            if (poseRoot) poseRest = poseRoot.localPosition;
        }

        public void SetVisible(bool value)
        {
            if (bodyRenderers == null) bodyRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var bodyRenderer in bodyRenderers) if (bodyRenderer)
            {
                bodyRenderer.enabled = value || keepShadowsWhenHidden;
                bodyRenderer.shadowCastingMode = !value && keepShadowsWhenHidden
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly : UnityEngine.Rendering.ShadowCastingMode.On;
            }
        }

        void LateUpdate()
        {
            if (!walker || !poseRoot) return;
            float dt = Time.deltaTime;
            float speed = walker.active ? walker.PlanarVelocity.magnitude : 0;
            locomotion = Mathf.MoveTowards(locomotion, Mathf.Clamp01(speed / 3.4f), dt * 5);
            swimBlend = Mathf.MoveTowards(swimBlend, walker.Swimming ? 1 : 0, dt * 3);
            phase += dt * Mathf.Lerp(1.7f, 9.5f, locomotion);
            float gait = Mathf.Sin(phase), opposite = Mathf.Sin(phase + Mathf.PI);
            float kick = Mathf.Sin(phase * 1.4f);
            float stroke = Mathf.Sin(phase * .8f);
            poseRoot.localPosition = poseRest + Vector3.up * (swimBlend * .48f + Mathf.Abs(gait) * .024f * locomotion * (1 - swimBlend));
            poseRoot.localRotation = Quaternion.Euler(swimBlend * 68, 0, gait * 2 * locomotion * (1 - swimBlend));
            SetJoint(leftHip, Mathf.Lerp(gait * 31 * locomotion, kick * 17, swimBlend), 0, -2);
            SetJoint(rightHip, Mathf.Lerp(opposite * 31 * locomotion, -kick * 17, swimBlend), 0, 2);
            SetJoint(leftKnee, Mathf.Lerp(Mathf.Max(0, -gait) * 35 * locomotion, 12 + Mathf.Max(0, kick) * 19, swimBlend), 0, 0);
            SetJoint(rightKnee, Mathf.Lerp(Mathf.Max(0, -opposite) * 35 * locomotion, 12 + Mathf.Max(0, -kick) * 19, swimBlend), 0, 0);
            SetJoint(leftShoulder, Mathf.Lerp(opposite * 27 * locomotion, -95 + stroke * 30, swimBlend), -swimBlend * 15, -6 - swimBlend * 27);
            SetJoint(rightShoulder, Mathf.Lerp(gait * 27 * locomotion, -95 - stroke * 30, swimBlend), swimBlend * 15, 6 + swimBlend * 27);
            SetJoint(leftElbow, -8 - locomotion * 10 - swimBlend * (15 + stroke * 12), 0, 0);
            SetJoint(rightElbow, -8 - locomotion * 10 - swimBlend * (15 - stroke * 12), 0, 0);
        }

        static void SetJoint(Transform joint, float x, float y, float z)
        {
            if (joint) joint.localRotation = Quaternion.Euler(x, y, z);
        }
    }
}
