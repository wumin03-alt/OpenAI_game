using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어 / 잡몹 / 보스가 공용으로 사용하는 체력 컴포넌트.
/// </summary>
public class Health : MonoBehaviour
{
    [Header("── HP ──")]
    [SerializeField] private float maxHP = 100f;
    [Tooltip("피격 후 무적 시간. 보스/잡몹은 0으로 두세요")]
    [SerializeField] private float invincibleTime = 0.6f;

    [Header("── 피격 연출 ──")]
    [SerializeField] private bool flashOnDamage = true;
    [SerializeField] private Color flashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float flashInterval = 0.08f;

    [Header("── 사망 처리 ──")]
    [Tooltip("플레이어는 체크 해제, 잡몹은 체크")]
    [SerializeField] private bool destroyOnDeath = true;
    [SerializeField] private float destroyDelay = 0f;
    [Tooltip("사망 시 꺼버릴 컴포넌트 (예: PlayerController, EnemyController)")]
    [SerializeField] private Behaviour[] disableOnDeath;
    [Tooltip("사망 시 Rigidbody2D 속도를 0으로 만들어 미끄러짐을 막습니다")]
    [SerializeField] private bool stopMovementOnDeath = true;
    [Tooltip("사망 후 이 프레임 수만큼 속도를 계속 0으로 유지합니다 (잔여 FixedUpdate 방어)")]
    [SerializeField] private int stopMovementFrames = 6;
    [Tooltip("사망 시 Rigidbody2D Constraints로 X 이동을 완전히 잠급니다")]
    [SerializeField] private bool freezePositionOnDeath = true;

    [Header("── 패링 연동 (플레이어만 체크) ──")]
    [SerializeField] private bool usePlayerParry = false;

    // ── 외부에서 읽는 값 ──
    public float MaxHP => maxHP;
    public float CurrentHP { get; private set; }
    public float Normalized => maxHP <= 0f ? 0f : Mathf.Clamp01(CurrentHP / maxHP);
    public bool IsDead { get; private set; }
    public bool IsInvincible => invincibleLeft > 0f;

    // ── Inspector에서 연결 가능한 이벤트 ──
    public UnityEvent onDamaged;
    public UnityEvent onParrySuccess;
    public UnityEvent onDeath;

    private float invincibleLeft;
    private float flashTimer;
    private bool flashOn;
    private SpriteRenderer[] renderers;
    private Color[] baseColors;
    private PlayerController player;   // 패링 판정용 (없으면 null)
    private Rigidbody2D rb;

    private void Awake()
    {
        CurrentHP = maxHP;

        renderers = GetComponentsInChildren<SpriteRenderer>();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) baseColors[i] = renderers[i].color;

        if (usePlayerParry) player = GetComponent<PlayerController>();

        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (invincibleLeft > 0f)
        {
            invincibleLeft -= Time.deltaTime;
            HandleFlash();
            if (invincibleLeft <= 0f) ResetColor();
        }
    }

    /// <summary>데미지를 입힙니다. 패링/무적 중이면 무시됩니다.</summary>
    public void TakeDamage(float amount, bool ignoreInvincible = false)
    {
        if (IsDead || amount <= 0f) return;

        // 1) 패링 성공 판정 (플레이어 전용)
        if (player != null && player.IsParrying)
        {
            Debug.Log($"[{name}] PARRY SUCCESS! 데미지 {amount} 무효화");
            onParrySuccess?.Invoke();
            return;
        }

        // 2) 무적 판정
        if (!ignoreInvincible && invincibleLeft > 0f) return;

        // 3) 실제 감소
        CurrentHP = Mathf.Max(0f, CurrentHP - amount);
        Debug.Log($"[{name}] -{amount}  →  HP {CurrentHP}/{maxHP}");
        onDamaged?.Invoke();

        if (CurrentHP <= 0f) { Die(); return; }

        if (invincibleTime > 0f)
        {
            invincibleLeft = invincibleTime;
            flashTimer = 0f;
        }
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        CurrentHP = Mathf.Min(maxHP, CurrentHP + amount);
    }

    /// <summary>보스 페이즈 전환 등에서 강제로 HP를 세팅할 때 사용</summary>
    public void SetHP(float value)
    {
        CurrentHP = Mathf.Clamp(value, 0f, maxHP);
    }

    /// <summary>스테이지 스폰 변형용으로 최대 체력을 비율만큼 조정합니다.</summary>
    public void ApplyMaxHPMultiplier(float multiplier)
    {
        maxHP *= Mathf.Max(0.01f, multiplier);
        CurrentHP = Mathf.Min(CurrentHP, maxHP);
    }

    // ───────────────────────── 사망 처리 ─────────────────────────
    private void Die()
    {
        IsDead = true;
        ResetColor();
        Debug.Log($"[{name}] DEAD");

        // ★ 순서 중요 : 이동 스크립트를 먼저 꺼야 속도를 되살리지 못합니다
        if (disableOnDeath != null)
            foreach (var b in disableOnDeath) if (b != null) b.enabled = false;

        StopMovementNow();

        // 이번 프레임에 이미 큐잉된 FixedUpdate가 속도를 되살리는 것을 막습니다
        if (stopMovementOnDeath && isActiveAndEnabled)
            StartCoroutine(HoldStopRoutine());

        onDeath?.Invoke();

        if (destroyOnDeath) Destroy(gameObject, destroyDelay);
    }

    /// <summary>즉시 속도를 0으로 만들고, 옵션에 따라 위치를 잠급니다.</summary>
    private void StopMovementNow()
    {
        if (!stopMovementOnDeath || rb == null) return;
        if (rb.bodyType == RigidbodyType2D.Static) return;

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        if (freezePositionOnDeath && rb.bodyType == RigidbodyType2D.Dynamic)
        {
            // X는 잠그고 Y(중력 낙하)는 남겨 공중에서 죽어도 착지하도록
            rb.constraints = RigidbodyConstraints2D.FreezeRotation
                           | RigidbodyConstraints2D.FreezePositionX;
        }
    }

    /// <summary>사망 직후 몇 프레임 동안 속도를 계속 0으로 유지</summary>
    private IEnumerator HoldStopRoutine()
    {
        int frames = Mathf.Max(1, stopMovementFrames);
        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForFixedUpdate();

            if (rb == null) yield break;
            if (rb.bodyType == RigidbodyType2D.Static) yield break;

            // Y는 남겨 중력 낙하는 유지, X만 확실히 제거
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            rb.angularVelocity = 0f;
        }
    }

    // ───────────────────────── 깜빡임 ─────────────────────────
    private void HandleFlash()
    {
        if (!flashOnDamage || renderers.Length == 0) return;

        flashTimer -= Time.deltaTime;
        if (flashTimer > 0f) return;

        flashTimer = flashInterval;
        flashOn = !flashOn;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].color = flashOn ? flashColor : baseColors[i];
        }
    }

    private void ResetColor()
    {
        flashOn = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            renderers[i].color = baseColors[i];
        }
    }
}
