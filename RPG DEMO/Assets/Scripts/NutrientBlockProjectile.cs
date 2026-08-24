using UnityEngine;

/// <summary>F.E.E.D.-6가 발사하는 영양 블록. 패링 성공은 공용 미니게임 브리지로 전달됩니다.</summary>
[RequireComponent(typeof(Rigidbody2D), typeof(Collider2D))]
public sealed class NutrientBlockProjectile : MonoBehaviour
{
    [SerializeField] private float lifeTime = 6f;

    private BossStaggerGauge staggerGauge;
    private Rigidbody2D body;
    private float damage;
    private bool resolved;

    public void Initialize(BossStaggerGauge gauge, Vector2 direction, float speed, float attackDamage)
    {
        staggerGauge = gauge;
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
        if (resolved) return;

        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        Health health = player.GetComponent<Health>();
        if (health == null || health.IsDead) return;

        resolved = true;
        bool parried = player.IsParrying;
        health.TakeDamage(damage);

        if (parried)
        {
            SpriteRenderer renderer = GetComponent<SpriteRenderer>();
            if (renderer != null) renderer.color = new Color(0.22f, 1f, 0.66f, 1f);
        }

        Destroy(gameObject, parried ? 0.08f : 0f);
    }
}
