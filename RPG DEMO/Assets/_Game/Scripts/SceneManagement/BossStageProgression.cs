using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SceneManagement
{
    /// <summary>보스 처치 후 우측 출구를 열어 플레이어가 직접 다음 스테이지로 나가게 합니다.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class BossStageProgression : MonoBehaviour
    {
        [SerializeField] private GameObject exitGate;

        private Health health;
        private bool triggered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadInstaller()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RemoveResultPanelsFromPreviousScene(scene);

            GameObject boss = GameObject.FindGameObjectWithTag("Boss");
            if (boss == null || boss.GetComponent<Health>() == null) return;
            if (boss.GetComponent<BossStageProgression>() == null)
            {
                boss.AddComponent<BossStageProgression>();
                Debug.Log($"[BossStageProgression] {scene.name} 런타임 연결 완료");
            }
        }

        private static void RemoveResultPanelsFromPreviousScene(Scene loadedScene)
        {
            RectTransform[] rects = FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (RectTransform rect in rects)
            {
                if (rect == null || rect.gameObject.name != "BossResultPanel") continue;
                if (rect.gameObject.scene == loadedScene) continue;

                rect.gameObject.SetActive(false);
                Destroy(rect.gameObject);
                Debug.Log("[BossStageProgression] 이전 결과 패널을 재시도 시 제거했습니다.");
            }
        }

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
            StartCoroutine(EnsureResultPanelVisible());
            if (exitGate != null)
                exitGate.SetActive(true);
            else
                Debug.LogWarning("[BossStageProgression] 열어 줄 출구가 연결되지 않았습니다.");
        }

        private IEnumerator EnsureResultPanelVisible()
        {
            // BossLearningHUD의 onDeath 구독도 같은 프레임에 실행되므로 한 프레임 기다립니다.
            // timeScale이 0이어도 yield return null은 다음 렌더 프레임에 계속됩니다.
            yield return null;

            RectTransform[] rects = FindObjectsByType<RectTransform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (RectTransform rect in rects)
            {
                if (rect == null || rect.gameObject.name != "BossResultPanel") continue;

                rect.gameObject.SetActive(true);
                rect.SetParent(null, false);
                Scene activeScene = SceneManager.GetActiveScene();
                if (activeScene.IsValid())
                    SceneManager.MoveGameObjectToScene(rect.gameObject, activeScene);
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                Canvas resultCanvas = rect.GetComponent<Canvas>();
                if (resultCanvas == null) resultCanvas = rect.gameObject.AddComponent<Canvas>();
                resultCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                resultCanvas.overrideSorting = true;
                resultCanvas.sortingOrder = short.MaxValue;
                rect.SetAsLastSibling();
                Debug.Log("[BossStageProgression] 기존 보스 결과 패널을 최상단에 표시했습니다.");
                yield break;
            }

            // 결과 UI가 생성되지 않은 경우 플레이가 영구 정지하지 않도록 복구합니다.
            if (Time.timeScale <= 0f)
            {
                Debug.LogError("[BossStageProgression] 결과 패널을 찾지 못해 시간을 복구합니다.");
                Time.timeScale = 1f;
            }
        }
    }
}
