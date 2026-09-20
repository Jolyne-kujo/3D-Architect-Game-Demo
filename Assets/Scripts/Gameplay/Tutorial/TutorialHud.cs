using System.Collections.Generic;
using CoastalTemple.Interaction;
using CoastalTemple.Player;
using UnityEngine;

namespace CoastalTemple.Tutorial
{
    /// <summary>Optional UI/audio presenter. Removing it leaves gameplay intact.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialHud : MonoBehaviour
    {
        public TutorialJourney journey;
        public PlayerInteractor interactor;
        public PlayerLantern lantern;
        public AudioSource chimeSource;
        public bool showHud = true;
        readonly Queue<string> rewards = new Queue<string>();
        TutorialJourney subscribedJourney;
        float rewardUntil;
        string rewardText;
        GUIStyle titleStyle, bodyStyle, smallStyle, promptStyle;
        AudioClip ownedChime;
        Font ownedFont;

        void OnEnable() => BindEvents();
        void OnDisable()
        {
            if (subscribedJourney) subscribedJourney.RewardIssued -= QueueReward;
            subscribedJourney = null;
        }
        void OnDestroy()
        {
            if (ownedChime) Destroy(ownedChime);
            if (ownedFont) Destroy(ownedFont);
        }
        void BindEvents()
        {
            if (subscribedJourney == journey) return;
            if (subscribedJourney) subscribedJourney.RewardIssued -= QueueReward;
            subscribedJourney = journey;
            if (subscribedJourney) subscribedJourney.RewardIssued += QueueReward;
        }
        void Update()
        {
            BindEvents();
            if (Time.unscaledTime >= rewardUntil && rewards.Count > 0)
            {
                rewardText = rewards.Dequeue();
                rewardUntil = Time.unscaledTime + 5;
                PlayChime();
            }
        }
        public void QueueReward(string message)
        {
            if (!string.IsNullOrWhiteSpace(message)) rewards.Enqueue(message);
        }

        void OnGUI()
        {
            if (!showHud || !interactor || (journey && !journey.showHud)) return;
            EnsureStyles();
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            float scale = Mathf.Clamp(Screen.height / 900f, .7f, 1.6f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            float width = Screen.width / scale, height = Screen.height / scale;
            var lesson = journey ? journey.CurrentLesson : null;
            if (lesson != null)
            {
                Panel(new Rect(24, 24, 430, string.IsNullOrEmpty(lesson.safetyNote) ? 94 : 124));
                GUI.Label(new Rect(42, 36, 396, 32), string.IsNullOrEmpty(lesson.title) ? lesson.name : lesson.title, titleStyle);
                string state = lesson.completionCondition && lesson.completionCondition.IsComplete ? "通路已经建立  ·  可以继续观察机关" : "观察光路与地形  ·  H 查看线索";
                GUI.Label(new Rect(43, 75, 390, 29), state, bodyStyle);
                if (!string.IsNullOrEmpty(lesson.safetyNote)) GUI.Label(new Rect(43, 108, 395, 29), lesson.safetyNote, smallStyle);
            }
            bool active = interactor.PlayerCanAct;
            Panel(new Rect(24, height - 72, width - 48, 48));
            string controls = active ? "WASD 慢跑  ·  鼠标观察  ·  Shift 快跑  ·  空格 跳跃 / 上浮  ·  Ctrl 下潜  ·  H 观察线索  ·  Esc 释放鼠标" : "鼠标已释放  ·  点击画面或按 Esc 继续游戏";
            GUI.Label(new Rect(41, height - 62, width - 83, 29), controls, smallStyle);
            if (active)
            {
                if (lantern) GUI.Label(new Rect(width - 270, 32, 235, 30), lantern.HasLantern ? "提灯  " + (lantern.LanternOn ? "亮" : "灭") + "  ·  L 切换" : "提灯尚未拾取", smallStyle);
                var nearby = interactor.Nearby;
                if (nearby && nearby.CanUse(interactor))
                {
                    float promptWidth = Mathf.Min(580, width - 48);
                    Panel(new Rect((width - promptWidth) * .5f, height - 139, promptWidth, 43));
                    string prompt = nearby.DisplayPrompt ?? "操作";
                    GUI.Label(new Rect((width - promptWidth) * .5f + 16, height - 133, promptWidth - 32, 31), prompt.StartsWith("E") ? prompt : "E  " + prompt, promptStyle);
                }
                GUI.Label(new Rect(width * .5f - 5, height * .5f - 15, 16, 26), "·", promptStyle);
            }
            if (Time.unscaledTime < rewardUntil && !string.IsNullOrEmpty(rewardText))
            {
                float popupWidth = Mathf.Min(620, width - 48);
                Panel(new Rect((width - popupWidth) * .5f, 140, popupWidth, 88));
                GUI.Label(new Rect((width - popupWidth) * .5f + 22, 151, popupWidth - 44, 66), rewardText, promptStyle);
            }
            if (active && journey && Time.unscaledTime < journey.ObservationUntil && !string.IsNullOrEmpty(journey.ObservationText))
            {
                float hintWidth = Mathf.Min(650, width - 48);
                Panel(new Rect(24, height - 266, hintWidth, 100));
                GUI.Label(new Rect(41, height - 257, hintWidth - 33, 25), "观察  ·  H 再看一条线索", smallStyle);
                GUI.Label(new Rect(41, height - 230, hintWidth - 33, 58), journey.ObservationText, bodyStyle);
            }
            GUI.matrix = previousMatrix;
            GUI.color = previousColor;
        }
        void EnsureStyles()
        {
            if (titleStyle != null) return;
            ownedFont = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "Arial" }, 18);
            bodyStyle = new GUIStyle(GUI.skin.label) { font = ownedFont, fontSize = 16, wordWrap = true, normal = { textColor = new Color(.88f, .89f, .82f) } };
            titleStyle = new GUIStyle(bodyStyle) { fontSize = 23, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(bodyStyle) { fontSize = 14 };
            promptStyle = new GUIStyle(bodyStyle) { fontSize = 19, alignment = TextAnchor.MiddleLeft };
        }
        static void Panel(Rect rect)
        {
            Color previous = GUI.color;
            GUI.color = new Color(.06f, .10f, .11f, .90f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
        void PlayChime()
        {
            if (!Application.isPlaying || !chimeSource) return;
            if (!ownedChime)
            {
                const int rate = 24000, samples = 9600;
                float[] data = new float[samples];
                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / rate;
                    float envelope = Mathf.Min(1, t * 80) * Mathf.Exp(-t * 11);
                    data[i] = envelope * (.50f * Mathf.Sin(2 * Mathf.PI * 659.25f * t) + .25f * Mathf.Sin(2 * Mathf.PI * 987.77f * t));
                }
                ownedChime = AudioClip.Create("Discovery chime", samples, 1, rate, false);
                ownedChime.SetData(data, 0);
            }
            chimeSource.PlayOneShot(ownedChime);
        }
    }
}
