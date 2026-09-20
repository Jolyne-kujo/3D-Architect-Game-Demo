using UnityEngine;

namespace CoastalTemple
{
    /// <summary>Author-facing documentation; it never drives terrain or gameplay.</summary>
    [DisallowMultipleComponent]
    public sealed class SceneObjectGuide : MonoBehaviour
    {
        public string label;
        [TextArea(3, 10)] public string explanation;
        public bool drawMarker;
        public Color markerColor = new Color(1, .8f, .3f);
        public Vector3 markerSize = new Vector3(8, 1, 8);
    }
}
