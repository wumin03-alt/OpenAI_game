using UnityEngine;

namespace AdaptiveBossPrototype
{
    public sealed class SimpleProjectile : MonoBehaviour
    {
        private Vector2 velocity;
        private Team owner;
        private float damage;
        private bool parryable;
        private float expires;
        private GameObject attacker;

        public void Configure(Vector2 direction, float speed, Team team, float hitDamage, bool canParry, GameObject source)
        {
            velocity = direction.normalized * speed;
            owner = team;
            damage = hitDamage;
            parryable = canParry;
            attacker = source;
            expires = Time.time + 4f;
        }

        private void Update()
        {
            transform.position += (Vector3)(velocity * Time.deltaTime);
            if (Time.time >= expires || Mathf.Abs(transform.position.x) > 14f || Mathf.Abs(transform.position.y) > 8f)
                Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            Health target = other.GetComponent<Health>();
            if (target == null || target.Team == owner || target.IsDead) return;

            if (target.Team == Team.Player)
            {
                PlayerController player = other.GetComponent<PlayerController>();
                if (player != null)
                    player.TakeEnemyHit(damage, new Vector2(Mathf.Sign(velocity.x) * 5f, 3f), parryable, attacker);
            }
            else
            {
                target.Damage(damage);
            }
            Destroy(gameObject);
        }
    }
}
