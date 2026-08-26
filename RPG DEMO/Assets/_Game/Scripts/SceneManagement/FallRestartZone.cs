using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SceneManagement
{
    /// <summary>플레이어 낙사는 씬을 재시작하고, 잡몹 낙사는 즉시 사망 처리합니다.</summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class FallRestartZone : MonoBehaviour
    {
        private bool restarting;

        private void Awake()
        {
            Collider2D trigger = GetComponent<Collider2D>();
            trigger.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 자식 공격/발판 콜라이더가 먼저 닿더라도 루트의 Health로 판정합니다.
            Health actorHealth = other.GetComponentInParent<Health>();

            if (actorHealth != null && actorHealth.CompareTag("Enemy"))
            {
                if (!actorHealth.IsDead)
                {
                    Debug.Log($"[FallRestartZone] {actorHealth.name} 낙사 처리");
                    actorHealth.TakeDamage(Mathf.Max(1f, actorHealth.CurrentHP), true);
                }
                return;
            }

            bool isPlayer = actorHealth != null
                ? actorHealth.CompareTag("Player")
                : other.CompareTag("Player");
            if (restarting || !isPlayer) return;

            restarting = true;
            Time.timeScale = 1f;
            GameManager.Instance?.SetState(GameState.Loading);

            if (SceneLoader.Instance != null)
                SceneLoader.Instance.ReloadCurrentScene();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
