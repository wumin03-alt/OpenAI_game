using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AdaptiveBossPrototype;
using UnityEngine;

namespace DemonCompany.Phase1
{
    public sealed class Phase1GameController : MonoBehaviour
    {
        public const int SalaryBudget = 100;
        public const int HireLimit = 2;
        public const int QuestionLimit = 3;

        private readonly List<CandidateRuntime> candidates = new List<CandidateRuntime>();
        private readonly List<BattleMonster> monsters = new List<BattleMonster>();
        private readonly List<BattleEnemy> enemies = new List<BattleEnemy>();
        private readonly List<string> battleEvents = new List<string>();
        private readonly Vector2[] slotPositions =
        {
            new Vector2(-4.5f, -1.15f),
            new Vector2(-2.7f, -1.15f),
            new Vector2(-0.9f, -1.15f)
        };

        private readonly IInterviewProvider interviewProvider = new FakeInterviewProvider();
        private Phase1UI ui;
        private GameObject battleRoot;
        private GamePhase phase;
        private int selectedCandidate;
        private int selectedDeploymentCandidate = -1;
        private int dungeonHp;
        private int enemiesSpawned;
        private int enemiesResolved;
        private float nextEnemySpawn;
        private bool battleEnding;

        public IReadOnlyList<CandidateRuntime> Candidates => candidates;
        public CandidateRuntime Selected => candidates[selectedCandidate];
        public GamePhase Phase => phase;
        public int CurrentBudget => SalaryBudget - candidates.Where(entry => entry.Decision == CandidateDecision.Hired).Sum(entry => entry.Candidate.Salary);
        public int HiredCount => candidates.Count(entry => entry.Decision == CandidateDecision.Hired);
        public int DungeonHp => dungeonHp;
        public int SelectedDeploymentCandidate => selectedDeploymentCandidate;

        public void Initialize()
        {
            foreach (Candidate candidate in CandidateDatabase.CreatePhase1Candidates())
                candidates.Add(new CandidateRuntime { Candidate = candidate, Decision = CandidateDecision.Pending });

            GameObject uiObject = new GameObject("Phase 1 UI");
            ui = uiObject.AddComponent<Phase1UI>();
            ui.Build(this);
            ShowInterview(0);
        }

        private void Update()
        {
            if (phase == GamePhase.Battle && !battleEnding)
                UpdateBattle(Time.deltaTime);
        }

        public void ShowInterview(int index)
        {
            if (phase != GamePhase.Interview || index < 0 || index >= candidates.Count) return;
            selectedCandidate = index;
            ui.ShowInterview();
        }

        public void AskQuestion(string question)
        {
            if (phase != GamePhase.Interview) return;
            CandidateRuntime runtime = Selected;
            string trimmed = question.Trim();
            if (trimmed.Length == 0)
            {
                ui.ShowNotice("질문을 입력하세요.", new Color(1f, 0.72f, 0.3f));
                return;
            }
            if (runtime.InterviewHistory.Count >= QuestionLimit)
            {
                ui.ShowNotice("이 지원자에게 사용할 수 있는 질문 3회를 모두 사용했습니다.", new Color(1f, 0.45f, 0.35f));
                return;
            }

            InterviewResponse response = interviewProvider.Ask(runtime.Candidate, runtime.InterviewHistory, trimmed);
            runtime.InterviewHistory.Add(new InterviewMessage
            {
                Question = trimmed,
                Answer = response.Answer,
                UsedLie = response.UsedLie
            });
            ui.ShowInterview();
            ui.PlayCandidateResponse();
        }

        public void HireSelected()
        {
            if (phase != GamePhase.Interview || Selected.Decision != CandidateDecision.Pending) return;
            if (HiredCount >= HireLimit)
            {
                ui.ShowNotice("채용 한도는 2명입니다.", new Color(1f, 0.45f, 0.35f));
                return;
            }
            if (Selected.Candidate.Salary > CurrentBudget)
            {
                ui.ShowNotice("급여 예산이 부족합니다.", new Color(1f, 0.45f, 0.35f));
                return;
            }

            Selected.Decision = CandidateDecision.Hired;
            ui.ShowNotice($"{Selected.Candidate.Name} 채용 완료 · 남은 예산 {CurrentBudget}", new Color(0.35f, 1f, 0.62f));
            ui.ShowInterview();
        }

        public void RejectSelected()
        {
            if (phase != GamePhase.Interview || Selected.Decision != CandidateDecision.Pending) return;
            Selected.Decision = CandidateDecision.Rejected;
            ui.ShowNotice($"{Selected.Candidate.Name} 지원을 거절했습니다.", new Color(0.75f, 0.8f, 0.9f));
            ui.ShowInterview();
        }

        public void BeginDeployment()
        {
            if (phase != GamePhase.Interview || HiredCount == 0)
            {
                ui.ShowNotice("전투에 투입할 지원자를 최소 1명 채용하세요.", new Color(1f, 0.55f, 0.3f));
                return;
            }
            phase = GamePhase.Deployment;
            selectedDeploymentCandidate = candidates.FindIndex(entry => entry.Decision == CandidateDecision.Hired);
            ui.ShowDeployment();
        }

        public void SelectDeploymentCandidate(int candidateIndex)
        {
            if (phase != GamePhase.Deployment || candidateIndex < 0 || candidateIndex >= candidates.Count) return;
            if (candidates[candidateIndex].Decision != CandidateDecision.Hired) return;
            selectedDeploymentCandidate = candidateIndex;
            ui.ShowDeployment();
        }

        public void AssignSelectedToSlot(int slotIndex)
        {
            if (phase != GamePhase.Deployment || selectedDeploymentCandidate < 0 || slotIndex < 0 || slotIndex >= slotPositions.Length) return;
            CandidateRuntime selected = candidates[selectedDeploymentCandidate];
            CandidateRuntime occupant = candidates.FirstOrDefault(entry => entry.SlotIndex == slotIndex);
            if (occupant != null) occupant.SlotIndex = -1;
            selected.SlotIndex = slotIndex;
            ui.ShowDeployment();
        }

        public void BackToInterview()
        {
            if (phase != GamePhase.Deployment) return;
            foreach (CandidateRuntime entry in candidates) entry.SlotIndex = -1;
            phase = GamePhase.Interview;
            ui.ShowInterview();
        }

        public void BeginBattle()
        {
            if (phase != GamePhase.Deployment) return;
            if (candidates.Any(entry => entry.Decision == CandidateDecision.Hired && entry.SlotIndex < 0))
            {
                ui.ShowNotice("채용한 몬스터를 모두 슬롯에 배치하세요.", new Color(1f, 0.55f, 0.3f));
                return;
            }

            phase = GamePhase.Battle;
            dungeonHp = 100;
            enemiesSpawned = 0;
            enemiesResolved = 0;
            battleEnding = false;
            battleEvents.Clear();
            BuildBattleArena();
            BuildMonsters();
            ApplyOpeningTraits();
            nextEnemySpawn = Time.time + 0.6f;
            ui.ShowBattle();
            LogBattleEvent("WAVE 1 START · Enemy Warriors 5명 접근 중");
        }

        public void RestartGame()
        {
            StopAllCoroutines();
            ClearBattleWorld();
            candidates.Clear();
            foreach (Candidate candidate in CandidateDatabase.CreatePhase1Candidates())
                candidates.Add(new CandidateRuntime { Candidate = candidate, Decision = CandidateDecision.Pending });
            selectedCandidate = 0;
            selectedDeploymentCandidate = -1;
            phase = GamePhase.Interview;
            ui.ShowInterview();
            ui.ShowNotice("새 채용 라운드를 시작합니다.", new Color(0.35f, 0.9f, 1f));
        }

        private void BuildBattleArena()
        {
            ClearBattleWorld();
            battleRoot = new GameObject("Phase 1 Battle World");
            CreateWorldShape("Battlefield", new Vector2(0f, -0.15f), new Vector2(18f, 7.2f), new Color(0.035f, 0.055f, 0.1f), -5);
            CreateWorldShape("Lane", new Vector2(0f, -1.15f), new Vector2(17f, 1.45f), new Color(0.09f, 0.12f, 0.18f), -3);
            CreateWorldShape("Dungeon Gate", new Vector2(-7.2f, -0.35f), new Vector2(1.25f, 4.2f), new Color(0.42f, 0.22f, 0.55f), 0);
            CreateWorldLabel("DUNGEON\nGATE", new Vector2(-7.2f, 1.25f), 44, new Color(0.9f, 0.7f, 1f));
            CreateWorldLabel("ENEMY SPAWN  →", new Vector2(6.2f, 2.15f), 34, new Color(1f, 0.55f, 0.4f));
            for (int i = 0; i < slotPositions.Length; i++)
            {
                CreateWorldShape($"Slot {i + 1}", slotPositions[i], new Vector2(1.25f, 1.85f), new Color(0.2f, 0.32f, 0.42f, 0.42f), -1);
                CreateWorldLabel($"SLOT {i + 1}", slotPositions[i] + Vector2.down * 1.18f, 26, new Color(0.5f, 0.7f, 0.85f));
            }
        }

        private void BuildMonsters()
        {
            monsters.Clear();
            foreach (CandidateRuntime runtime in candidates.Where(entry => entry.Decision == CandidateDecision.Hired))
            {
                Vector2 position = slotPositions[runtime.SlotIndex];
                Color color = runtime.Candidate.Trait == TraitId.Coward
                    ? new Color(0.25f, 0.9f, 0.45f)
                    : runtime.Candidate.Trait == TraitId.Reckless
                        ? new Color(0.95f, 0.33f, 0.23f)
                        : new Color(0.25f, 0.82f, 0.95f);
                GameObject view = CreateWorldShape(runtime.Candidate.Name, position, new Vector2(0.92f, 1.32f), color, 2);
                CreateWorldLabel(runtime.Candidate.Name, position + Vector2.up * 0.98f, 30, Color.white, view.transform);
                BattleMonster monster = new BattleMonster
                {
                    Runtime = runtime,
                    View = view,
                    Position = position,
                    TargetPosition = position,
                    Hp = runtime.Candidate.Stats.MaxHp,
                    Record = new PerformanceRecord { Candidate = runtime.Candidate, TraitEvent = "No trait incident." }
                };
                monsters.Add(monster);
            }
        }

        private void ApplyOpeningTraits()
        {
            foreach (BattleMonster monster in monsters)
            {
                if (monster.Runtime.Candidate.Trait == TraitId.Reckless)
                {
                    monster.TargetPosition = new Vector2(2.15f, -1.15f);
                    monster.RecklessCharging = true;
                    monster.Record.TraitEvent = "Broke formation and charged the enemy.";
                    CreateWorldLabel("RECKLESS!\nFORMATION BREAK", monster.Position + Vector2.up * 1.55f, 28, new Color(1f, 0.38f, 0.25f), monster.View.transform);
                    LogBattleEvent($"{monster.Runtime.Candidate.Name}: RECKLESS — 대형을 이탈해 돌진!");
                }
                else if (monster.Runtime.Candidate.Trait == TraitId.TeamPlayer)
                {
                    int buffed = 0;
                    foreach (BattleMonster ally in monsters)
                    {
                        if (ally == monster || Vector2.Distance(ally.Position, monster.Position) > 2.25f) continue;
                        ally.AttackSpeedMultiplier *= 1.2f;
                        buffed++;
                    }
                    GameObject aura = CreateWorldShape("Team Aura", monster.Position, new Vector2(4.2f, 2.7f), new Color(0.15f, 0.8f, 1f, 0.16f), -2);
                    aura.transform.SetParent(monster.View.transform, true);
                    monster.Record.TraitEvent = buffed > 0
                        ? $"Boosted {buffed} nearby ally's Attack Speed by 20%."
                        : "Team aura activated, but no ally was in range.";
                    CreateWorldLabel("TEAM PLAYER AURA\nALLY ATK SPD +20%", monster.Position + Vector2.up * 1.55f, 26, new Color(0.35f, 0.95f, 1f), monster.View.transform);
                    LogBattleEvent($"{monster.Runtime.Candidate.Name}: TEAM_PLAYER — 주변 아군 {buffed}명 공격속도 +20%");
                }
            }
        }

        private void UpdateBattle(float deltaTime)
        {
            if (enemiesSpawned < 5 && Time.time >= nextEnemySpawn)
            {
                SpawnEnemy(enemiesSpawned);
                enemiesSpawned++;
                nextEnemySpawn = Time.time + 0.9f;
            }

            foreach (BattleMonster monster in monsters)
            {
                if (!monster.Active) continue;
                if (monster.Fleeing)
                {
                    monster.Position += new Vector2(-7.5f, 1.8f) * deltaTime;
                    monster.View.transform.position = monster.Position;
                    if (monster.Position.x < -8.8f)
                    {
                        monster.Active = false;
                        monster.View.SetActive(false);
                    }
                    continue;
                }

                if (monster.RecklessCharging && Vector2.Distance(monster.Position, monster.TargetPosition) > 0.08f)
                {
                    monster.Position = Vector2.MoveTowards(monster.Position, monster.TargetPosition,
                        monster.Runtime.Candidate.Stats.MoveSpeed * 1.8f * deltaTime);
                    monster.View.transform.position = monster.Position;
                }
                else
                {
                    monster.RecklessCharging = false;
                }

                BattleEnemy target = enemies
                    .Where(enemy => enemy.Active)
                    .OrderBy(enemy => Vector2.Distance(monster.Position, enemy.Position))
                    .FirstOrDefault();
                if (target != null
                    && Vector2.Distance(monster.Position, target.Position) <= monster.Runtime.Candidate.Stats.Range
                    && Time.time >= monster.NextAttack)
                {
                    monster.NextAttack = Time.time + 1f / (monster.Runtime.Candidate.Stats.AttackSpeed * monster.AttackSpeedMultiplier);
                    float damage = monster.Runtime.Candidate.Stats.Attack;
                    monster.Record.Damage += damage;
                    FlashBetween(monster.Position, target.Position, new Color(0.35f, 1f, 0.75f));
                    if (target.TakeDamage(damage))
                    {
                        monster.Record.Kills++;
                        ResolveEnemy(target, false);
                    }
                }
            }

            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                BattleEnemy enemy = enemies[i];
                if (!enemy.Active) continue;
                BattleMonster assigned = enemy.AssignedMonster;
                if (assigned != null && assigned.Active && !assigned.Fleeing)
                {
                    float distance = Vector2.Distance(enemy.Position, assigned.Position);
                    if (distance <= 0.95f)
                    {
                        if (Time.time >= enemy.NextAttack)
                        {
                            enemy.NextAttack = Time.time + 1.15f;
                            float applied = Mathf.Min(enemy.Attack, assigned.Hp);
                            assigned.Hp -= enemy.Attack;
                            assigned.Record.DamageTaken += applied;
                            FlashBetween(enemy.Position, assigned.Position, new Color(1f, 0.35f, 0.25f));
                            if (assigned.Runtime.Candidate.Trait == TraitId.Coward
                                && assigned.Hp <= assigned.Runtime.Candidate.Stats.MaxHp * 0.5f
                                && !assigned.Fleeing)
                            {
                                assigned.Fleeing = true;
                                assigned.Record.TraitEvent = "Ran away from battle below 50% HP.";
                                CreateWorldLabel("COWARD!\nRUNNING AWAY", assigned.Position + Vector2.up * 1.55f, 30,
                                    new Color(1f, 0.85f, 0.25f), assigned.View.transform);
                                LogBattleEvent($"{assigned.Runtime.Candidate.Name}: COWARD — HP 50% 이하, 전장에서 도주!");
                            }
                            else if (assigned.Hp <= 0f)
                            {
                                assigned.Active = false;
                                assigned.View.SetActive(false);
                                LogBattleEvent($"{assigned.Runtime.Candidate.Name} 전투 불능");
                            }
                        }
                    }
                    else
                    {
                        enemy.Position = Vector2.MoveTowards(enemy.Position, assigned.Position + Vector2.right * 0.8f,
                            enemy.MoveSpeed * deltaTime);
                        enemy.View.transform.position = enemy.Position;
                    }
                }
                else
                {
                    enemy.Position += Vector2.left * enemy.MoveSpeed * deltaTime;
                    enemy.View.transform.position = enemy.Position;
                    if (enemy.Position.x <= -6.45f)
                    {
                        dungeonHp = Mathf.Max(0, dungeonHp - 20);
                        LogBattleEvent($"Dungeon Gate 피격! HP {dungeonHp}/100");
                        ResolveEnemy(enemy, true);
                    }
                }
            }

            ui.UpdateBattleHud();
            if (dungeonHp <= 0)
                StartCoroutine(FinishBattle(false));
            else if (enemiesSpawned == 5 && enemiesResolved == 5)
                StartCoroutine(FinishBattle(true));
        }

        private void SpawnEnemy(int index)
        {
            Vector2 position = new Vector2(7.4f + index * 0.35f, -1.15f);
            GameObject view = CreateWorldShape($"Enemy Warrior {index + 1}", position, new Vector2(0.78f, 1.22f),
                new Color(0.95f, 0.72f, 0.2f), 2);
            CreateWorldLabel($"HERO {index + 1}", position + Vector2.up * 0.92f, 24, new Color(1f, 0.85f, 0.42f), view.transform);
            List<BattleMonster> activeTargets = monsters.Where(monster => monster.Active).ToList();
            BattleEnemy enemy = new BattleEnemy
            {
                View = view,
                Position = position,
                Hp = 58f,
                Attack = 13f,
                MoveSpeed = 1.05f,
                AssignedMonster = activeTargets.Count == 0 ? null : activeTargets[index % activeTargets.Count]
            };
            enemies.Add(enemy);
        }

        private void ResolveEnemy(BattleEnemy enemy, bool reachedGate)
        {
            if (!enemy.Active) return;
            enemy.Active = false;
            enemy.View.SetActive(false);
            enemiesResolved++;
            if (!reachedGate) LogBattleEvent($"Enemy Warrior 처치 · {enemiesResolved}/5 해결");
        }

        private IEnumerator FinishBattle(bool allEnemiesResolved)
        {
            if (battleEnding) yield break;
            battleEnding = true;
            bool victory = dungeonHp > 0 && allEnemiesResolved;
            LogBattleEvent(victory ? "WAVE CLEAR" : "GAME OVER");
            ui.UpdateBattleHud();
            yield return new WaitForSeconds(1.6f);
            phase = GamePhase.Review;
            ui.ShowReview(victory, monsters.Select(monster => monster.Record).ToList());
        }

        public string GetBattleRosterText()
        {
            if (monsters.Count == 0) return string.Empty;
            return string.Join("\n", monsters.Select(monster =>
            {
                float shownHp = Mathf.Max(0f, monster.Hp);
                string state = monster.Fleeing ? "FLEEING" : !monster.Active ? "OUT" : "ACTIVE";
                return $"{monster.Runtime.Candidate.Name,-8}  HP {shownHp:0}/{monster.Runtime.Candidate.Stats.MaxHp:0}  [{state}]";
            }));
        }

        public string GetBattleEventText()
        {
            return string.Join("\n", battleEvents.TakeLast(6));
        }

        private void LogBattleEvent(string message)
        {
            battleEvents.Add("• " + message);
            if (ui != null && phase == GamePhase.Battle) ui.UpdateBattleHud();
        }

        private GameObject CreateWorldShape(string name, Vector2 position, Vector2 scale, Color color, int order)
        {
            GameObject obj = GameBootstrap.CreateActor(name, position, scale, color, order);
            obj.transform.SetParent(battleRoot.transform, true);
            return obj;
        }

        private void CreateWorldLabel(string label, Vector2 position, int fontSize, Color color, Transform parent = null)
        {
            GameObject obj = new GameObject(label + " Label");
            obj.transform.position = new Vector3(position.x, position.y, -0.1f);
            if (parent == null) obj.transform.SetParent(battleRoot.transform, true);
            else obj.transform.SetParent(parent, true);
            TextMesh text = obj.AddComponent<TextMesh>();
            text.text = label;
            text.fontSize = fontSize;
            text.characterSize = 0.055f;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = color;
            text.fontStyle = FontStyle.Bold;
            obj.GetComponent<MeshRenderer>().sortingOrder = 50;
        }

        private void FlashBetween(Vector2 from, Vector2 to, Color color)
        {
            Vector2 midpoint = (from + to) * 0.5f;
            float length = Vector2.Distance(from, to);
            GameObject flash = CreateWorldShape("Attack Flash", midpoint, new Vector2(length, 0.08f), color, 4);
            float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
            flash.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            Destroy(flash, 0.12f);
        }

        private void ClearBattleWorld()
        {
            if (battleRoot != null) Destroy(battleRoot);
            battleRoot = null;
            monsters.Clear();
            enemies.Clear();
        }

        private sealed class BattleMonster
        {
            public CandidateRuntime Runtime;
            public GameObject View;
            public Vector2 Position;
            public Vector2 TargetPosition;
            public float Hp;
            public float NextAttack;
            public float AttackSpeedMultiplier = 1f;
            public bool Active = true;
            public bool Fleeing;
            public bool RecklessCharging;
            public PerformanceRecord Record;
        }

        private sealed class BattleEnemy
        {
            public GameObject View;
            public Vector2 Position;
            public float Hp;
            public float Attack;
            public float MoveSpeed;
            public float NextAttack;
            public bool Active = true;
            public BattleMonster AssignedMonster;

            public bool TakeDamage(float damage)
            {
                Hp -= damage;
                return Hp <= 0f;
            }
        }
    }
}
