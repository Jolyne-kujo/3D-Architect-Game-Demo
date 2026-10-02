using CoastalTemple.Interaction;
using CoastalTemple.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using WaterCourtyard;

namespace CoastalTemple
{
    /// <summary>Optional sandbox presentation. The mechanism prefabs do not depend on this component.</summary>
    public sealed class MechanismPlaygroundHud : MonoBehaviour
    {
        public CourtyardWalker walker;
        public PlayerInteractor interactor;
        public CoastalPlayerCamera cameraRig;
        Font font;
        void Start() { font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",18); }
        void OnDestroy() { if(font) Destroy(font); }
        void Update()
        {
            if (walker && walker.transform.position.y < -12) { walker.ResetPosition(); if(cameraRig) cameraRig.SnapToTarget(); }
            if (Keyboard.current != null && Keyboard.current.homeKey.wasPressedThisFrame && walker) { walker.ResetPosition(); if(cameraRig)cameraRig.SnapToTarget(); }
        }
        void OnGUI()
        {
            var style=new GUIStyle(GUI.skin.label){font=font,fontSize=18,wordWrap=true};
            GUI.Box(new Rect(20,20,540,166),GUIContent.none);
            GUI.Label(new Rect(34,28,514,152),"机关预制体试验场\n黄 → 蓝 → 红 → 关闭；上大灯当前 / 下小灯下次\n蓝光开桥；红受光器升台，黄受光器降台\n池前岸边 DRAIN / REFILL：E 排水，再按 E 补水\n踩到水下楼梯即可站立并沿楼梯走出\n上浮板：在水面贴近边缘向前，自动抓边上岸",style);
            GUI.Box(new Rect(20,Screen.height-70,Screen.width-40,48),GUIContent.none);
            bool operating=cameraRig&&cameraRig.IsMechanismView;
            GUI.Label(new Rect(32,Screen.height-61,Screen.width-64,40),operating?"E 继续操作当前机关 · WASD / 空格 / Esc 返回第一人称":"WASD 移动 · Shift 快跑 · 空格 跳跃 / 上浮 · Ctrl 下潜 · E 操作 · Home 回起点 · Esc 鼠标",style);
            if(interactor && interactor.Nearby)
                GUI.Label(new Rect(Screen.width*.5f-230,Screen.height-122,460,40),"E  "+interactor.Nearby.DisplayPrompt,style);
        }
    }
}
