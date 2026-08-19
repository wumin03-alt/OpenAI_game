using Game.Audio;
using Game.Core;
using Game.Save;
using Game.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>게임 플레이 중 ESC로 여는 공통 일시정지 메뉴입니다.</summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        private GameObject menuRoot;
        private GameObject pausePanel;
        private GameObject settingsPanel;

        private void Awake()
        {
            BuildUI();
            menuRoot.SetActive(false);
        }

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (!IsGameplayScene()) return;
            if (SceneLoader.Instance != null && SceneLoader.Instance.IsLoading) return;

            if (menuRoot.activeSelf)
            {
                if (settingsPanel.activeSelf)
                    ShowPausePanel();
                else
                    Resume();
            }
            else
            {
                Open();
            }
        }

        private void BuildUI()
        {
            Canvas canvas = RuntimeUIFactory.CreateCanvas("PauseMenuCanvas", transform, short.MaxValue - 1);
            menuRoot = canvas.gameObject;

            Image shade = RuntimeUIFactory.CreateImage(canvas.transform, "PauseShade",
                new Color(0f, 0f, 0f, 0.72f));
            RuntimeUIFactory.Stretch(shade.rectTransform);

            pausePanel = new GameObject("PausePanel", typeof(RectTransform));
            pausePanel.transform.SetParent(canvas.transform, false);
            RuntimeUIFactory.Stretch(pausePanel.GetComponent<RectTransform>());

            RuntimeUIFactory.CreateText(pausePanel.transform, "PAUSED", 58,
                new Vector2(0f, 285f), new Vector2(600f, 90f), Color.white);
            RuntimeUIFactory.CreateButton(pausePanel.transform, "RESUME", new Vector2(0f, 155f),
                new Vector2(430f, 66f), Resume);
            RuntimeUIFactory.CreateButton(pausePanel.transform, "RESTART STAGE", new Vector2(0f, 75f),
                new Vector2(430f, 66f), RestartStage);
            RuntimeUIFactory.CreateButton(pausePanel.transform, "SETTINGS", new Vector2(0f, -5f),
                new Vector2(430f, 66f), ShowSettings);
            RuntimeUIFactory.CreateButton(pausePanel.transform, "MAIN MENU", new Vector2(0f, -85f),
                new Vector2(430f, 66f), LoadMainMenu);
            RuntimeUIFactory.CreateButton(pausePanel.transform, "QUIT GAME", new Vector2(0f, -165f),
                new Vector2(430f, 66f), Quit);

            BuildSettingsPanel(canvas.transform);
        }

        private void BuildSettingsPanel(Transform canvas)
        {
            settingsPanel = new GameObject("PauseSettingsPanel", typeof(RectTransform));
            settingsPanel.transform.SetParent(canvas, false);
            RuntimeUIFactory.Stretch(settingsPanel.GetComponent<RectTransform>());

            RuntimeUIFactory.CreateText(settingsPanel.transform, "SETTINGS", 50,
                new Vector2(0f, 270f), new Vector2(600f, 80f), Color.white);

            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : new SaveData();
            RuntimeUIFactory.CreateSlider(settingsPanel.transform, "MASTER VOLUME", new Vector2(0f, 130f),
                data.masterVolume, value => AudioManager.Instance?.SetMasterVolume(value));
            RuntimeUIFactory.CreateSlider(settingsPanel.transform, "MUSIC VOLUME", new Vector2(0f, 20f),
                data.musicVolume, value => AudioManager.Instance?.SetMusicVolume(value));
            RuntimeUIFactory.CreateSlider(settingsPanel.transform, "SFX VOLUME", new Vector2(0f, -90f),
                data.sfxVolume, value => AudioManager.Instance?.SetSfxVolume(value));
            RuntimeUIFactory.CreateButton(settingsPanel.transform, "BACK", new Vector2(0f, -230f),
                new Vector2(340f, 68f), ShowPausePanel);
        }

        private void Open()
        {
            RuntimeUIFactory.EnsureEventSystem();
            menuRoot.SetActive(true);
            ShowPausePanel();
            GameManager.Instance?.Pause();
        }

        private void Resume()
        {
            menuRoot.SetActive(false);
            GameManager.Instance?.Resume();
            if (GameManager.Instance == null) Time.timeScale = 1f;
        }

        private void RestartStage()
        {
            PrepareSceneChange();
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.ReloadCurrentScene();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void LoadMainMenu()
        {
            PrepareSceneChange();
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene("MainMenu");
            else
                SceneManager.LoadScene("MainMenu");
        }

        private void ShowSettings()
        {
            pausePanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void ShowPausePanel()
        {
            settingsPanel.SetActive(false);
            pausePanel.SetActive(true);
        }

        private void PrepareSceneChange()
        {
            Time.timeScale = 1f;
            menuRoot.SetActive(false);
        }

        private void Quit()
        {
            Time.timeScale = 1f;
            SaveManager.Instance?.Save();
            Application.Quit();
            Debug.Log("[PauseMenu] 게임 종료 요청");
        }

        private static bool IsGameplayScene()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(sceneName)
                   && sceneName != "Bootstrap"
                   && sceneName != "MainMenu";
        }
    }
}
