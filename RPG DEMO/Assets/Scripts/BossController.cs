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

    [Header("── 경직 (패링 성공 시) ──")]
    [SerializeField] private Color staggerColor = new Color(0.6f, 0.8f, 1f);

    // ★ 10단계 ① : 페이즈 전환
    [Header("── 페이즈 전환 ──")]
    [Tooltip("씬의 AnalysisPanel에 붙인 AnalysisUI")]
    [SerializeField] private AnalysisUI analysisUI;
    [Tooltip("이 비율 이하로 내려가면 Phase 2 (0.5 = 50%)")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float phase2Threshold = 0.5f;
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
    private Coroutine aiRoutine;
    private Coroutine attackRoutine;       // ★ ExecuteAttack
    private Coroutine subRoutine;          // ★ 개별 공격 (Atk_*)
    private Coroutine transitionRoutine;   // ★ 페이즈 전환

    // ★ 수정 ② : 저공 세션 상태
    private int lowHoverAttackCount;
    private bool lowHoverTimerActive;

    // ★ 10단계 ② : 페이즈 전환이 1회만 실행되게 하는 플래그
    private bool phase2Triggered;
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

        if (debugHoverKeys) DebugHoverInput();
    }

    private void UpdateHoverPosition()
    {
        float hSpeed = staggerLeft > 0f ? 0f : horizontalSpeed;
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

        // 기본값 (Phase 1 / 분석 단계)
        lowTailBonusCurrent = lowHoverTailBonus;
        repositionChanceCurrent = repositionChance;

        if (Phase <= 1)
        {
            weights[BossAttack.PlasmaBreath] = 3;
            weights[BossAttack.Descend] = 3;
            weights[BossAttack.TailSweep] = 4;
            weights[BossAttack.Ascend] = 1;

            Debug.Log("[Boss] Phase 1 가중치 (균등 · 분석 중)");
            return;
        }

        // ───────── Phase 2 이상 : 적응 ─────────
        switch (adaptedStyle)
        {
            case DominantStyle.Melee:
                // 근접형 카운터 : 붙기 어렵게 만들되 완전히 막지는 않음
                weights[BossAttack.PlasmaBreath] = 4;   // 원거리 견제 증가
                weights[BossAttack.TailSweep] = 6;   // 근접 응징
                weights[BossAttack.Ascend] = 4;   // 붙으면 상공으로 도망
                weights[BossAttack.Descend] = 1;   // 근접 기회는 남겨둠 (0 아님)

                lowTailBonusCurrent = 2f;
                repositionChanceCurrent = 0.7f;         // 자주 자리를 옮김

                Debug.Log("[Boss] Phase 2 가중치 → vs MELEE (넉백·이탈 중심)");
                break;

            case DominantStyle.Ranged:
                // 원거리형 카운터 : 거리를 못 벌리게 계속 따라붙음
                weights[BossAttack.Descend] = 5;   // 플레이어 위로 강하
                weights[BossAttack.TailSweep] = 2;
                weights[BossAttack.PlasmaBreath] = 2;   // 원거리 공방은 줄임
                weights[BossAttack.Ascend] = 1;   // 거의 안 올라감 (0 아님)

                lowTailBonusCurrent = 1.2f;
                repositionChanceCurrent = 0.25f;        // 자리를 잘 안 뜸

                Debug.Log("[Boss] Phase 2 가중치 → vs RANGED (추격·접근 중심)");
                break;

            default:
                // 판정 실패 시 Phase 1과 동일
                weights[BossAttack.PlasmaBreath] = 3;
                weights[BossAttack.Descend] = 3;
                weights[BossAttack.TailSweep] = 4;
                weights[BossAttack.Ascend] = 1;

                Debug.LogWarning("[Boss] DominantStyle이 None입니다. 기본 가중치 사용");
                break;
        }
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
            default: return false;   // 확장 공격은 아직 미구현
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
        if (tailHitbox == null) return;
        tailHitbox.transform.position =
            new Vector3(transform.position.x, groundSurfaceY + tailHeightAboveGround, 0f);
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
        if (tailHitbox != null) tailHitbox.SetActive(false);
        HideGroundWarning();
        SetVisualColor(visualBaseColor);
    }

    // ═════════════════ 10단계 ④ : 페이즈 전환 ═════════════════
    private void CheckPhaseTransition()
    {
        if (phase2Triggered || phaseTransitionRunning) return;
        if (health == null || health.IsDead) return;

        if (health.Normalized <= phase2Threshold)
        {
            phase2Triggered = true;
            transitionRoutine = StartCoroutine(Phase2TransitionRoutine());
        }
    }

    private IEnumerator Phase2TransitionRoutine()
    {
        phaseTransitionRunning = true;

        // ── 1) 진행 중인 AI/공격 코루틴만 중단 (자기 자신은 살아남음) ──
        //     ⚠ StopAllCoroutines()를 쓰면 이 코루틴까지 죽어서 아래가 실행되지 않습니다.
        StopCombatRoutines();

        // ── 2) 공격 표시/판정 정리 ──
        ClearAttackVisuals();
        State = BossState.Idle;
        staggerLeft = 0f;

        Debug.Log("[Boss] ===== PHASE 2 진입 =====");

        // ── 3) 분석 연출 (Time.timeScale = 0) ──
        if (analysisUI != null && PlayerCombatTracker.Instance != null)
        {
            yield return StartCoroutine(analysisUI.PlayAnalysis(PlayerCombatTracker.Instance));
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

        // ── 5) 페이즈 갱신 ──
        // ★ 11단계 : 확정된 스타일을 보스에 반영
        if (PlayerCombatTracker.Instance != null)
            adaptedStyle = PlayerCombatTracker.Instance.LockedStyle;

        Phase = 2;
        ApplyPhaseWeights();
        ResetStats();   // Phase 2 통계를 따로 집계

        if (phaseLabel != null)
        {
            phaseLabel.text = adaptedStyle == DominantStyle.Melee
                ? "PHASE 2 - COUNTER: CLOSE RANGE"
                : "PHASE 2 - COUNTER: LONG RANGE";
        }

        phaseTransitionRunning = false;
        transitionRoutine = null;

        // ── 6) AI 루프 재시작 ──
        aiRoutine = StartCoroutine(AIRoutine());

        Debug.Log("[Boss] Phase 2 AI 재시작");
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
