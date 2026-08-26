using UnityEngine;

/// <summary>F.E.E.D.-6가 발사하는 영양 블록. 패링하면 보스에게 되돌아갑니다.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class NutrientBlockProjectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 6f;
    [SerializeField, Min(1f)] private float reflectedSpeedMultiplier = 1.35f;
    [SerializeField, Range(0.1f, 1f)] private float reflectedGroggyRatio = 0.5f;
    [SerializeField, Min(0f)] private float reflectedBossDamage = 20f;

    private BossStaggerGauge staggerGauge;
    private BossParryMiniGameBridge parryMiniGameBridge;
    private Rigidbody2D body;
    private float damage;
    private bool reflected;
    private bool completed;

    public bool IsReflected => reflected;
    public float ReflectedBossDamage => reflectedBossDamage;

    public void Initialize(BossStaggerGauge gauge, Vector2 direction, float speed, float attackDamage)
    {
        staggerGauge = gauge;
        parryMiniGameBridge = gauge != null ? gauge.GetComponent<BossParryMiniGameBridge>() : null;
        damage = attackDamage;
        body = GetComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.linearVelocity = direction.sqrMagnitude > 0.001f
            ? direction.normalized * speed
            : Vector2.left * speed;
        Destroy(gameObject, lifeTime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (completed) return;

        if (reflected)
        {
            BossStaggerGauge hitGauge = other.GetComponentInParent<BossStaggerGauge>();
            if (hitGauge == null || hitGauge != staggerGauge) return;

            completed = true;
            ApplyReflectedHit();
            Destroy(gameObject);
            return;
        }

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Health health = player.GetComponent<Health>();
        if (health == null || health.IsDead) return;

        bool parried = player.IsParrying;

        if (parried)
        {
            reflected = true;
            parryMiniGameBridge?.SuppressNextParryMiniGame();
            health.TakeDamage(damage);
            parryMiniGameBridge?.ClearParryMiniGameSuppression();
            ReflectTowardBoss();
            return;
        }

        completed = true;
        health.TakeDamage(damage);

        Destroy(gameObject);
    }

    private void ReflectTowardBoss()
    {
        if (body == null || staggerGauge == null)
        {
            completed = true;
            Destroy(gameObject);
            return;
        }

        Vector2 target = staggerGauge.transform.position;
        Vector2 direction = target - body.position;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;

        float speed = Mathf.Max(1f, body.linearVelocity.magnitude) * reflectedSpeedMultiplier;
        body.linearVelocity = direction.normalized * speed;

        SpriteRenderer renderer = GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.color = new Color(0.22f, 1f, 0.66f, 1f);

        Debug.Log("[NutrientBlock] PARRIED // RETURN TO SENDER", this);
    }

    private void ApplyReflectedHit()
    {
        if (staggerGauge == null) return;

        float groggyBefore = staggerGauge.CurrentGroggy;
        Health bossHealth = staggerGauge.GetComponent<Health>();
        float healthBefore = bossHealth != null ? bossHealth.CurrentHP : 0f;

        if (bossHealth != null && !bossHealth.IsDead && reflectedBossDamage > 0f)
            bossHealth.TakeDamage(reflectedBossDamage, true);

        float healthDamage = bossHealth != null
            ? Mathf.Max(0f, healthBefore - bossHealth.CurrentHP)
            : 0f;
        float groggyLostFromHealth = Mathf.Max(0f, groggyBefore - staggerGauge.CurrentGroggy);
        float desiredGroggyDamage = staggerGauge.MaxGroggy * reflectedGroggyRatio;
        float additionalGroggyDamage = Mathf.Max(0f, desiredGroggyDamage - groggyLostFromHealth);
        staggerGauge.ApplyGroggyDamage(additionalGroggyDamage);

        float actualGroggyDamage = Mathf.Max(0f, groggyBefore - staggerGauge.CurrentGroggy);
        Debug.Log(
            $"[NutrientBlock] REFLECT HIT // BOSS HP -{healthDamage:0.#} // GROGGY -{actualGroggyDamage:0.#}",
            this);
    }
}
