using System.Collections;
using UnityEngine;

namespace AdaptiveBossPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(PlayerCombatTracker))]
    public sealed class PlayerController : MonoBehaviour
    {
        private const float MoveSpeed = 6.5f;
        private const float JumpPower = 11.5f;
        private const float DashSpeed = 16f;

        private Rigidbody2D body;
        private SpriteRenderer sprite;
        private StageManager stage;
        private PlayerCombatTracker tracker;
        private Health health;
        private float facing = 1f;
        private float meleeReady;
        private float rangedReady;
        private float dashReady;
        private float skillReady;
        private bool dashing;
        private bool parrying;
        private bool invulnerable;
        private Color baseColor;

        public Health Health => health;
        public PlayerCombatTracker Tracker => tracker;
        public bool IsParrying => parrying;

        public void Configure(StageManager owner, Color color)
        {
            stage = owner;
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            tracker = GetComponent<PlayerCombatTracker>();
            sprite = GetComponent<SpriteRenderer>();
            baseColor = color;
            health.Configure(100f, Team.Player);
            health.Died += _ => stage.OnPlayerDefeated();
        }

        private void Update()
        {
            if (stage == null || health.IsDead || stage.InputLocked) return;

            float axis = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) axis -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) axis += 1f;
            if (Mathf.Abs(axis) > 0.01f) facing = Mathf.Sign(axis);

            if (!dashing) body.linearVelocity = new Vector2(axis * MoveSpeed, body.linearVelocity.y);
            if ((Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.UpArrow)) && IsGrounded())
                body.linearVelocity = new Vector2(body.linearVelocity.x, JumpPower);

            if ((Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)) && Time.time >= dashReady)
                StartCoroutine(Dash());
            if (Input.GetKeyDown(KeyCode.J) && Time.time >= meleeReady) Melee();
            if (Input.GetKeyDown(KeyCode.K) && Time.time >= rangedReady) Ranged();
            if (Input.GetKeyDown(KeyCode.L) && !parrying) StartCoroutine(Parry());
            if (Input.GetKeyDown(KeyCode.U) && Time.time >= skillReady) Skill();
        }

        private bool IsGrounded()
        {
            return transform.position.y <= -2.08f && body.linearVelocity.y <= 0.2f;
        }

        private IEnumerator Dash()
        {
            tracker.RecordAction(ActionType.Dash);
            dashReady = Time.time + 0.75f;
            dashing = true;
            invulnerable = true;
            sprite.color = new Color(1f, 0.85f, 0.3f);
            body.gravityScale = 0f;
            body.linearVelocity = new Vector2(facing * DashSpeed, 0f);
            yield return new WaitForSeconds(0.16f);
            body.gravityScale = 3.4f;
            dashing = false;
            yield return new WaitForSeconds(0.08f);
            invulnerable = false;
            sprite.color = baseColor;
        }

        private void Melee()
        {
            tracker.RecordAction(ActionType.Melee);
            meleeReady = Time.time + 0.34f;
            Vector2 center = (Vector2)transform.position + new Vector2(facing * 1.05f, 0.05f);
            stage.CreateFlash(center, new Vector2(1.5f, 1.1f), new Color(1f, 0.45f, 0.25f, 0.75f), 0.10f);
            stage.DamageEnemiesInRadius(center, 1.15f, 11f);
        }

        private void Ranged()
        {
            tracker.RecordAction(ActionType.Ranged);
            rangedReady = Time.time + 0.52f;
            Vector2 origin = (Vector2)transform.position + new Vector2(facing * 0.75f, 0.1f);
            stage.SpawnProjectile(origin, new Vector2(facing, 0f), Team.Player, 8f, 12f, false, new Color(0.25f, 0.85f, 1f));
        }

        private IEnumerator Parry()
        {
            tracker.RecordAction(ActionType.Parry);
            parrying = true;
            sprite.color = new Color(0.75f, 0.45f, 1f);
            stage.CreateRing(transform.position, 1.15f, new Color(0.7f, 0.35f, 1f, 0.75f), 0.25f);
            yield return new WaitForSeconds(0.24f);
            parrying = false;
            sprite.color = baseColor;
        }

        private void Skill()
        {
            skillReady = Time.time + 4f;
            stage.CreateRing(transform.position, 3.3f, new Color(0.3f, 1f, 0.72f, 0.8f), 0.35f);
            stage.DamageEnemiesInRadius(transform.position, 3.1f, 20f);
        }

        public bool TakeEnemyHit(float damage, Vector2 knockback, bool parryable, GameObject attacker)
        {
            if (health.IsDead || invulnerable) return false;
            if (parryable && parrying)
            {
                stage.CreateFlash(transform.position, Vector2.one * 1.8f, Color.white, 0.14f);
                if (attacker != null)
                {
                    Health attackerHealth = attacker.GetComponent<Health>();
                    if (attackerHealth != null) attackerHealth.Damage(8f);
                    BossAI boss = attacker.GetComponent<BossAI>();
                    if (boss != null) boss.Stun(0.65f);
                }
                return false;
            }

            bool applied = health.Damage(damage);
            if (applied)
            {
                body.linearVelocity = knockback;
                StartCoroutine(HitFlash());
            }
            return applied;
        }

        private IEnumerator HitFlash()
        {
            invulnerable = true;
            sprite.color = Color.white;
            yield return new WaitForSeconds(0.18f);
            sprite.color = baseColor;
            yield return new WaitForSeconds(0.22f);
            invulnerable = false;
        }

        public void ResetForStage(Vector2 position)
        {
            StopAllCoroutines();
            transform.position = position;
            body.linearVelocity = Vector2.zero;
            body.gravityScale = 3.4f;
            dashing = parrying = invulnerable = false;
            sprite.color = baseColor;
            health.RestoreFull();
        }
    }
}
