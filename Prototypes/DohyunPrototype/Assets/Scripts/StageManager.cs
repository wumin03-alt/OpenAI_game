using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AdaptiveBossPrototype
{
    public sealed class StageManager : MonoBehaviour
    {
        private readonly List<EnemyGrunt> grunts = new List<EnemyGrunt>();
        private PlayerController player;
        private BossAI boss;
        private float tutorialStart;
        private bool changingStage;

        public PrototypeUI UI { get; private set; }
        public StageState State { get; private set; }
        public bool InputLocked { get; private set; }

        public void Configure(PlayerController playerController, PrototypeUI prototypeUI)
        {
            player = playerController;
            UI = prototypeUI;
            State = StageState.Tutorial;
            tutorialStart = Time.time;
            player.Tracker.ResetCounts();
            UI.ShowBanner("ADAPTIVE COMBAT SIMULATION", new Color(0.2f, 0.85f, 1f), 2f);
            UI.SetGuide("A/D 이동  ·  SPACE 점프  ·  SHIFT 대시  ·  J 근접  ·  K 원거리  ·  L 패링  ·  U 스킬");
        }

        private void Update()
        {
            if (player == null) return;
            UI.UpdateHud(State, player.Health, boss == null ? null : boss.Health, player.Tracker,
                boss == null ? 1 : boss.Phase, boss == null ? DominantStyle.Balanced : boss.LearnedStyle);

            if (State == StageState.Tutorial && !changingStage)
            {
                CombatSnapshot log = player.Tracker.CaptureSnapshot();
                bool complete = log.melee > 0 && log.ranged > 0 && log.dash > 0 && log.parry > 0;
                if (complete && Time.time > tutorialStart + 2f) StartCoroutine(BeginMobBattle());
                else
                {
                    string missing = "훈련: ";
                    missing += log.melee == 0 ? "[J 근접] " : "[J ✓] ";
                    missing += log.ranged == 0 ? "[K 원거리] " : "[K ✓] ";
                    missing += log.dash == 0 ? "[SHIFT 대시] " : "[SHIFT ✓] ";
                    missing += log.parry == 0 ? "[L 패링]" : "[L ✓]";
                    UI.SetGuide(missing);
                }
            }
            else if (State == StageState.Victory && Input.GetKeyDown(KeyCode.R))
            {
                RestartSimulation();
            }
        }

        private IEnumerator BeginMobBattle()
        {
            changingStage = true;
            InputLocked = true;
            UI.ShowBanner("TRAINING COMPLETE\nCOMBAT TEST LOADING", new Color(0.35f, 1f, 0.65f), 1.7f);
            yield return new WaitForSeconds(1.3f);
            player.ResetForStage(new Vector2(-6f, -2.05f));
            player.Tracker.ResetCounts();
            State = StageState.MobBattle;
            UI.SetGuide("적을 모두 처치하십시오 — 공격 예고가 보이면 대시하거나 L로 패링");
            SpawnGrunt(new Vector2(2.5f, -2.25f));
            SpawnGrunt(new Vector2(5f, -2.25f));
            SpawnGrunt(new Vector2(7.2f, -2.25f));
            InputLocked = false;
            changingStage = false;
        }

        private void SpawnGrunt(Vector2 position)
        {
            GameObject obj = GameBootstrap.CreateActor("Training Drone", position, new Vector2(0.9f, 1.25f), new Color(0.95f, 0.3f, 0.36f), 1f);
            Rigidbody2D rigidbody = obj.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 3.4f;
            rigidbody.freezeRotation = true;
            obj.AddComponent<BoxCollider2D>();
            EnemyGrunt grunt = obj.AddComponent<EnemyGrunt>();
            grunt.Configure(this, player);
            grunts.Add(grunt);
        }

        public void OnGruntDefeated(EnemyGrunt grunt)
        {
            grunts.Remove(grunt);
            if (State == StageState.MobBattle && grunts.Count == 0 && !changingStage)
                StartCoroutine(BeginBossBattle());
        }

        private IEnumerator BeginBossBattle()
        {
            changingStage = true;
            InputLocked = true;
            UI.ShowBanner("ALL TARGETS ELIMINATED\nADAPTIVE CORE DETECTED", new Color(1f, 0.4f, 0.3f), 2.1f);
            yield return new WaitForSeconds(1.8f);
            player.ResetForStage(new Vector2(-6f, -2.05f));
            player.Tracker.ResetCounts();
            State = StageState.BossBattle;
            SpawnBoss();
            UI.SetGuide("PHASE 1: AI가 행동을 기록합니다 — 한 전술에만 의존하지 마십시오");
            InputLocked = false;
            changingStage = false;
        }

        private void SpawnBoss()
        {
            GameObject obj = GameBootstrap.CreateActor("Adaptive Core", new Vector2(5.5f, -1.75f), new Vector2(1.8f, 2.3f), new Color(0.9f, 0.2f, 0.33f), 1.5f);
            Rigidbody2D rigidbody = obj.AddComponent<Rigidbody2D>();
            rigidbody.gravityScale = 3.4f;
            rigidbody.freezeRotation = true;
            obj.AddComponent<BoxCollider2D>();
            boss = obj.AddComponent<BossAI>();
            boss.Configure(this, player);
        }

        public void OnBossDefeated()
        {
            State = StageState.Victory;
            InputLocked = true;
            UI.ShowBanner("ADAPTIVE CORE DEFEATED\nSIMULATION CLEAR", new Color(0.35f, 1f, 0.7f), 30f);
            UI.SetGuide("프로토타입 클리어  ·  R 키로 처음부터 다시 시작");
        }

        public void OnPlayerDefeated()
        {
            if (!changingStage) StartCoroutine(RestartCurrentStage());
        }

        private IEnumerator RestartCurrentStage()
        {
            changingStage = true;
            InputLocked = true;
            UI.ShowBanner("SIMULATION FAILED\nCHECKPOINT RELOADING", new Color(1f, 0.28f, 0.3f), 1.8f);
            yield return new WaitForSeconds(1.5f);
            ClearEnemies();
            player.ResetForStage(new Vector2(-6f, -2.05f));
            player.Tracker.ResetCounts();
            if (State == StageState.MobBattle)
            {
                SpawnGrunt(new Vector2(2.5f, -2.25f));
                SpawnGrunt(new Vector2(5f, -2.25f));
                SpawnGrunt(new Vector2(7.2f, -2.25f));
            }
            else if (State == StageState.BossBattle)
            {
                SpawnBoss();
            }
            InputLocked = false;
            changingStage = false;
        }

        private void RestartSimulation()
        {
            StopAllCoroutines();
            ClearEnemies();
            InputLocked = false;
            changingStage = false;
            player.ResetForStage(new Vector2(-6f, -2.05f));
            Configure(player, UI);
        }

        private void ClearEnemies()
        {
            foreach (Health health in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (health != null && health.Team == Team.Enemy) Destroy(health.gameObject);
            }
            grunts.Clear();
            boss = null;
        }

        public void DamageEnemiesInRadius(Vector2 center, float radius, float damage)
        {
            foreach (Health target in FindObjectsByType<Health>(FindObjectsSortMode.None))
            {
                if (target != null && target.Team == Team.Enemy && !target.IsDead
                    && Vector2.Distance(center, target.transform.position) <= radius)
                    target.Damage(damage);
            }
        }

        public void SpawnProjectile(Vector2 position, Vector2 direction, Team owner, float speed, float damage,
            bool parryable, Color color, GameObject attacker = null)
        {
            GameObject projectile = GameBootstrap.CreateActor("Projectile", position, Vector2.one * 0.32f, color, 2f);
            CircleCollider2D collider = projectile.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            SimpleProjectile simple = projectile.AddComponent<SimpleProjectile>();
            simple.Configure(direction, speed, owner, damage, parryable, attacker);
        }

        public void CreateFlash(Vector2 position, Vector2 scale, Color color, float duration)
        {
            GameObject effect = GameBootstrap.CreateActor("Impact", position, scale, color, 0.5f);
            effect.AddComponent<EffectFade>().Configure(duration, false);
        }

        public void CreateRing(Vector2 position, float diameter, Color color, float duration)
        {
            GameObject effect = GameBootstrap.CreateActor("Telegraph", position, Vector2.one * diameter, color, 0.25f);
            effect.AddComponent<EffectFade>().Configure(duration, true);
        }

        public void SetBriefInputLock(float seconds)
        {
            StartCoroutine(BriefLock(seconds));
        }

        private IEnumerator BriefLock(float seconds)
        {
            InputLocked = true;
            yield return new WaitForSeconds(seconds);
            if (State != StageState.Victory) InputLocked = false;
        }
    }
}
