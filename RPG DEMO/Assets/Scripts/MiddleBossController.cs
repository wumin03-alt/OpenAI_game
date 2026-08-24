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
[DefaultExecutionOrder(200)]
public sealed class MiddleBossController : MonoBehaviour
{
    [Header("Scene references")]
    [SerializeField] private SpriteRenderer visual;
    [SerializeField] private Sprite attackSprite;
    [SerializeField] private Sprite nutrientBlockSprite;
    [SerializeField] private GameObject exitGate;
    [SerializeField] private Transform aimTarget;

    [Header("Attack art")]
    [SerializeField] private Sprite[] groundClawFrames;
    [SerializeField] private Sprite[] suctionFrames;
    [SerializeField] private Sprite[] feedJetFrames;
    [SerializeField] private Sprite[] pressFrames;
    [SerializeField] private Sprite[] conveyorFrames;

    [Header("Arena")]
    [SerializeField] private float arenaMinX = -10f;
    [SerializeField] private float arenaMaxX = 10f;
    [SerializeField] private float groundY = -3.75f;

    [Header("Combat")]
    [SerializeField, Min(0.1f)] private float patternCooldown = 1.55f;
    [SerializeField, Min(1f)] private float captureFailureDamage = 36f;
    [SerializeField, Min(1f)] private float nutrientBlockDamage = 9f;
    [SerializeField, Min(1f)] private float nutrientBlockSpeed = 7.2f;
    [SerializeField, Min(0.1f)] private float suctionTickDamage = 6f;
    [SerializeField, Min(0.1f)] private float suctionTickInterval = 0.65f;
    [SerializeField, Min(0.1f)] private float conveyorCarrySpeed = 5.2f;

    public int Phase { get; private set; } = 1;
    public string CurrentPattern { get; private set; } = "BOOT SEQUENCE";

    private readonly List<GameObject> attackVisuals = new List<GameObject>();
    private readonly Dictionary<SpriteRenderer, Color> bossBaseColors =
        new Dictionary<SpriteRenderer, Color>();
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
    private bool conveyorActive;

    private Canvas hudCanvas;
    private Image healthFill;
    private Text healthText;
    private Text phaseText;
    private Text statusText;

    private void Awake()
    {
        health = GetComponent<Health>();
        staggerGauge = GetComponent<BossStaggerGauge>();
        escapeSequence = GetComponent<DirectionSequenceEscape>();
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        CacheBossRenderers();
        if (exitGate != null) exitGate.SetActive(false);

        if (GetComponent<BossStaggerHUD>() == null)
            gameObject.AddComponent<BossStaggerHUD>();
        if (GetComponent<BossParryMiniGameBridge>() == null)
            gameObject.AddComponent<BossParryMiniGameBridge>();

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
            patternCooldown = Mathf.Max(1.05f, patternCooldown * 0.82f);
            CurrentPattern = "DIRECT RECYCLING AUTHORIZED";
            if (phaseText != null)
            {
                phaseText.text = "2페이즈 · 직접 재활용";
                phaseText.color = new Color(1f, 0.25f, 0.48f);
            }
            SetBossTint(new Color(1f, 0.76f, 0.78f, 1f));
            Debug.Log("[MiddleBoss] Phase 2 - 생명체 직접 재활용 승인", this);
        }

        if (healthFill != null)
        {
            MiddleBossUIStyle.HorizontalFill(healthFill, health.Normalized, 4f);
            healthFill.color = Color.Lerp(new Color(0.9f, 0.08f, 0.24f),
                new Color(1f, 0.55f, 0.16f), health.Normalized);
            healthText.text = $"F.E.E.D.-6   {Mathf.CeilToInt(health.CurrentHP)} / {Mathf.CeilToInt(health.MaxHP)}";
        }

        if (statusText != null && !staggerGauge.IsStaggered && !escapeSequence.IsActive && !dead)
            statusText.text = $"패턴 · {CurrentPattern}";
    }

    private void FixedUpdate()
    {
        if (!conveyorActive || playerBody == null || !CanAttack() || !IsPlayerOnGroundRoute())
            return;

        Vector2 velocity = playerBody.linearVelocity;
        velocity.x = Mathf.Max(velocity.x, conveyorCarrySpeed);
        playerBody.linearVelocity = velocity;
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
        GameObject telegraph = CreateWorldRect("SuctionTelegraph",
            new Vector2((transform.position.x + arenaMinX) * 0.5f, groundY + 1.5f),
            new Vector2(Mathf.Abs(transform.position.x - arenaMinX), 4.2f),
            new Color(0.15f, 0.9f, 1f, 0.12f), -2);
        yield return WaitInterruptible(0.95f);
        if (!CanAttack())
        {
            DestroyAttackVisual(telegraph);
            yield break;
        }

        DestroyAttackVisual(telegraph);
        GameObject field = CreateWorldSprite("SuctionVortex",
            new Vector2((transform.position.x + arenaMinX) * 0.5f, groundY + 1.25f),
            new Vector2(Mathf.Abs(transform.position.x - arenaMinX), 4.3f), suctionFrames, 17);

        float time = 1.35f;
        float elapsed = 0f;
        float nextDamageAt = 0.25f;
        while (time > 0f && CanAttack())
        {
            time -= Time.deltaTime;
            elapsed += Time.deltaTime;
            SetEffectFrame(field, suctionFrames, elapsed, 0.11f, true);
            bool playerIsOnGroundRoute = IsPlayerOnGroundRoute();
            if (playerBody != null && playerIsOnGroundRoute)
            {
                float targetVelocity = Mathf.Sign(transform.position.x - player.transform.position.x) * 5.5f;
                Vector2 velocity = playerBody.linearVelocity;
                velocity.x = Mathf.MoveTowards(velocity.x, targetVelocity, 12f * Time.deltaTime);
                playerBody.linearVelocity = velocity;
            }

            if (playerIsOnGroundRoute && elapsed >= nextDamageAt)
            {
                playerHealth.TakeDamage(suctionTickDamage);
                nextDamageAt += suctionTickInterval;
            }
            yield return null;
        }

        if (CanAttack() && IsPlayerOnGroundRoute() &&
            Mathf.Abs(player.transform.position.x - transform.position.x) < 1.9f)
            playerHealth.TakeDamage(10f);
        DestroyAttackVisual(field);
    }

    private IEnumerator SortingClawPattern()
    {
        CurrentPattern = "자동 선별 집게";
        float targetX = Mathf.Clamp(player.transform.position.x, arenaMinX + 1f, arenaMaxX - 1f);
        GameObject marker = CreateWorldRect("ClawTarget",
            new Vector2(targetX, groundY + 0.08f), new Vector2(2.4f, 0.18f),
            new Color(1f, 0.65f, 0.16f, 0.86f), 15);
        yield return WaitInterruptible(1.15f);
        if (!CanAttack())
        {
            DestroyAttackVisual(marker);
            yield break;
        }

        DestroyAttackVisual(marker);
        GameObject claw = CreateWorldSprite("GroundClawAttack",
            new Vector2(targetX, groundY + 2.15f), new Vector2(4.35f, 4.35f),
            groundClawFrames, 19);
        float elapsed = 0f;
        bool captureChecked = false;
        while (elapsed < 1.2f && CanAttack())
        {
            elapsed += Time.deltaTime;
            SetEffectFrame(claw, groundClawFrames, elapsed, 0.18f, false);
            if (!captureChecked && elapsed >= 0.68f)
            {
                captureChecked = true;
                if (Mathf.Abs(player.transform.position.x - targetX) <= 1.05f &&
                    player.transform.position.y <= groundY + 3.4f)
                    TryCapturePlayer();
            }
            yield return null;
        }
        DestroyAttackVisual(marker);
        DestroyAttackVisual(claw);
    }

    private IEnumerator NutritionBlockPattern(bool fast)
    {
        CurrentPattern = "영양 블록 배급";
        GameObject warning = CreateWorldRect("BlockWarning",
            new Vector2(transform.position.x - 1.7f, transform.position.y + 1f),
            new Vector2(0.35f, 2.7f), new Color(0.72f, 0.2f, 1f, 0.78f), 18);
        yield return WaitInterruptible(fast ? 0.65f : 0.9f);
        DestroyAttackVisual(warning);

        int blockCount = fast ? 3 : 2;
        for (int i = 0; i < blockCount && CanAttack(); i++)
        {
            FireNutrientBlock(fast ? 9.2f : nutrientBlockSpeed);
            yield return WaitInterruptible(fast ? 0.42f : 0.62f);
        }
    }

    private IEnumerator ForcedTransferPattern()
    {
        CurrentPattern = "강제 이송";
        List<GameObject> beltSegments = new List<GameObject>();
        for (float x = arenaMinX + 1.5f; x < arenaMaxX; x += 3f)
            beltSegments.Add(CreateWorldSprite("ForcedConveyorSegment",
                new Vector2(x, groundY + 0.12f), new Vector2(3.25f, 0.78f),
                conveyorFrames, 10));
        yield return WaitInterruptible(0.85f);
        if (!CanAttack())
        {
            foreach (GameObject segment in beltSegments)
                DestroyAttackVisual(segment);
            yield break;
        }

        conveyorActive = true;
        float time = 1.8f;
        float elapsed = 0f;
        while (time > 0f && CanAttack())
        {
            time -= Time.deltaTime;
            elapsed += Time.deltaTime;
            foreach (GameObject segment in beltSegments)
                SetEffectFrame(segment, conveyorFrames, elapsed, 0.09f, true);
            bool playerIsOnGroundRoute = IsPlayerOnGroundRoute();

            if (playerIsOnGroundRoute && Mathf.Abs(player.transform.position.x - transform.position.x) < 2f)
            {
                TryCapturePlayer();
                break;
            }
            yield return null;
        }
        conveyorActive = false;
        foreach (GameObject segment in beltSegments)
            DestroyAttackVisual(segment);
    }

    private IEnumerator ForceFeedPattern()
    {
        CurrentPattern = "강제 급식";
        float beamY = player.transform.position.y;
        GameObject warning = CreateWorldRect("FeedWarning",
            new Vector2((transform.position.x + arenaMinX) * 0.5f, beamY),
            new Vector2(Mathf.Abs(transform.position.x - arenaMinX), 0.18f),
            new Color(1f, 0.7f, 0.18f, 0.82f), 16);
        yield return WaitInterruptible(1.05f);
        if (!CanAttack())
        {
            DestroyAttackVisual(warning);
            yield break;
        }

        DestroyAttackVisual(warning);
        GameObject jet = CreateWorldSprite("ForceFeedJet",
            new Vector2((transform.position.x + arenaMinX) * 0.5f, beamY),
            new Vector2(Mathf.Abs(transform.position.x - arenaMinX), 2.4f), feedJetFrames, 18);
        if (Mathf.Abs(player.transform.position.y - beamY) < 0.8f &&
            player.transform.position.x < transform.position.x)
            playerHealth.TakeDamage(15f);
        float elapsed = 0f;
        while (elapsed < 0.62f && CanAttack())
        {
            elapsed += Time.deltaTime;
            SetEffectFrame(jet, feedJetFrames, elapsed, 0.12f, true);
            yield return null;
        }
        DestroyAttackVisual(jet);
    }

    private IEnumerator CompressionDistributionPattern()
    {
        CurrentPattern = "과부하 압축 배급";
        GameObject leftPress = CreateWorldSprite("LeftPress",
            new Vector2(arenaMinX + 0.8f, groundY + 0.72f), new Vector2(2.5f, 1.65f),
            pressFrames, 18);
        GameObject rightPress = CreateWorldSprite("RightPress",
            new Vector2(arenaMaxX - 0.8f, groundY + 0.72f), new Vector2(2.5f, 1.65f),
            pressFrames, 18);
        SpriteRenderer rightRenderer = rightPress.GetComponent<SpriteRenderer>();
        if (rightRenderer != null) rightRenderer.flipX = true;
        yield return WaitInterruptible(0.9f);

        float moveTime = 0.85f;
        float elapsed = 0f;
        while (moveTime > 0f && CanAttack())
        {
            moveTime -= Time.deltaTime;
            elapsed += Time.deltaTime;
            SetEffectFrame(leftPress, pressFrames, elapsed, 0.12f, true);
            SetEffectFrame(rightPress, pressFrames, elapsed, 0.12f, true);
            float step = 5.8f * Time.deltaTime;
            leftPress.transform.position += Vector3.right * step;
            rightPress.transform.position += Vector3.left * step;
            yield return null;
        }

        if (CanAttack() && IsPlayerOnGroundRoute(1.35f) &&
            player.transform.position.x > leftPress.transform.position.x - 0.8f &&
            player.transform.position.x < rightPress.transform.position.x + 0.8f)
            playerHealth.TakeDamage(18f);

        DestroyAttackVisual(leftPress);
        DestroyAttackVisual(rightPress);
        yield return NutritionBlockPattern(true);
    }

    private void FireNutrientBlock(float speed)
    {
        GameObject block = new GameObject("NutrientBlock");
        block.transform.position = transform.position + new Vector3(-2.3f, 0.8f, 0f);

        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        Sprite blockSprite = nutrientBlockSprite != null ? nutrientBlockSprite : attackSprite;
        renderer.sprite = blockSprite;
        SetWorldSize(block.transform, blockSprite, new Vector2(0.9f, 0.9f));
        renderer.color = Color.white;
        renderer.sortingOrder = 20;

        Rigidbody2D body = block.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = blockSprite != null ? (Vector2)blockSprite.bounds.size : Vector2.one;

        Vector2 direction = player != null
            ? (Vector2)(player.transform.position + Vector3.up * 0.6f - block.transform.position).normalized
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
        conveyorActive = false;
        CleanupAttackVisuals();
        CurrentPattern = $"GROGGY {duration:0}s";
        SetBossTint(new Color(0.55f, 0.25f, 1f, 1f));
        if (statusText != null)
        {
            statusText.text = "CORE OVERLOAD // 10초간 공격 가능";
            statusText.color = new Color(0.72f, 0.3f, 1f);
        }
    }

    private void HandleStaggerEnded()
    {
        if (dead) return;
        if (Phase == 1) RestoreBossTint();
        else SetBossTint(new Color(1f, 0.76f, 0.78f, 1f));
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
        conveyorActive = false;
        CleanupAttackVisuals();

        gameObject.tag = "Untagged";
        if (aimTarget != null) aimTarget.gameObject.tag = "Untagged";
        SetBossTint(new Color(0.24f, 0.3f, 0.34f, 1f));
        foreach (Collider2D bossCollider in GetComponentsInChildren<Collider2D>())
            bossCollider.enabled = false;
        if (exitGate != null) exitGate.SetActive(true);
        StartCoroutine(FadeOutDefeatedBoss());

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

    private bool IsPlayerOnGroundRoute(float height = 1.7f)
    {
        if (player == null) return false;
        float playerY = playerBody != null ? playerBody.position.y : player.transform.position.y;
        return playerY < groundY + height;
    }

    private void CacheBossRenderers()
    {
        bossBaseColors.Clear();
        foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            bossBaseColors[renderer] = renderer.color;
    }

    private void SetBossTint(Color color)
    {
        foreach (SpriteRenderer renderer in bossBaseColors.Keys)
            if (renderer != null) renderer.color = color;
    }

    private void RestoreBossTint()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> entry in bossBaseColors)
            if (entry.Key != null) entry.Key.color = entry.Value;
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

    private GameObject CreateWorldSprite(string objectName, Vector2 position, Vector2 worldSize,
        Sprite[] frames, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.position = position;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        Sprite firstFrame = frames != null && frames.Length > 0 ? frames[0] : attackSprite;
        renderer.sprite = firstFrame;
        SetWorldSize(go.transform, firstFrame, worldSize);
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
        attackVisuals.Add(go);
        return go;
    }

    private static void SetEffectFrame(GameObject effect, Sprite[] frames, float elapsed,
        float secondsPerFrame, bool loop)
    {
        if (effect == null || frames == null || frames.Length == 0) return;
        int frame = Mathf.FloorToInt(elapsed / Mathf.Max(secondsPerFrame, 0.01f));
        frame = loop ? frame % frames.Length : Mathf.Min(frame, frames.Length - 1);
        SpriteRenderer renderer = effect.GetComponent<SpriteRenderer>();
        if (renderer != null && frames[frame] != null) renderer.sprite = frames[frame];
    }

    private IEnumerator FadeOutDefeatedBoss()
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        Color[] startColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) startColors[i] = renderers[i].color;

        float elapsed = 0f;
        const float duration = 0.75f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < renderers.Length; i++)
            {
                Color color = startColors[i];
                color.a *= alpha;
                renderers[i].color = color;
            }
            yield return null;
        }

        foreach (SpriteRenderer renderer in renderers)
            renderer.enabled = false;
    }

    private static void SetWorldSize(Transform target, Sprite sprite, Vector2 worldSize)
    {
        Vector2 nativeSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
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

        Image panel = RuntimeUIFactory.CreateImage(hudCanvas.transform, "MiddleBossNamePlate",
            new Color(0.18f, 0.07f, 0.06f, 0.96f));
        MiddleBossUIStyle.Rounded(panel, new Color(0.18f, 0.07f, 0.06f, 0.96f));
        MiddleBossUIStyle.Outline(panel, new Color(1f, 0.78f, 0.28f, 0.95f), 3f);
        MiddleBossUIStyle.Shadow(panel, new Color(0f, 0f, 0f, 0.72f), 6f);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -18f);
        panelRect.sizeDelta = new Vector2(850f, 104f);

        Image innerPanel = RuntimeUIFactory.CreateImage(panelRect, "CreamInlay",
            new Color(0.98f, 0.86f, 0.57f, 0.98f));
        MiddleBossUIStyle.Rounded(innerPanel, new Color(0.98f, 0.86f, 0.57f, 0.98f));
        RuntimeUIFactory.Stretch(innerPanel.rectTransform, 7f, -7f, 7f, -7f);

        Image titleRibbon = RuntimeUIFactory.CreateImage(innerPanel.rectTransform, "TitleRibbon",
            new Color(0.34f, 0.12f, 0.1f, 1f));
        MiddleBossUIStyle.Rounded(titleRibbon, new Color(0.34f, 0.12f, 0.1f, 1f));
        RectTransform ribbonRect = titleRibbon.rectTransform;
        ribbonRect.anchorMin = ribbonRect.anchorMax = new Vector2(0.5f, 1f);
        ribbonRect.pivot = new Vector2(0.5f, 1f);
        ribbonRect.anchoredPosition = new Vector2(0f, -7f);
        ribbonRect.sizeDelta = new Vector2(800f, 39f);

        phaseText = RuntimeUIFactory.CreateText(ribbonRect, "1페이즈 · 정상 생산", 18,
            new Vector2(-285f, 0f), new Vector2(230f, 31f), new Color(1f, 0.82f, 0.36f));
        phaseText.alignment = TextAnchor.MiddleLeft;
        phaseText.fontStyle = FontStyle.Bold;

        healthText = RuntimeUIFactory.CreateText(ribbonRect, "F.E.E.D.-6", 23,
            new Vector2(0f, 0f), new Vector2(350f, 32f), Color.white);
        healthText.fontStyle = FontStyle.Bold;

        statusText = RuntimeUIFactory.CreateText(ribbonRect, "가동 준비", 17,
            new Vector2(285f, 0f), new Vector2(230f, 31f), new Color(1f, 0.75f, 0.42f));
        statusText.alignment = TextAnchor.MiddleRight;

        Image healthBackground = RuntimeUIFactory.CreateImage(innerPanel.rectTransform, "BossHealthBackground",
            new Color(0.25f, 0.1f, 0.09f, 1f));
        MiddleBossUIStyle.Rounded(healthBackground, new Color(0.25f, 0.1f, 0.09f, 1f));
        MiddleBossUIStyle.Outline(healthBackground, new Color(0.55f, 0.27f, 0.12f, 1f), 1f);
        RectTransform barRect = healthBackground.rectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
        barRect.anchoredPosition = new Vector2(0f, -26f);
        barRect.sizeDelta = new Vector2(790f, 30f);

        healthFill = RuntimeUIFactory.CreateImage(barRect, "BossHealthFill",
            new Color(1f, 0.42f, 0.22f));
        MiddleBossUIStyle.Rounded(healthFill, new Color(1f, 0.42f, 0.22f));
        MiddleBossUIStyle.HorizontalFill(healthFill, 1f, 4f);
    }
}
