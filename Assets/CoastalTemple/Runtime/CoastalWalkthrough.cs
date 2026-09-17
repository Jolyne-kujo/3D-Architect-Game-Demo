using Courtyard.Water;
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
        public WaterVolume water;
        public RefractedDrainBeam beam;
        public Transform drainGate;
        public BuoyantBody[] floaters;
        public Transform[] viewpoints;
        bool overview;
        int viewpoint;
        float gateAngle;
        GUIStyle label, title;

        void Start(){Application.targetFrameRate=60;SetOverview(false);}
        public void SetOverview(bool value)
        {
            overview=value;walker.SetActive(!value);
            if(value){view.transform.SetParent(null,true);ShowView(viewpoint);}
            else {view.transform.SetParent(walker.eye,false);view.transform.localPosition=Vector3.zero;view.transform.localRotation=Quaternion.identity;}
        }
        public void ShowView(int index)
        {
            viewpoint=Mathf.Clamp(index,0,viewpoints.Length-1);
            if(!overview)SetOverview(true);
            var marker=viewpoints[viewpoint];view.transform.SetPositionAndRotation(marker.position,marker.rotation);
        }
        public void ResetWater()
        {
            beam.powered=false;water.ResetWater();
            foreach(var floater in floaters)floater.ResetBody();
            gateAngle=0;
        }
        void Update()
        {
            gateAngle=Mathf.MoveTowards(gateAngle,water.Gate.IsOpen?82:0,Time.deltaTime*35);
            drainGate.localRotation=Quaternion.Euler(gateAngle,0,0);
            var kb=Keyboard.current;if(kb==null)return;
            if(kb.tabKey.wasPressedThisFrame||kb.escapeKey.wasPressedThisFrame)SetOverview(!overview);
            if(kb.f1Key.wasPressedThisFrame)ShowView(0);
            if(kb.f2Key.wasPressedThisFrame)ShowView(1);
            if(kb.f3Key.wasPressedThisFrame)ShowView(2);
            if(kb.digit1Key.wasPressedThisFrame)beam.powered=true;
            if(kb.rKey.wasPressedThisFrame)ResetWater();
            if(kb.vKey.wasPressedThisFrame)water.showFlow=!water.showFlow;
            if(kb.homeKey.wasPressedThisFrame){walker.ResetPosition();SetOverview(false);}
        }
        void OnDisable(){if(walker)walker.SetActive(false);}
        void OnGUI()
        {
            if(label==null)
            {
                var font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},18);
                label=new GUIStyle(GUI.skin.label){font=font,fontSize=15,normal={textColor=new Color(.87f,.92f,.91f)}};
                title=new GUIStyle(label){fontSize=23,fontStyle=FontStyle.Bold};
            }
            float scale=Mathf.Max(.7f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
            GUI.color=new Color(.025f,.06f,.065f,.83f);GUI.DrawTexture(new Rect(24,24,365,84),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(41,34,340,34),"潮岸遗迹 · 朝圣之路",title);
            GUI.Label(new Rect(42,73,340,24),"岸边起点 → 露天遗迹 → 山顶神庙",label);
            GUI.color=new Color(.025f,.06f,.065f,.83f);GUI.DrawTexture(new Rect(24,h-79,w-48,55),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(41,h-68,w-75,24),"WASD 行走 · Shift 快走 · 空格跳跃 · Tab 观察 / 行走 · F1 全景 · F2 山脚 · F3 神庙 · Home 回起点",label);
            GUI.Label(new Rect(41,h-46,w-75,22),$"1 开启水池排水 · R 重新蓄水 · V 水流视图    |    余水 {water.Remaining*100:0.0}% · 排水倍率 ×{water.drainSpeedMultiplier:0.0}",label);
            if(!overview)GUI.Label(new Rect(w*.5f-4,h*.5f-12,18,24),"·",label);
        }
    }
}
