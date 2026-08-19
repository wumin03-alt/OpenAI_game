using UnityEngine;

/// <summary>분석에 기록하는 플레이어 행동 종류입니다.</summary>
public enum ActionType { Melee, Ranged, Parry, Dash, Jump }

/// <summary>보스가 대응할 플레이 스타일입니다.</summary>
public enum DominantStyle { None, Balanced, Melee, Ranged, Evasive, Airborne }

/// <summary>한 Phase 동안 모은 전투 데이터와 판정 결과입니다.</summary>
public struct CombatAnalysis
{
    public DominantStyle style;
    public int meleeUses;
    public int meleeHits;
    public int rangedUses;
    public int rangedHits;
    public int dashUses;
    public int jumpUses;
    public float averageDistance;
    public float closeRatio;
    public float farRatio;
    public float confidence;
}

/// <summary>
/// BossArena의 규칙 기반 적응 AI가 읽는 행동 데이터 소스입니다.
/// 입력과 실제 보스 피격을 분리해 Phase마다 독립된 분석 윈도우를 만듭니다.
/// </summary>
public class PlayerCombatTracker : MonoBehaviour
{
    public static PlayerCombatTracker Instance { get; private set; }

    [Header("Analysis Window")]
    [SerializeField] private bool isRecording = true;
    [SerializeField] private int minSampleCount = 4;
    [SerializeField] private float closeDistance = 3.5f;
    [SerializeField] private float farDistance = 6.5f;

    [Header("Debug")]
    [SerializeField] private bool debugReportKey = true;
    [SerializeField] private bool logEveryAction;

    public int MeleeCount { get; private set; }
    public int MeleeHitCount { get; private set; }
    public int RangedCount { get; private set; }
    public int RangedHitCount { get; private set; }
    public int ParryCount { get; private set; }
    public int ParrySuccessCount { get; private set; }
    public int DashCount { get; private set; }
    public int JumpCount { get; private set; }
    public bool IsRecording => isRecording;
    public int TotalAttackCount => MeleeCount + RangedCount;
    public bool HasEnoughSamples => TotalAttackCount >= minSampleCount;
    public DominantStyle LockedStyle { get; private set; } = DominantStyle.None;
    public CombatAnalysis LastAnalysis { get; private set; }

    private Health health;
    private Rigidbody2D body;
    private Transform boss;
    private float distanceTotal;
    private float distanceSampleTime;
    private float closeTime;
    private float farTime;
    private bool wasRising;

    private void Awake()
    {
        Instance = this;
        health = GetComponent<Health>();
        body = GetComponent<Rigidbody2D>();
        if (health != null && health.onParrySuccess != null)
            health.onParrySuccess.AddListener(RecordParrySuccess);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (isRecording)
        {
            RecordSpacing();
            RecordJumpFromMotion();
        }

#if UNITY_EDITOR
        if (debugReportKey && Input.GetKeyDown(KeyCode.G)) Debug.Log(BuildReport());
#endif
    }

    private void RecordSpacing()
    {
        if (boss == null)
        {
            GameObject bossObject = GameObject.FindGameObjectWithTag("Boss");
            if (bossObject != null) boss = bossObject.transform;
        }
        if (boss == null) return;

        float distance = Vector2.Distance(transform.position, boss.position);
        distanceTotal += distance * Time.deltaTime;
        distanceSampleTime += Time.deltaTime;
        if (distance <= closeDistance) closeTime += Time.deltaTime;
        if (distance >= farDistance) farTime += Time.deltaTime;
    }

    // PlayerController의 점프 입력을 건드리지 않고 실제 상승 시작을 한 번만 기록합니다.
    private void RecordJumpFromMotion()
    {
        bool rising = body != null && body.linearVelocity.y > 2.5f;
        if (rising && !wasRising) JumpCount++;
        wasRising = rising;
    }

    /// <summary>PlayerController가 유효한 행동을 실행했을 때 호출합니다.</summary>
    public void RecordAction(ActionType type)
    {
        if (!isRecording) return;

        switch (type)
        {
            case ActionType.Melee: MeleeCount++; break;
            case ActionType.Ranged: RangedCount++; break;
            case ActionType.Parry: ParryCount++; break;
            case ActionType.Dash: DashCount++; break;
            case ActionType.Jump: JumpCount++; break;
        }

        if (logEveryAction) Debug.Log($"[Tracker] {type} | {BuildReport()}");
    }

    /// <summary>DamageZone/Projectile이 실제 Boss Health에 적중했을 때 호출합니다.</summary>
    public void RecordBossHit(ActionType type)
    {
        if (!isRecording) return;
        if (type == ActionType.Melee) MeleeHitCount++;
        else if (type == ActionType.Ranged) RangedHitCount++;
    }

    private void RecordParrySuccess()
    {
        if (isRecording) ParrySuccessCount++;
    }

    /// <summary>현재 분석 윈도우를 확정하고 다음 Phase가 읽을 결과를 반환합니다.</summary>
    public CombatAnalysis FinishWindow()
    {
        isRecording = false;
        LastAnalysis = BuildAnalysis();
        LockedStyle = LastAnalysis.style;
        Debug.Log($"[Tracker] 분석 완료 → {LockedStyle}\n{BuildReport()}");
        return LastAnalysis;
    }

    /// <summary>새 Phase용 데이터를 0부터 다시 수집합니다.</summary>
    public void BeginNewWindow()
    {
        MeleeCount = MeleeHitCount = RangedCount = RangedHitCount = 0;
        ParryCount = ParrySuccessCount = DashCount = JumpCount = 0;
        distanceTotal = distanceSampleTime = closeTime = farTime = 0f;
        isRecording = true;
    }

    public void StartRecording() => isRecording = true;
    public void StopRecording() => isRecording = false;

    public void ResetAll()
    {
        LockedStyle = DominantStyle.None;
        LastAnalysis = new CombatAnalysis { style = DominantStyle.None };
        BeginNewWindow();
    }

    private CombatAnalysis BuildAnalysis()
    {
        float sampleTime = Mathf.Max(0.01f, distanceSampleTime);
        float averageDistance = distanceTotal / sampleTime;
        float closeRatio = closeTime / sampleTime;
        float farRatio = farTime / sampleTime;
        int actionSamples = TotalAttackCount + DashCount + JumpCount;

        float meleeScore = MeleeHitCount * 2.5f + MeleeCount * 0.8f + closeRatio * 4f;
        float rangedScore = RangedHitCount * 2.5f + RangedCount * 0.8f + farRatio * 4f + Mathf.Clamp01(averageDistance / farDistance);
        float evasiveScore = DashCount * 1.7f + (float)DashCount / Mathf.Max(1, TotalAttackCount) * 3f + farRatio * 1.5f;
        float airborneScore = JumpCount * 1.6f + (float)JumpCount / Mathf.Max(1, TotalAttackCount) * 3f;

        DominantStyle style = DominantStyle.Balanced;
        float bestScore = 0f;
        float secondScore = 0f;
        SelectBest(DominantStyle.Melee, meleeScore, ref style, ref bestScore, ref secondScore);
        SelectBest(DominantStyle.Ranged, rangedScore, ref style, ref bestScore, ref secondScore);
        SelectBest(DominantStyle.Evasive, evasiveScore, ref style, ref bestScore, ref secondScore);
        SelectBest(DominantStyle.Airborne, airborneScore, ref style, ref bestScore, ref secondScore);

        // 표본 부족 또는 비슷한 점수면 억지로 스타일을 뒤집지 않습니다.
        if (actionSamples < minSampleCount || bestScore < 3f || bestScore - secondScore < 1.15f)
            style = DominantStyle.Balanced;

        return new CombatAnalysis
        {
            style = style,
            meleeUses = MeleeCount,
            meleeHits = MeleeHitCount,
            rangedUses = RangedCount,
            rangedHits = RangedHitCount,
            dashUses = DashCount,
            jumpUses = JumpCount,
            averageDistance = averageDistance,
            closeRatio = closeRatio,
            farRatio = farRatio,
            confidence = bestScore <= 0f ? 0f : Mathf.Clamp01((bestScore - secondScore + 1f) / (bestScore + 1f))
        };
    }

    private static void SelectBest(DominantStyle candidate, float score, ref DominantStyle style, ref float best, ref float second)
    {
        if (score > best)
        {
            second = best;
            best = score;
            style = candidate;
        }
        else if (score > second)
        {
            second = score;
        }
    }

    public string GetStyleLabel(DominantStyle style)
    {
        switch (style)
        {
            case DominantStyle.Melee: return "MELEE";
            case DominantStyle.Ranged: return "RANGED";
            case DominantStyle.Evasive: return "EVASIVE";
            case DominantStyle.Airborne: return "AIRBORNE";
            case DominantStyle.Balanced: return "BALANCED";
            default: return "UNKNOWN";
        }
    }

    public string GetDistanceLabel(float distance)
    {
        if (distance >= farDistance) return "HIGH";
        if (distance <= closeDistance) return "LOW";
        return "MEDIUM";
    }

    public string BuildReport()
    {
        CombatAnalysis current = isRecording ? BuildAnalysis() : LastAnalysis;
        return $"[Tracker] Q {MeleeHitCount}/{MeleeCount}  W {RangedHitCount}/{RangedCount}  " +
               $"Dash:{DashCount} Jump:{JumpCount} AvgDist:{current.averageDistance:F1} " +
               $"→ {GetStyleLabel(current.style)}  기록중:{isRecording}";
    }
}
