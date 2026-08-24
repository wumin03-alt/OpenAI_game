using Game.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>중간보스와 최종보스가 공유하는 3칸 그로기 HUD입니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BossStaggerGauge))]
public sealed class BossStaggerHUD : MonoBehaviour
{
    private static readonly Color FullColor = new Color(0.2f, 0.88f, 0.72f, 1f);
    private static readonly Color EmptyColor = new Color(0.31f, 0.2f, 0.16f, 1f);
    private static readonly Color GroggyColor = new Color(0.78f, 0.32f, 1f, 1f);

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
            timer.text = $"그로기  {gauge.StaggerTimeRemaining:0.0}초";
    }

    private void BuildHud()
    {
        canvas = RuntimeUIFactory.CreateCanvas("BossStaggerCanvas", null, 225);

        Image panel = RuntimeUIFactory.CreateImage(canvas.transform, "StaggerCharm",
            new Color(0.2f, 0.08f, 0.07f, 0.95f));
        MiddleBossUIStyle.Rounded(panel, new Color(0.2f, 0.08f, 0.07f, 0.95f));
        MiddleBossUIStyle.Outline(panel, new Color(1f, 0.72f, 0.24f, 0.92f), 2f);
        MiddleBossUIStyle.Shadow(panel, new Color(0f, 0f, 0f, 0.7f), 5f);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -132f);
        panelRect.sizeDelta = new Vector2(430f, 76f);

        label = RuntimeUIFactory.CreateText(panelRect, "코어 그로기", 18,
            new Vector2(-125f, 20f), new Vector2(150f, 28f), new Color(1f, 0.86f, 0.5f));
        label.alignment = TextAnchor.MiddleLeft;
        label.fontStyle = FontStyle.Bold;

        timer = RuntimeUIFactory.CreateText(panelRect, "패링  3 / 3", 17,
            new Vector2(125f, 20f), new Vector2(150f, 28f), new Color(0.42f, 1f, 0.72f));
        timer.alignment = TextAnchor.MiddleRight;
        timer.fontStyle = FontStyle.Bold;

        segments = new Image[gauge.ParriesRequired];
        float totalWidth = 370f;
        float gap = 12f;
        float width = (totalWidth - gap * (segments.Length - 1)) / segments.Length;
        float startX = -totalWidth * 0.5f + width * 0.5f;
        for (int i = 0; i < segments.Length; i++)
        {
            Image segment = RuntimeUIFactory.CreateImage(panelRect, $"Segment_{i + 1}", FullColor);
            RectTransform rect = segment.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(startX + i * (width + gap), -17f);
            rect.sizeDelta = new Vector2(width, 22f);
            MiddleBossUIStyle.Rounded(segment, FullColor);
            MiddleBossUIStyle.Outline(segment, new Color(1f, 0.86f, 0.46f, 0.7f), 1f);
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
            timer.text = $"패링  {remaining} / {gauge.ParriesRequired}";
            timer.color = new Color(0.34f, 1f, 0.7f);
        }
    }

    private void HandleStaggerStarted(float duration)
    {
        label.text = "코어 과부하";
        label.color = GroggyColor;
        timer.color = GroggyColor;
        foreach (Image segment in segments) segment.color = GroggyColor;
    }

    private void HandleStaggerEnded()
    {
        label.text = "코어 그로기";
        label.color = new Color(1f, 0.86f, 0.5f);
        HandleGaugeChanged(gauge.Normalized);
    }
}
