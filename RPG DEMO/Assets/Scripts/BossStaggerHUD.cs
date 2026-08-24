using Game.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>보스 HP 바 아래에 표시되는 연속형 그로기 HUD입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossStaggerGauge))]
public sealed class BossStaggerHUD : MonoBehaviour
{
    private static readonly Color TrackColor = new Color(0.035f, 0.045f, 0.065f, 0.98f);
    private static readonly Color BorderColor = new Color(0.55f, 0.58f, 0.61f, 0.9f);
    private static readonly Color FullColor = new Color(1f, 0.84f, 0.22f, 1f);
    private static readonly Color LowColor = new Color(1f, 0.38f, 0.12f, 1f);
    private static readonly Color GroggyColor = new Color(0.78f, 0.32f, 1f, 1f);

    private BossStaggerGauge gauge;
    private Canvas canvas;
    private Image track;
    private Image fill;
    private Image glint;
    private Text timer;

    private void Start()
    {
        gauge = GetComponent<BossStaggerGauge>();
        BuildHud();
        gauge.GaugeChanged += HandleGaugeChanged;
        gauge.StaggerStarted += HandleStaggerStarted;
        gauge.StaggerEnded += HandleStaggerEnded;
        HandleGaugeChanged(gauge.Normalized);
    }

    private void OnDestroy()
    {
        if (gauge != null)
        {
            gauge.GaugeChanged -= HandleGaugeChanged;
            gauge.StaggerStarted -= HandleStaggerStarted;
            gauge.StaggerEnded -= HandleStaggerEnded;
        }

        if (canvas != null) Destroy(canvas.gameObject);
    }

    private void Update()
    {
        if (gauge == null || timer == null || !gauge.IsStaggered) return;
        timer.text = $"GROGGY  {gauge.StaggerTimeRemaining:0.0}s";
    }

    private void BuildHud()
    {
        bool middleBossLayout = GetComponent<MiddleBossController>() != null;
        float barWidth = middleBossLayout ? 790f : 940f;
        float centerY = middleBossLayout ? -131f : -68f;

        canvas = RuntimeUIFactory.CreateCanvas("BossStaggerCanvas", null, 225);

        Image shadow = RuntimeUIFactory.CreateImage(canvas.transform, "GroggyShadow",
            new Color(0f, 0f, 0f, 0.72f));
        SetTopCenter(shadow.rectTransform, new Vector2(0f, centerY - 2f),
            new Vector2(barWidth + 8f, 20f));
        MiddleBossUIStyle.Rounded(shadow, shadow.color);

        track = RuntimeUIFactory.CreateImage(canvas.transform, "GroggyTrack", TrackColor);
        SetTopCenter(track.rectTransform, new Vector2(0f, centerY),
            new Vector2(barWidth, 14f));
        MiddleBossUIStyle.Rounded(track, TrackColor);
        MiddleBossUIStyle.Outline(track, BorderColor, 1f);
        track.raycastTarget = false;

        fill = RuntimeUIFactory.CreateImage(track.rectTransform, "GroggyFill", FullColor);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = 0;
        fill.raycastTarget = false;
        RuntimeUIFactory.Stretch(fill.rectTransform, 3f, -3f, 3f, -3f);

        glint = RuntimeUIFactory.CreateImage(track.rectTransform, "GroggyHighlight",
            new Color(1f, 0.96f, 0.58f, 0.78f));
        glint.type = Image.Type.Filled;
        glint.fillMethod = Image.FillMethod.Horizontal;
        glint.fillOrigin = 0;
        glint.raycastTarget = false;
        RuntimeUIFactory.Stretch(glint.rectTransform, 5f, -5f, 8f, -3f);

        timer = RuntimeUIFactory.CreateText(canvas.transform, string.Empty, 15,
            new Vector2(0f, centerY - 20f), new Vector2(280f, 24f), GroggyColor);
        RectTransform timerRect = timer.rectTransform;
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(0.5f, 1f);
        timerRect.pivot = new Vector2(0.5f, 0.5f);
        timer.fontStyle = FontStyle.Bold;
        timer.gameObject.SetActive(false);
    }

    private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void HandleGaugeChanged(float normalized)
    {
        if (fill == null || glint == null) return;

        float clamped = Mathf.Clamp01(normalized);
        fill.fillAmount = clamped;
        glint.fillAmount = clamped;
        fill.color = Color.Lerp(LowColor, FullColor, clamped);

        if (!gauge.IsStaggered)
        {
            track.color = TrackColor;
            timer.gameObject.SetActive(false);
        }
    }

    private void HandleStaggerStarted(float duration)
    {
        track.color = new Color(0.16f, 0.055f, 0.22f, 0.98f);
        timer.color = GroggyColor;
        timer.text = $"GROGGY  {duration:0.0}s";
        timer.gameObject.SetActive(true);
    }

    private void HandleStaggerEnded()
    {
        timer.gameObject.SetActive(false);
        HandleGaugeChanged(gauge.Normalized);
    }
}
