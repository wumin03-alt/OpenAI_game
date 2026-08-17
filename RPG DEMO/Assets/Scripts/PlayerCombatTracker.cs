using UnityEngine;

/// <summary>플레이어 행동 종류</summary>
public enum ActionType { Melee, Ranged, Parry, Dash }

/// <summary>플레이어 주력 전투 스타일</summary>
public enum DominantStyle { None, Melee, Ranged }

/// <summary>
/// 학습형 보스의 데이터 소스.
/// 플레이어 행동을 실시간 카운트하고, Phase 2에서 주력 스타일을 판정합니다.
/// Player 오브젝트에 부착합니다.
/// </summary>
public class PlayerCombatTracker : MonoBehaviour
{
    public static PlayerCombatTracker Instance { get; private set; }

    [Header("── 기록 상태 ──")]
    [Tooltip("Phase 1 동안만 true. 보스가 50%에 도달하면 꺼집니다")]
    [SerializeField] private bool isRecording = true;

    [Header("── 판정 기준 ──")]
    [Tooltip("이 횟수 미만이면 표본 부족으로 봅니다 (그래도 판정은 내립니다)")]
    [SerializeField] private int minSampleCount = 4;

    [Header("── 디버그 ──")]
    [Tooltip("G 키로 현재 기록을 Console에 출력")]
    [SerializeField] private bool debugReportKey = true;
    [Tooltip("행동할 때마다 Console에 로그")]
    [SerializeField] private bool logEveryAction = false;

    // ── 핵심 카운터 (Phase 2 판정에 사용) ──
    public int MeleeCount { get; private set; }
    public int RangedCount { get; private set; }

    // ── 보조 카운터 (분석 UI 표시용) ──
    public int ParryCount { get; private set; }          // 패링 시도
    public int ParrySuccessCount { get; private set; }   // 패링 성공
    public int DashCount { get; private set; }

    public bool IsRecording => isRecording;
    public int TotalAttackCount => MeleeCount + RangedCount;
    public bool HasEnoughSamples => TotalAttackCount >= minSampleCount;

    /// <summary>확정된 주력 스타일. 판정 전에는 None</summary>
    public DominantStyle LockedStyle { get; private set; } = DominantStyle.None;

    private ActionType lastAttackType = ActionType.Ranged;   // 동점 타이브레이크용
    private bool hasAnyAttack;
    private Health health;

    private void Awake()
    {
        Instance = this;

        // 패링 성공을 자동 구독 (Inspector 연결 불필요)
        health = GetComponent<Health>();
        if (health != null && health.onParrySuccess != null)
            health.onParrySuccess.AddListener(RecordParrySuccess);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (debugReportKey && Input.GetKeyDown(KeyCode.G))
            Debug.Log(BuildReport());
    }

    // ───────────────────────── 기록 ─────────────────────────
    /// <summary>PlayerController에서 행동할 때마다 호출합니다.</summary>
    public void RecordAction(ActionType type)
    {
        if (!isRecording) return;

        switch (type)
        {
            case ActionType.Melee:
                MeleeCount++;
                lastAttackType = ActionType.Melee;
                hasAnyAttack = true;
                break;

            case ActionType.Ranged:
                RangedCount++;
                lastAttackType = ActionType.Ranged;
                hasAnyAttack = true;
                break;

            case ActionType.Parry:
                ParryCount++;
                break;

            case ActionType.Dash:
                DashCount++;
                break;
        }

        if (logEveryAction)
            Debug.Log($"[Tracker] {type}  |  M:{MeleeCount} R:{RangedCount} P:{ParryCount} D:{DashCount}");
    }

    private void RecordParrySuccess()
    {
        if (!isRecording) return;
        ParrySuccessCount++;
    }

    // ───────────────────────── 판정 ─────────────────────────
    /// <summary>
    /// 주력 스타일을 판정합니다. melee / ranged 만 사용합니다.
    /// parry / dash 는 분석 UI 표시용 보조 데이터입니다.
    /// (확장 시 이 함수 안에서만 규칙을 추가하면 됩니다)
    /// </summary>
    public DominantStyle GetDominantStyle()
    {
        if (!hasAnyAttack) return DominantStyle.None;

        if (MeleeCount > RangedCount) return DominantStyle.Melee;
        if (RangedCount > MeleeCount) return DominantStyle.Ranged;

        // 동점 — 마지막으로 사용한 공격 타입으로 결정
        return lastAttackType == ActionType.Melee
            ? DominantStyle.Melee
            : DominantStyle.Ranged;
    }

    /// <summary>기록을 멈추고 스타일을 확정합니다. 보스가 HP 50%에서 호출합니다.</summary>
    public DominantStyle LockAndStopRecording()
    {
        isRecording = false;
        LockedStyle = GetDominantStyle();

        Debug.Log($"[Tracker] 분석 완료 → {LockedStyle}\n{BuildReport()}");
        return LockedStyle;
    }

    public void StartRecording() => isRecording = true;
    public void StopRecording() => isRecording = false;

    /// <summary>재시작 시 초기화</summary>
    public void ResetAll()
    {
        MeleeCount = RangedCount = ParryCount = ParrySuccessCount = DashCount = 0;
        hasAnyAttack = false;
        LockedStyle = DominantStyle.None;
        isRecording = true;
    }

    // ───────────────── 분석 UI가 사용할 표시용 문자열 ─────────────────
    /// <summary>"CLOSE RANGE" / "LONG RANGE" — 10단계 AnalysisUI에서 사용</summary>
    public string GetStyleLabel()
    {
        switch (GetDominantStyle())
        {
            case DominantStyle.Melee: return "CLOSE RANGE";
            case DominantStyle.Ranged: return "LONG RANGE";
            default: return "UNKNOWN";
        }
    }

    /// <summary>주력 스타일이 전체 공격에서 차지하는 비율 (0~1)</summary>
    public float GetDominantRatio()
    {
        if (TotalAttackCount == 0) return 0f;
        int dominant = Mathf.Max(MeleeCount, RangedCount);
        return (float)dominant / TotalAttackCount;
    }

    public string BuildReport()
    {
        return $"[Tracker] MELEE:{MeleeCount}  RANGED:{RangedCount}  " +
               $"PARRY:{ParryCount}(성공 {ParrySuccessCount})  DASH:{DashCount}  " +
               $"→ {GetDominantStyle()} ({GetDominantRatio() * 100f:F0}%)  " +
               $"기록중:{isRecording}  표본충분:{HasEnoughSamples}";
    }
}