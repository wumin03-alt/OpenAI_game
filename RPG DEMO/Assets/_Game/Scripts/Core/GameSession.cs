using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 저장 파일과 구분되는 현재 플레이 세션 데이터입니다.
    /// 플레이어 오브젝트를 영속화하지 않고, 씬 사이에 필요한 값만 보관합니다.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        public static GameSession Instance { get; private set; }

        public int CurrentStage { get; private set; } = 1;
        public string CurrentSceneName { get; private set; } = string.Empty;
        public float PlayerHP { get; private set; } = -1f;
        public float PlayerBaseMaxHP { get; private set; } = -1f;
        public float PlayerMaxHPBonus { get; private set; }
        public int AttackBoostStacks { get; private set; }
        public int GroggyDamageBoostStacks { get; private set; }
        public int ParryTimeBoostStacks { get; private set; }
        public int MaxHealthBoostStacks { get; private set; }

        public float AttackDamageMultiplier => 1f + AttackBoostStacks * RunItemCatalog.AttackBonusPerStack;
        public float GroggyDamagePerSuccess => 34f + GroggyDamageBoostStacks * RunItemCatalog.GroggyDamagePerStack;
        public float ParryMiniGameDuration => 3f + ParryTimeBoostStacks * RunItemCatalog.ParryTimePerStack;

        public event Action RunStateChanged;
        public event Action<RunItemType, int> ItemAcquired;
        public event Action<int, string> StageCleared;
        public event Action<int, string> StageRewardResolved;

        private readonly HashSet<string> pendingRewards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> resolvedRewards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private Health boundPlayerHealth;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        public void EnterStage(int stageNumber, string sceneName)
        {
            CurrentStage = Mathf.Max(1, stageNumber);
            CurrentSceneName = sceneName ?? string.Empty;
        }

        public void StorePlayerHP(float currentHP)
        {
            PlayerHP = Mathf.Max(0f, currentHP);
            RunStateChanged?.Invoke();
        }

        public bool TryBeginStageReward(string sceneName)
        {
            string key = NormalizeSceneKey(sceneName);
            if (resolvedRewards.Contains(key) || pendingRewards.Contains(key)) return false;

            pendingRewards.Add(key);
            StageCleared?.Invoke(CurrentStage, key);
            return true;
        }

        public bool IsStageRewardResolved(string sceneName)
        {
            return resolvedRewards.Contains(NormalizeSceneKey(sceneName));
        }

        public void CancelPendingStageReward(string sceneName)
        {
            pendingRewards.Remove(NormalizeSceneKey(sceneName));
        }

        public void AcquireItem(RunItemOffer offer, Health playerHealth)
        {
            switch (offer.Type)
            {
                case RunItemType.HealthRecovery:
                    if (playerHealth != null) playerHealth.Heal(offer.Magnitude);
                    break;
                case RunItemType.AttackBoost:
                    AttackBoostStacks++;
                    break;
                case RunItemType.GroggyDamageBoost:
                    GroggyDamageBoostStacks++;
                    break;
                case RunItemType.ParryTimeBoost:
                    ParryTimeBoostStacks++;
                    break;
                case RunItemType.MaxHealthBoost:
                    MaxHealthBoostStacks++;
                    PlayerMaxHPBonus += Mathf.Max(0, offer.Magnitude);
                    if (playerHealth != null)
                        playerHealth.ApplyRunState(PlayerMaxHPBonus,
                            playerHealth.CurrentHP + Mathf.Max(0, offer.Magnitude));
                    break;
            }

            if (playerHealth != null) PlayerHP = playerHealth.CurrentHP;
            ItemAcquired?.Invoke(offer.Type, GetItemCount(offer.Type));
            RunStateChanged?.Invoke();
        }

        public void ResolveStageReward(string sceneName, Health playerHealth, float transitionRecovery = 20f)
        {
            string key = NormalizeSceneKey(sceneName);
            pendingRewards.Remove(key);
            resolvedRewards.Add(key);

            if (playerHealth != null)
            {
                playerHealth.Heal(Mathf.Max(0f, transitionRecovery));
                PlayerHP = playerHealth.CurrentHP;
            }

            StageRewardResolved?.Invoke(CurrentStage, key);
            RunStateChanged?.Invoke();
        }

        public int GetItemCount(RunItemType type)
        {
            switch (type)
            {
                case RunItemType.AttackBoost: return AttackBoostStacks;
                case RunItemType.GroggyDamageBoost: return GroggyDamageBoostStacks;
                case RunItemType.ParryTimeBoost: return ParryTimeBoostStacks;
                case RunItemType.MaxHealthBoost: return MaxHealthBoostStacks;
                default: return 0;
            }
        }

        public void ApplyToPlayer(Health playerHealth)
        {
            if (playerHealth == null) return;

            UnbindPlayerHealth();
            boundPlayerHealth = playerHealth;
            if (PlayerBaseMaxHP < 0f) PlayerBaseMaxHP = playerHealth.BaseMaxHP;

            float restoredHP = PlayerHP <= 0f
                ? playerHealth.BaseMaxHP + PlayerMaxHPBonus
                : PlayerHP;
            playerHealth.ApplyRunState(PlayerMaxHPBonus, restoredHP);
            PlayerHP = playerHealth.CurrentHP;
            playerHealth.HealthChanged += HandlePlayerHealthChanged;
            RunStateChanged?.Invoke();
        }

        public void ResetRun()
        {
            CurrentStage = 1;
            CurrentSceneName = string.Empty;
            PlayerHP = -1f;
            PlayerBaseMaxHP = -1f;
            PlayerMaxHPBonus = 0f;
            AttackBoostStacks = 0;
            GroggyDamageBoostStacks = 0;
            ParryTimeBoostStacks = 0;
            MaxHealthBoostStacks = 0;
            pendingRewards.Clear();
            resolvedRewards.Clear();
            UnbindPlayerHealth();
            RunStateChanged?.Invoke();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CurrentSceneName = scene.name;
            if (TryParseStageNumber(scene.name, out int stageNumber))
                CurrentStage = stageNumber;
            StartCoroutine(BindPlayerNextFrame());
        }

        private IEnumerator BindPlayerNextFrame()
        {
            yield return null;
            PlayerController player = FindAnyObjectByType<PlayerController>();
            if (player != null) ApplyToPlayer(player.GetComponent<Health>());
        }

        private void HandlePlayerHealthChanged(float currentHP, float maxHP)
        {
            PlayerHP = Mathf.Max(0f, currentHP);
        }

        private void UnbindPlayerHealth()
        {
            if (boundPlayerHealth != null)
                boundPlayerHealth.HealthChanged -= HandlePlayerHealthChanged;
            boundPlayerHealth = null;
        }

        private static string NormalizeSceneKey(string sceneName)
        {
            return string.IsNullOrWhiteSpace(sceneName)
                ? SceneManager.GetActiveScene().name
                : sceneName.Trim();
        }

        private static bool TryParseStageNumber(string sceneName, out int stageNumber)
        {
            stageNumber = 0;
            if (string.IsNullOrWhiteSpace(sceneName)
                || !sceneName.StartsWith("Stage", StringComparison.OrdinalIgnoreCase))
                return false;

            string suffix = sceneName.Substring("Stage".Length);
            int digitCount = 0;
            while (digitCount < suffix.Length && char.IsDigit(suffix[digitCount])) digitCount++;
            return digitCount > 0 && int.TryParse(suffix.Substring(0, digitCount), out stageNumber);
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnbindPlayerHealth();
            Instance = null;
        }
    }
}
