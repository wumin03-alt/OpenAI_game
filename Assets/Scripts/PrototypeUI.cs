using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace AdaptiveBossPrototype
{
    public sealed class PrototypeUI : MonoBehaviour
    {
        private Text stageText;
        private Text guideText;
        private Text telemetryText;
        private Text bannerText;
        private Image playerFill;
        private Image bossFill;
        private GameObject bossBarRoot;
        private Coroutine bannerRoutine;

        public void Build()
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            stageText = MakeText("Stage", new Vector2(32, -25), new Vector2(900, 55), 30, TextAnchor.UpperLeft, Color.white);
            guideText = MakeText("Guide", new Vector2(0, 34), new Vector2(1500, 60), 25, TextAnchor.MiddleCenter, new Color(0.84f, 0.9f, 1f));
            guideText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            guideText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            guideText.rectTransform.pivot = new Vector2(0.5f, 0f);
            telemetryText = MakeText("Telemetry", new Vector2(-32, -25), new Vector2(520, 180), 23, TextAnchor.UpperRight, new Color(0.75f, 0.85f, 0.95f));
            telemetryText.rectTransform.anchorMin = Vector2.one;
            telemetryText.rectTransform.anchorMax = Vector2.one;
            telemetryText.rectTransform.pivot = Vector2.one;

            playerFill = MakeBar("PlayerHP", new Vector2(32, -92), new Vector2(460, 30), new Color(0.25f, 0.9f, 0.55f), false, out _);
            bossFill = MakeBar("BossHP", new Vector2(0, -32), new Vector2(780, 28), new Color(0.95f, 0.25f, 0.28f), true, out bossBarRoot);
            bossBarRoot.SetActive(false);

            bannerText = MakeText("Banner", Vector2.zero, new Vector2(1500, 180), 42, TextAnchor.MiddleCenter, Color.white);
            bannerText.rectTransform.anchorMin = new Vector2(0.5f, 0.63f);
            bannerText.rectTransform.anchorMax = new Vector2(0.5f, 0.63f);
            bannerText.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            bannerText.gameObject.SetActive(false);
        }

        public void UpdateHud(StageState state, Health player, Health boss, PlayerCombatTracker tracker, int phase, DominantStyle learned)
        {
            playerFill.fillAmount = player == null ? 0f : player.Normalized;
            bool showBoss = boss != null && !boss.IsDead;
            bossBarRoot.SetActive(showBoss);
            if (showBoss) bossFill.fillAmount = boss.Normalized;

            string stageName = state == StageState.Tutorial ? "01  TRAINING SIMULATION"
                : state == StageState.MobBattle ? "02  COMBAT TEST"
                : state == StageState.BossBattle ? $"03  ADAPTIVE BOSS  /  PHASE {phase}"
                : "SIMULATION COMPLETE";
            stageText.text = stageName;
            telemetryText.text = $"ACTION LOG\nMELEE  {tracker.MeleeCount:00}   RANGE  {tracker.RangedCount:00}\nDASH   {tracker.DashCount:00}   PARRY  {tracker.ParryCount:00}"
                + (state == StageState.BossBattle ? $"\n\nAI READ: {learned.KoreanName()}" : string.Empty);
        }

        public void SetGuide(string text) => guideText.text = text;

        public void ShowBanner(string text, Color color, float seconds = 2.2f)
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(Banner(text, color, seconds));
        }

        private IEnumerator Banner(string text, Color color, float seconds)
        {
            bannerText.text = text;
            bannerText.color = color;
            bannerText.gameObject.SetActive(true);
            yield return new WaitForSeconds(seconds);
            bannerText.gameObject.SetActive(false);
        }

        private Text MakeText(string name, Vector2 position, Vector2 size, int fontSize, TextAnchor align, Color color)
        {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(transform, false);
            RectTransform rect = obj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = obj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = align;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Image MakeBar(string name, Vector2 position, Vector2 size, Color color, bool centered, out GameObject root)
        {
            root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(transform, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = centered ? new Vector2(0.5f, 1f) : new Vector2(0f, 1f);
            rect.anchorMax = rect.anchorMin;
            rect.pivot = centered ? new Vector2(0.5f, 1f) : new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image back = root.AddComponent<Image>();
            back.color = new Color(0.04f, 0.055f, 0.085f, 0.92f);

            GameObject fillObject = new GameObject("Fill", typeof(RectTransform));
            fillObject.transform.SetParent(root.transform, false);
            RectTransform fillRect = fillObject.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0.01f, 0.14f);
            fillRect.anchorMax = new Vector2(0.99f, 0.86f);
            fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
            Image fill = fillObject.AddComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
        }
    }
}
