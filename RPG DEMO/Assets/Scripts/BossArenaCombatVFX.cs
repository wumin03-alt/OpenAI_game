using System.Collections;
using UnityEngine;

/// <summary>
/// BossArena에만 남는 보스 전용 전투 피드백입니다.
/// 플레이어 캐릭터와 플레이어 공격 VFX는 Player 프리팹의 PlayerCombatVFX가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossArenaCombatVFX : MonoBehaviour
{
    [SerializeField] private BossController boss;
    [SerializeField] private Sprite bossCoreCharge;
    [SerializeField] private int sortingOrder = 14;
    [SerializeField] private float bossChargeScale = 0.72f;

    private Material spriteMaterial;
    private BossController.BossState previousBossState;

    public void Configure(BossController bossController, Sprite charge)
    {
        boss = bossController;
        bossCoreCharge = charge;
    }

    private void Awake()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null) spriteMaterial = new Material(shader) { name = "BossArena_VFX_Runtime" };

        if (boss == null) boss = FindAnyObjectByType<BossController>();
        previousBossState = boss != null ? boss.State : BossController.BossState.Idle;
    }

    private void OnDestroy()
    {
        if (spriteMaterial != null) Destroy(spriteMaterial);
    }

    private void Update()
    {
        if (boss == null) return;
        BossController.BossState state = boss.State;
        if (state == previousBossState) return;

        if (state == BossController.BossState.Windup)
        {
            SpawnPulse("BossCoreWindup", boss.transform.position + Vector3.up * 0.45f,
                bossChargeScale * 0.68f, 0.82f, new Color(1f, 0.25f, 0.48f, 0.78f), -54f, boss.transform);
        }
        else if (state == BossController.BossState.Attack)
        {
            SpawnPulse("BossAttackRelease", boss.transform.position + Vector3.up * 0.45f,
                bossChargeScale, 0.34f, Color.white, 86f, null);
        }

        previousBossState = state;
    }

    private void SpawnPulse(string objectName, Vector3 position, float scale,
        float duration, Color color, float rotationSpeed, Transform follow)
    {
        if (bossCoreCharge == null) return;

        GameObject go = new GameObject(objectName);
        go.transform.position = position;
        go.transform.localScale = Vector3.one * scale * 0.72f;
        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = bossCoreCharge;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (spriteMaterial != null) renderer.sharedMaterial = spriteMaterial;
        StartCoroutine(AnimatePulse(go, renderer, scale, duration, rotationSpeed, follow));
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
            go.transform.localScale = Vector3.one * Mathf.Lerp(targetScale * 0.72f, targetScale * 1.18f, eased);
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
