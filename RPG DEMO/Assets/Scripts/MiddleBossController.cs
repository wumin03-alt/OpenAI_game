using System.Collections;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// F.E.E.D.-6 중간보스. 두 페이즈와 각 세 패턴만 운용합니다.
/// 공용 플레이어 계약은 변경하지 않고 기존 Health/PlayerController를 사용합니다.
/// </summary>
[RequireComponent(typeof(Health), typeof(BossStaggerGauge), typeof(DirectionSequenceEscape))]
public sealed class MiddleBossController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite attackSprite;
    [SerializeField] private GameObject exitGate;
    [SerializeField] private Transform aimTarget;

    [Header("Arena")]
    [SerializeField] private float arenaMinX = -10f;
    [SerializeField] private float arenaMaxX = 10f;
    [SerializeField] private float groundY = -3.65f;

    [Header("Combat")]
    [SerializeField, Min(0.1f)] private float patternCooldown = 1.1f;
    [SerializeField, Min(1f)] private float captureFailureDamage = 42f;
    [SerializeField, Min(1f)] private float nutrientBlockDamage = 13f;
    [SerializeField, Min(1f)] private float nutrientBlockSpeed = 9f;

    public int Phase { get; private set; } = 1;
    public string CurrentPattern { get; private set; } = "BOOT SEQUENCE";

    private readonly List<GameObject> attackVisuals = new List<GameObject>();
    private Health health;
    private BossStaggerGauge staggerGauge;
    private DirectionSequenceEscape escapeSequence;
    private PlayerController player;
    private Health playerHealth;
    private Rigidbody2D playerBody;
    private Coroutine brainRoutine;
    private Coroutine attackRoutine;
    private int lastPattern = -1;
    private bool dead;

    private Canvas hudCanvas;
    private Image healthFill;
    private Text healthText;
    private Text phaseText;
    private Text statusText;
    private Color baseVisualColor = Color.white;

    private void Awake()
    {
        health = GetComponent<Health>();
        staggerGauge = GetComponent<BossStaggerGauge>();
        escapeSequence = GetComponent<DirectionSequenceEscape>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null) baseVisualColor = visual.color;
        if (exitGate != null) exitGate.SetActive(false);

        if (GetComponent<BossStaggerHUD>() == null)
            gameObject.AddComponent<BossStaggerHUD>();

        health.onDeath.AddListener(HandleDeath);
        staggerGauge.StaggerStarted += HandleStaggerStarted;
        staggerGauge.StaggerEnded += HandleStaggerEnded;
    }

    private void Start()
    {
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.GetComponent<PlayerController>();
            playerHealth = playerObject.GetComponent<Health>();
            playerBody = playerObject.GetComponent<Rigidbody2D>();
        }

        BuildHud();
        if (player == null || playerHealth == null)
        {
            Debug.LogError("[MiddleBoss] Player 또는 Health를 찾지 못했습니다.", this);
            enabled = false;
            return;
        }

        brainRoutine = StartCoroutine(BrainLoop());
    }

    private void OnDestroy()
    {
        if (health != null) health.onDeath.RemoveListener(HandleDeath);
        if (staggerGauge != null)
        {
            staggerGauge.StaggerStarted -= HandleStaggerStarted;
            staggerGauge.StaggerEnded -= HandleStaggerEnded;
        }
        if (hudCanvas != null) Destroy(hudCanvas.gameObject);
    }

    private void Update()
    {
        if (health == null) return;

        if (Phase == 1 && health.Normalized <= 0.5f)
        {
            Phase = 2;
            patternCooldown = Mathf.Max(0.65f, patternCooldown * 0.72f);
            CurrentPattern = "DIRECT RECYCLING AUTHORIZED";
            if (phaseText != null)
            {
                phaseText.text = "PHASE 02 // 직접 재활용";
                phaseText.color = new Color(1f, 0.25f, 0.48f);
            }
            if (visual != null) visual.color = new Color(1f, 0.76f, 0.78f, 1f);
            Debug.Log("[MiddleBoss] Phase 2 - 생명체 직접 재활용 승인", this);
        }

        if (healthFill != null)
        {
            healthFill.fillAmount = health.Normalized;
            healthFill.color = Color.Lerp(new Color(0.9f, 0.08f, 0.24f),
                new Color(1f, 0.55f, 0.16f), health.Normalized);
            healthText.text = $"F.E.E.D.-6   {Mathf.CeilToInt(health.CurrentHP)} / {Mathf.CeilToInt(health.MaxHP)}";
        }

        if (statusText != null && !staggerGauge.IsStaggered && !escapeSequence.IsActive && !dead)
            statusText.text = $"PROCESS // {CurrentPattern}";
    }

    private IEnumerator BrainLoop()
    {
        yield return new WaitForSeconds(1.4f);

        while (!dead)
        {
            if (staggerGauge.IsStaggered || escapeSequence.IsActive)
            {
                yield return null;
                continue;
            }

            int pattern = ChoosePattern();
            attackRoutine = StartCoroutine(Phase == 1
                ? RunPhaseOnePattern(pattern)
                : RunPhaseTwoPattern(pattern));
            yield return attackRoutine;
            attackRoutine = null;

            if (!dead && !staggerGauge.IsStaggered)
                yield return new WaitForSeconds(patternCooldown);
        }
    }

    private int ChoosePattern()
    {
        int selected;
        do selected = Random.Range(0, 3);
        while (selected == lastPattern);
        lastPattern = selected;
        return selected;
    }

    private IEnumerator RunPhaseOnePattern(int pattern)
    {
        switch (pattern)
        {
            case 0: yield return SuctionPattern(); break;
            case 1: yield return SortingClawPattern(); break;
            default: yield return NutritionBlockPattern(false); break;
        }
    }

    private IEnumerator RunPhaseTwoPattern(int pattern)
    {
        switch (pattern)
        {
            case 0: yield return ForcedTransferPattern(); break;
            case 1: yield return ForceFeedPattern(); break;
            default: yield return CompressionDistributionPattern(); break;
        }
    }

    private IEnumerator SuctionPattern()
    {
        CurrentPattern = "원료 흡입";
        GameObject field = CreateWorldRect("SuctionTelegraph",
            new Vector2((transform.position.x + arenaMinX) * 0.5f, groundY + 1.5f),
            new Vector2(Mathf.Abs(transform.position.x - arenaMinX), 4.2f),
            new Color(0.15f, 0.9f, 1f, 0.22f), -2);
        yield return WaitInterruptible(0.75f);

        float time = 1.8f;
        while (time > 0f && CanAttack())
        {
            time -= Time.deltaTime;
            if (playerBody != null)
            {
                float targetVelocity = Mathf.Sign(transform.position.x - player.transform.position.x) * 8f;
                Vector2 velocity = playerBody.linearVelocity;
                velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity, 18f * Time.deltaTime);
                playerBody.linearVelocity = velocity;
            }
            yield return null;
        }

        if (CanAttack() && Mathf.Abs(player.transform.position.x - transform.position.x) < 2.3f)
            playerHealth.TakeDamage(15f);
        DestroyAttackVisual(field);
    }

    private IEnumerator SortingClawPattern()
    {
        CurrentPattern = "자동 선별 집게";
        float targetX = Mathf.Clamp(player.transform.position.x, arenaMinX + 1f, arenaMaxX - 1f);
        GameObject marker = CreateWorldRect("ClawTarget",
            new Vector2(targetX, groundY + 0.08f), new Vector2(2.8f, 0.18f),
            new Color(1f, 0.65f, 0.16f, 0.86f), 15);
        yield return WaitInterruptible(0.85f);
        if (!CanAttack()) yield break;

        GameObject claw = CreateWorldRect("SortingClaw",
            new Vector2(targetX, groundY + 3.3f), new Vector2(2.2f, 6.5f),
            new Color(1f, 0.22f, 0.42f, 0.72f), 14);
        if (Mathf.Abs(player.transform.position.x - targetX) <= 1.25f &&
            player.transform.position.y <= groundY + 3.4f)
            TryCapturePlayer();

        yield return WaitInterruptible(0.55f);
        DestroyAttackVisual(marker);
        DestroyAttackVisual(claw);
    }

    private IEnumerator NutritionBlockPattern(bool fast)
    {
        CurrentPattern = "영양 블록 배급";
        GameObject warning = CreateWorldRect("BlockWarning",
            new Vector2(transform.position.x - 1.7f, transform.position.y + 1f),
            new Vector2(0.35f, 2.7f), new Color(0.72f, 0.2f, 1f, 0.78f), 18);
        yield return WaitInterruptible(fast ? 0.45f : 0.7f);
        DestroyAttackVisual(warning);

        for (int i = 0; i < 3 && CanAttack(); i++)
        {
            FireNutrientBlock(fast ? 11.5f : nutrientBlockSpeed);
            yield return WaitInterruptible(fast ? 0.3f : 0.48f);
        }
    }

    private IEnumerator ForcedTransferPattern()
    {
        CurrentPattern = "강제 이송";
        GameObject belt = CreateWorldRect("ForcedConveyor",
            new Vector2(0f, groundY - 0.12f), new Vector2(arenaMaxX - arenaMinX, 0.32f),
            new Color(1f, 0.32f, 0.18f, 0.68f), 10);
        yield return WaitInterruptible(0.6f);

        float time = 2.25f;
        while (time > 0f && CanAttack())
        {
            time -= Time.deltaTime;
            if (playerBody != null)
            {
                Vector2 velocity = playerBody.linearVelocity;
                velocity.x = Mathf.MoveTowards(velocity.x, 10f, 22f * Time.deltaTime);
                playerBody.linearVelocity = velocity;
            }

            if (Mathf.Abs(player.transform.position.x - transform.position.x) < 2.4f)
            {
                TryCapturePlayer();
                break;
            }
            yield return null;
        }
        DestroyAttackVisual(belt);
    }

    private IEnumerator ForceFeedPattern()
    {
        CurrentPattern = "강제 급식";
        float beamY = player.transform.position.y;
        GameObject warning = CreateWorldRect("FeedWarning",
            new Vector2((transform.position.x + arenaMinX) * 0.5f, beamY),
            new Vector2(Mathf.Abs(transform.position.x - arenaMinX), 0.18f),
            new Color(1f, 0.7f, 0.18f, 0.82f), 16);
        yield return WaitInterruptible(0.75f);
        if (!CanAttack()) yield break;

        warning.transform.localScale = new Vector3(warning.transform.localScale.x, 1.4f, 1f);
        SpriteRenderer renderer = warning.GetComponent<SpriteRenderer>();
        renderer.color = new Color(0.42f, 1f, 0.2f, 0.78f);
        if (Mathf.Abs(player.transform.position.y - beamY) < 0.8f &&
            player.transform.position.x < transform.position.x)
            playerHealth.TakeDamage(22f);
        yield return WaitInterruptible(0.55f);
        DestroyAttackVisual(warning);
    }

    private IEnumerator CompressionDistributionPattern()
    {
        CurrentPattern = "과부하 압축 배급";
        GameObject leftPress = CreateWorldRect("LeftPress",
            new Vector2(arenaMinX + 0.6f, groundY + 2.3f), new Vector2(1.2f, 6f),
            new Color(1f, 0.2f, 0.4f, 0.74f), 14);
        GameObject rightPress = CreateWorldRect("RightPress",
            new Vector2(arenaMaxX - 0.6f, groundY + 2.3f), new Vector2(1.2f, 6f),
            new Color(1f, 0.2f, 0.4f, 0.74f), 14);
        yield return WaitInterruptible(0.65f);

        float moveTime = 0.85f;
        while (moveTime > 0f && CanAttack())
        {
            moveTime -= Time.deltaTime;
            float step = 8.2f * Time.deltaTime;
            leftPress.transform.position += Vector3.right * step;
            rightPress.transform.position += Vector3.left * step;
            yield return null;
        }

        if (CanAttack() && player.transform.position.x > leftPress.transform.position.x - 0.8f &&
            player.transform.position.x < rightPress.transform.position.x + 0.8f)
            playerHealth.TakeDamage(26f);

        DestroyAttackVisual(leftPress);
        DestroyAttackVisual(rightPress);
        yield return NutritionBlockPattern(true);
    }

    private void FireNutrientBlock(float speed)
    {
        GameObject block = new GameObject("NutrientBlock");
        block.transform.position = transform.position + new Vector3(-2.3f, 0.8f, 0f);

        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = attackSprite;
        SetWorldSize(block.transform, attackSprite, new Vector2(0.72f, 0.72f));
        renderer.color = new Color(0.72f, 0.18f, 1f, 1f);
        renderer.sortingOrder = 20;

        Rigidbody2D body = block.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = attackSprite != null ? attackSprite.bounds.size : Vector2.one;

        Vector2 direction = player != null
            ? (player.transform.position + Vector3.up * 0.6f - block.transform.position).normalized
            : Vector2.left;
        NutrientBlockProjectile projectile = block.AddComponent<NutrientBlockProjectile>();
        projectile.Initialize(staggerGauge, direction, speed, nutrientBlockDamage);
    }

    private bool TryCapturePlayer()
    {
        if (player == null || playerHealth == null || escapeSequence.IsActive || player.IsParrying)
            return false;

        player.transform.position = transform.position + new Vector3(-2.25f, 0.65f, 0f);
        bool started = escapeSequence.BeginEscape(player, playerHealth,
            HandleEscapeSuccess, HandleEscapeFailure);
        if (started && statusText != null)
        {
            statusText.text = "CAPTURED // 방향키 4개를 입력하여 탈출";
            statusText.color = new Color(1f, 0.68f, 0.18f);
        }
        return started;
    }

    private void HandleEscapeSuccess()
    {
        PushPlayerAway(5f);
        if (statusText != null) statusText.color = new Color(0.25f, 1f, 0.68f);
    }

    private void HandleEscapeFailure()
    {
        if (playerHealth != null && !playerHealth.IsDead)
            playerHealth.TakeDamage(captureFailureDamage, true);
        PushPlayerAway(8f);
        if (statusText != null) statusText.color = new Color(1f, 0.24f, 0.38f);
    }

    private void PushPlayerAway(float force)
    {
        if (playerBody == null) return;
        playerBody.linearVelocity = Vector2.zero;
        playerBody.AddForce(new Vector2(-force, force * 0.45f), ForceMode2D.Impulse);
    }

    private void HandleStaggerStarted(float duration)
    {
        if (attackRoutine != null)
        {
            StopCoroutine(attackRoutine);
            attackRoutine = null;
        }
        escapeSequence.Cancel(true);
        CleanupAttackVisuals();
        CurrentPattern = $"GROGGY {duration:0}s";
        if (visual != null) visual.color = new Color(0.55f, 0.25f, 1f, 1f);
        if (statusText != null)
        {
            statusText.text = "CORE OVERLOAD // 10초간 공격 가능";
            statusText.color = new Color(0.72f, 0.3f, 1f);
        }
    }

    private void HandleStaggerEnded()
    {
        if (dead) return;
        if (visual != null) visual.color = Phase == 1
            ? baseVisualColor
            : new Color(1f, 0.76f, 0.78f, 1f);
        CurrentPattern = "PROCESS RESUMED";
    }

    private void HandleDeath()
    {
        if (dead) return;
        dead = true;
        CurrentPattern = "OBJECTIVE CONFLICT";
        if (brainRoutine != null) StopCoroutine(brainRoutine);
        if (attackRoutine != null) StopCoroutine(attackRoutine);
        escapeSequence.Cancel(true);
        CleanupAttackVisuals();

        gameObject.tag = "Untagged";
        if (aimTarget != null) aimTarget.gameObject.tag = "Untagged";
        if (visual != null) visual.color = new Color(0.24f, 0.3f, 0.34f, 1f);
        if (exitGate != null) exitGate.SetActive(true);

        if (phaseText != null)
        {
            phaseText.text = "FINAL OUTPUT: 1  //  배급 대상: 0";
            phaseText.color = new Color(0.25f, 1f, 0.68f);
        }
        if (statusText != null)
        {
            statusText.text = "목표 달성 여부: 계산 불가 // 우측 출구 개방";
            statusText.color = new Color(0.25f, 1f, 0.68f);
        }
    }

    private IEnumerator WaitInterruptible(float duration)
    {
        float remaining = duration;
        while (remaining > 0f && CanAttack())
        {
            remaining -= Time.deltaTime;
            yield return null;
        }
    }

    private bool CanAttack()
    {
        return !dead && !staggerGauge.IsStaggered && !escapeSequence.IsActive &&
               playerHealth != null && !playerHealth.IsDead;
    }

    private GameObject CreateWorldRect(string objectName, Vector2 position, Vector2 scale,
        Color color, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.position = position;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = attackSprite;
        SetWorldSize(go.transform, attackSprite, scale);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        attackVisuals.Add(go);
        return go;
    }

    private static void SetWorldSize(Transform target, Sprite sprite, Vector2 worldSize)
    {
        Vector2 nativeSize = sprite != null ? sprite.bounds.size : Vector2.one;
        target.localScale = new Vector3(
            worldSize.x / Mathf.Max(nativeSize.x, 0.001f),
            worldSize.y / Mathf.Max(nativeSize.y, 0.001f),
            1f);
    }

    private void DestroyAttackVisual(GameObject visualObject)
    {
        if (visualObject == null) return;
        attackVisuals.Remove(visualObject);
        Destroy(visualObject);
    }

    private void CleanupAttackVisuals()
    {
        foreach (GameObject visualObject in attackVisuals)
            if (visualObject != null) Destroy(visualObject);
        attackVisuals.Clear();
    }

    private void BuildHud()
    {
        hudCanvas = RuntimeUIFactory.CreateCanvas("MiddleBossCombatCanvas", null, 220);

        Image panel = RuntimeUIFactory.CreateImage(hudCanvas.transform, "MiddleBossPanel",
            new Color(0.02f, 0.035f, 0.07f, 0.94f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -20f);
        panelRect.sizeDelta = new Vector2(940f, 72f);

        phaseText = RuntimeUIFactory.CreateText(panelRect, "PHASE 01 // 정상 생산", 19,
            new Vector2(-325f, 20f), new Vector2(270f, 28f), new Color(0.2f, 0.9f, 1f));
        phaseText.alignment = TextAnchor.MiddleLeft;
        phaseText.fontStyle = FontStyle.Bold;

        healthText = RuntimeUIFactory.CreateText(panelRect, "F.E.E.D.-6", 21,
            new Vector2(0f, 20f), new Vector2(360f, 28f), Color.white);
        healthText.fontStyle = FontStyle.Bold;

        statusText = RuntimeUIFactory.CreateText(panelRect, "PROCESS // BOOT SEQUENCE", 17,
            new Vector2(325f, 20f), new Vector2(270f, 28f), new Color(1f, 0.72f, 0.28f));
        statusText.alignment = TextAnchor.MiddleRight;

        Image healthBackground = RuntimeUIFactory.CreateImage(panelRect, "BossHealthBackground",
            new Color(0.08f, 0.1f, 0.15f, 1f));
        RectTransform barRect = healthBackground.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(0f, -18f);
        barRect.sizeDelta = new Vector2(880f, 18f);

        healthFill = RuntimeUIFactory.CreateImage(barRect, "BossHealthFill",
            new Color(1f, 0.52f, 0.16f));
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = 0;
        RuntimeUIFactory.Stretch(healthFill.rectTransform, 2f, -2f, 2f, -2f);
    }
}
