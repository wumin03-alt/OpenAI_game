using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using UnityEngine;

/// <summary>
/// 플레이어 이동 / 점프 / 엎드리기 / 대시 / 패링 / 근거리(Q) / 원거리(W).
/// 조작: ← → 이동, ↑ 점프, ↓ 엎드리기, Q 근거리, W 원거리, E 패링, R 대시
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CapsuleCollider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("── 이동 ──")]
    [SerializeField] private float moveSpeed = 7f;

    [Header("── 점프 ──")]
    [SerializeField] private float jumpForce = 15f;
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.18f;
    [SerializeField] private LayerMask groundLayer;
    [Tooltip("발판에서 떨어진 직후에도 잠깐 점프를 허용하는 시간")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("착지 직전에 누른 점프 입력을 기억하는 시간")]
    [SerializeField] private float jumpBufferTime = 0.1f;

    [Header("── 단방향 발판 ──")]
    [Tooltip("발판 위에서 ↓를 두 번 누를 때 두 입력을 하나로 인식하는 시간")]
    [SerializeField, Min(0.05f)] private float dropTapWindow = 0.32f;
    [Tooltip("발판 아래로 내려갈 때 주는 초기 하강 속도")]
    [SerializeField, Min(0.1f)] private float dropThroughSpeed = 3f;
    [Tooltip("일반 BoxCollider를 단방향 발판으로 인식할 오브젝트 이름 토큰")]
    [SerializeField] private string platformNameToken = "Platform";
    [Tooltip("점프 정점에서 발판 윗면에 조금 못 미쳤을 때 착지를 보정할 최대 거리")]
    [SerializeField, Min(0f)] private float platformLandingSnapDistance = 0.22f;

    [Header("── 엎드리기 (↓) ──")]
    [Tooltip("엎드린 상태의 이동 속도 배율. 0으로 두면 이동 불가")]
    [SerializeField] private float crouchSpeedMultiplier = 0.35f;
    [Tooltip("엎드릴 때 콜라이더/비주얼 높이 배율")]
    [SerializeField] private float crouchHeightRatio = 0.5f;

    [Header("── 대시 (R) ──")]
    [SerializeField] private float dashSpeed = 22f;
    [SerializeField] private float dashDuration = 0.18f;
    [SerializeField] private float dashCooldown = 0.6f;

    [Header("── 패링 (E) ──")]
    [Tooltip("이 시간 동안 피격되면 데미지 무효 + 보스 경직")]
    [SerializeField] private float parryWindow = 0.25f;
    [SerializeField] private float parryCooldown = 0.8f;

    [Header("── 피격 반응 (넉백) ──")]
    [Tooltip("피격 후 이 시간 동안 이동 입력이 속도를 덮어쓰지 않습니다")]
    [SerializeField] private float knockbackTime = 0.25f;
    [Tooltip("넉백 중에 대시(R)로 탈출할 수 있게 할지")]
    [SerializeField] private bool allowDashDuringKnockback = true;

    [Header("── 근거리 공격 (Q) ──")]
    [Tooltip("Player 자식으로 만든 MeleeHitbox 오브젝트")]
    [SerializeField] private GameObject meleeHitbox;
    [Tooltip("Q를 누르고 판정이 나가기까지의 선딜레이")]
    [SerializeField] private float meleeStartDelay = 0.05f;
    [Tooltip("히트박스가 켜져 있는 시간")]
    [SerializeField] private float meleeActiveTime = 0.12f;
    [SerializeField] private float meleeCooldown = 0.35f;

    [Header("── 원거리 공격 (W) ──")]
    [Tooltip("Assets/Prefabs 의 Bullet_Player 프리팹")]
    [SerializeField] private GameObject projectilePrefab;
    [Tooltip("Player 자식으로 만든 FirePoint 오브젝트")]
    [SerializeField] private Transform firePoint;
    [SerializeField] private float rangedStartDelay = 0.05f;
    [SerializeField] private float rangedCooldown = 0.4f;

    // ★ 신규 ① : 공중 보스 자동 조준
    [Tooltip("이 태그의 오브젝트가 씬에 있으면 W가 그쪽으로 자동 조준됩니다 (Stage01에는 없으므로 수평 발사)")]
    [SerializeField] private bool autoAimEnabled = true;
    [SerializeField] private string autoAimTargetTag = "Boss";
    [Tooltip("조준점을 대상 pivot보다 위로 올리는 값. 보스 몸통 중심을 노리게 함")]
    [SerializeField] private float autoAimVerticalOffset = 1.2f;

    [Header("── 비주얼 ──")]
    [SerializeField] private Transform visual;

    [Header("── 공통 HUD ──")]
    [Tooltip("씬에 공통 HUD가 없을 때 자동으로 생성할 프리팹")]
    [SerializeField] private PlayerCommonHUD commonHudPrefab;

    // ── 다른 스크립트가 읽어갈 상태값 ──
    public int Facing { get; private set; } = 1;   // 1 = 오른쪽, -1 = 왼쪽
    public bool IsGrounded { get; private set; }
    public bool IsDashing { get; private set; }
    public bool IsCrouching { get; private set; }
    public bool IsParrying { get; private set; }
    public bool IsAttacking { get; private set; }
    public bool IsKnockedBack => knockbackLeft > 0f;

    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private Health health;
    private Transform autoAimTarget;               // ★ 신규 ②

    private float moveInput;
    private float coyoteLeft;
    private float jumpBufferLeft;
    private float dashTimeLeft;
    private float dashCooldownLeft;
    private float parryTimeLeft;
    private float parryCooldownLeft;
    private float knockbackLeft;
    private float defaultGravity;
    private bool jumpRequested;
    private float dropTapTimeLeft;
    private float upwardVelocityBeforePhysics;

    private readonly Dictionary<Collider2D, IgnoredPlatform> ignoredPlatforms =
        new Dictionary<Collider2D, IgnoredPlatform>();
    private readonly List<Collider2D> ignoredPlatformCleanup = new List<Collider2D>();

    private float meleeCooldownLeft;
    private float meleeHitboxBaseX;
    private DamageZone meleeDamageZone;
    private float rangedCooldownLeft;
    private float firePointBaseX;
    private GameObject activeBullet;

    private Vector2 standSize, standOffset, crouchSize, crouchOffset;
    private Vector3 visualStandScale, visualStandPos;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        defaultGravity = rb.gravityScale;

        standSize = col.size;
        standOffset = col.offset;
        crouchSize = new Vector2(standSize.x, standSize.y * crouchHeightRatio);
        crouchOffset = new Vector2(standOffset.x, standOffset.y - (standSize.y - crouchSize.y) * 0.5f);

        if (visual == null)
        {
            SpriteRenderer sr = GetComponentInChildren<SpriteRenderer>();
            if (sr != null) visual = sr.transform;
        }
        if (visual != null)
        {
            visualStandScale = visual.localScale;
            visualStandPos = visual.localPosition;
        }

        if (meleeHitbox != null)
        {
            meleeHitboxBaseX = Mathf.Abs(meleeHitbox.transform.localPosition.x);
            meleeDamageZone = meleeHitbox.GetComponent<DamageZone>();
            meleeHitbox.SetActive(false);
        }

        if (firePoint != null)
            firePointBaseX = Mathf.Abs(firePoint.localPosition.x);

        health = GetComponent<Health>();
        if (health != null && health.onDamaged != null)
            health.onDamaged.AddListener(OnDamaged);
    }

    // ★ 신규 ② : 자동 조준 대상 캐싱 (없으면 null → 수평 발사)
    private void Start()
    {
        EnsureCommonHud();

        if (autoAimEnabled && !string.IsNullOrEmpty(autoAimTargetTag))
        {
            GameObject t = GameObject.FindGameObjectWithTag(autoAimTargetTag);
            if (t != null) autoAimTarget = t.transform;
        }
    }

    private void Update()
    {
        if (knockbackLeft > 0f) knockbackLeft -= Time.deltaTime;

        ReadMoveInput();
        CheckGround();
        HandleDropThroughInput();
        HandleCrouch();
        HandleJumpInput();
        HandleDash();
        HandleParry();
        HandleMelee();
        HandleRanged();
    }

    private void FixedUpdate()
    {
        UpdateIgnoredPlatforms();

        // 사망 후 이동 코드가 속도를 되살리는 것을 차단
        if (health != null && health.IsDead) return;

        if (IsDashing)
        {
            rb.linearVelocity = new Vector2(Facing * dashSpeed, 0f);
            return;
        }

        // 넉백 중에는 velocity를 건드리지 않아 AddForce가 살아남게 함
        if (knockbackLeft > 0f) return;

        float speed = IsCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed;
        rb.linearVelocity = new Vector2(moveInput * speed, rb.linearVelocity.y);

        if (jumpRequested)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpRequested = false;
        }

        PreparePassThroughPlatformsWhileRising();
        upwardVelocityBeforePhysics = rb.linearVelocity.y;
    }

    // ───────────────────────── 피격 반응 ─────────────────────────
    private void OnDamaged()
    {
        BeginKnockback(knockbackTime);
    }

    /// <summary>외부에서 강한 넉백을 줄 때 사용 (8단계 TailSweep 등)</summary>
    public void BeginKnockback(float duration)
    {
        if (duration <= 0f) return;

        knockbackLeft = Mathf.Max(knockbackLeft, duration);
        jumpRequested = false;
        jumpBufferLeft = 0f;

        if (IsCrouching)
        {
            IsCrouching = false;
            SetCrouchVisual(false);
        }
    }

    // ───────────────────────── 이동 (← →) ─────────────────────────
    private void ReadMoveInput()
    {
        moveInput = 0f;
        if (Input.GetKey(KeyCode.LeftArrow)) moveInput -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) moveInput += 1f;

        if (!IsDashing && moveInput != 0f)
        {
            Facing = moveInput > 0f ? 1 : -1;
            if (visual != null)
            {
                Vector3 s = visual.localScale;
                s.x = Mathf.Abs(s.x) * Facing;
                visual.localScale = s;
            }
        }
    }

    // ───────────────────────── 접지 판정 ─────────────────────────
    private void CheckGround()
    {
        Collider2D ground = groundCheck != null
            ? Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer)
            : null;
        IsGrounded = ground != null && !ignoredPlatforms.ContainsKey(ground);

        coyoteLeft = IsGrounded ? coyoteTime : coyoteLeft - Time.deltaTime;
    }

    // ───────────────────── 단방향 발판 ───────────────────────
    private void HandleDropThroughInput()
    {
        if (dropTapTimeLeft > 0f)
            dropTapTimeLeft -= Time.deltaTime;

        if (!Input.GetKeyDown(KeyCode.DownArrow)) return;

        if (dropTapTimeLeft > 0f && IsGrounded && TryGetSupportingPlatform(out Collider2D platform))
        {
            IgnorePlatform(platform, true);
            dropTapTimeLeft = 0f;
            coyoteLeft = 0f;
            jumpBufferLeft = 0f;
            IsGrounded = false;

            if (IsCrouching)
            {
                IsCrouching = false;
                SetCrouchVisual(false);
            }

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, -dropThroughSpeed);
            return;
        }

        dropTapTimeLeft = dropTapWindow;
    }

    private bool TryGetSupportingPlatform(out Collider2D platform)
    {
        platform = null;
        if (groundCheck == null) return false;

        Collider2D[] contacts = Physics2D.OverlapCircleAll(
            groundCheck.position, groundCheckRadius, groundLayer);
        float highestSurface = float.NegativeInfinity;

        foreach (Collider2D candidate in contacts)
        {
            if (!IsPassThroughPlatform(candidate) || ignoredPlatforms.ContainsKey(candidate))
                continue;

            float surface = candidate.bounds.max.y;
            if (surface <= highestSurface) continue;

            highestSurface = surface;
            platform = candidate;
        }

        return platform != null;
    }

    private bool IsPassThroughPlatform(Collider2D candidate)
    {
        if (candidate == null || candidate.isTrigger) return false;
        if ((groundLayer.value & (1 << candidate.gameObject.layer)) == 0) return false;
        if (candidate.GetComponent<PlatformEffector2D>() != null) return true;

        return !string.IsNullOrWhiteSpace(platformNameToken)
               && candidate.name.IndexOf(platformNameToken,
                   System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// 상승 중 다음 물리 프레임에 닿을 단방향 발판을 미리 감지해 통과시킵니다.
    /// 충돌 콜백이 먼저 속도를 꺾는 얇은 발판에서도 점프 관성을 보존합니다.
    /// </summary>
    private void PreparePassThroughPlatformsWhileRising()
    {
        if (col == null || rb.linearVelocity.y <= 0.05f) return;

        float castDistance = Mathf.Max(0.08f,
            rb.linearVelocity.y * Time.fixedDeltaTime + 0.05f);
        RaycastHit2D[] hits = Physics2D.CapsuleCastAll(
            col.bounds.center,
            col.bounds.size,
            col.direction,
            0f,
            Vector2.up,
            castDistance,
            groundLayer);

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D platform = hit.collider;
            if (!IsPassThroughPlatform(platform) || ignoredPlatforms.ContainsKey(platform))
                continue;
            if (platform.bounds.center.y <= col.bounds.center.y) continue;

            IgnorePlatform(platform, false);
        }
    }

    private void EnsureCommonHud()
    {
        if (FindAnyObjectByType<PlayerCommonHUD>() != null || commonHudPrefab == null) return;

        HealthBarUI[] healthBars = FindObjectsByType<HealthBarUI>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (HealthBarUI healthBar in healthBars)
        {
            if (healthBar != null && healthBar.gameObject.name == "PlayerHPBar")
                healthBar.gameObject.SetActive(false);
        }

        PlayerCommonHUD hud = Instantiate(commonHudPrefab);
        hud.name = commonHudPrefab.name;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        PassThroughPlatformWhileRising(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        PassThroughPlatformWhileRising(collision);
    }

    private void PassThroughPlatformWhileRising(Collision2D collision)
    {
        if (upwardVelocityBeforePhysics <= 0.05f) return;

        Collider2D platform = collision.collider == col
            ? collision.otherCollider
            : collision.collider;
        if (!IsPassThroughPlatform(platform) || ignoredPlatforms.ContainsKey(platform)) return;

        // 발판이 플레이어 위쪽에 있을 때만 아래에서 위로 통과시킵니다.
        if (platform.bounds.center.y <= col.bounds.center.y) return;

        IgnorePlatform(platform, false);
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, upwardVelocityBeforePhysics);
    }

    private void IgnorePlatform(Collider2D platform, bool droppingDown)
    {
        Physics2D.IgnoreCollision(col, platform, true);
        ignoredPlatforms[platform] = new IgnoredPlatform(droppingDown, Time.time + 1.5f);
    }

    private void UpdateIgnoredPlatforms()
    {
        if (ignoredPlatforms.Count == 0) return;

        ignoredPlatformCleanup.Clear();
        foreach (KeyValuePair<Collider2D, IgnoredPlatform> pair in ignoredPlatforms)
        {
            Collider2D platform = pair.Key;
            if (platform == null)
            {
                ignoredPlatformCleanup.Add(platform);
                continue;
            }

            Bounds playerBounds = col.bounds;
            Bounds platformBounds = platform.bounds;
            bool descendingNearSurface = !pair.Value.DroppingDown
                                         && rb.linearVelocity.y <= 0.05f
                                         && playerBounds.center.y > platformBounds.center.y
                                         && playerBounds.min.y >= platformBounds.max.y - platformLandingSnapDistance;

            if (descendingNearSurface && playerBounds.min.y < platformBounds.max.y + 0.02f)
            {
                float correction = platformBounds.max.y + 0.02f - playerBounds.min.y;
                rb.position += Vector2.up * correction;
            }

            bool cleared = pair.Value.DroppingDown
                ? playerBounds.max.y < platformBounds.min.y - 0.03f
                : playerBounds.min.y > platformBounds.max.y + 0.03f || descendingNearSurface;
            bool safetyExpired = Time.time >= pair.Value.SafetyEndTime
                                 && !playerBounds.Intersects(platformBounds);

            if (cleared || safetyExpired)
                ignoredPlatformCleanup.Add(platform);
        }

        foreach (Collider2D platform in ignoredPlatformCleanup)
        {
            if (platform != null)
                Physics2D.IgnoreCollision(col, platform, false);
            ignoredPlatforms.Remove(platform);
        }
    }

    private void RestoreIgnoredPlatformCollisions()
    {
        foreach (Collider2D platform in ignoredPlatforms.Keys)
        {
            if (col != null && platform != null)
                Physics2D.IgnoreCollision(col, platform, false);
        }
        ignoredPlatforms.Clear();
    }

    private void OnDisable()
    {
        RestoreIgnoredPlatformCollisions();
    }

    private readonly struct IgnoredPlatform
    {
        public readonly bool DroppingDown;
        public readonly float SafetyEndTime;

        public IgnoredPlatform(bool droppingDown, float safetyEndTime)
        {
            DroppingDown = droppingDown;
            SafetyEndTime = safetyEndTime;
        }
    }

    // ───────────────────────── 엎드리기 (↓) ─────────────────────────
    private void HandleCrouch()
    {
        bool wantCrouch = Input.GetKey(KeyCode.DownArrow) && IsGrounded && !IsDashing && knockbackLeft <= 0f;
        if (wantCrouch == IsCrouching) return;

        IsCrouching = wantCrouch;
        SetCrouchVisual(IsCrouching);
    }

    private void SetCrouchVisual(bool crouch)
    {
        col.size = crouch ? crouchSize : standSize;
        col.offset = crouch ? crouchOffset : standOffset;

        if (visual == null) return;

        Vector3 s = visualStandScale;
        s.x = Mathf.Abs(s.x) * Facing;
        if (crouch) s.y *= crouchHeightRatio;
        visual.localScale = s;

        Vector3 p = visualStandPos;
        if (crouch) p.y -= (standSize.y - crouchSize.y) * 0.5f;
        visual.localPosition = p;
    }

    // ───────────────────────── 점프 (↑) ─────────────────────────
    private void HandleJumpInput()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow)) jumpBufferLeft = jumpBufferTime;
        else jumpBufferLeft -= Time.deltaTime;

        if (jumpBufferLeft > 0f && coyoteLeft > 0f && !IsDashing && !IsCrouching && knockbackLeft <= 0f)
        {
            jumpRequested = true;
            jumpBufferLeft = 0f;
            coyoteLeft = 0f;
        }

        if (Input.GetKeyUp(KeyCode.UpArrow) && rb.linearVelocity.y > 0f)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, rb.linearVelocity.y * 0.45f);
    }

    // ───────────────────────── 대시 (R) ─────────────────────────
    private void HandleDash()
    {
        if (dashCooldownLeft > 0f) dashCooldownLeft -= Time.deltaTime;

        bool blockedByKnockback = knockbackLeft > 0f && !allowDashDuringKnockback;

        if (Input.GetKeyDown(KeyCode.R) && !IsDashing && dashCooldownLeft <= 0f && !blockedByKnockback)
            StartDash();

        if (IsDashing)
        {
            dashTimeLeft -= Time.deltaTime;
            if (dashTimeLeft <= 0f) EndDash();
        }
    }

    private void StartDash()
    {
        if (IsCrouching)
        {
            IsCrouching = false;
            SetCrouchVisual(false);
        }

        knockbackLeft = 0f;

        IsDashing = true;
        dashTimeLeft = dashDuration;
        dashCooldownLeft = dashCooldown;
        rb.gravityScale = 0f;

        Debug.Log("DASH");
        AudioManager.Instance?.PlayPlayerDash();
        
        if (PlayerCombatTracker.Instance != null)
            PlayerCombatTracker.Instance.RecordAction(ActionType.Dash);
    }

    private void EndDash()
    {
        IsDashing = false;
        rb.gravityScale = defaultGravity;
    }

    // ───────────────────────── 패링 (E) ─────────────────────────
    private void HandleParry()
    {
        if (parryCooldownLeft > 0f) parryCooldownLeft -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.E) && !IsParrying && !IsDashing && parryCooldownLeft <= 0f)
        {
            IsParrying = true;
            parryTimeLeft = parryWindow;
            parryCooldownLeft = parryCooldown;

            Debug.Log("PARRY!");

            if (PlayerCombatTracker.Instance != null)
                PlayerCombatTracker.Instance.RecordAction(ActionType.Parry);
        }

        if (IsParrying)
        {
            parryTimeLeft -= Time.deltaTime;
            if (parryTimeLeft <= 0f) IsParrying = false;
        }
    }

    // ───────────────────────── 근거리 공격 (Q) ─────────────────────────
    private void HandleMelee()
    {
        if (meleeCooldownLeft > 0f) meleeCooldownLeft -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.Q) && !IsAttacking && !IsDashing && meleeCooldownLeft <= 0f)
            StartCoroutine(MeleeRoutine());
    }

    private IEnumerator MeleeRoutine()
    {
        IsAttacking = true;
        meleeCooldownLeft = meleeCooldown;

        Debug.Log("MELEE");
        AudioManager.Instance?.PlayPlayerMeleeSwing();

        if (PlayerCombatTracker.Instance != null)
            PlayerCombatTracker.Instance.RecordAction(ActionType.Melee);

        yield return new WaitForSeconds(meleeStartDelay);

        if (meleeHitbox != null)
        {
            Vector3 p = meleeHitbox.transform.localPosition;
            p.x = meleeHitboxBaseX * Facing;
            meleeHitbox.transform.localPosition = p;

            meleeHitbox.SetActive(true);
            Physics2D.SyncTransforms();
            meleeDamageZone?.HitCurrentOverlaps();
        }

        yield return new WaitForSeconds(meleeActiveTime);

        if (meleeHitbox != null) meleeHitbox.SetActive(false);

        IsAttacking = false;
    }

    // ───────────────────────── 원거리 공격 (W) ─────────────────────────
    private void HandleRanged()
    {
        if (rangedCooldownLeft > 0f) rangedCooldownLeft -= Time.deltaTime;

        if (Input.GetKeyDown(KeyCode.W) && !IsAttacking && !IsDashing && rangedCooldownLeft <= 0f)
            StartCoroutine(RangedRoutine());
    }

    private IEnumerator RangedRoutine()
    {
        IsAttacking = true;
        rangedCooldownLeft = rangedCooldown;

        Debug.Log("RANGED");

        if (PlayerCombatTracker.Instance != null)
            PlayerCombatTracker.Instance.RecordAction(ActionType.Ranged);

        yield return new WaitForSeconds(rangedStartDelay);

        if (projectilePrefab != null && firePoint != null)
        {
            // 이전 총알이 아직 남아있으면 제거해서 화면/히어라키에 항상 총알이 1개만 존재하도록 합니다
            if (activeBullet != null)
            {
                Destroy(activeBullet);
                activeBullet = null;
            }

            // ★ 신규 ③ : 발사 방향 결정
            Vector2 shootDir = new Vector2(Facing, 0f);
            int spawnSide = Facing;

            if (autoAimTarget != null)
            {
                Vector3 aimPoint = autoAimTarget.position + Vector3.up * autoAimVerticalOffset;
                Vector2 toTarget = (Vector2)(aimPoint - firePoint.position);

                if (toTarget.sqrMagnitude > 0.01f)
                {
                    shootDir = toTarget.normalized;
                    // 보스가 있는 쪽에서 총알이 나가도록 발사 위치도 이동
                    if (Mathf.Abs(toTarget.x) > 0.2f)
                        spawnSide = toTarget.x > 0f ? 1 : -1;
                }
            }

            Vector3 fp = firePoint.localPosition;
            fp.x = firePointBaseX * spawnSide;
            fp.y = IsCrouching ? -0.35f : 0f;
            firePoint.localPosition = fp;

            GameObject go = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            activeBullet = go;
            Projectile p = go.GetComponent<Projectile>();
            if (p != null) p.Launch(shootDir);
            AudioManager.Instance?.PlayPlayerRangedShot();
        }

        IsAttacking = false;
    }

    // ───────────── 에디터 디버그 (게임 동작에는 영향 없음) ─────────────
    private void OnDrawGizmosSelected()
    {
        if (groundCheck == null) return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
