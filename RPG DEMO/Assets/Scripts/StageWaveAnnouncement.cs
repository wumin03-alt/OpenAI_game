using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Stage01 전용 웨이브 안내문입니다.
/// 웨이브 진행을 멈추지 않고 현재 StageArenaWaveController 상태만 표시합니다.
/// </summary>
[RequireComponent(typeof(StageArenaWaveController))]
public sealed class StageWaveAnnouncement : MonoBehaviour
{
    [Header("── 연결 ──")]
    [SerializeField] private Canvas stageCanvas;

    [Header("── 타이밍 ──")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.12f;
    [SerializeField, Min(0f)] private float holdDuration = 0.72f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.26f;

    [Header("── 스케일 ──")]
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private float overshootScale = 1.05f;

    private StageArenaWaveController waveController;
    private GameObject announcementRoot;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI announcementText;
    private Coroutine announcementRoutine;
    private int displayedWave;
    private bool displayedClear;

    private void Awake()
    {
        waveController = GetComponent<StageArenaWaveController>();
        CreateVisual();
    }

    private void Update()
    {
        if (waveController == null || announcementRoot == null) return;

        if (waveController.CurrentWave > displayedWave)
        {
            displayedWave = waveController.CurrentWave;
            Show(GetWaveLabel(displayedWave), GetWaveColor(displayedWave));
        }

        if (!displayedClear && waveController.IsCleared)
        {
            displayedClear = true;
            Show("STAGE CLEAR", new Color(0.22f, 0.95f, 0.68f, 1f));
        }
    }

    private void CreateVisual()
    {
        Canvas canvas = stageCanvas != null ? stageCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Stage01] Wave announcement cannot initialize: Canvas is missing.", this);
            enabled = false;
            return;
        }

        announcementRoot = new GameObject("WaveAnnouncement", typeof(RectTransform), typeof(CanvasGroup));
        announcementRoot.transform.SetParent(canvas.transform, false);
        announcementRoot.transform.SetAsLastSibling();

        RectTransform rootRect = announcementRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, 80f);
        rootRect.sizeDelta = new Vector2(1100f, 140f);

        canvasGroup = announcementRoot.GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(announcementRoot.transform, false);

        announcementText = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text template = FindFirstObjectByType<TMP_Text>();
        announcementText.font = template != null ? template.font : TMP_Settings.defaultFontAsset;
        announcementText.fontSize = 64f;
        announcementText.fontStyle = FontStyles.Bold;
        announcementText.alignment = TextAlignmentOptions.Center;
        announcementText.textWrappingMode = TextWrappingModes.NoWrap;
        announcementText.characterSpacing = 5f;
        announcementText.outlineColor = new Color(0.02f, 0.05f, 0.1f, 0.95f);
        announcementText.outlineWidth = 0.18f;
        announcementText.raycastTarget = false;

        RectTransform textRect = announcementText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        announcementRoot.SetActive(false);
    }

    private void Show(string message, Color color)
    {
        if (announcementRoutine != null) StopCoroutine(announcementRoutine);

        announcementRoot.SetActive(true);
        announcementText.text = message;
        announcementText.color = color;
        announcementRoutine = StartCoroutine(AnimateAnnouncement());
    }

    private IEnumerator AnimateAnnouncement()
    {
        Vector3 initialScale = Vector3.one * startScale;
        Vector3 peakScale = Vector3.one * overshootScale;
        announcementRoot.transform.localScale = initialScale;
        canvasGroup.alpha = 0f;

        yield return FadeAndScale(0f, 1f, initialScale, peakScale, fadeInDuration);
        announcementRoot.transform.localScale = Vector3.one;
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return FadeAndScale(1f, 0f, Vector3.one, Vector3.one, fadeOutDuration);

        announcementRoot.SetActive(false);
        announcementRoutine = null;
    }

    private IEnumerator FadeAndScale(float fromAlpha, float toAlpha, Vector3 fromScale, Vector3 toScale, float duration)
    {
        if (duration <= 0f)
        {
            canvasGroup.alpha = toAlpha;
            announcementRoot.transform.localScale = toScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = progress * progress * (3f - 2f * progress);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            announcementRoot.transform.localScale = Vector3.LerpUnclamped(fromScale, toScale, eased);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
        announcementRoot.transform.localScale = toScale;
    }

    /// <summary>스테이지 번호가 아니라 실제 웨이브 수를 기준으로 마지막 웨이브를 판별합니다.</summary>
    private bool IsFinalWave(int wave)
    {
        int totalWaves = waveController.TotalWaves;
        return totalWaves > 1 && wave >= totalWaves;
    }

    private string GetWaveLabel(int wave)
    {
        return IsFinalWave(wave) ? "FINAL WAVE" : $"WAVE {wave}";
    }

    private Color GetWaveColor(int wave)
    {
        return IsFinalWave(wave)
            ? new Color(1f, 0.72f, 0.24f, 1f)
            : new Color(0.15f, 0.9f, 1f, 1f);
    }
}
