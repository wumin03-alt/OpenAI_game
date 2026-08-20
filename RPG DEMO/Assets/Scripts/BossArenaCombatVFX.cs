using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// BossArena 전용 전투 피드백 레이어입니다.
/// 공용 Player/Projectile 프리팹을 수정하지 않고 런타임 인스턴스에만 연출을 더합니다.
/// </summary>
public class BossArenaCombatVFX : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private PlayerController player;
    [SerializeField] private BossController boss;
    [SerializeField] private PlayerCombatTracker tracker;

    [Header("픽셀 VFX")]
    [SerializeField] private Sprite playerRangedBurst;
    [SerializeField] private Sprite bossCoreCharge;
    [SerializeField] private Sprite parrySuccessRing;

    [Header("표시")]
    [SerializeField] private int sortingOrder = 14;
    [SerializeField] private float playerBurstScale = 0.32f;
    [SerializeField] private float bossChargeScale = 0.72f;
    [SerializeField] private float parryRingScale = 0.48f;

    private readonly HashSet<int> enhancedProjectiles = new HashSet<int>();
    private Material spriteMaterial;
    private BossController.BossState previousBossState;
    private bool previousParryState;
    private int previousRangedCount;
    private TMP_Text parryNotice;
    private Coroutine noticeRoutine;

    public void Configure(PlayerController playerController, BossController bossController,
        PlayerCombatTracker combatTracker, Sprite ranged, Sprite charge, Sprite parry)
    {
        player = playerController;
        boss = bossController;
        tracker = combatTracker;
        playerRangedBurst = ranged;
        bossCoreCharge = charge;
        parrySuccessRing = parry;
    }

    private void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) spriteMaterial = new Material(shader) { name = "BossArena_VFX_Runtime" };

        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (boss == null) boss = FindAnyObjectByType<BossController>();
        if (tracker == null) tracker = PlayerCombatTracker.Instance;

        previousBossState = boss != null ? boss.State : BossController.BossState.Idle;
        previousParryState = player != null && player.IsParrying;
        previousRangedCount = tracker != null ? tracker.RangedCount : 0;
        BuildParryNotice();
    }

    private void OnEnable()
    {
        if (tracker == null) tracker = PlayerCombatTracker.Instance;
        if (tracker != null) tracker.ParrySucceeded += OnParrySucceeded;
    }

    private void Start()
    {
        // PlayerCombatTracker의 Awake가 이 컴포넌트보다 늦은 경우를 보완합니다.
        if (tracker == null)
        {
            tracker = PlayerCombatTracker.Instance;
            if (tracker != null) tracker.ParrySucceeded += OnParrySucceeded;
        }
    }

    private void OnDisable()
    {
        if (tracker != null) tracker.ParrySucceeded -= OnParrySucceeded;
    }

    private void OnDestroy()
    {
        if (spriteMaterial != null) Destroy(spriteMaterial);
    }

    private void Update()
    {
        UpdatePlayerFeedback();
        UpdateBossFeedback();
    }

    private void UpdatePlayerFeedback()
    {
        if (player == null) return;

        bool parrying = player.IsParrying;
        if (parrying && !previousParryState)
        {
            Vector3 position = player.transform.position + Vector3.up * 0.15f;
            SpawnPulse("ParryWindow", parrySuccessRing, position, parryRingScale * 0.58f,
                0.26f, new Color(0.28f, 1f, 0.82f, 0.56f), 110f, player.transform);
        }
        previousParryState = parrying;

        if (tracker == null) return;
        if (tracker.RangedCount > previousRangedCount)
        {
            Vector3 muzzle = player.transform.position + Vector3.up * 0.2f + Vector3.right * player.Facing * 0.95f;
            GameObject burst = SpawnPulse("PlayerRangedBurst", playerRangedBurst, muzzle,
                playerBurstScale, 0.22f, Color.white, 0f, null);
            if (burst != null)
            {
                Vector3 scale = burst.transform.localScale;
                scale.x *= player.Facing;
                burst.transform.localScale = scale;
            }
            StartCoroutine(EnhanceProjectilesFor(0.35f));
        }
        previousRangedCount = tracker.RangedCount;
    }

    private void UpdateBossFeedback()
    {
        if (boss == null) return;
        BossController.BossState state = boss.State;
        if (state == previousBossState) return;

        if (state == BossController.BossState.Windup)
        {
            SpawnPulse("BossCoreWindup", bossCoreCharge, boss.transform.position + Vector3.up * 0.45f,
                bossChargeScale * 0.68f, 0.82f, new Color(1f, 0.25f, 0.48f, 0.78f), -54f, boss.transform);
        }
        else if (state == BossController.BossState.Attack)
        {
            SpawnPulse("BossAttackRelease", bossCoreCharge, boss.transform.position + Vector3.up * 0.45f,
                bossChargeScale, 0.34f, Color.white, 86f, null);
            StartCoroutine(EnhanceProjectilesFor(0.7f));
        }

        previousBossState = state;
    }

    private void OnParrySucceeded()
    {
        if (player != null)
        {
            SpawnPulse("ParrySuccess_Player", parrySuccessRing, player.transform.position + Vector3.up * 0.2f,
                parryRingScale, 0.48f, Color.white, -180f, null);
        }
        if (boss != null)
        {
            SpawnPulse("ParrySuccess_Boss", parrySuccessRing, boss.transform.position + Vector3.up * 0.45f,
                parryRingScale * 1.65f, 0.62f, new Color(0.75f, 0.48f, 1f, 0.94f), 145f, null);
        }

        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(ShowParryNotice());
    }

    private GameObject SpawnPulse(string objectName, Sprite sprite, Vector3 position, float scale,
        float duration, Color color, float rotationSpeed, Transform follow)
    {
        if (sprite == null) return null;

        GameObject go = new GameObject(objectName);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale * 0.72f;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
        StartCoroutine(AnimatePulse(go, renderer, scale, duration, rotationSpeed, follow));
        return go;
    }

    private IEnumerator AnimatePulse(GameObject go, SpriteRenderer renderer, float targetScale,
        float duration, float rotationSpeed, Transform follow)
    {
        Vector3 followOffset = follow != null ? go.transform.position - follow.position : Vector3.zero;
        float elapsed = 0f;
        while (elapsed < duration && go != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            go.transform.localScale = Vector3.one * Mathf.Lerp(targetScale * 0.72f, targetScale * 1.18f, eased);
            go.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            if (follow != null) go.transform.position = follow.position + followOffset;

            Color c = renderer.color;
            c.a = Mathf.Lerp(c.a, 0f, t * t);
            renderer.color = c;
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    private IEnumerator EnhanceProjectilesFor(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            EnhanceCurrentProjectiles();
            elapsed += 0.08f;
            yield return new WaitForSeconds(0.08f);
        }
    }

    private void EnhanceCurrentProjectiles()
    {
        Projectile[] projectiles = FindObjectsByType<Projectile>(FindObjectsSortMode.None);
        foreach (Projectile projectile in projectiles)
        {
            if (projectile == null || !enhancedProjectiles.Add(projectile.GetHashCode())) continue;

            bool isBoss = projectile.name.Contains("Boss");
            TrailRenderer trail = projectile.GetComponent<TrailRenderer>();
            if (trail == null) trail = projectile.gameObject.AddComponent<TrailRenderer>();
            trail.time = isBoss ? 0.32f : 0.2f;
            trail.startWidth = isBoss ? 0.42f : 0.3f;
            trail.endWidth = 0.02f;
            trail.minVertexDistance = 0.05f;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.sortingOrder = sortingOrder - 1;
            if (spriteMaterial != null) trail.sharedMaterial = spriteMaterial;
            trail.startColor = isBoss ? new Color(1f, 0.12f, 0.38f, 0.95f) : new Color(0.2f, 0.95f, 1f, 0.95f);
            trail.endColor = isBoss ? new Color(0.72f, 0.05f, 1f, 0f) : new Color(0.18f, 0.42f, 1f, 0f);
        }
    }

    private void BuildParryNotice()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;

        GameObject go = new GameObject("BossArena_ParryNotice", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(canvas.transform, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 190f);
        rect.sizeDelta = new Vector2(860f, 64f);
        parryNotice = go.GetComponent<TextMeshProUGUI>();
        TMP_Text template = FindAnyObjectByType<TMP_Text>();
        if (template != null && template != parryNotice) parryNotice.font = template.font;
        parryNotice.text = "PARRY SUCCESS  //  학습 모델 교란";
        parryNotice.fontSize = 31f;
        parryNotice.fontStyle = FontStyles.Bold;
        parryNotice.alignment = TextAlignmentOptions.Center;
        parryNotice.color = Color.clear;
        parryNotice.raycastTarget = false;
    }

    private IEnumerator ShowParryNotice()
    {
        if (parryNotice == null) yield break;
        float elapsed = 0f;
        const float duration = 0.95f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = t < 0.18f ? t / 0.18f : 1f - Mathf.InverseLerp(0.62f, 1f, t);
            parryNotice.color = new Color(0.38f, 1f, 0.76f, alpha);
            parryNotice.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(t * 5f));
            yield return null;
        }
        parryNotice.color = Color.clear;
        noticeRoutine = null;
    }
}
