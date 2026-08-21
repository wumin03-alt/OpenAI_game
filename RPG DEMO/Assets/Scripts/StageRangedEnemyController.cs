using UnityEngine;

/// <summary>
/// Stage01 전용 단순 원거리 적 AI입니다.
/// 플레이어와 거리를 유지하며, 안전한 지면에서만 이동하고 기존 Projectile로 공격합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class StageRangedEnemyController : MonoBehaviour
{
    [Header("── 거리 유지 ──")]
    [SerializeField] private float approachDistance = 7.5f;
    [SerializeField] private float retreatDistance = 3.25f;
    [SerializeField] private float approachSpeed = 2.4f;
    [SerializeField] private float retreatSpeed = 2.8f;

    [Header("── 원거리 공격 ──")]
    [SerializeField] private Projectile projectilePrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float attackCooldown = 1.75f;
    [SerializeField] private float initialShotDelay = 0.6f;

    [Header("── 지형 감지 ──")]
    [SerializeField] private Transform edgeCheck;
    [SerializeField] private Transform wallCheck;
    [SerializeField] private float edgeCheckDistance = 1.2f;
    [SerializeField] private float wallCheckDistance = 0.4f;
    [SerializeField] private LayerMask groundLayer;

    [Header("── 피격 경직 ──")]
    [Tooltip("플레이어 공격 넉백이 AI 이동에 즉시 덮어써지지 않도록 유지하는 시간")]
    [SerializeField] private float hitStunTime = 0.25f;

    [Header("── 비주얼 ──")]
    [SerializeField] private Transform visual;

    private Rigidbody2D rb;
    private Health health;
    private Transform player;
    private float shotCooldown;
    private float hitStunLeft;
    private int facing = -1;
    private float visualScaleX = 1f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();

        if (visual == null)
        {
            SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) visual = spriteRenderer.transform;
        }

        if (firePoint == null) firePoint = transform;
        if (visual != null) visualScaleX = Mathf.Abs(visual.localScale.x);
        shotCooldown = initialShotDelay;

        if (health != null) health.onDamaged.AddListener(OnDamaged);
    }

    private void Start()
    {
        FindPlayer();
        ApplyFacing();
    }

    private void Update()
    {
        if (health != null && health.IsDead) return;

        if (hitStunLeft > 0f)
        {
            hitStunLeft -= Time.deltaTime;
            return;
        }

        if (player == null) FindPlayer();
        if (player == null) return;

        float distance = Vector2.Distance(transform.position, player.position);
        shotCooldown -= Time.deltaTime;

        if (distance <= attackRange && shotCooldown <= 0f)
            FireAtPlayer();
    }

    private void FixedUpdate()
    {
        if (health != null && health.IsDead) return;
        if (hitStunLeft > 0f) return;

        if (player == null)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        float dx = player.position.x - transform.position.x;
        if (Mathf.Abs(dx) > 0.05f)
        {
            facing = dx > 0f ? 1 : -1;
            ApplyFacing();
        }

        float horizontalVelocity = 0f;
        float horizontalDistance = Mathf.Abs(dx);
        if (horizontalDistance > approachDistance)
            horizontalVelocity = facing * approachSpeed;
        else if (horizontalDistance < retreatDistance)
            horizontalVelocity = -facing * retreatSpeed;

        if (horizontalVelocity != 0f && (!HasGroundAhead(Mathf.Sign(horizontalVelocity)) || HasWallAhead(Mathf.Sign(horizontalVelocity))))
            horizontalVelocity = 0f;

        rb.linearVelocity = new Vector2(horizontalVelocity, rb.linearVelocity.y);
    }

    private void FireAtPlayer()
    {
        shotCooldown = attackCooldown;

        if (projectilePrefab == null)
        {
            Debug.LogError("[Stage01] Ranged enemy cannot fire: Projectile prefab is missing.", this);
            enabled = false;
            return;
        }

        Vector3 origin = firePoint.position;
        Vector2 direction = (Vector2)(player.position - origin);
        Projectile projectile = Instantiate(projectilePrefab, origin, Quaternion.identity);
        projectile.Launch(direction);
    }

    private void FindPlayer()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    private void OnDamaged()
    {
        hitStunLeft = hitStunTime;
    }

    private bool HasGroundAhead(float direction)
    {
        if (edgeCheck == null) return true;

        Vector2 origin = edgeCheck.position;
        origin.x = transform.position.x + Mathf.Abs(edgeCheck.localPosition.x) * direction;
        return Physics2D.Raycast(origin, Vector2.down, edgeCheckDistance, groundLayer);
    }

    private bool HasWallAhead(float direction)
    {
        if (wallCheck == null) return false;

        Vector2 origin = wallCheck.position;
        origin.x = transform.position.x + Mathf.Abs(wallCheck.localPosition.x) * direction;
        return Physics2D.Raycast(origin, Vector2.right * direction, wallCheckDistance, groundLayer);
    }

    private void ApplyFacing()
    {
        if (visual == null) return;

        Vector3 scale = visual.localScale;
        scale.x = visualScaleX * facing;
        visual.localScale = scale;
    }
}
