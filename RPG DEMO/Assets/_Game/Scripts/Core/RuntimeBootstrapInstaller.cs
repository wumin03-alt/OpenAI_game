using Game.Audio;
using Game.Save;
using Game.SceneManagement;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Core
{
    /// <summary>
    /// Bootstrap 씬의 직렬화 의존성을 최소화하기 위한 설치 컴포넌트입니다.
    /// 실행 시 App에 필요한 공통 컴포넌트와 페이드 UI를 구성합니다.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class RuntimeBootstrapInstaller : MonoBehaviour
    {
        private void Awake()
        {
            AddIfMissing<Bootstrapper>();
            AddIfMissing<GameManager>();
            AddIfMissing<GameSession>();
            AddIfMissing<SaveManager>();
            AddIfMissing<AudioManager>();
            AddIfMissing<FallbackCameraController>();

            EnsureCommonUI();
            AddIfMissing<PauseMenuController>();

            // ScreenFader가 먼저 만들어진 뒤 SceneLoader가 이를 자동으로 찾게 합니다.
            AddIfMissing<SceneLoader>();
        }

        private void EnsureCommonUI()
        {
            if (GetComponentInChildren<ScreenFader>(true) != null) return;

            GameObject commonUi = new GameObject("CommonUI", typeof(RectTransform));
            commonUi.transform.SetParent(transform, false);

            Canvas canvas = commonUi.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            commonUi.AddComponent<CanvasScaler>();
            commonUi.AddComponent<GraphicRaycaster>();
            commonUi.AddComponent<CanvasGroup>();
            commonUi.AddComponent<ScreenFader>();

            RectTransform uiRect = commonUi.GetComponent<RectTransform>();
            uiRect.anchorMin = Vector2.zero;
            uiRect.anchorMax = Vector2.one;
            uiRect.offsetMin = Vector2.zero;
            uiRect.offsetMax = Vector2.zero;

            GameObject fadeImageObject = new GameObject("FadeImage", typeof(RectTransform), typeof(Image));
            fadeImageObject.transform.SetParent(commonUi.transform, false);

            RectTransform fadeRect = fadeImageObject.GetComponent<RectTransform>();
            fadeRect.anchorMin = Vector2.zero;
            fadeRect.anchorMax = Vector2.one;
            fadeRect.offsetMin = Vector2.zero;
            fadeRect.offsetMax = Vector2.zero;
            fadeImageObject.GetComponent<Image>().color = Color.black;
        }

        private T AddIfMissing<T>() where T : Component
        {
            T component = GetComponent<T>();
            return component != null ? component : gameObject.AddComponent<T>();
        }
    }
}
