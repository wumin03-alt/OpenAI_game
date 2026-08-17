using System.Collections;
using UnityEngine;

namespace AdaptiveBossPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class EnemyGrunt : MonoBehaviour
    {
        private StageManager stage;
        private PlayerController player;
        private Rigidbody2D body;
        private Health health;
        private SpriteRenderer sprite;
        private float attackReady;
        private bool attacking;

        public void Configure(StageManager owner, PlayerController target)
        {
            stage = owner;
            player = target;
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            sprite = GetComponent<SpriteRenderer>();
            health.Configure(28f, Team.Enemy);
            health.Died += OnDeath;
        }

        private void Update()
        {
            if (player == null || player.Health.IsDead || health.IsDead || attacking || stage.InputLocked) return;
            float distance = player.transform.position.x - transform.position.x;
            if (Mathf.Abs(distance) > 1.25f)
                body.linearVelocity = new Vector2(Mathf.Sign(distance) * 2.6f, body.linearVelocity.y);
            else
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                if (Time.time >= attackReady) StartCoroutine(Attack());
            }
        }

        private IEnumerator Attack()
        {
            attacking = true;
            attackReady = Time.time + 1.25f;
            sprite.color = new Color(1f, 0.75f, 0.25f);
            stage.CreateFlash((Vector2)transform.position + Vector2.up * 0.2f, new Vector2(1.4f, 1.8f), new Color(1f, 0.65f, 0.15f, 0.35f), 0.32f);
            yield return new WaitForSeconds(0.34f);
            if (!health.IsDead && Vector2.Distance(transform.position, player.transform.position) < 1.7f)
            {
                float direction = Mathf.Sign(player.transform.position.x - transform.position.x);
                player.TakeEnemyHit(9f, new Vector2(direction * 4.5f, 3.5f), true, gameObject);
            }
            sprite.color = new Color(0.95f, 0.3f, 0.36f);
            attacking = false;
        }

        private void OnDeath(Health _)
        {
            StopAllCoroutines();
            stage.CreateFlash(transform.position, Vector2.one * 1.2f, new Color(1f, 0.3f, 0.25f, 0.8f), 0.25f);
            stage.OnGruntDefeated(this);
            Destroy(gameObject);
        }
    }
}
