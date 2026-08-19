using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Phase 1 → Phase 2 전환 시 보스의 "분석" 연출을 재생합니다.
/// Time.timeScale = 0 으로 전투를 멈추므로 모든 대기는 Realtime을 사용합니다.
/// </summary>
public class AnalysisUI : MonoBehaviour
{
    [Header("── 패널 ──")]
    [Tooltip("연출 중에만 켜지는 루트 오브젝트")]
    [SerializeField] private GameObject panelRoot;
    [Tooltip("배경 어둡게 하는 이미지 (선택)")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("── 텍스트 ──")]
    [SerializeField] private TMP_Text titleText;      // COMBAT DATA ANALYSIS
    [SerializeField] private TMP_Text statsText;      // MELEE : 14 ...
    [SerializeField] private TMP_Text styleLabel;     // DOMINANT STYLE
    [SerializeField] private TMP_Text styleValue;     // CLOSE RANGE
    [SerializeField] private TMP_Text protocolText;   // COUNTER PROTOCOL LOADING...

    [Header("── 타이밍 (전부 Realtime) ──")]
    [SerializeField] private float fadeInTime = 0.25f;
    [Tooltip("한 글자 출력 간격")]
    [SerializeField] private float typeSpeed = 0.02f;
    [Tooltip("스탯 한 줄 출력 후 대기")]
    [SerializeField] private float lineDelay = 0.35f;
    [Tooltip("DOMINANT STYLE 강조 전 뜸들이는 시간")]
    [SerializeField] private float suspenseDelay = 0.7f;
    [Tooltip("로딩 점 애니메이션 시간")]
    [SerializeField] private float loadingTime = 1.4f;
    [Tooltip("ADAPTATION COMPLETE 표시 시간")]
    [SerializeField] private float completeHold = 1.1f;
    [SerializeField] private float fadeOutTime = 0.3f;

    [Header("── 스타일 색상 ──")]
    [SerializeField] private Color meleeColor = new Color(1f, 0.45f, 0.3f);
    [SerializeField] private Color rangedColor = new Color(0.4f, 0.8f, 1f);
    [SerializeField] private Color evasiveColor = new Color(0.9f, 0.7f, 0.3f);
    [SerializeField] private Color airborneColor = new Color(0.72f, 0.5f, 1f);
    [SerializeField] private Color completeColor = new Color(0.4f, 1f, 0.55f);

    [Header("── 문구 ──")]
    [SerializeField] private string titleLine = "COMBAT DATA ANALYSIS";
    [SerializeField] private string loadingLine = "COUNTER PROTOCOL LOADING";
    [SerializeField] private string completeLine = "ADAPTATION COMPLETE";

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>기존 호출 호환용. 현재 윈도우를 확정해 분석 연출을 재생합니다.</summary>
    public IEnumerator PlayAnalysis(PlayerCombatTracker tracker)
    {
        if (tracker == null) yield break;
        CombatAnalysis analysis = tracker.FinishWindow();
        yield return StartCoroutine(PlayAnalysis(tracker, analysis, DominantStyle.None));
    }

    /// <summary>Phase별 분석 결과와 이전 전략을 보여 준 뒤 Counter Strategy 갱신을 알립니다.</summary>
    public IEnumerator PlayAnalysis(PlayerCombatTracker tracker, CombatAnalysis analysis, DominantStyle previousStyle)
    {
        if (tracker == null)
        {
            Debug.LogWarning("[AnalysisUI] tracker가 null입니다.");
            yield break;
        }

        IsPlaying = true;

        DominantStyle style = analysis.style;
        bool tacticChanged = previousStyle != DominantStyle.None && previousStyle != style;

        // ── 화면 정지 ──
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;

        if (panelRoot != null) panelRoot.SetActive(true);
        ClearTexts();

        // ── 1) 페이드 인 ──
        yield return StartCoroutine(Fade(0f, 1f, fadeInTime));

        // ── 2) 타이틀 타이핑 ──
        string header = tacticChanged ? "TACTIC CHANGE DETECTED" : titleLine;
        yield return StartCoroutine(TypeText(titleText, header));
        yield return new WaitForSecondsRealtime(lineDelay);

        // ── 3) 스탯 한 줄씩 ──
        string[] lines =
        {
            $"MELEE HIT   : {analysis.meleeHits}/{analysis.meleeUses}",
            $"RANGED HIT  : {analysis.rangedHits}/{analysis.rangedUses}",
            $"AVG DISTANCE: {tracker.GetDistanceLabel(analysis.averageDistance)}",
            $"DASH USAGE  : {UsageLabel(analysis.dashUses)}"
        };

        string acc = "";
        foreach (string line in lines)
        {
            acc += line + "\n";
            if (statsText != null) statsText.text = acc;
            yield return new WaitForSecondsRealtime(lineDelay);
        }

        // ── 4) 뜸들이기 ──
        if (styleLabel != null) styleLabel.text = "PLAYSTYLE";
        yield return new WaitForSecondsRealtime(suspenseDelay);

        // ── 5) 스타일 공개 ──
        if (styleValue != null)
        {
            styleValue.color = GetStyleColor(style);
            yield return StartCoroutine(TypeText(styleValue, tracker.GetStyleLabel(style)));
        }
        yield return new WaitForSecondsRealtime(suspenseDelay);

        // ── 6) 로딩 점 애니메이션 ──
        float t = 0f;
        int dots = 0;
        while (t < loadingTime)
        {
            if (protocolText != null)
                protocolText.text = tacticChanged
                    ? $"{tracker.GetStyleLabel(previousStyle)} -> {tracker.GetStyleLabel(style)}\nCOUNTER STRATEGY UPDATING" + new string('.', dots % 4)
                    : loadingLine + new string('.', dots % 4);

            dots++;
            t += 0.18f;
            yield return new WaitForSecondsRealtime(0.18f);
        }

        // ── 7) 완료 ──
        if (protocolText != null)
        {
            protocolText.color = completeColor;
            protocolText.text = completeLine;
        }
        yield return new WaitForSecondsRealtime(completeHold);

        // ── 8) 페이드 아웃 ──
        yield return StartCoroutine(Fade(1f, 0f, fadeOutTime));

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = prevScale <= 0f ? 1f : prevScale;

        IsPlaying = false;
        Debug.Log($"[AnalysisUI] 연출 종료 → {style} (변경:{tacticChanged})");
    }

    private Color GetStyleColor(DominantStyle style)
    {
        switch (style)
        {
            case DominantStyle.Melee: return meleeColor;
            case DominantStyle.Ranged: return rangedColor;
            case DominantStyle.Evasive: return evasiveColor;
            case DominantStyle.Airborne: return airborneColor;
            default: return completeColor;
        }
    }

    private static string UsageLabel(int count)
    {
        if (count >= 7) return "HIGH";
        if (count >= 3) return "MEDIUM";
        return "LOW";
    }

    // ───────────────────────── 유틸 ─────────────────────────
    private IEnumerator TypeText(TMP_Text target, string full)
    {
        if (target == null) yield break;

        target.text = "";
        for (int i = 0; i < full.Length; i++)
        {
            target.text += full[i];
            if (typeSpeed > 0f) yield return new WaitForSecondsRealtime(typeSpeed);
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        if (duration <= 0f) { canvasGroup.alpha = to; yield break; }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }

    private void ClearTexts()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (titleText != null) titleText.text = "";
        if (statsText != null) statsText.text = "";
        if (styleLabel != null) styleLabel.text = "";
        if (styleValue != null) styleValue.text = "";
        if (protocolText != null)
        {
            protocolText.text = "";
            protocolText.color = Color.white;
        }
    }
}
