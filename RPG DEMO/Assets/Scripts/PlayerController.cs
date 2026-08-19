using System.Collections;
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

    private float meleeCooldownLeft;
    private float meleeHitboxBaseX;
    private float rangedCooldownLeft;
    private float firePointBaseX;

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
        HandleCrouch();
        HandleJumpInput();
        HandleDash();
        HandleParry();
        HandleMelee();
        HandleRanged();
    }

    private void FixedUpdate()
    {

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
        IsGrounded = groundCheck != null &&
                     Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer) != null;

        coyoteLeft = IsGrounded ? coyoteTime : coyoteLeft - Time.deltaTime;
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
