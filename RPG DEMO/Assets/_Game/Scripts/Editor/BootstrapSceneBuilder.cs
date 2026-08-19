#if UNITY_EDITOR
using System.Linq;
using Game.Audio;
using Game.Core;
using Game.Save;
using Game.SceneManagement;
using Game.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.EditorTools
{
    public static class BootstrapSceneBuilder
    {
        private const string BootstrapScenePath = "Assets/_Game/Scenes/Bootstrap.unity";

        [MenuItem("Tools/Game/Create or Refresh Bootstrap Scene")]
        public static void CreateBootstrapScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject app = new GameObject("App");
            app.AddComponent<Bootstrapper>();
            app.AddComponent<GameManager>();
            app.AddComponent<GameSession>();
            app.AddComponent<SceneLoader>();
            app.AddComponent<SaveManager>();
            app.AddComponent<AudioManager>();

            GameObject commonUi = new GameObject("CommonUI", typeof(RectTransform));
            commonUi.transform.SetParent(app.transform, false);

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

            EditorSceneManager.SaveScene(scene, BootstrapScenePath);
            PrependBootstrapToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[BootstrapSceneBuilder] 생성 완료: {BootstrapScenePath}");
        }

        private static void PrependBootstrapToBuildSettings()
        {
            EditorBuildSettingsScene bootstrap = new EditorBuildSettingsScene(BootstrapScenePath, true);
            EditorBuildSettingsScene[] existing = EditorBuildSettings.scenes
                .Where(item => item.path != BootstrapScenePath)
                .ToArray();

            EditorBuildSettings.scenes = new[] { bootstrap }.Concat(existing).ToArray();
        }
    }
}
#endif
