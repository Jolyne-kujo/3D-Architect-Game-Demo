using UnityEngine;

namespace CoastalTemple.Tutorial
{
    /// <summary>Observes arrival inside an authored destination box; never operates or unlocks mechanisms.</summary>
    public sealed class PlayerReachedCondition : LessonCompletionCondition
    {
        public Transform player;
        public Transform destination;
        public Vector3 halfExtents = new Vector3(3, 2, 3);

        public override bool IsComplete
        {
            get
            {
                if (!player || !destination) return false;
                Vector3 local = destination.InverseTransformPoint(player.position);
                return Mathf.Abs(local.x) <= halfExtents.x
                    && Mathf.Abs(local.y) <= halfExtents.y
                    && Mathf.Abs(local.z) <= halfExtents.z;
            }
        }
    }
}
