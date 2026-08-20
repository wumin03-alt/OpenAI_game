using System;
using UnityEngine;

public enum ActionType { Melee, Ranged, Parry, Dash }
public enum DominantStyle { None, Melee, Ranged }
public enum MobilityStyle { None, Grounded, Airborne }
public enum EvasionBias { Balanced, Left, Right }

/// <summary>
/// 학습형 보스의 데이터 소스. 공격 선택, 공중 체류, 이동 방향과 패링을 관찰합니다.
/// 성공한 패링은 데이터를 오염시켜 Phase 2 카운터의 정확도를 낮춥니다.
/// </summary>
public class PlayerCombatTracker : MonoBehaviour
{
    public static PlayerCombatTracker Instance { get; private set; }

    [Header("── 기록 상태 ──")]
    [SerializeField] private bool isRecording = true;

    [Header("── 판정 기준 ──")]
    [SerializeField] private int minSampleCount = 4;
    [SerializeField] private float movementSampleInterval = 0.1f;
    [Tooltip("성공한 패링 1회가 학습 데이터에 주는 오염도")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float corruptionPerParry = 0.18f;

    [Header("── 디버그 ──")]
    [SerializeField] private bool debugReportKey = true;
    [SerializeField] private bool logEveryAction = false;

    public int MeleeCount { get; private set; }
    public int RangedCount { get; private set; }
    public int ParryCount { get; private set; }
    public int ParrySuccessCount { get; private set; }
    public int DashCount { get; private set; }
    public int GroundedSamples { get; private set; }
    public int AirborneSamples { get; private set; }
    public int LeftMovementSamples { get; private set; }
    public int RightMovementSamples { get; private set; }

    public bool IsRecording => isRecording;
    public int TotalAttackCount => MeleeCount + RangedCount;
    public bool HasEnoughSamples => TotalAttackCount >= minSampleCount;
    public float DataCorruption { get; private set; }
    public float DataIntegrity => 1f - DataCorruption;
    public float ParrySuccessRate => ParryCount <= 0 ? 0f : (float)ParrySuccessCount / ParryCount;

    public DominantStyle ObservedStyle { get; private set; } = DominantStyle.None;
    public DominantStyle LockedStyle { get; private set; } = DominantStyle.None;
    public MobilityStyle LockedMobility { get; private set; } = MobilityStyle.None;
    public EvasionBias LockedEvasion { get; private set; } = EvasionBias.Balanced;

    public event Action ParrySucceeded;

    private ActionType lastAttackType = ActionType.Ranged;
    private bool hasAnyAttack;
    private Health health;
    private PlayerController controller;
    private float sampleTimer;
    private float lastSampleX;

    private void Awake()
    {
        Instance = this;
        controller = GetComponent<PlayerController>();
        health = GetComponent<Health>();
        lastSampleX = transform.position.x;

        if (health != null && health.onParrySuccess != null)
            health.onParrySuccess.AddListener(RecordParrySuccess);
    }

    private void OnDestroy()
    {
        if (health != null && health.onParrySuccess != null)
            health.onParrySuccess.RemoveListener(RecordParrySuccess);
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (isRecording) SampleMovement();
        if (debugReportKey && Input.GetKeyDown(KeyCode.G)) Debug.Log(BuildReport());
    }

    private void SampleMovement()
    {
        sampleTimer += Time.deltaTime;
        if (sampleTimer < movementSampleInterval) return;
        sampleTimer = 0f;

        if (controller != null && controller.IsGrounded) GroundedSamples++;
        else AirborneSamples++;

        float delta = transform.position.x - lastSampleX;
        if (delta < -0.025f) LeftMovementSamples++;
        else if (delta > 0.025f) RightMovementSamples++;
        lastSampleX = transform.position.x;
    }

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
                if (controller != null && controller.Facing < 0) LeftMovementSamples += 3;
                else RightMovementSamples += 3;
                break;
        }

        if (logEveryAction)
            Debug.Log($"[Tracker] {type} | M:{MeleeCount} R:{RangedCount} P:{ParryCount} D:{DashCount}");
    }

    private void RecordParrySuccess()
    {
        // Phase 2에서도 패링 성공 이벤트는 보스 경직/교란에 사용합니다.
        ParrySuccessCount++;
        DataCorruption = Mathf.Clamp01(DataCorruption + corruptionPerParry);
        ParrySucceeded?.Invoke();
        Debug.Log($"[Tracker] FALSE DATA INJECTED | integrity {DataIntegrity * 100f:F0}%");
    }

    public DominantStyle GetDominantStyle()
    {
        if (!hasAnyAttack) return DominantStyle.None;
        if (MeleeCount > RangedCount) return DominantStyle.Melee;
        if (RangedCount > MeleeCount) return DominantStyle.Ranged;
        return lastAttackType == ActionType.Melee ? DominantStyle.Melee : DominantStyle.Ranged;
    }

    public MobilityStyle GetMobilityStyle()
    {
        if (GroundedSamples + AirborneSamples <= 0) return MobilityStyle.None;
        return AirborneSamples > GroundedSamples * 0.45f ? MobilityStyle.Airborne : MobilityStyle.Grounded;
    }

    public EvasionBias GetEvasionBias()
    {
        int movement = LeftMovementSamples + RightMovementSamples;
        if (movement < 5) return EvasionBias.Balanced;
        float rightRatio = (float)RightMovementSamples / movement;
        if (rightRatio > 0.62f) return EvasionBias.Right;
        if (rightRatio < 0.38f) return EvasionBias.Left;
        return EvasionBias.Balanced;
    }

    /// <summary>오염도가 55% 이상이면 보스가 실제 성향과 반대로 학습합니다.</summary>
    public DominantStyle LockAndStopRecording()
    {
        if (!isRecording && LockedStyle != DominantStyle.None) return LockedStyle;

        isRecording = false;
        ObservedStyle = GetDominantStyle();
        LockedStyle = ObservedStyle;
        LockedMobility = GetMobilityStyle();
        LockedEvasion = GetEvasionBias();

        if (DataCorruption >= 0.55f)
        {
            if (ObservedStyle == DominantStyle.Melee) LockedStyle = DominantStyle.Ranged;
            else if (ObservedStyle == DominantStyle.Ranged) LockedStyle = DominantStyle.Melee;
        }

        Debug.Log($"[Tracker] 분석 완료 → observed:{ObservedStyle}, learned:{LockedStyle}\n{BuildReport()}");
        return LockedStyle;
    }

    public void StartRecording()
    {
        isRecording = true;
        lastSampleX = transform.position.x;
    }

    public void StopRecording() => isRecording = false;

    public void ResetAll()
    {
        MeleeCount = RangedCount = ParryCount = ParrySuccessCount = DashCount = 0;
        GroundedSamples = AirborneSamples = LeftMovementSamples = RightMovementSamples = 0;
        DataCorruption = 0f;
        sampleTimer = 0f;
        hasAnyAttack = false;
        ObservedStyle = DominantStyle.None;
        LockedStyle = DominantStyle.None;
        LockedMobility = MobilityStyle.None;
        LockedEvasion = EvasionBias.Balanced;
        isRecording = true;
        lastSampleX = transform.position.x;
    }

    public string GetStyleLabel()
    {
        DominantStyle style = isRecording ? GetDominantStyle() : LockedStyle;
        switch (style)
        {
            case DominantStyle.Melee: return "근접 전투";
            case DominantStyle.Ranged: return "원거리 전투";
            default: return "분석 중";
        }
    }

    public string GetMobilityLabel()
    {
        MobilityStyle style = isRecording ? GetMobilityStyle() : LockedMobility;
        return style == MobilityStyle.Airborne ? "공중 기동" : style == MobilityStyle.Grounded ? "지상 기동" : "분석 중";
    }

    public string GetEvasionLabel()
    {
        EvasionBias bias = isRecording ? GetEvasionBias() : LockedEvasion;
        return bias == EvasionBias.Left ? "좌측 편향" : bias == EvasionBias.Right ? "우측 편향" : "균형 회피";
    }

    public string GetCounterProtocolLabel()
    {
        string attackCounter = LockedStyle == DominantStyle.Melee ? "접근 거부" :
            LockedStyle == DominantStyle.Ranged ? "고속 추격" : "표준 대응";
        string mobilityCounter = LockedMobility == MobilityStyle.Airborne ? "대공 예측" : "지면 예측";
        return $"{attackCounter} · {mobilityCounter}";
    }

    public float GetDominantRatio()
    {
        if (TotalAttackCount == 0) return 0f;
        return (float)Mathf.Max(MeleeCount, RangedCount) / TotalAttackCount;
    }

    public string BuildReport()
    {
        return $"[Tracker] MELEE:{MeleeCount} RANGED:{RangedCount} " +
               $"PARRY:{ParryCount}(success {ParrySuccessCount}) DASH:{DashCount} " +
               $"AIR:{AirborneSamples} GROUND:{GroundedSamples} MOVE L:{LeftMovementSamples} R:{RightMovementSamples} " +
               $"INTEGRITY:{DataIntegrity * 100f:F0}% → {GetDominantStyle()} / {GetMobilityStyle()} / {GetEvasionBias()}";
    }
}
