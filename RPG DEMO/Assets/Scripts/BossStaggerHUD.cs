using Game.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>중간보스와 최종보스가 공유하는 3칸 그로기 HUD입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossStaggerGauge))]
public sealed class BossStaggerHUD : MonoBehaviour
{
    private static readonly Color FullColor = new Color(0.15f, 0.9f, 1f, 1f);
    private static readonly Color EmptyColor = new Color(0.12f, 0.16f, 0.24f, 1f);
    private static readonly Color GroggyColor = new Color(0.72f, 0.22f, 1f, 1f);

    private BossStaggerGauge gauge;
    private Canvas canvas;
    private Image[] segments;
    private Text label;
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
        if (gauge == null || timer == null) return;

        if (gauge.IsStaggered)
            timer.text = $"GROGGY  {gauge.StaggerTimeRemaining:0.0}s";
    }

    private void BuildHud()
    {
        canvas = RuntimeUIFactory.CreateCanvas("BossStaggerCanvas", null, 225);

        Image panel = RuntimeUIFactory.CreateImage(canvas.transform, "StaggerPanel",
            new Color(0.02f, 0.04f, 0.09f, 0.92f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -98f);
        panelRect.sizeDelta = new Vector2(500f, 76f);

        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.9f, 1f, 0.86f);
        outline.effectDistance = new Vector2(2f, -2f);

        label = RuntimeUIFactory.CreateText(panelRect, "STAGGER GAUGE", 18,
            new Vector2(-145f, 20f), new Vector2(190f, 28f), new Color(0.82f, 0.92f, 1f));
        label.alignment = TextAnchor.MiddleLeft;
        label.fontStyle = FontStyle.Bold;

        timer = RuntimeUIFactory.CreateText(panelRect, "PARRY  3 / 3", 17,
            new Vector2(150f, 20f), new Vector2(190f, 28f), new Color(0.34f, 1f, 0.7f));
        timer.alignment = TextAnchor.MiddleRight;
        timer.fontStyle = FontStyle.Bold;

        segments = new Image[gauge.ParriesRequired];
        float totalWidth = 440f;
        float gap = 10f;
        float width = (totalWidth - gap * (segments.Length - 1)) / segments.Length;
        float startX = -totalWidth * 0.5f + width * 0.5f;
        for (int i = 0; i < segments.Length; i++)
        {
            Image segment = RuntimeUIFactory.CreateImage(panelRect, $"Segment_{i + 1}", FullColor);
            RectTransform rect = segment.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(startX + i * (width + gap), -17f);
            rect.sizeDelta = new Vector2(width, 18f);
            segments[i] = segment;
        }
    }

    private void HandleGaugeChanged(float normalized)
    {
        if (segments == null) return;

        int remaining = gauge.RemainingSegments;
        for (int i = 0; i < segments.Length; i++)
            segments[i].color = i < remaining ? FullColor : EmptyColor;

        if (!gauge.IsStaggered)
        {
            timer.text = $"PARRY  {remaining} / {gauge.ParriesRequired}";
            timer.color = new Color(0.34f, 1f, 0.7f);
        }
    }

    private void HandleStaggerStarted(float duration)
    {
        label.text = "CORE OVERLOAD";
        label.color = GroggyColor;
        timer.color = GroggyColor;
        foreach (Image segment in segments) segment.color = GroggyColor;
    }

    private void HandleStaggerEnded()
    {
        label.text = "STAGGER GAUGE";
        label.color = new Color(0.82f, 0.92f, 1f);
        HandleGaugeChanged(gauge.Normalized);
    }
}
