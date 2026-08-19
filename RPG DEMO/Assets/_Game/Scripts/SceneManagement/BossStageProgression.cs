using UnityEngine;

namespace Game.SceneManagement
{
    /// <summary>보스 처치 후 우측 출구를 열어 플레이어가 직접 다음 스테이지로 나가게 합니다.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class BossStageProgression : MonoBehaviour
    {
        [SerializeField] private GameObject exitGate;

        private Health health;
        private bool triggered;

        private void Awake()
        {
            health = GetComponent<Health>();
            if (exitGate != null)
                exitGate.SetActive(false);
        }

        private void OnEnable()
        {
            if (health != null)
                health.onDeath.AddListener(HandleBossDeath);
        }

        private void OnDisable()
        {
            if (health != null)
                health.onDeath.RemoveListener(HandleBossDeath);
        }

        private void HandleBossDeath()
        {
            if (triggered) return;

            triggered = true;
            if (exitGate != null)
                exitGate.SetActive(true);
            else
                Debug.LogWarning("[BossStageProgression] 열어 줄 출구가 연결되지 않았습니다.");
        }
    }
}
