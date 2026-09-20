using UnityEngine;

namespace CoastalTemple.Tutorial
{
    /// <summary>An optional observer adapter. It must read actual world state, never grant access.</summary>
    public abstract class LessonCompletionCondition : MonoBehaviour
    {
        public abstract bool IsComplete { get; }
    }
}
