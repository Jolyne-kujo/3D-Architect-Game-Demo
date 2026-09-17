using System;
using Courtyard.Water;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaterCourtyard
{
    public sealed class CourtyardLab : MonoBehaviour
    {
        public WaterVolume water;
        public CourtyardWalker walker;
        public Camera view;
        public RefractedDrainBeam beam;
        public Transform drainGate,console;
        public BuoyantBody[] floaters;
        public bool Overview { get; private set; }=true;
        public float RunSeconds { get; private set; }
        public float Fps { get; private set; }
        float yaw=-28,pitch=36,distance=22,gateAngle;
        GUIStyle title,body,small,button,metric;
        Font font;Texture2D white;
        public bool VerificationMode { get; private set; }
        void Awake()
        {
            Application.targetFrameRate=60;QualitySettings.vSyncCount=0;
            VerificationMode=Array.Exists(Environment.GetCommandLineArgs(),a=>a=="--verify-water");
            if(VerificationMode)gameObject.AddComponent<WaterAcceptance>().lab=this;
        }
        void Start(){SetOverview(true);}
        public void BeginDrain(){beam.powered=true;}
        public void ResetLab(){beam.powered=false;water.ResetWater();foreach(var b in floaters)b.ResetBody();walker.ResetPosition();RunSeconds=0;gateAngle=0;}
        public void SetOverview(bool overview)
        {
            Overview=overview;walker.SetActive(!overview);
            if(!overview){view.transform.SetParent(walker.eye,false);view.transform.localPosition=Vector3.zero;view.transform.localRotation=Quaternion.identity;}
            else view.transform.SetParent(null,true);
        }
        public void SetInspectionView(Vector3 position,Vector3 target){view.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));}
        void Update()
        {
            Fps=Mathf.Lerp(Fps,1/Mathf.Max(.001f,Time.unscaledDeltaTime),.025f);
            if(water.Gate.IsOpen)RunSeconds+=Time.deltaTime;
            gateAngle=Mathf.MoveTowards(gateAngle,water.Gate.IsOpen?82:0,Time.deltaTime*35);drainGate.localRotation=Quaternion.Euler(gateAngle,0,0);
            if(VerificationMode)return;
            var kb=Keyboard.current;var mouse=Mouse.current;
            if(kb==null||mouse==null)return;
            if(kb.tabKey.wasPressedThisFrame||kb.escapeKey.wasPressedThisFrame)SetOverview(!Overview);
            if(kb.rKey.wasPressedThisFrame)ResetLab();
            if(kb.digit1Key.wasPressedThisFrame)BeginDrain();
            if(kb.vKey.wasPressedThisFrame)water.showFlow=!water.showFlow;
            if(kb.digit2Key.wasPressedThisFrame)water.Disturb(new Vector3(0,0,-2),.10f);
            if(!Overview&&kb.eKey.wasPressedThisFrame&&Vector3.Distance(walker.transform.position,console.position)<3)BeginDrain();
            if(Overview)
            {
                if(mouse.rightButton.isPressed){Vector2 d=mouse.delta.ReadValue();yaw+=d.x*.18f;pitch=Mathf.Clamp(pitch-d.y*.18f,12,80);}
                distance=Mathf.Clamp(distance-mouse.scroll.ReadValue().y*.012f,9,34);
                Vector3 target=new Vector3(0,-1.7f,.4f);Vector3 p=target+Quaternion.Euler(pitch,yaw,0)*Vector3.back*distance;SetInspectionView(p,target);
            }
        }
        void Styles()
        {
            if(body!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},20);white=Texture2D.whiteTexture;
            title=new GUIStyle(GUI.skin.label){font=font,fontSize=27,fontStyle=FontStyle.Bold,normal={textColor=new Color(.92f,.97f,.96f)}};
            body=new GUIStyle(GUI.skin.label){font=font,fontSize=16,normal={textColor=new Color(.83f,.9f,.89f)}};
            small=new GUIStyle(body){fontSize=13,normal={textColor=new Color(.58f,.74f,.74f)}};
            metric=new GUIStyle(title){fontSize=24};
            button=new GUIStyle(GUI.skin.button){font=font,fontSize=16,padding=new RectOffset(14,14,10,10)};
        }
        void Panel(Rect r,Color c){GUI.color=c;GUI.DrawTexture(r,white);GUI.color=Color.white;}
        void OnGUI()
        {
            Styles();float scale=Mathf.Max(.7f,Screen.height/900f);GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));float w=Screen.width/scale,h=Screen.height/scale;
            if(water.Sample(view.transform.position,out float surface,out _,out _)&&view.transform.position.y<surface)Panel(new Rect(0,0,w,h),new Color(.015f,.21f,.24f,.16f));
            Panel(new Rect(26,25,374,148),new Color(.025f,.075f,.084f,.92f));
            GUI.Label(new Rect(46,39,340,24),"W A T E R   /   C O U R T   0 1",small);
            GUI.Label(new Rect(44,65,345,40),"沉水庭院 · 排水实验",title);
            GUI.Label(new Rect(46,111,330,26),"白模玩法原型   /   保守水量 · 局部流动",body);
            GUI.Label(new Rect(46,141,332,24),"高度场流体  /  3,072 单元  /  30 Hz",small);
            Panel(new Rect(w-310,25,284,220),new Color(.025f,.075f,.084f,.91f));
            string state=water.Remaining<.005f?"池底已露出":water.Gate.IsOpen?"正在排水 · 机关已锁定":beam.powered?"水下接收器充能":"蓄水中 · 等待光照";
            GUI.Label(new Rect(w-290,41,252,28),state,body);
            GUI.Label(new Rect(w-290,77,252,38),$"{water.Remaining*100:0.0}%   /   {water.Level-water.bottom:0.00} m",metric);
            Panel(new Rect(w-290,123,244,3),new Color(.15f,.25f,.27f));Panel(new Rect(w-290,123,244*Mathf.Clamp01(water.Remaining),3),new Color(.35f,.85f,.77f));
            GUI.Label(new Rect(w-290,138,252,26),$"出流  {water.Grid.LastDischargeRate:0.00} m³/s     {RunSeconds:0} s",body);
            GUI.Label(new Rect(w-290,174,252,24),$"核心 {water.SolverMilliseconds:0.00} ms   ·   {Fps:0} FPS",small);
            GUI.Label(new Rect(w-290,201,252,24),water.showFlow?"流速着色：蓝 慢 → 橙 快":$"排水倍率 ×{water.drainSpeedMultiplier:0.0} · Inspector 可调",small);
            if(Overview)
            {
                Panel(new Rect(26,h-120,w-52,94),new Color(.025f,.075f,.084f,.94f));
                if(GUI.Button(new Rect(43,h-103,174,42),water.Gate.IsOpen?"排水进行中":"启动排水实验  1",button))BeginDrain();
                if(GUI.Button(new Rect(229,h-103,160,42),"进入庭院  Tab",button))SetOverview(false);
                if(GUI.Button(new Rect(401,h-103,137,42),"扰动水面  2",button))water.Disturb(new Vector3(0,0,-2),.10f);
                if(GUI.Button(new Rect(550,h-103,137,42),"流速视图  V",button))water.showFlow=!water.showFlow;
                if(GUI.Button(new Rect(699,h-103,118,42),"重新蓄水  R",button))ResetLab();
                GUI.Label(new Rect(44,h-56,w-100,24),"右键拖动环视 · 滚轮缩放    |    观察左侧台阶露出、右侧浮台下沉和排水口汇流",small);
            }
            else
            {
                GUI.Label(new Rect(w*.5f-4,h*.5f-14,20,28),"+",body);
                Panel(new Rect(26,h-79,w-52,53),new Color(.025f,.075f,.084f,.92f));
                GUI.Label(new Rect(44,h-67,w-88,40),walker.Swimming?"WASD 游动  ·  空格上浮 / Ctrl 下潜  ·  Tab 返回观察  ·  1 排水  ·  R 重置":"WASD 行走  ·  鼠标观察  ·  空格跳跃  ·  Tab 返回观察  ·  E 操作主控台  ·  1 排水",body);
                if(Vector3.Distance(walker.transform.position,console.position)<3)GUI.Label(new Rect(w*.5f-155,h*.6f,370,32),"E  开启光源 · 激活水下排水机关",body);
            }
        }
    }
}
