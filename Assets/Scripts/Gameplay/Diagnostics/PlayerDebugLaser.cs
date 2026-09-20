using CoastalTemple.LightPuzzles;
using CoastalTemple.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using WaterCourtyard;

namespace CoastalTemple.Diagnostics
{
    /// <summary>Opt-in camera-aimed tool. Uses the ordinary optical world and receiver rules.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(200)]
    public sealed class PlayerDebugLaser : MonoBehaviour
    {
        public CourtyardWalker walker;
        public CoastalPlayerCamera cameraRig;
        public Camera view;
        public LaserEmitter emitter;
        public bool readInput = true;
        [Tooltip("Normally available only in the Editor and Development builds.")]
        public bool allowInReleaseBuild;
        public bool DebugEnabled { get; private set; }
        public LightColorChannel SelectedColor { get; private set; } = LightColorChannel.Red;
        public bool IsFiring => emitter && emitter.Powered;
        public bool CanUse => isActiveAndEnabled && (Application.isEditor || Debug.isDebugBuild || allowInReleaseBuild)
            && view && emitter && (!walker || walker.active) && (!cameraRig || !cameraRig.IsOverview);
        bool firing;
        Font font;
        GUIStyle style;

        void Awake()
        {
            if(!walker)walker=GetComponent<CourtyardWalker>();
            if(!cameraRig)cameraRig=GetComponent<CoastalPlayerCamera>();
            if(!view&&cameraRig)view=cameraRig.view;
            SetDebugEnabled(false);
        }
        void OnDisable(){firing=false;DebugEnabled=false;if(emitter)emitter.SetChannel(LightColorChannel.None);}
        void OnDestroy(){if(font)Destroy(font);}
        public void SetDebugEnabled(bool enabled)
        {
            DebugEnabled=enabled&&(Application.isEditor||Debug.isDebugBuild||allowInReleaseBuild);
            firing=false;RefreshEmitter();
        }
        public void SelectColor(LightColorChannel color)
        {
            if(color!=LightColorChannel.Red&&color!=LightColorChannel.Yellow&&color!=LightColorChannel.Blue)return;
            SelectedColor=color;RefreshEmitter();
        }
        public void CycleColor()=>SelectColor(SelectedColor==LightColorChannel.Red?LightColorChannel.Yellow:SelectedColor==LightColorChannel.Yellow?LightColorChannel.Blue:LightColorChannel.Red);
        public void SetFiring(bool pressed){firing=pressed;RefreshEmitter();}
        void Update()
        {
            if(!readInput)return;
            var keyboard=Keyboard.current;var mouse=Mouse.current;
            if(keyboard!=null&&keyboard.f6Key.wasPressedThisFrame)SetDebugEnabled(!DebugEnabled);
            if(DebugEnabled&&keyboard!=null&&keyboard.f7Key.wasPressedThisFrame)CycleColor();
            firing=DebugEnabled&&mouse!=null&&mouse.rightButton.isPressed;
        }
        // Follow the finalized camera (order 150), before optical tracing (order 400).
        void LateUpdate()=>RefreshEmitter();
        public void RefreshEmitter()
        {
            if(!emitter)return;
            if(view)emitter.Origin=view.transform;
            if(walker)emitter.IgnoreRoot=walker.transform;
            emitter.SetChannel(CanUse&&DebugEnabled&&firing?SelectedColor:LightColorChannel.None);
        }
        void OnGUI()
        {
            if(!(Application.isEditor||Debug.isDebugBuild||allowInReleaseBuild))return;
            if(style==null){font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",16);style=new GUIStyle(GUI.skin.label){font=font,fontSize=16,wordWrap=true};}
            string color=SelectedColor==LightColorChannel.Red?"红":SelectedColor==LightColorChannel.Yellow?"黄":"蓝";
            var box=new Rect(Mathf.Max(12,Screen.width-330),Screen.width<940?200:70,310,DebugEnabled?102:38);GUI.Box(box,GUIContent.none);
            GUI.Label(new Rect(box.x+12,box.y+7,286,90),DebugEnabled?"调试激光："+color+"光  ·  F6 关闭\nF7 切色：红 → 黄 → 蓝\n按住鼠标右键，朝屏幕中心发射":"F6 开启玩家调试激光",style);
            if(DebugEnabled&&CanUse)
            {
                Color old=GUI.color;GUI.color=LaserEmitter.ColorForChannel(SelectedColor);float x=Screen.width*.5f,y=Screen.height*.5f;
                GUI.DrawTexture(new Rect(x-9,y-1,18,2),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(x-1,y-9,2,18),Texture2D.whiteTexture);GUI.color=old;
            }
        }
    }
}
