using System.Collections;
using UnityEngine;

/// <summary>
/// Player 프리팹에 포함되어 모든 스테이지에서 동일하게 재생되는 공통 전투 VFX입니다.
/// PlayerCombatTracker의 기록 상태를 읽기만 하므로 기존 공격 판정과 수치는 변경하지 않습니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCombatVFX : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private PlayerCombatTracker tracker;
    [SerializeField] private Sprite attackBurst;
    [SerializeField] private Sprite parryRing;
    [SerializeField] private int sortingOrder = 14;
    [SerializeField] private float rangedScale = 0.32f;
    [SerializeField] private float meleeScale = 0.38f;
    [SerializeField] private float parryScale = 0.48f;

    private Material spriteMaterial;
    private bool previousParryState;
    private int previousMeleeCount;
    private int previousDashCount;

    public void Configure(PlayerController controller, PlayerCombatTracker combatTracker,
        Sprite burst, Sprite ring)
    {
        player = controller;
        tracker = combatTracker;
        attackBurst = burst;
        parryRing = ring;
    }

    private void Awake()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (tracker == null) tracker = GetComponent<PlayerCombatTracker>();

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) spriteMaterial = new Material(shader) { name = "Player_VFX_Runtime" };

        previousParryState = player != null && player.IsParrying;
        previousMeleeCount = tracker != null ? tracker.MeleeCount : 0;
        previousDashCount = tracker != null ? tracker.DashCount : 0;
    }

    private void OnEnable()
    {
        if (tracker == null) tracker = GetComponent<PlayerCombatTracker>();
        if (tracker != null) tracker.ParrySucceeded += OnParrySucceeded;
    }

    private void Start()
    {
        // 같은 프리팹의 Tracker.Awake 실행 순서와 무관하게 구독을 보장합니다.
        if (tracker == null)
        {
            tracker = GetComponent<PlayerCombatTracker>();
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
        if (player == null) return;

        bool parrying = player.IsParrying;
        if (parrying && !previousParryState)
        {
            SpawnPulse("ParryWindow", parryRing, transform.position + Vector3.up * 0.15f,
                parryScale * 0.58f, 0.26f, new Color(0.28f, 1f, 0.82f, 0.56f), 110f, transform);
        }
        previousParryState = parrying;

        if (tracker == null) return;

        if (tracker.MeleeCount > previousMeleeCount)
        {
            Vector3 point = transform.position + Vector3.up * 0.1f + Vector3.right * player.Facing * 0.9f;
            SpawnFacingPulse("PlayerMeleeBurst", point, meleeScale, 0.2f,
                new Color(0.4f, 1f, 0.78f, 0.95f), player.Facing, -75f);
        }

        if (tracker.DashCount > previousDashCount)
        {
            Vector3 point = transform.position + Vector3.up * 0.05f - Vector3.right * player.Facing * 0.35f;
            SpawnFacingPulse("PlayerDashBurst", point, rangedScale * 0.85f, 0.18f,
                new Color(0.28f, 0.86f, 1f, 0.72f), -player.Facing, 95f);
        }

        previousMeleeCount = tracker.MeleeCount;
        previousDashCount = tracker.DashCount;
    }

    private void OnParrySucceeded()
    {
        SpawnPulse("ParrySuccess", parryRing, transform.position + Vector3.up * 0.2f,
            parryScale, 0.48f, Color.white, -180f, null);
    }

    private void SpawnFacingPulse(string objectName, Vector3 position, float scale,
        float duration, Color color, int facing, float rotationSpeed)
    {
        GameObject burst = SpawnPulse(objectName, attackBurst, position, scale,
            duration, color, rotationSpeed, null);
        if (burst == null) return;
        Vector3 localScale = burst.transform.localScale;
        localScale.x *= facing;
        burst.transform.localScale = localScale;
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

    private static IEnumerator AnimatePulse(GameObject go, SpriteRenderer renderer, float targetScale,
        float duration, float rotationSpeed, Transform follow)
    {
        Vector3 followOffset = follow != null ? go.transform.position - follow.position : Vector3.zero;
        float elapsed = 0f;
        float startAlpha = renderer.color.a;
        while (elapsed < duration && go != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float facing = Mathf.Sign(go.transform.localScale.x);
            go.transform.localScale = new Vector3(
                facing * Mathf.Lerp(targetScale * 0.72f, targetScale * 1.18f, eased),
                Mathf.Lerp(targetScale * 0.72f, targetScale * 1.18f, eased), 1f);
            go.transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
            if (follow != null) go.transform.position = follow.position + followOffset;

            Color color = renderer.color;
            color.a = Mathf.Lerp(startAlpha, 0f, t * t);
            renderer.color = color;
            yield return null;
        }
        if (go != null) Destroy(go);
    }
}
