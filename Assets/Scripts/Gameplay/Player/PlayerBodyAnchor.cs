using UnityEngine;
using WaterCourtyard;

namespace CoastalTemple.Player
{
    /// <summary>World body and shadow pivot at the controller's feet, independent of eye pitch.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(75)]
    public sealed class PlayerBodyAnchor : MonoBehaviour
    {
        public CourtyardWalker walker;
        public RiggedPlayerAnimation animationDriver;

        void Awake()
        {
            if (!walker) walker = GetComponentInParent<CourtyardWalker>();
            if (!animationDriver) animationDriver = GetComponent<RiggedPlayerAnimation>();
            SnapToWalker();
        }

        public void SnapToWalker()
        {
            if (!walker || transform == walker.transform) return;
            // A first-person body is a sibling of Eye, never a child of the camera/head bone.
            if (transform.parent != walker.transform) transform.SetParent(walker.transform, true);
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (animationDriver && animationDriver.facingRoot)
                animationDriver.facingRoot.localRotation = Quaternion.Euler(0, animationDriver.modelYawOffset, 0);
        }

        void LateUpdate() => SnapToWalker();
    }
}
