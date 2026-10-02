using System.Collections.Generic;
using System.Linq;
using CoastalTemple.LightPuzzles;
using UnityEditor;
using UnityEngine;

namespace CoastalTemple.Editor
{
    public sealed class LightPathPreviewWindow : EditorWindow
    {
        [SerializeField] bool preview = true, allEmitters = true, normals = true;
        [SerializeField] ReceiverColor testColor = ReceiverColor.Red;
        readonly List<BeamSegment> path = new List<BeamSegment>(32);
        readonly List<LightTarget> reached = new List<LightTarget>(8);
        string report = "等待场景刷新";
        double nextRefresh;

        [MenuItem("Coastal Temple/光路测试 (Scene 预览)")]
        public static void Open()
        {
            var w = GetWindow<LightPathPreviewWindow>("光路测试");
            w.minSize = new Vector2(320,280); w.Show();
        }
        void OnEnable() { SceneView.duringSceneGui += DrawScene; EditorApplication.update += Refresh; }
        void OnDisable() { SceneView.duringSceneGui -= DrawScene; EditorApplication.update -= Refresh; SceneView.RepaintAll(); }
        void Refresh()
        {
            if (!preview || EditorApplication.timeSinceStartup < nextRefresh) return;
            nextRefresh = EditorApplication.timeSinceStartup + .12;
            SceneView.RepaintAll(); Repaint();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("摆放镜子时检查反射方向", EditorStyles.boldLabel);
            preview = EditorGUILayout.Toggle("启用 Scene 光路预览", preview);
            allEmitters = EditorGUILayout.Toggle("显示当前场景全部发射器", allEmitters);
            testColor = (ReceiverColor)EditorGUILayout.EnumPopup("测试光色", testColor);
            normals = EditorGUILayout.Toggle("显示镜面朝向", normals);
            EditorGUILayout.HelpBox("关闭的发射器也会显示测试光。预览仅查询当前几何，不触发机关或改变光源开关。Scene 工具栏需开启 Gizmos。", MessageType.Info);
            if (!allEmitters) EditorGUILayout.LabelField("请选择发射器或其父对象。", EditorStyles.wordWrappedLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("选中镜面 −15°")) Rotate(-15);
            if (GUILayout.Button("选中镜面 +15°")) Rotate(15);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(report, EditorStyles.wordWrappedLabel, GUILayout.ExpandHeight(true));
        }
        static void Rotate(float amount)
        {
            if (!Selection.activeGameObject) return;
            var console = Selection.activeGameObject.GetComponentInParent<LightMirrorConsole>();
            if (!console) console = Selection.activeGameObject.GetComponentInChildren<LightMirrorConsole>();
            var mirror = Selection.activeGameObject.GetComponentInParent<LightMirror>();
            if (!mirror) mirror = Selection.activeGameObject.GetComponentInChildren<LightMirror>();
            Transform basis = console && console.pivot ? console.pivot : mirror ? mirror.transform : null;
            if (!basis) return;
            Undo.RecordObject(basis,"调整测试镜面"); basis.Rotate(Vector3.up,amount,Space.Self);
            PrefabUtility.RecordPrefabInstancePropertyModifications(basis); SceneView.RepaintAll();
        }
        void DrawScene(SceneView view)
        {
            if (!preview || Event.current.type != EventType.Repaint) return;
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var targets = Object.FindObjectsByType<LightTarget>(FindObjectsSortMode.None).Where(v=>v.gameObject.scene==scene).ToArray();
            var mirrors = Object.FindObjectsByType<LightMirror>(FindObjectsSortMode.None).Where(v=>v.gameObject.scene==scene).ToArray();
            var portals = Object.FindObjectsByType<LightPortal>(FindObjectsSortMode.None).Where(v=>v.gameObject.scene==scene).ToArray();
            var emitters = Object.FindObjectsByType<LaserEmitter>(FindObjectsSortMode.None).Where(v=>v.gameObject.scene==scene);
            if (!allEmitters) emitters = emitters.Where(e=>Selection.gameObjects.Any(g=>e.transform.IsChildOf(g.transform)||g.transform.IsChildOf(e.transform)));
            Physics.SyncTransforms();
            Color prior = Handles.color;
            var depth = Handles.zTest; Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;
            var descriptions = new List<string>();
            try
            {
                foreach (var source in emitters)
                {
                    LightPuzzleWorld.Preview(source,path,targets,mirrors,portals,reached);
                    Color color = LaserEmitter.ColorForChannel((LightColorChannel)testColor); color /= Mathf.Max(1,color.maxColorComponent);color.a=1;Handles.color=color;
                    for(int i=0;i<path.Count;i++)
                    {
                        var s=path[i];var direction=s.End-s.Start;float length=direction.magnitude;
                        Handles.DrawAAPolyLine(3,s.Start,s.End);
                        if(length>.02f) Handles.ArrowHandleCap(0,Vector3.Lerp(s.Start,s.End,.55f),Quaternion.LookRotation(direction),Mathf.Min(.5f,length*.25f),EventType.Repaint);
                        Handles.SphereHandleCap(0,s.End,Quaternion.identity,.1f,EventType.Repaint);
                        Handles.Label((s.Start+s.End)*.5f,$"{i+1} · {length:F1} m");
                    }
                    string hit = reached.Count==0 ? "未命中受光器（遮挡或超出距离）" : string.Join("、",reached.Select(t=>t is LightReceiver r ? (r.transform.parent?r.transform.parent.name:r.name)+(r.AcceptedChannel==(LightColorChannel)testColor?" ✓ 同色命中":" × 颜色不匹配") : t.name+" · 石幕/光路目标"));
                    descriptions.Add(source.name+"："+path.Count+" 段；"+hit);
                }
                if(normals)foreach(var mirror in mirrors)
                {
                    var basis=mirror.face?mirror.face.transform:mirror.transform;
                    var center=mirror.face?basis.TransformPoint(mirror.face.center):basis.position;
                    var normal=basis.worldToLocalMatrix.transpose.MultiplyVector(Vector3.forward).normalized;
                    Handles.color=Color.cyan;Handles.ArrowHandleCap(0,center,Quaternion.LookRotation(normal),.8f,EventType.Repaint);
                }
                report=descriptions.Count>0?string.Join("\n\n",descriptions):"当前选择范围内没有发射器。";
            }
            finally{Handles.color=prior;Handles.zTest=depth;}
        }
    }

    [CustomEditor(typeof(LightMirror))]
    public sealed class LightMirrorInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("板面为本地 XY，反射方向由本地 Z 法线决定。",MessageType.None);
            if(GUILayout.Button("打开光路测试"))LightPathPreviewWindow.Open();
        }
    }
}
