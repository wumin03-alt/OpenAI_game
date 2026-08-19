using UnityEngine;

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

        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
        }

        public void EnterStage(int stageNumber, string sceneName)
        {
            CurrentStage = Mathf.Max(1, stageNumber);
            CurrentSceneName = sceneName ?? string.Empty;
        }

        public void StorePlayerHP(float currentHP)
        {
            PlayerHP = Mathf.Max(0f, currentHP);
        }

        public void ResetRun()
        {
            CurrentStage = 1;
            CurrentSceneName = string.Empty;
            PlayerHP = -1f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
