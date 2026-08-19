using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 공중 호버형 거대 보스. 화면 상단에서 아래(플레이어)를 공격합니다.
/// 8단계 + 저공 흐름 보완:
///   - Descend 완료 시점부터 저공 타이머 시작
///   - Descend 직후 minLowHoverAttacks 회 전에는 Ascend 선택 불가 (불변식)
///   - 저공에서 TailSweep 가중치 증폭
/// 11단계에서는 ApplyPhaseWeights()의 테이블만 교체하면 적응이 완성됩니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class BossController : MonoBehaviour
{
    public enum BossAttack
    {
        PlasmaBreath,   // Core : 상공에서 하방 부채꼴
        TailSweep,      // Core : 저공에서 지면 휩쓸기 (강한 넉백)
        Ascend,         // Core : 상공 복귀 + 먼 슬롯 이탈
        Descend,        // Core : 저공 강하 (근접 기회 제공)
        ClawSlam,       // 확장
        SkyDive,        // 확장
        Barrage         // 확장
    }

    public enum BossState { Idle, Moving, Windup, Attack, Recover, Staggered, Dead }

    [Header("── 비주얼 ──")]
    [SerializeField] private Transform visual;
    [SerializeField] private bool flipTowardPlayer = true;

    [Header("── 호버 위치 ──")]
    [SerializeField] private float hoverHighY = 3f;
    [SerializeField] private float hoverLowY = -1.5f;
    [SerializeField] private float[] slotX = { -7f, 0f, 7f };
    [SerializeField] private float horizontalSpeed = 4.5f;
    [SerializeField] private float verticalSpeed = 3.5f;

    // ★ 수정 ① : 저공 흐름 제어
    [Tooltip("저공에 이 시간 이상 머무르면 강제로 상승합니다 (Descend 완료 후부터 측정)")]
    [SerializeField] private float maxLowHoverTime = 12f;
    [Tooltip("Descend 후 이 횟수만큼 저공 공격을 하기 전에는 Ascend를 선택하지 않습니다")]
    [SerializeField] private int minLowHoverAttacks = 2;
    [Tooltip("저공에서 TailSweep 가중치에 곱할 배율")]
    [SerializeField] private float lowHoverTailBonus = 2f;

    [Header("── 부유 연출 ──")]
    [SerializeField] private float bobAmplitude = 0.3f;
    [SerializeField] private float bobSpeed = 1.4f;

    [Header("── 아레나 ──")]
    [SerializeField] private float arenaMinX = -9f;
    [SerializeField] private float arenaMaxX = 9f;
    [Tooltip("지면 표면의 Y 좌표. 예고 표시 / 꼬리 높이 계산에 사용")]
    [SerializeField] private float groundSurfaceY = -5f;

    [Header("── 행동 타이밍 ──")]
    [SerializeField] private float attackInterval = 1.8f;
    [SerializeField] private float telegraphTime = 0.85f;
    [SerializeField] private float recoverTime = 1.2f;
    [SerializeField] private float startDelay = 1.5f;
    [Range(0f, 1f)]
    [SerializeField] private float repositionChance = 0.45f;
    [SerializeField] private float chargeSpeedMultiplier = 2.8f;

    [Header("── 예고 연출 ──")]
    [SerializeField] private GameObject telegraphMark;
    [SerializeField] private GameObject groundWarning;
    [SerializeField] private float warningPadding = 1.2f;
    [SerializeField] private float warningHeight = 0.35f;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.35f, 0.35f);
    [SerializeField] private float telegraphBlinkInterval = 0.1f;

    [Header("── PlasmaBreath ──")]
    [SerializeField] private GameObject breathPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private int breathShots = 3;
    [SerializeField] private float breathSpreadAngle = 24f;
    [SerializeField] private float breathShotInterval = 0.05f;
    [SerializeField] private float aimVerticalOffset = 0.5f;
    [Range(-1f, 0f)]
    [SerializeField] private float minDownwardY = -0.35f;

    [Header("── TailSweep (저공 전용) ──")]
    [Tooltip("Boss 자식의 TailHitbox 오브젝트")]
    [SerializeField] private GameObject tailHitbox;
    [Tooltip("휩쓰는 좌우 폭")]
    [SerializeField] private float tailWidth = 9f;
    [Tooltip("판정이 켜져 있는 시간")]
    [SerializeField] private float tailActiveTime = 0.35f;
    [Tooltip("꼬리 판정의 지면 위 높이. 점프로 넘을 수 있어야 함")]
    [SerializeField] private float tailHeightAboveGround = 0.7f;

    [Header("── Descend / Ascend ──")]
    [Tooltip("강하/상승 완료를 기다리는 최대 시간")]
    [SerializeField] private float verticalMoveTimeout = 2.5f;
    [Tooltip("Ascend의 짧은 예고 시간")]
    [SerializeField] private float ascendWindup = 0.35f;
    [Tooltip("Descend 후 근접 기회를 주기 위한 추가 경직 시간")]
    [SerializeField] private float descendSettleTime = 0.8f;

    [Header("── 확장 패턴 ──")]
    [SerializeField] private float groundAoEWidth = 4.5f;
    [SerializeField] private float groundAoEActiveTime = 0.2f;
    [SerializeField] private int barrageShots = 5;
    [SerializeField] private float barrageShotInterval = 0.18f;

    [Header("── 경직 (패링 성공 시) ──")]
    [SerializeField] private Color staggerColor = new Color(0.6f, 0.8f, 1f);

    // ★ 10단계 ① : 페이즈 전환
    [Header("── 페이즈 전환 ──")]
    [Tooltip("씬의 AnalysisPanel에 붙인 AnalysisUI")]
    [SerializeField] private AnalysisUI analysisUI;
    [Tooltip("이 비율 이하로 내려가면 첫 분석 후 Phase 2에 진입합니다 (기본 0.7 = 70%)")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float phase2Threshold = 0.7f;
    [Tooltip("이 비율 이하에서 Phase 3 재분석을 실행합니다")]
    [Range(0.1f, 0.8f)]
    [SerializeField] private float phase3Threshold = 0.4f;
    [Tooltip("Phase 2 진입 시 화면에 표시할 라벨 (선택)")]
    [SerializeField] private TMPro.TMP_Text phaseLabel;

    // ══════════════════════════════════════════════════
    // ▼▼▼ DEBUG : 테스트 전용. 완료 후 삭제 가능 ▼▼▼
    [Header("── ⚠ 디버그 (테스트 후 해제) ──")]
    [Tooltip("T = 저공 / Y = 상공 / U = 다음 슬롯 / I = 플레이어 위로")]
    [SerializeField] private bool debugHoverKeys = false;
    [Tooltip("체크하면 다음 공격을 forcedAttack으로 고정합니다")]
    [SerializeField] private bool debugForceAttack = false;
    [SerializeField] private BossAttack forcedAttack = BossAttack.TailSweep;
    // ▲▲▲ DEBUG ▲▲▲
    // ══════════════════════════════════════════════════

    // ── 외부에서 읽는 값 ──
    public BossState State { get; private set; } = BossState.Idle;
    public int Phase { get; private set; } = 1;
    public bool IsLowHovering => baseY < (hoverHighY + hoverLowY) * 0.5f;
    public float HorizontalDistanceToPlayer =>
        player == null ? 999f : Mathf.Abs(player.position.x - transform.position.x);

    private Rigidbody2D rb;
    private Health health;
    private Transform player;
    private SpriteRenderer visualSr;
    private Color visualBaseColor;

    private float targetX;
    private float targetY;
    private float baseY;
    private float bobPhase;
    private int currentSlot = 1;
    private int facing = -1;
    private float visualScaleX = 1f;
    private float staggerLeft;
    private float lowHoverTime;
    private float movementSpeedMultiplier = 1f;
    private float strategyMovementSpeedMultiplier = 1f;
    private Vector3 tailHitboxBaseScale = Vector3.one;
    private Coroutine aiRoutine;
    private Coroutine attackRoutine;       // ★ ExecuteAttack
    private Coroutine subRoutine;          // ★ 개별 공격 (Atk_*)
    private Coroutine transitionRoutine;   // ★ 페이즈 전환

    // ★ 수정 ② : 저공 세션 상태
    private int lowHoverAttackCount;
    private bool lowHoverTimerActive;

    // ★ 10단계 ② : 페이즈 전환이 1회만 실행되게 하는 플래그
    private bool phase2Triggered;
    private bool phase3Triggered;
    private bool phaseTransitionRunning;

    // ★ 11단계 : 적응 상태
    private DominantStyle adaptedStyle = DominantStyle.None;
    private float lowTailBonusCurrent;      // 런타임에 조절되는 TailSweep 배율
    private float repositionChanceCurrent;  // 런타임에 조절되는 재배치 확률

    // 패턴 가중치 — 11단계에서 이 테이블만 교체합니다
    private readonly Dictionary<BossAttack, int> weights = new Dictionary<BossAttack, int>();
    private readonly List<BossAttack> candidates = new List<BossAttack>();
    private readonly List<int> weightBuf = new List<int>();   // ★ 수정 ⑤

    // ───────────────────────── 초기화 ─────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();

        if (visual == null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) visual = sr.transform;
        }
        if (visual != null)
        {
            visualScaleX = Mathf.Abs(visual.localScale.x);
            visualSr = visual.GetComponent<SpriteRenderer>();
            if (visualSr != null) visualBaseColor = visualSr.color;
        }

        if (telegraphMark != null) telegraphMark.SetActive(false);
        if (groundWarning != null) groundWarning.SetActive(false);
        if (tailHitbox != null) tailHitbox.SetActive(false);
        if (tailHitbox != null) tailHitboxBaseScale = tailHitbox.transform.localScale;

        health.onDeath.AddListener(OnBossDeath);
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null) player = p.transform;

        currentSlot = Mathf.Clamp(slotX.Length / 2, 0, Mathf.Max(0, slotX.Length - 1));
        targetX = slotX.Length > 0 ? slotX[currentSlot] : transform.position.x;
        targetY = hoverHighY;
        baseY = hoverHighY;
        transform.position = new Vector3(targetX, baseY, transform.position.z);

        lowTailBonusCurrent = lowHoverTailBonus;
        repositionChanceCurrent = repositionChance;

        ApplyPhaseWeights();

        aiRoutine = StartCoroutine(AIRoutine());
    }

    private void Update()
    {
        if (State == BossState.Dead) return;

        if (staggerLeft > 0f)
        {
            staggerLeft -= Time.deltaTime;
            if (staggerLeft <= 0f) SetVisualColor(visualBaseColor);
        }

        // ★ 수정 ③ : Descend가 완전히 끝난 뒤부터만 저공 시간 측정
        if (lowHoverTimerActive && IsLowHovering)
        {
            lowHoverTime += Time.deltaTime;
        }
        else if (!IsLowHovering)
        {
            lowHoverTime = 0f;
            lowHoverTimerActive = false;
        }

        UpdateHoverPosition();
        UpdateFacing();

        CheckPhaseTransition();     // ★ 10단계 ③

#if UNITY_EDITOR
        if (debugHoverKeys) DebugHoverInput();
        DebugStyleInput();
#endif
    }

    private void UpdateHoverPosition()
    {
        float hSpeed = staggerLeft > 0f ? 0f : horizontalSpeed * movementSpeedMultiplier;
        float vSpeed = staggerLeft > 0f ? 0f : verticalSpeed;

        float x = Mathf.MoveTowards(transform.position.x, targetX, hSpeed * Time.deltaTime);
        x = Mathf.Clamp(x, arenaMinX, arenaMaxX);

        baseY = Mathf.MoveTowards(baseY, targetY, vSpeed * Time.deltaTime);

        bobPhase += Time.deltaTime * bobSpeed;
        float bob = Mathf.Sin(bobPhase) * bobAmplitude;

        transform.position = new Vector3(x, baseY + bob, transform.position.z);
    }

    private bool ArrivedAtTargetX => Mathf.Abs(transform.position.x - targetX) < 0.15f;
    private bool ArrivedAtTargetY => Mathf.Abs(baseY - targetY) < 0.1f;

    // ───────────────────── 가중치 테이블 ─────────────────────
    /// <summary>
    /// Phase와 DominantStyle에 따라 패턴 가중치를 설정합니다.
    /// 어떤 공격도 0이 되지 않게 유지합니다 (플레이 방식 완전 봉쇄 금지).
    /// </summary>
    private void ApplyPhaseWeights()
    {
        weights.Clear();
        lowTailBonusCurrent = lowHoverTailBonus;
        repositionChanceCurrent = repositionChance;
        strategyMovementSpeedMultiplier = 1f;
        movementSpeedMultiplier = strategyMovementSpeedMultiplier;

        if (Phase <= 1)
        {
            SetWeights(18, 14, 12, 8, 14, 12, 14);
            Debug.Log("[Boss] Phase 1 가중치 (분석 중 · 균형 패턴)");
            return;
        }

        switch (adaptedStyle)
        {
            case DominantStyle.Melee:
                // 붙어서 때리는 플레이: 근접 휩쓸기, 장판, 이탈을 늘립니다.
                SetWeights(14, 24, 8, 18, 24, 5, 12);
                lowTailBonusCurrent = 2.2f;
                repositionChanceCurrent = 0.78f;
                Debug.Log("[Boss] Counter → MELEE (이탈·근접 장판 중심)");
                break;

            case DominantStyle.Ranged:
                // 멀리서 W를 쓰면 차지/강하로 안전 거리를 줄입니다.
                SetWeights(8, 10, 12, 6, 16, 30, 10);
                lowTailBonusCurrent = 1.15f;
                repositionChanceCurrent = 0.7f;
                strategyMovementSpeedMultiplier = 1.25f;
                movementSpeedMultiplier = strategyMovementSpeedMultiplier;
                Debug.Log("[Boss] Counter → RANGED (추격·Charge 중심)");
                break;

            case DominantStyle.Evasive:
                // Dash 회피 습관: 지연 장판과 지속 탄막으로 타이밍을 섞습니다.
                SetWeights(16, 10, 8, 10, 28, 8, 16);
                repositionChanceCurrent = 0.58f;
                Debug.Log("[Boss] Counter → EVASIVE (Delayed Strike·Wide Attack 중심)");
                break;

            case DominantStyle.Airborne:
                // 자주 뛰는 플레이: 상단을 넓게 덮는 브레스/탄막을 선호합니다.
                SetWeights(24, 8, 8, 10, 12, 8, 22);
                repositionChanceCurrent = 0.62f;
                Debug.Log("[Boss] Counter → AIRBORNE (상공 견제·탄막 중심)");
                break;

            default:
                SetWeights(16, 14, 12, 10, 14, 12, 16);
                Debug.Log("[Boss] Counter → BALANCED (혼합 전략)");
                break;
        }
    }

    private void SetWeights(int breath, int tail, int descend, int ascend, int groundAoE, int charge, int barrage)
    {
        weights[BossAttack.PlasmaBreath] = breath;
        weights[BossAttack.TailSweep] = tail;
        weights[BossAttack.Descend] = descend;
        weights[BossAttack.Ascend] = ascend;
        weights[BossAttack.ClawSlam] = groundAoE;
        weights[BossAttack.SkyDive] = charge;
        weights[BossAttack.Barrage] = barrage;
    }

    /// <summary>현재 위치 상태에서 사용 가능한 공격인지</summary>
    private bool IsUsable(BossAttack a)
    {
        switch (a)
        {
            case BossAttack.PlasmaBreath: return true;
            case BossAttack.Descend: return !IsLowHovering;
            case BossAttack.TailSweep: return IsLowHovering;
            case BossAttack.Ascend: return IsLowHovering;
            case BossAttack.ClawSlam: return true;
            case BossAttack.SkyDive: return !IsLowHovering;
            case BossAttack.Barrage: return true;
            default: return false;
        }
    }

    // ★ 수정 ⑤ : 저공 흐름 제어가 반영된 패턴 선택
    private BossAttack ChoosePattern()
    {
        // 저공 체류 초과 — 단, 최소 1회는 공격하고 올라감
        if (IsLowHovering && lowHoverTime > maxLowHoverTime && lowHoverAttackCount >= 1)
        {
            Debug.Log($"[Boss] 저공 체류 {lowHoverTime:F1}s 초과 → 강제 Ascend");
            return BossAttack.Ascend;
        }

        candidates.Clear();
        weightBuf.Clear();
        int total = 0;

        foreach (var kv in weights)
        {
            if (kv.Value <= 0) continue;
            if (!IsUsable(kv.Key)) continue;

            // Descend 직후에는 Ascend 금지 (확률이 아닌 불변식)
            if (kv.Key == BossAttack.Ascend && lowHoverAttackCount < minLowHoverAttacks)
                continue;

            // 저공에서는 TailSweep 가중치 증폭 (적응에 따라 배율이 달라짐)
            int w = kv.Value;
            if (IsLowHovering && kv.Key == BossAttack.TailSweep)
                w = Mathf.Max(1, Mathf.RoundToInt(w * lowTailBonusCurrent));

            candidates.Add(kv.Key);
            weightBuf.Add(w);
            total += w;
        }

        if (candidates.Count == 0 || total <= 0)
            return IsLowHovering ? BossAttack.TailSweep : BossAttack.PlasmaBreath;

        int roll = Random.Range(0, total);
        for (int i = 0; i < candidates.Count; i++)
        {
            roll -= weightBuf[i];
            if (roll < 0) return candidates[i];
        }
        return candidates[candidates.Count - 1];
    }

    // ───────────────────────── AI 루프 ─────────────────────────
    private IEnumerator AIRoutine()
    {
        yield return new WaitForSeconds(startDelay);

        while (State != BossState.Dead)
        {
            while (staggerLeft > 0f) yield return null;

            State = BossState.Idle;
            yield return new WaitForSeconds(attackInterval);

            if (State == BossState.Dead) yield break;
            if (staggerLeft > 0f) continue;

            // 상공에 있을 때만 가끔 슬롯 이동
            if (!IsLowHovering && slotX.Length > 1 && Random.value < repositionChanceCurrent)
                yield return StartCoroutine(RepositionRoutine());

            if (staggerLeft > 0f) continue;

            BossAttack next = debugForceAttack ? forcedAttack : ChoosePattern();
            attackRoutine = StartCoroutine(ExecuteAttack(next));
            yield return attackRoutine;
            attackRoutine = null;
        }
    }

    private IEnumerator RepositionRoutine()
    {
        State = BossState.Moving;

        // ★ 11단계 : 적응 스타일에 따라 이동 방향에 의도를 부여
        if (Phase >= 2 && adaptedStyle == DominantStyle.Melee)
        {
            MoveToFarthestSlot();       // 근접 플레이어에게서 멀어짐
        }
        else if (Phase >= 2 && adaptedStyle == DominantStyle.Ranged)
        {
            MoveToNearestSlot();        // 원거리 플레이어에게 접근
        }
        else if (Phase >= 2 && adaptedStyle == DominantStyle.Evasive)
        {
            MoveToPlayerX();            // Dash 직후 위치를 다시 압박
        }
        else if (Phase >= 2 && adaptedStyle == DominantStyle.Airborne)
        {
            GoHigh();
            MoveToNearestSlot();        // 상공에서도 플레이어의 수평 위치를 덮음
        }
        else
        {
            int next = currentSlot;
            while (next == currentSlot) next = Random.Range(0, slotX.Length);
            MoveToSlot(next);
        }

        float timeout = 3f;
        while (!ArrivedAtTargetX && timeout > 0f && staggerLeft <= 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    // ★ 수정 ⑥ : 저공 공격 카운터 갱신
    private IEnumerator ExecuteAttack(BossAttack attack)
    {
        if (debugHoverKeys) CountUse(attack);   // ★ 통계 (적응 검증용)
        Debug.Log($"[Boss] ATTACK → {attack}   (저공: {IsLowHovering}, 저공공격수: {lowHoverAttackCount})");

        switch (attack)
        {
            case BossAttack.PlasmaBreath:
                subRoutine = StartCoroutine(Atk_PlasmaBreath());
                yield return subRoutine;
                subRoutine = null;
                if (IsLowHovering) lowHoverAttackCount++;   // 저공일 때만 카운트
                break;

            case BossAttack.TailSweep:
                subRoutine = StartCoroutine(Atk_TailSweep());
                yield return subRoutine;
                subRoutine = null;
                lowHoverAttackCount++;
                break;

            case BossAttack.Descend:
                subRoutine = StartCoroutine(Atk_Descend());
                yield return subRoutine;
                subRoutine = null;
                break;

            case BossAttack.Ascend:
                subRoutine = StartCoroutine(Atk_Ascend());
                yield return subRoutine;
                subRoutine = null;
                break;

            case BossAttack.ClawSlam:
                subRoutine = StartCoroutine(Atk_ClawSlam());
                yield return subRoutine;
                subRoutine = null;
                if (IsLowHovering) lowHoverAttackCount++;
                break;

            case BossAttack.SkyDive:
                subRoutine = StartCoroutine(Atk_SkyDive());
                yield return subRoutine;
                subRoutine = null;
                lowHoverAttackCount++;
                break;

            case BossAttack.Barrage:
                subRoutine = StartCoroutine(Atk_Barrage());
                yield return subRoutine;
                subRoutine = null;
                if (IsLowHovering) lowHoverAttackCount++;
                break;

            default:
                Debug.LogWarning($"[Boss] {attack} 는 아직 구현되지 않았습니다.");
                yield return new WaitForSeconds(0.5f);
                break;
        }

        State = BossState.Recover;
        yield return new WaitForSeconds(recoverTime);
    }

    // ───────────────── 공격 1 : PlasmaBreath ─────────────────
    private IEnumerator Atk_PlasmaBreath()
    {
        Vector2 aim = ComputeAimDirection();
        Vector2[] dirs = BuildFan(aim, breathShots, breathSpreadAngle);

        ShowGroundWarningForFan(dirs);

        bool ok = true;
        yield return StartCoroutine(Telegraph(telegraphTime, r => ok = r));

        HideGroundWarning();
        if (!ok) yield break;

        State = BossState.Attack;

        for (int i = 0; i < dirs.Length; i++)
        {
            if (staggerLeft > 0f) break;

            if (breathPrefab != null && firePoint != null)
            {
                GameObject go = Instantiate(breathPrefab, firePoint.position, Quaternion.identity);
                Projectile p = go.GetComponent<Projectile>();
                if (p != null) p.Launch(dirs[i]);
            }

            if (breathShotInterval > 0f)
                yield return new WaitForSeconds(breathShotInterval);
        }
    }

    // ───────────────── 공격 2 : TailSweep (근거리 카운터) ─────────────────
    private IEnumerator Atk_TailSweep()
    {
        ShowGroundWarningAt(transform.position.x, tailWidth + warningPadding);

        bool ok = true;
        yield return StartCoroutine(Telegraph(telegraphTime, r => ok = r));

        HideGroundWarning();
        if (!ok) yield break;

        State = BossState.Attack;

        if (tailHitbox != null)
        {
            PlaceTailHitbox();
            tailHitbox.SetActive(true);

            float t = 0f;
            while (t < tailActiveTime)
            {
                t += Time.deltaTime;
                PlaceTailHitbox();   // 부유로 흔들려도 지면에 고정
                yield return null;
            }

            tailHitbox.SetActive(false);
        }
        else
        {
            yield return new WaitForSeconds(tailActiveTime);
        }
    }

    private void PlaceTailHitbox()
    {
        PlaceTailHitboxAt(transform.position.x, tailWidth);
    }

    private void PlaceTailHitboxAt(float centerX, float width)
    {
        if (tailHitbox == null) return;
        tailHitbox.transform.position = new Vector3(centerX, groundSurfaceY + tailHeightAboveGround, 0f);
        Vector3 scale = tailHitboxBaseScale;
        scale.x *= Mathf.Max(0.25f, width / Mathf.Max(0.1f, tailWidth));
        tailHitbox.transform.localScale = scale;
    }

    private IEnumerator ActivateGroundHitbox(float centerX, float width, float duration)
    {
        if (tailHitbox == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        PlaceTailHitboxAt(centerX, width);
        tailHitbox.SetActive(true);
        yield return new WaitForSeconds(duration);
        tailHitbox.SetActive(false);
        tailHitbox.transform.localScale = tailHitboxBaseScale;
    }

    // 지연된 플레이어 위치에 작은 장판을 찍는 공격입니다. Dash 습관을 견제합니다.
    private IEnumerator Atk_ClawSlam()
    {
        float strikeX = player == null ? transform.position.x : Mathf.Clamp(player.position.x, arenaMinX, arenaMaxX);
        ShowGroundWarningAt(strikeX, groundAoEWidth + warningPadding);

        bool ok = true;
        yield return StartCoroutine(Telegraph(telegraphTime + 0.2f, r => ok = r));
        HideGroundWarning();
        if (!ok) yield break;

        State = BossState.Attack;
        yield return StartCoroutine(ActivateGroundHitbox(strikeX, groundAoEWidth, groundAoEActiveTime));
    }

    // 원거리 플레이어를 향해 빠르게 저공으로 파고드는 Charge입니다.
    private IEnumerator Atk_SkyDive()
    {
        float chargeX = player == null ? transform.position.x : Mathf.Clamp(player.position.x, arenaMinX, arenaMaxX);
        ShowGroundWarningAt(chargeX, groundAoEWidth + warningPadding);

        bool ok = true;
        yield return StartCoroutine(Telegraph(telegraphTime, r => ok = r));
        HideGroundWarning();
        if (!ok) yield break;

        State = BossState.Attack;
        targetX = chargeX;
        GoLow();
        movementSpeedMultiplier = chargeSpeedMultiplier;

        float timeout = verticalMoveTimeout;
        while ((!ArrivedAtTargetX || !ArrivedAtTargetY) && timeout > 0f && staggerLeft <= 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }
        movementSpeedMultiplier = strategyMovementSpeedMultiplier;
        if (staggerLeft > 0f) yield break;
        yield return StartCoroutine(ActivateGroundHitbox(chargeX, groundAoEWidth * 0.8f, groundAoEActiveTime));

        lowHoverAttackCount = 0;
        lowHoverTime = 0f;
        lowHoverTimerActive = true;
    }

    // 넓은 브레스와 달리 같은 플레이어를 여러 번 추적하는 탄막입니다.
    private IEnumerator Atk_Barrage()
    {
        ShowGroundWarningAt(player == null ? transform.position.x : player.position.x, 3.5f + warningPadding);
        bool ok = true;
        yield return StartCoroutine(Telegraph(telegraphTime, r => ok = r));
        HideGroundWarning();
        if (!ok) yield break;

        State = BossState.Attack;
        for (int i = 0; i < barrageShots; i++)
        {
            if (staggerLeft > 0f) break;
            if (breathPrefab != null && firePoint != null)
            {
                GameObject go = Instantiate(breathPrefab, firePoint.position, Quaternion.identity);
                Projectile projectile = go.GetComponent<Projectile>();
                if (projectile != null) projectile.Launch(ComputeAimDirection());
            }
            yield return new WaitForSeconds(barrageShotInterval);
        }
    }

    // ───────────────── 공격 3 : Descend (원거리 카운터 + 근접 기회) ─────────────────
    private IEnumerator Atk_Descend()
    {
        MoveToPlayerX();
        ShowGroundWarningAt(targetX, 5f);

        bool ok = true;
        yield return StartCoroutine(Telegraph(telegraphTime, r => ok = r));

        HideGroundWarning();
        if (!ok) yield break;

        State = BossState.Attack;
        GoLow();

        float timeout = verticalMoveTimeout;
        while (!ArrivedAtTargetY && timeout > 0f && staggerLeft <= 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // 착지 후 잠시 정지 — 근접 플레이어의 공격 창
        yield return new WaitForSeconds(descendSettleTime);

        // ★ 저공 세션 시작 — 여기서부터 카운트/타이머 시작
        lowHoverAttackCount = 0;
        lowHoverTime = 0f;
        lowHoverTimerActive = true;
        Debug.Log("[Boss] 저공 세션 시작");
    }

    // ───────────────── 공격 4 : Ascend (근거리 봉쇄) ─────────────────
    private IEnumerator Atk_Ascend()
    {
        State = BossState.Windup;
        yield return new WaitForSeconds(ascendWindup);
        if (staggerLeft > 0f) yield break;

        State = BossState.Attack;
        GoHigh();
        MoveToFarthestSlot();

        float timeout = verticalMoveTimeout;
        while (!ArrivedAtTargetY && timeout > 0f && staggerLeft <= 0f)
        {
            timeout -= Time.deltaTime;
            yield return null;
        }

        // ★ 저공 세션 종료
        lowHoverTimerActive = false;
        lowHoverTime = 0f;
        Debug.Log("[Boss] 상공 복귀");
    }

    // ───────────────────── 조준 / 부채꼴 ─────────────────────
    private Vector2 ComputeAimDirection()
    {
        Vector2 origin = firePoint != null ? (Vector2)firePoint.position : (Vector2)transform.position;

        Vector2 dir = player != null
            ? ((Vector2)player.position + Vector2.up * aimVerticalOffset) - origin
            : Vector2.down;

        if (dir.sqrMagnitude < 0.01f) dir = Vector2.down;
        dir.Normalize();

        if (dir.y > minDownwardY)
        {
            dir.y = minDownwardY;
            dir.Normalize();
        }
        return dir;
    }

    private Vector2[] BuildFan(Vector2 center, int count, float stepAngle)
    {
        count = Mathf.Max(1, count);
        Vector2[] result = new Vector2[count];

        float half = (count - 1) * 0.5f;
        for (int i = 0; i < count; i++)
            result[i] = Rotate(center, (i - half) * stepAngle);

        return result;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(rad);
        float s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ───────────────────── 지면 위험 구역 ─────────────────────
    private void ShowGroundWarningForFan(Vector2[] dirs)
    {
        if (groundWarning == null || firePoint == null || dirs == null || dirs.Length == 0) return;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        Vector2 origin = firePoint.position;

        foreach (Vector2 d in dirs)
        {
            if (d.y >= -0.05f) continue;
            float t = (groundSurfaceY - origin.y) / d.y;
            float x = origin.x + d.x * t;
            minX = Mathf.Min(minX, x);
            maxX = Mathf.Max(maxX, x);
        }

        if (minX > maxX) return;

        ShowGroundWarningAt((minX + maxX) * 0.5f, (maxX - minX) + warningPadding);
    }

    private void ShowGroundWarningAt(float centerX, float width)
    {
        if (groundWarning == null) return;

        groundWarning.transform.position = new Vector3(centerX, groundSurfaceY + 0.12f, 0f);
        groundWarning.transform.localScale = new Vector3(Mathf.Max(1f, width), warningHeight, 1f);
        groundWarning.SetActive(true);
    }

    private void HideGroundWarning()
    {
        if (groundWarning != null) groundWarning.SetActive(false);
    }

    // ───────────────────── 공용 : 예고 연출 ─────────────────────
    private IEnumerator Telegraph(float duration, System.Action<bool> result)
    {
        State = BossState.Windup;

        if (telegraphMark != null) telegraphMark.SetActive(true);

        float t = 0f;
        float blink = 0f;
        bool on = false;

        while (t < duration)
        {
            if (staggerLeft > 0f)
            {
                if (telegraphMark != null) telegraphMark.SetActive(false);
                SetVisualColor(staggerColor);
                result?.Invoke(false);
                yield break;
            }

            t += Time.deltaTime;
            blink -= Time.deltaTime;
            if (blink <= 0f)
            {
                blink = telegraphBlinkInterval;
                on = !on;
                SetVisualColor(on ? telegraphColor : visualBaseColor);
            }
            yield return null;
        }

        if (telegraphMark != null) telegraphMark.SetActive(false);
        SetVisualColor(visualBaseColor);
        result?.Invoke(true);
    }

    // ───────────────────── 경직 (패링 성공) ─────────────────────
    public void Stagger(float duration)
    {
        if (State == BossState.Dead) return;

        staggerLeft = Mathf.Max(staggerLeft, duration);
        State = BossState.Staggered;
        SetVisualColor(staggerColor);
        HideGroundWarning();

        if (tailHitbox != null) tailHitbox.SetActive(false);

        Debug.Log($"[Boss] STAGGERED {duration}s");
    }

    // ───────────────── 코루틴 / 표시 정리 헬퍼 ─────────────────
    /// <summary>
    /// 진행 중인 AI/공격 코루틴만 중단합니다.
    /// StopAllCoroutines()를 쓰지 않으므로 호출한 코루틴 자신은 살아남습니다.
    /// </summary>
    private void StopCombatRoutines()
    {
        if (aiRoutine != null) { StopCoroutine(aiRoutine); aiRoutine = null; }
        if (attackRoutine != null) { StopCoroutine(attackRoutine); attackRoutine = null; }
        if (subRoutine != null) { StopCoroutine(subRoutine); subRoutine = null; }
    }

    /// <summary>공격 관련 표시/판정을 모두 끕니다.</summary>
    private void ClearAttackVisuals()
    {
        if (telegraphMark != null) telegraphMark.SetActive(false);
        if (tailHitbox != null)
        {
            tailHitbox.SetActive(false);
            tailHitbox.transform.localScale = tailHitboxBaseScale;
        }
        movementSpeedMultiplier = strategyMovementSpeedMultiplier;
        HideGroundWarning();
        SetVisualColor(visualBaseColor);
    }

    // ═════════════════ 10단계 ④ : 페이즈 전환 ═════════════════
    private void CheckPhaseTransition()
    {
        if (phaseTransitionRunning) return;
        if (health == null || health.IsDead) return;

        if (!phase2Triggered && health.Normalized <= phase2Threshold)
        {
            phase2Triggered = true;
            transitionRoutine = StartCoroutine(PhaseTransitionRoutine(2));
        }
        else if (phase2Triggered && !phase3Triggered && health.Normalized <= phase3Threshold)
        {
            phase3Triggered = true;
            transitionRoutine = StartCoroutine(PhaseTransitionRoutine(3));
        }
    }

    private IEnumerator PhaseTransitionRoutine(int nextPhase)
    {
        phaseTransitionRunning = true;

        // ── 1) 진행 중인 AI/공격 코루틴만 중단 (자기 자신은 살아남음) ──
        //     ⚠ StopAllCoroutines()를 쓰면 이 코루틴까지 죽어서 아래가 실행되지 않습니다.
        StopCombatRoutines();

        // ── 2) 공격 표시/판정 정리 ──
        ClearAttackVisuals();
        State = BossState.Idle;
        staggerLeft = 0f;

        Debug.Log($"[Boss] ===== PHASE {nextPhase} 분석 시작 =====");

        PlayerCombatTracker tracker = PlayerCombatTracker.Instance;
        CombatAnalysis analysis = new CombatAnalysis { style = DominantStyle.Balanced };
        DominantStyle previousStyle = adaptedStyle;
        if (tracker != null) analysis = tracker.FinishWindow();

        // 재분석은 충분히 뚜렷한 변화만 새 전략으로 인정해 작은 수치 변화로 흔들리지 않습니다.
        if (nextPhase >= 3 && previousStyle != DominantStyle.None &&
            (analysis.style == DominantStyle.Balanced || analysis.confidence < 0.25f))
            analysis.style = previousStyle;

        // ── 3) 분석 연출 (Time.timeScale = 0) ──
        if (analysisUI != null && tracker != null)
        {
            yield return StartCoroutine(analysisUI.PlayAnalysis(tracker, analysis, previousStyle));
        }
        else
        {
            Debug.LogWarning("[Boss] AnalysisUI 또는 Tracker가 없습니다. 연출을 건너뜁니다.");
            yield return null;
        }

        // ── 4) 안전장치 : 연출이 중간에 실패해도 시간은 반드시 복구 ──
        if (Time.timeScale <= 0f)
        {
            Debug.LogWarning("[Boss] timeScale이 0으로 남아 있어 강제 복구합니다.");
            Time.timeScale = 1f;
        }

        // ── 5) 현재 윈도우의 결과를 Counter Strategy로 반영 ──
        adaptedStyle = analysis.style;
        Phase = nextPhase;
        ApplyPhaseWeights();
        ResetStats();   // Phase 2 통계를 따로 집계

        if (phaseLabel != null)
        {
            phaseLabel.text = $"PHASE {Phase} - COUNTER: {adaptedStyle.ToString().ToUpperInvariant()}";
        }

        // Phase 2 이후에는 새 행동을 별도 윈도우로 모아 Phase 3에서 다시 읽습니다.
        if (nextPhase < 3 && tracker != null) tracker.BeginNewWindow();

        phaseTransitionRunning = false;
        transitionRoutine = null;

        // ── 6) AI 루프 재시작 ──
        aiRoutine = StartCoroutine(AIRoutine());

        Debug.Log($"[Boss] Phase {Phase} AI 재시작 → {adaptedStyle}");
    }

    // ───────────────────── 이동 헬퍼 ─────────────────────
    public void MoveToSlot(int index)
    {
        if (slotX.Length == 0) return;
        currentSlot = Mathf.Clamp(index, 0, slotX.Length - 1);
        targetX = slotX[currentSlot];
    }

    public void MoveToFarthestSlot()
    {
        if (player == null || slotX.Length == 0) return;

        int best = 0;
        float bestDist = -1f;
        for (int i = 0; i < slotX.Length; i++)
        {
            float d = Mathf.Abs(slotX[i] - player.position.x);
            if (d > bestDist) { bestDist = d; best = i; }
        }
        MoveToSlot(best);
    }

    /// <summary>플레이어에게 가장 가까운 슬롯으로 이동 (원거리 카운터용)</summary>
    public void MoveToNearestSlot()
    {
        if (player == null || slotX.Length == 0) return;

        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < slotX.Length; i++)
        {
            float d = Mathf.Abs(slotX[i] - player.position.x);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        MoveToSlot(best);
    }

    public void MoveToPlayerX()
    {
        if (player == null) return;
        targetX = Mathf.Clamp(player.position.x, arenaMinX, arenaMaxX);
    }

    public void SetHoverY(float y) => targetY = y;
    public void GoHigh() => targetY = hoverHighY;
    public void GoLow() => targetY = hoverLowY;

    // ───────────────────────── 유틸 ─────────────────────────
    private void UpdateFacing()
    {
        if (!flipTowardPlayer || player == null || visual == null) return;
        if (State == BossState.Attack) return;

        int dir = player.position.x < transform.position.x ? -1 : 1;
        if (dir == facing) return;

        facing = dir;
        Vector3 s = visual.localScale;
        s.x = visualScaleX * facing;
        visual.localScale = s;
    }

    private void SetVisualColor(Color c)
    {
        if (visualSr != null) visualSr.color = c;
    }

    private void OnBossDeath()
    {
        State = BossState.Dead;

        StopCombatRoutines();
        if (transitionRoutine != null) { StopCoroutine(transitionRoutine); transitionRoutine = null; }
        StopAllCoroutines();

        // 전환 연출 도중 사망했을 경우 시간 복구 (게임이 멈춘 채 굳는 것 방지)
        if (Time.timeScale <= 0f) Time.timeScale = 1f;

        ClearAttackVisuals();
        SetVisualColor(new Color(0.35f, 0.35f, 0.4f));

        Debug.Log("[Boss] DEFEATED");
    }

    // ══════════════════════════════════════════════════════════
    #region DEBUG — 테스트 전용. 이 region + Update()의 호출 한 줄만 지우면 제거됩니다.
    // ══════════════════════════════════════════════════════════
    private void DebugHoverInput()
    {
        if (Input.GetKeyDown(KeyCode.T)) { GoLow(); Debug.Log($"[Boss DEBUG] GoLow()  targetY={hoverLowY}"); }
        if (Input.GetKeyDown(KeyCode.Y)) { GoHigh(); Debug.Log($"[Boss DEBUG] GoHigh() targetY={hoverHighY}"); }

        if (Input.GetKeyDown(KeyCode.U))
        {
            int next = (currentSlot + 1) % Mathf.Max(1, slotX.Length);
            MoveToSlot(next);
            Debug.Log($"[Boss DEBUG] MoveToSlot({next}) targetX={targetX}");
        }

        if (Input.GetKeyDown(KeyCode.I))
        {
            MoveToPlayerX();
            Debug.Log($"[Boss DEBUG] MoveToPlayerX() targetX={targetX}");
        }

        // P : 패턴 사용 통계 출력 (적응 검증용)
        if (Input.GetKeyDown(KeyCode.P)) DebugPrintStats();
    }

#if UNITY_EDITOR
    // 1~5는 현재 Phase와 무관하게 Counter Strategy를 즉시 바꿔 회의 중 체감 확인에 사용합니다.
    private void DebugStyleInput()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) ApplyDebugStyle(DominantStyle.Ranged);
        if (Input.GetKeyDown(KeyCode.Alpha2)) ApplyDebugStyle(DominantStyle.Melee);
        if (Input.GetKeyDown(KeyCode.Alpha3)) ApplyDebugStyle(DominantStyle.Evasive);
        if (Input.GetKeyDown(KeyCode.Alpha4)) ApplyDebugStyle(DominantStyle.Balanced);
        if (Input.GetKeyDown(KeyCode.Alpha5)) ApplyDebugStyle(DominantStyle.Airborne);
    }

    private void ApplyDebugStyle(DominantStyle style)
    {
        if (State == BossState.Dead) return;

        adaptedStyle = style;
        Phase = Mathf.Max(2, Phase);
        phase2Triggered = true;
        ApplyPhaseWeights();
        if (phaseLabel != null)
            phaseLabel.text = $"DEBUG COUNTER: {style.ToString().ToUpperInvariant()}";

        Debug.Log($"[Boss DEBUG] Style forced → {style}");
    }
#endif

    // ── 패턴 사용 통계 (적응 검증용) ──
    private readonly Dictionary<BossAttack, int> useCount = new Dictionary<BossAttack, int>();

    private void CountUse(BossAttack a)
    {
        if (!useCount.ContainsKey(a)) useCount[a] = 0;
        useCount[a]++;
    }

    private void DebugPrintStats()
    {
        string s = $"[Boss STATS] Phase {Phase} / 적응: {adaptedStyle}\n";
        int total = 0;
        foreach (var kv in useCount) total += kv.Value;

        foreach (var kv in useCount)
            s += $"  {kv.Key,-13} {kv.Value,3}회  ({(total > 0 ? kv.Value * 100f / total : 0):F0}%)\n";

        Debug.Log(s);
    }

    /// <summary>페이즈 전환 시 통계 초기화</summary>
    private void ResetStats() => useCount.Clear();
    #endregion
    // ══════════════════════════════════════════════════════════

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        if (slotX != null)
            foreach (float sx in slotX) Gizmos.DrawWireSphere(new Vector3(sx, hoverHighY, 0f), 0.5f);

        Gizmos.color = new Color(1f, 0.6f, 0f);
        if (slotX != null)
            foreach (float sx in slotX) Gizmos.DrawWireSphere(new Vector3(sx, hoverLowY, 0f), 0.4f);

        // 꼬리 판정 높이
        Gizmos.color = Color.magenta;
        float ty = groundSurfaceY + tailHeightAboveGround;
        Gizmos.DrawWireCube(new Vector3(transform.position.x, ty, 0f), new Vector3(tailWidth, 1.6f, 0f));

        Gizmos.color = Color.red;
        Gizmos.DrawLine(new Vector3(arenaMinX, groundSurfaceY, 0f), new Vector3(arenaMinX, hoverHighY + 4f, 0f));
        Gizmos.DrawLine(new Vector3(arenaMaxX, groundSurfaceY, 0f), new Vector3(arenaMaxX, hoverHighY + 4f, 0f));

        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(arenaMinX - 3f, groundSurfaceY, 0f), new Vector3(arenaMaxX + 3f, groundSurfaceY, 0f));
    }
}
