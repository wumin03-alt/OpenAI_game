using System.Collections;
using Game.Core;
using Game.Save;
using Game.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.SceneManagement
{
    /// <summary>페이드와 비동기 로딩을 포함하는 모든 씬 전환의 단일 진입점입니다.</summary>
    public sealed class SceneLoader : MonoBehaviour
    {
        public static SceneLoader Instance { get; private set; }

        [SerializeField] private ScreenFader screenFader;
        [SerializeField, Min(0f)] private float fadeDuration = 0.25f;

        public bool IsLoading { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;

            if (screenFader == null)
                screenFader = GetComponentInChildren<ScreenFader>(true);
        }

        public void LoadScene(string sceneName)
        {
            if (IsLoading || string.IsNullOrWhiteSpace(sceneName)) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        public void ReloadCurrentScene()
        {
            LoadScene(SceneManager.GetActiveScene().name);
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            IsLoading = true;
            Time.timeScale = 1f;
            GameManager.Instance?.SetState(GameState.Loading);

            if (screenFader != null)
                yield return screenFader.FadeOut(fadeDuration);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"[SceneLoader] 씬을 불러올 수 없습니다: {sceneName}");
                IsLoading = false;
                yield break;
            }

            while (!operation.isDone)
                yield return null;

            RecordLoadedStage(sceneName);

            if (screenFader != null)
                yield return screenFader.FadeIn(fadeDuration);

            GameManager.Instance?.SetState(GameState.Playing);
            IsLoading = false;
        }

        private static void RecordLoadedStage(string sceneName)
        {
            if (!sceneName.StartsWith("Stage", System.StringComparison.OrdinalIgnoreCase)) return;

            string suffix = sceneName.Substring("Stage".Length);
            int digitCount = 0;
            while (digitCount < suffix.Length && char.IsDigit(suffix[digitCount]))
                digitCount++;

            if (digitCount == 0 || !int.TryParse(suffix.Substring(0, digitCount), out int stageNumber))
                return;

            GameSession.Instance?.EnterStage(stageNumber, sceneName);
            SaveManager.Instance?.UnlockStage(stageNumber);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
