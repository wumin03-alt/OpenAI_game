using System.Collections;
using UnityEngine;

namespace AdaptiveBossPrototype
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health))]
    public sealed class BossAI : MonoBehaviour
    {
        private enum BossState { Idle, Attack, PhaseTransition, Berserk, Dead }
        private enum BossMove { Shot, Slash, Knockback, Charge, AreaPulse }

        private StageManager stage;
        private PlayerController player;
        private PlayerCombatTracker tracker;
        private Rigidbody2D body;
        private Health health;
        private SpriteRenderer sprite;
        private BossState state;
        private CombatSnapshot phaseTwoStart;
        private float nextAttack;
        private float stunnedUntil;
        private Coroutine currentAttack;
        private int phase = 1;
        private DominantStyle learnedStyle = DominantStyle.Balanced;

        public int Phase => phase;
        public DominantStyle LearnedStyle => learnedStyle;
        public Health Health => health;

        public void Configure(StageManager owner, PlayerController target)
        {
            stage = owner;
            player = target;
            tracker = target.Tracker;
            body = GetComponent<Rigidbody2D>();
            health = GetComponent<Health>();
            sprite = GetComponent<SpriteRenderer>();
            health.Configure(240f, Team.Enemy);
            health.Died += OnDeath;
            state = BossState.Idle;
            nextAttack = Time.time + 1.2f;
        }

        private void Update()
        {
            if (health.IsDead || player.Health.IsDead || stage.InputLocked) return;
            CheckPhase();
            if (Time.time < stunnedUntil)
            {
                body.linearVelocity = new Vector2(0f, body.linearVelocity.y);
                return;
            }

            if ((state == BossState.Idle || state == BossState.Berserk) && Time.time >= nextAttack)
                currentAttack = StartCoroutine(PerformMove(ChooseMove()));
            else if (state == BossState.Idle && Mathf.Abs(player.transform.position.x - transform.position.x) > 5f)
                body.linearVelocity = new Vector2(Mathf.Sign(player.transform.position.x - transform.position.x) * 1.35f, body.linearVelocity.y);
        }

        private void CheckPhase()
        {
            if (phase == 1 && health.Normalized <= 0.5f)
            {
                phase = 2;
                learnedStyle = tracker.GetDominantStyle();
                phaseTwoStart = tracker.CaptureSnapshot();
                StartCoroutine(Transition(false));
            }
            else if (phase == 2 && health.Normalized <= 0.2f)
            {
                phase = 3;
                DominantStyle recent = tracker.GetDominantStyleSince(phaseTwoStart);
                learnedStyle = recent == DominantStyle.Balanced ? tracker.GetDominantStyle() : recent;
                StartCoroutine(Transition(true));
            }
        }

        private IEnumerator Transition(bool berserk)
        {
            if (currentAttack != null)
            {
                StopCoroutine(currentAttack);
                currentAttack = null;
            }
            state = BossState.PhaseTransition;
            body.linearVelocity = Vector2.zero;
            stage.SetBriefInputLock(0.8f);
            string title = berserk ? "FINAL LEARNING COMPLETE  //  BERSERK" : "COMBAT PROFILE ACQUIRED";
            string counter = CounterDescription(learnedStyle);
            stage.UI.ShowBanner($"{title}\n{learnedStyle.KoreanName()} 성향 감지 — {counter}", learnedStyle.StyleColor(), 2.5f);
            Color original = sprite.color;
            for (int i = 0; i < 6; i++)
            {
                sprite.color = i % 2 == 0 ? learnedStyle.StyleColor() : Color.white;
                yield return new WaitForSeconds(0.1f);
            }
            sprite.color = berserk ? new Color(1f, 0.18f, 0.5f) : original;
            state = berserk ? BossState.Berserk : BossState.Idle;
            nextAttack = Time.time + 0.45f;
        }

        private BossMove ChooseMove()
        {
            float shot = 1f, slash = 1f, knockback = 0.7f, charge = 0.7f, area = phase >= 2 ? 0.45f : 0f;
            if (phase >= 2)
            {
                switch (learnedStyle)
                {
                    case DominantStyle.Melee: knockback += 2.3f; area += 0.8f; break;
                    case DominantStyle.Ranged: charge += 2.4f; slash += 0.7f; break;
                    case DominantStyle.Dash: area += 2.2f; shot += 0.7f; break;
                    case DominantStyle.Parry: area += 1.6f; charge += 0.8f; break;
                    default: shot += 0.5f; slash += 0.5f; break;
                }
            }
            if (phase == 3) { area += 1.2f; charge += 0.6f; }
            float roll = Random.value * (shot + slash + knockback + charge + area);
            if ((roll -= shot) < 0f) return BossMove.Shot;
            if ((roll -= slash) < 0f) return BossMove.Slash;
            if ((roll -= knockback) < 0f) return BossMove.Knockback;
            if ((roll -= charge) < 0f) return BossMove.Charge;
            return BossMove.AreaPulse;
        }

        private IEnumerator PerformMove(BossMove move)
        {
            state = BossState.Attack;
            body.linearVelocity = Vector2.zero;
            switch (move)
            {
                case BossMove.Shot: yield return Shot(); break;
                case BossMove.Slash: yield return Slash(); break;
                case BossMove.Knockback: yield return Knockback(); break;
                case BossMove.Charge: yield return Charge(); break;
                case BossMove.AreaPulse: yield return AreaPulse(); break;
            }
            if (!health.IsDead)
            {
                currentAttack = null;
                state = phase == 3 ? BossState.Berserk : BossState.Idle;
                nextAttack = Time.time + (phase == 3 ? 0.48f : phase == 2 ? 0.72f : 1.0f);
            }
        }

        private IEnumerator Shot()
        {
            sprite.color = new Color(1f, 0.72f, 0.2f);
            stage.CreateFlash(transform.position + Vector3.up * 0.3f, Vector2.one * 1.8f, new Color(1f, 0.75f, 0.2f, 0.3f), 0.42f);
            yield return new WaitForSeconds(phase == 3 ? 0.25f : 0.42f);
            Vector2 direction = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
            stage.SpawnProjectile(transform.position, direction, Team.Enemy, phase == 3 ? 11f : 8.5f, 11f, true, new Color(1f, 0.55f, 0.15f), gameObject);
            if (phase == 3)
            {
                stage.SpawnProjectile(transform.position, Rotate(direction, 12f), Team.Enemy, 10f, 8f, true, new Color(1f, 0.3f, 0.5f), gameObject);
                stage.SpawnProjectile(transform.position, Rotate(direction, -12f), Team.Enemy, 10f, 8f, true, new Color(1f, 0.3f, 0.5f), gameObject);
            }
            ResetColor();
        }

        private IEnumerator Slash()
        {
            float direction = Mathf.Sign(player.transform.position.x - transform.position.x);
            stage.CreateFlash((Vector2)transform.position + new Vector2(direction * 1.3f, 0f), new Vector2(2.2f, 2.5f), new Color(1f, 0.28f, 0.2f, 0.38f), 0.48f);
            yield return new WaitForSeconds(0.48f);
            if (Vector2.Distance(transform.position, player.transform.position) < 2.7f)
                player.TakeEnemyHit(15f, new Vector2(direction * 7f, 4f), true, gameObject);
        }

        private IEnumerator Knockback()
        {
            stage.CreateRing(transform.position, 3.1f, new Color(1f, 0.5f, 0.12f, 0.7f), 0.68f);
            yield return new WaitForSeconds(0.68f);
            if (Vector2.Distance(transform.position, player.transform.position) < 3.2f)
            {
                float direction = Mathf.Sign(player.transform.position.x - transform.position.x);
                player.TakeEnemyHit(13f, new Vector2(direction * 12f, 5f), true, gameObject);
            }
        }

        private IEnumerator Charge()
        {
            float direction = Mathf.Sign(player.transform.position.x - transform.position.x);
            stage.CreateFlash(transform.position, new Vector2(1.8f, 3f), new Color(0.2f, 0.7f, 1f, 0.45f), 0.5f);
            yield return new WaitForSeconds(0.48f);
            float end = Time.time + 0.34f;
            bool hit = false;
            while (Time.time < end)
            {
                body.linearVelocity = new Vector2(direction * (phase == 3 ? 15f : 11f), body.linearVelocity.y);
                if (!hit && Vector2.Distance(transform.position, player.transform.position) < 1.7f)
                {
                    hit = true;
                    player.TakeEnemyHit(17f, new Vector2(direction * 9f, 4f), true, gameObject);
                }
                yield return null;
            }
            body.linearVelocity = Vector2.zero;
        }

        private IEnumerator AreaPulse()
        {
            Color warning = new Color(0.75f, 0.15f, 1f, 0.72f);
            stage.CreateRing(transform.position, phase == 3 ? 5f : 4.2f, warning, phase == 3 ? 0.58f : 0.82f);
            yield return new WaitForSeconds(phase == 3 ? 0.58f : 0.82f);
            float radius = phase == 3 ? 5f : 4.2f;
            stage.CreateFlash(transform.position, Vector2.one * radius * 1.8f, new Color(0.7f, 0.1f, 1f, 0.25f), 0.16f);
            if (Vector2.Distance(transform.position, player.transform.position) < radius)
            {
                float direction = Mathf.Sign(player.transform.position.x - transform.position.x);
                player.TakeEnemyHit(18f, new Vector2(direction * 6f, 6f), false, gameObject);
            }
        }

        private void ResetColor()
        {
            sprite.color = phase == 3 ? new Color(1f, 0.18f, 0.5f) : new Color(0.9f, 0.2f, 0.33f);
        }

        private static Vector2 Rotate(Vector2 value, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector2(value.x * cos - value.y * sin, value.x * sin + value.y * cos);
        }

        private static string CounterDescription(DominantStyle style)
        {
            switch (style)
            {
                case DominantStyle.Melee: return "넉백·거리 확보 강화";
                case DominantStyle.Ranged: return "돌진·근접 압박 강화";
                case DominantStyle.Dash: return "광역 제압 패턴 강화";
                case DominantStyle.Parry: return "패링 불가 광역기 혼합";
                default: return "복합 패턴 최적화";
            }
        }

        public void Stun(float seconds)
        {
            if (currentAttack != null)
            {
                StopCoroutine(currentAttack);
                currentAttack = null;
            }
            stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);
            body.linearVelocity = Vector2.zero;
            state = phase == 3 ? BossState.Berserk : BossState.Idle;
            nextAttack = stunnedUntil + 0.25f;
            sprite.color = Color.white;
            Invoke(nameof(ResetColor), seconds);
        }

        private void OnDeath(Health _)
        {
            state = BossState.Dead;
            StopAllCoroutines();
            body.linearVelocity = Vector2.zero;
            stage.CreateFlash(transform.position, Vector2.one * 5f, new Color(1f, 0.25f, 0.5f, 0.8f), 0.7f);
            stage.OnBossDefeated();
            Destroy(gameObject, 0.75f);
        }
    }
}
