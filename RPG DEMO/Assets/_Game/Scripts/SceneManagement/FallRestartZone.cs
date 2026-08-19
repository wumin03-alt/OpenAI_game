using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SceneManagement
{
    /// <summary>플레이어가 낙사하면 사망 연출 없이 현재 스테이지를 바로 재시작합니다.</summary>
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
            if (restarting || !other.CompareTag("Player")) return;

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
