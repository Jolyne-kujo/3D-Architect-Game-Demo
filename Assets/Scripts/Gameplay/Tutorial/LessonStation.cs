using System;
using UnityEngine;
using UnityEngine.Events;

namespace CoastalTemple.Tutorial
{
    [Serializable]
    public sealed class LessonStation
    {
        public string name;
        public string title;
        [TextArea] public string safetyNote;
        public Transform marker;
        [Min(0)] public float radius = 12;
        [TextArea(2, 5)] public string[] hints = Array.Empty<string>();
        public LessonCompletionCondition completionCondition;
        [TextArea(1, 3)] public string completionMessage;
        public UnityEvent onCompleted = new UnityEvent();
    }
}
