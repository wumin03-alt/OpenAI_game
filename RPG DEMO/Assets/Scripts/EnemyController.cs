using UnityEngine;

/// <summary>
/// 잡몹 AI. 순찰 → 플레이어 감지 → 추격 → 거리 기반 근접 공격.
/// 물리 접촉이 풀려도 콜라이더 표면 사이가 사거리 안이면 주기적으로 공격합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    [Header("── 이동 속도 ──")]
    [SerializeField] private float patrolSpeed = 1.8f;
    [SerializeField] private float chaseSpeed = 3.6f;

    [Header("── 감지 ──")]
    [Tooltip("이 거리 안에 들어오면 추격 시작")]
    [SerializeField] private float detectRange = 6f;
    [Tooltip("이 거리보다 멀어지면 추격 포기 (detectRange보다 크게)")]
    [SerializeField] private float loseRange = 9f;
    [Tooltip("이 거리 안에서는 멈춰서 밀착 (계속 밀지 않도록)")]
    [SerializeField] private float stopDistance = 0.9f;

    [Header("── 순찰 ──")]
    [Tooltip("시작 위치에서 좌우로 움직일 거리")]
    [SerializeField] private float patrolDistance = 3f;
    [Tooltip("체크 해제하면 제자리에 서 있기만 함")]
    [SerializeField] private bool doPatrol = true;

    [Header("── 지형 감지 ──")]
    [SerializeField] private Transform edgeCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float edgeCheckDistance = 1.2f;
    [SerializeField] private float wallCheckDistance = 0.4f;
    [SerializeField] private LayerMask groundLayer;

    [Header("── 피격 경직 ──")]
    [Tooltip("맞았을 때 넉백이 먹히도록 잠시 이동 정지")]
    [SerializeField] private float hitStunTime = 0.25f;

    [Header("── 근접 공격 ──")]
    [Tooltip("두 몸 콜라이더 표면 사이의 최대 공격 거리")]
    [SerializeField, Min(0f)] private float attackReach = 0.3f;
    [SerializeField, Min(0.05f)] private float attackInterval = 0.9f;
    [SerializeField, Min(0f)] private float attackDamage = 12f;
    [SerializeField, Min(0f)] private float attackKnockbackForce = 7f;
    [SerializeField, Min(0f)] private float attackKnockbackUp = 3f;

    [Header("── 비주얼 ──")]
    [SerializeField] private Transform visual;

    public bool IsChasing { get; private set; }

    private Rigidbody2D rb;
    private Health health;
    private Collider2D bodyCollider;
    private Transform player;
    private Health playerHealth;
    private Collider2D playerCollider;
    private Vector2 startPos;
    private int facing = -1;          // 1 = 오른쪽, -1 = 왼쪽
    private float hitStunLeft;
    private float nextAttackTime;
    private float visualScaleX = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();
        bodyCollider = GetComponent<Collider2D>();
        startPos = transform.position;

        if (visual == null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) visual = sr.transform;
        }
        if (visual != null) visualScaleX = Mathf.Abs(visual.localScale.x);

        // 맞았을 때 잠시 멈추도록 Health 이벤트에 자동 등록 (Inspector 연결 불필요)
        if (health != null) health.onDamaged.AddListener(OnDamaged);
    }

    private void Start()
    {
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            player = p.transform;
            playerHealth = p.GetComponent<Health>();
            playerCollider = p.GetComponent<Collider2D>();
        }
        ApplyFacing();
    }

    private void OnDamaged()
    {
        hitStunLeft = hitStunTime;
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (hitStunLeft > 0f) hitStunLeft -= Time.deltaTime;

        UpdateDetection();
        TryMeleeAttack();
    }

    private void FixedUpdate()
    {
        if (health != null && health.IsDead) return;

        // 피격 경직 중에는 넉백이 그대로 먹히도록 속도를 건드리지 않음
        if (hitStunLeft > 0f) return;

        if (IsChasing) DoChase();
        else DoPatrol();
    }

    // ───────────────────────── 감지 ─────────────────────────
    private void UpdateDetection()
    {
        if (player == null) return;

        float dist = Vector2.Distance(transform.position, player.position);

        if (!IsChasing && dist <= detectRange) IsChasing = true;
        else if (IsChasing && dist > loseRange) IsChasing = false;
    }

    // ───────────────────────── 추격 ─────────────────────────
    private void DoChase()
    {
        float dx = player.position.x - transform.position.x;
        int dir = dx > 0f ? 1 : -1;
        if (dir != facing) { facing = dir; ApplyFacing(); }

        // 너무 가까우면 멈춤 (플레이어를 계속 밀지 않도록)
        if (Mathf.Abs(dx) <= stopDistance)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        // 낭떠러지나 벽이 있으면 더 가지 않음
        if (!HasGroundAhead() || HasWallAhead())
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(facing * chaseSpeed, rb.linearVelocity.y);
    }

    // ───────────────────────── 근접 공격 ─────────────────────────
    private void TryMeleeAttack()
    {
        if (!IsChasing || player == null || playerHealth == null || playerHealth.IsDead) return;
        if (hitStunLeft > 0f || Time.time < nextAttackTime || !IsPlayerInAttackRange()) return;

        nextAttackTime = Time.time + attackInterval;

        float hpBefore = playerHealth.CurrentHP;
        playerHealth.TakeDamage(attackDamage);

        // 패링/무적 등으로 실제 체력이 줄지 않았다면 넉백도 주지 않습니다.
        if (playerHealth.CurrentHP >= hpBefore) return;

        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
        if (playerBody == null) return;

        float direction = player.position.x >= transform.position.x ? 1f : -1f;
        Vector2 velocity = playerBody.linearVelocity;
        velocity.x = 0f;
        playerBody.linearVelocity = velocity;
        playerBody.AddForce(new Vector2(direction * attackKnockbackForce, attackKnockbackUp),
                            ForceMode2D.Impulse);
    }

    private bool IsPlayerInAttackRange()
    {
        if (bodyCollider != null && playerCollider != null)
        {
            ColliderDistance2D distance = bodyCollider.Distance(playerCollider);
            return distance.isOverlapped || distance.distance <= attackReach;
        }

        // 콜라이더 참조가 없는 비정상 프리팹에서도 완전히 공격 불능이 되지 않게 폴백합니다.
        return Vector2.Distance(transform.position, player.position) <= stopDistance + attackReach;
    }

    // ───────────────────────── 순찰 ─────────────────────────
    private void DoPatrol()
    {
        if (!doPatrol)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        bool turn = false;

        // 순찰 범위를 벗어났으면 방향 전환
        if (facing > 0 && transform.position.x >= startPos.x + patrolDistance) turn = true;
        if (facing < 0 && transform.position.x <= startPos.x - patrolDistance) turn = true;

        // 낭떠러지 / 벽
        if (!HasGroundAhead() || HasWallAhead()) turn = true;

        if (turn)
        {
            facing *= -1;
            ApplyFacing();
        }

        rb.linearVelocity = new Vector2(facing * patrolSpeed, rb.linearVelocity.y);
    }

    // ───────────────────────── 지형 판정 ─────────────────────────
    private bool HasGroundAhead()
    {
        if (edgeCheck == null) return true;

        Vector2 origin = edgeCheck.position;
        origin.x = transform.position.x + Mathf.Abs(edgeCheck.localPosition.x) * facing;
        return Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, groundLayer);
    }

    private bool HasWallAhead()
    {
        if (wallCheck == null) return false;

        Vector2 origin = wallCheck.position;
        origin.x = transform.position.x + Mathf.Abs(wallCheck.localPosition.x) * facing;
        return Physics2D.Raycast(origin, Vector2.right * facing, wallCheckDistance, groundLayer);
    }

    private void ApplyFacing()
    {
        if (visual == null) return;
        Vector3 s = visual.localScale;
        s.x = visualScaleX * facing;
        visual.localScale = s;
    }

    // ───────────── 에디터 디버그 (게임에는 영향 없음) ─────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectRange);
        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, loseRange);

        if (edgeCheck != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(edgeCheck.position,
                            edgeCheck.position + Vector3.down * edgeCheckDistance);
        }
        if (wallCheck != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(wallCheck.position,
                            wallCheck.position + Vector3.right * wallCheckDistance);
        }
    }
}
