using Courtyard.Water;
using CoastalTemple.Mechanisms;
using UnityEngine;
using UnityEngine.InputSystem;
using WaterCourtyard;

namespace CoastalTemple
{
    // Controls an authored scene. No environment geometry is created at runtime.
    public sealed class CoastalWalkthrough : MonoBehaviour
    {
        public CourtyardWalker walker;
        public Camera view;
        public CourtyardDrainExperiment experiment;
        // Serialized V8 references are kept for explicit editor migration and older inspection tools.
        [HideInInspector] public WaterVolume water;
        [HideInInspector] public RefractedDrainBeam beam;
        [HideInInspector] public Transform drainGate;
        [HideInInspector] public BuoyantBody[] floaters;
        public Transform[] viewpoints;
        public SeaBoundary seaBoundary;
        public CoastalTemple.Player.CoastalPlayerCamera playerCamera;
        public CoastalTemple.Tutorial.TutorialJourney tutorial;
        [Tooltip("Enable only for water-system inspection; gameplay uses nearby physical controls.")]
        public bool waterDebugShortcuts;
        [Tooltip("Author inspection only. Keep disabled during normal gameplay.")]
        public bool inspectionShortcuts;
        bool overview;
        int viewpoint;
        GUIStyle label, title;

        void Start(){Application.targetFrameRate=60;SetOverview(false);}
        public void SetOverview(bool value)
        {
            overview=value;walker.SetActive(!value);walker.simulateWhileInactive=value;
            if(playerCamera)playerCamera.SetOverview(value);
            if(value){view.transform.SetParent(null,true);ShowView(viewpoint);}
            else if(!playerCamera){view.transform.SetParent(walker.eye,false);view.transform.localPosition=Vector3.zero;view.transform.localRotation=Quaternion.identity;}
        }
        public void ShowView(int index)
        {
            viewpoint=Mathf.Clamp(index,0,viewpoints.Length-1);
            if(!overview)SetOverview(true);
            var marker=viewpoints[viewpoint];view.transform.SetPositionAndRotation(marker.position,marker.rotation);
        }
        public void ResetWater() { if(experiment)experiment.ResetExperiment(); }
        void Update()
        {
            var kb=Keyboard.current;if(kb==null)return;
            if(inspectionShortcuts)
            {
                if(kb.tabKey.wasPressedThisFrame)SetOverview(!overview);
                if(kb.f1Key.wasPressedThisFrame)ShowView(0);
                if(kb.f2Key.wasPressedThisFrame)ShowView(1);
                if(kb.f3Key.wasPressedThisFrame)ShowView(2);
            }
            if(kb.escapeKey.wasPressedThisFrame&&!overview)
            {
                if(playerCamera&&playerCamera.IsMechanismView){playerCamera.EndMechanismView();return;}
                bool released=walker.active;walker.SetActive(!released);walker.simulateWhileInactive=released;
            }
            if(!overview&&!walker.active&&Mouse.current!=null&&Mouse.current.leftButton.wasPressedThisFrame)
            {walker.SetActive(true);walker.simulateWhileInactive=false;}
            if(waterDebugShortcuts||!tutorial)
            {
                if(kb.digit1Key.wasPressedThisFrame&&experiment)experiment.StartDrain();
                if(kb.rKey.wasPressedThisFrame)ResetWater();
                if(kb.vKey.wasPressedThisFrame&&experiment&&experiment.water)experiment.water.showFlow=!experiment.water.showFlow;
            }
            if(kb.homeKey.wasPressedThisFrame){walker.ResetPosition();SetOverview(false);}
        }
        void OnDisable(){if(walker)walker.SetActive(false);}
        void OnGUI()
        {
            if(tutorial&&tutorial.showHud)return;
            if(label==null)
            {
                var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},18);
                label=new GUIStyle(GUI.skin.label){font=font,fontSize=15,normal={textColor=new Color(.87f,.92f,.91f)}};
                title=new GUIStyle(label){fontSize=23,fontStyle=FontStyle.Bold};
            }
            float scale=Mathf.Max(.7f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
            GUI.color=new Color(.025f,.06f,.065f,.83f);GUI.DrawTexture(new Rect(24,24,365,84),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(41,34,340,34),"潮岸遗迹 · 朝圣之路",title);
            GUI.Label(new Rect(42,73,340,24),"沙滩遗迹 → 半山海湾 → 山顶神庙",label);
            GUI.color=new Color(.025f,.06f,.065f,.83f);GUI.DrawTexture(new Rect(24,h-79,w-48,55),Texture2D.whiteTexture);GUI.color=Color.white;
            bool operating=playerCamera&&playerCamera.IsMechanismView;
            GUI.Label(new Rect(41,h-68,w-75,24),operating?"E 继续操作当前机关 · WASD / 空格 / Esc 返回第一人称":"WASD 行走 / 游泳 · 鼠标转头 · E 操纵机关 · 空格上浮 · Ctrl 下潜 · Esc 释放鼠标 · Home 回起点",label);
            if(experiment&&experiment.water)
                GUI.Label(new Rect(41,h-46,w-75,22),$"1 开启水池排水 · R 重新蓄水 · V 水流视图    |    余水 {experiment.water.Remaining*100:0.0}% · 排水倍率 ×{experiment.water.drainSpeedMultiplier:0.0}",label);
            if(!overview&&!operating)GUI.Label(new Rect(w*.5f-4,h*.5f-12,18,24),"·",label);
            if(seaBoundary&&Time.time-seaBoundary.LastDeathTime<3)
            {
                GUI.color=new Color(.08f,.12f,.15f,.9f);GUI.DrawTexture(new Rect(w*.5f-215,h*.42f,430,60),Texture2D.whiteTexture);GUI.color=Color.white;
                GUI.Label(new Rect(w*.5f-192,h*.42f+15,405,36),"已越过近海边界 · 返回岸边起点",title);
            }
        }
    }
}
