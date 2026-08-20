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
    [SerializeField] private Color completeColor = new Color(0.4f, 1f, 0.55f);

    [Header("── 문구 ──")]
    [SerializeField] private string titleLine = "root@ai-core:~/combat $ analyze --live";
    [SerializeField] private string loadingLine = "$ countermeasure --build";
    [SerializeField] private string completeLine = "[OK] 대응 프로토콜 배포 완료";

    public bool IsPlaying { get; private set; }

    private void Awake()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    /// <summary>보스가 HP 50%에 도달했을 때 호출합니다. 연출이 끝날 때까지 대기합니다.</summary>
    public IEnumerator PlayAnalysis(PlayerCombatTracker tracker)
    {
        if (tracker == null)
        {
            Debug.LogWarning("[AnalysisUI] tracker가 null입니다.");
            yield break;
        }

        IsPlaying = true;

        // ── 데이터 확정 (기록 중단 + 스타일 락) ──
        DominantStyle style = tracker.LockAndStopRecording();

        // ── 화면 정지 ──
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;

        if (panelRoot != null) panelRoot.SetActive(true);
        ClearTexts();

        // ── 1) 페이드 인 ──
        yield return StartCoroutine(Fade(0f, 1f, fadeInTime));

        // ── 2) 타이틀 타이핑 ──
        yield return StartCoroutine(TypeText(titleText, titleLine));
        yield return new WaitForSecondsRealtime(lineDelay);

        // ── 3) 스탯 한 줄씩 ──
        string[] lines =
        {
            $"[01] melee.sample      {tracker.MeleeCount:D2}",
            $"[02] ranged.sample     {tracker.RangedCount:D2}",
            $"[03] mobility.profile  {tracker.GetMobilityLabel()}",
            $"[04] evade.vector      {tracker.GetEvasionLabel()}",
            $"[05] parry.inject      {tracker.ParrySuccessCount}/{tracker.ParryCount}",
            $"[06] data.integrity    {tracker.DataIntegrity * 100f:F0}%"
        };

        string acc = "";
        foreach (string line in lines)
        {
            acc += line + "\n";
            if (statsText != null) statsText.text = acc;
            yield return new WaitForSecondsRealtime(lineDelay);
        }

        // ── 4) 뜸들이기 ──
        if (styleLabel != null) styleLabel.text = "> dominant_profile";
        yield return new WaitForSecondsRealtime(suspenseDelay);

        // ── 5) 스타일 공개 ──
        if (styleValue != null)
        {
            styleValue.color = style == DominantStyle.Melee ? meleeColor : rangedColor;
            yield return StartCoroutine(TypeText(styleValue, tracker.GetStyleLabel()));
        }
        yield return new WaitForSecondsRealtime(suspenseDelay);

        // ── 6) 보스가 실제로 장착할 카운터를 공개 ──
        float t = 0f;
        int dots = 0;
        while (t < loadingTime)
        {
            if (protocolText != null)
                protocolText.text = loadingLine + new string('.', dots % 4);

            dots++;
            t += 0.18f;
            yield return new WaitForSecondsRealtime(0.18f);
        }

        // ── 7) 완료 ──
        if (protocolText != null)
        {
            protocolText.color = completeColor;
            protocolText.text = completeLine + "\n" + tracker.GetCounterProtocolLabel();
        }
        yield return new WaitForSecondsRealtime(completeHold);

        // ── 8) 페이드 아웃 ──
        yield return StartCoroutine(Fade(1f, 0f, fadeOutTime));

        if (panelRoot != null) panelRoot.SetActive(false);
        Time.timeScale = prevScale <= 0f ? 1f : prevScale;

        IsPlaying = false;
        Debug.Log($"[AnalysisUI] 연출 종료 → {style}");
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
