using UnityEditor;
using UnityEngine;

namespace CoastalTemple.Editor
{
    [CustomEditor(typeof(SceneObjectGuide))]
    public sealed class SceneObjectGuideEditor : UnityEditor.Editor
    {
        bool edit;
        public override void OnInspectorGUI()
        {
            var guide = (SceneObjectGuide)target;
            EditorGUILayout.LabelField(guide.label, EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(guide.explanation, MessageType.Info);
            edit = EditorGUILayout.Foldout(edit, "编辑此说明");
            if (edit) DrawDefaultInspector();
        }

        [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
        static void DrawGuide(SceneObjectGuide guide, GizmoType type)
        {
            if (!guide.drawMarker) return;
            Gizmos.color = guide.markerColor;
            Gizmos.DrawWireCube(guide.transform.position, guide.markerSize);
            Handles.Label(guide.transform.position + Vector3.up * 1.5f, guide.label);
        }
    }
}
