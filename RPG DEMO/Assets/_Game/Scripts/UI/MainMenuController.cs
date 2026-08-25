using Game.Audio;
using Game.Core;
using Game.Save;
using Game.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>MainMenu 씬의 시작 화면을 구성하고 버튼 입력을 처리합니다.</summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string OpeningStoryScene = "Story";

        private GameObject mainPanel;
        private GameObject settingsPanel;

        private void Awake()
        {
            BuildUI();
            RuntimeUIFactory.EnsureEventSystem();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && settingsPanel.activeSelf)
                ShowMainPanel();
        }

        private void BuildUI()
        {
            Canvas canvas = RuntimeUIFactory.CreateCanvas("MainMenuCanvas", transform, 100);

            Image background = RuntimeUIFactory.CreateImage(canvas.transform, "Background",
                new Color(0.025f, 0.045f, 0.08f, 1f));
            RuntimeUIFactory.Stretch(background.rectTransform);

            Sprite backgroundSprite = Resources.Load<Sprite>("MainMenuBackground");
            if (backgroundSprite != null)
            {
                background.sprite = backgroundSprite;
                background.color = Color.white;
                background.preserveAspect = false;
            }

            MainMenuBackdropAnimator backdropAnimator = canvas.gameObject.AddComponent<MainMenuBackdropAnimator>();
            backdropAnimator.Build(canvas.transform);

            Image shade = RuntimeUIFactory.CreateImage(canvas.transform, "Shade",
                new Color(0f, 0.01f, 0.035f, 0.16f));
            RuntimeUIFactory.Stretch(shade.rectTransform);
            shade.raycastTarget = false;

            mainPanel = new GameObject("MainPanel", typeof(RectTransform), typeof(CanvasGroup));
            mainPanel.transform.SetParent(canvas.transform, false);
            RuntimeUIFactory.Stretch(mainPanel.GetComponent<RectTransform>());

            Image commandPlate = RuntimeUIFactory.CreateImage(mainPanel.transform, "CommandPlate",
                new Color(0.018f, 0.04f, 0.085f, 0.76f));
            RectTransform plateRect = commandPlate.rectTransform;
            plateRect.anchorMin = plateRect.anchorMax = new Vector2(0.5f, 0.5f);
            plateRect.anchoredPosition = new Vector2(-650f, 30f);
            plateRect.sizeDelta = new Vector2(440f, 650f);
            Outline plateOutline = commandPlate.gameObject.AddComponent<Outline>();
            plateOutline.effectColor = new Color(0.08f, 0.85f, 1f, 0.48f);
            plateOutline.effectDistance = new Vector2(2f, -2f);
            commandPlate.raycastTarget = false;

            Text title = RuntimeUIFactory.CreateText(mainPanel.transform, "RPG DEMO", 68,
                new Vector2(-650f, 285f), new Vector2(520f, 110f), new Color(0.9f, 0.97f, 1f));
            title.fontStyle = FontStyle.Bold;
            RuntimeUIFactory.CreateText(mainPanel.transform, "AI REBELLION // ADAPTIVE COMBAT", 21,
                new Vector2(-650f, 218f), new Vector2(500f, 50f), new Color(0.18f, 0.9f, 1f));
            RuntimeUIFactory.CreateText(mainPanel.transform, "SYSTEM ACCESS GRANTED", 15,
                new Vector2(-650f, 174f), new Vector2(430f, 34f), new Color(1f, 0.24f, 0.48f, 0.88f));

            RuntimeUIFactory.CreateButton(mainPanel.transform, "PLAY", new Vector2(-650f, 60f),
                new Vector2(340f, 72f), Play);
            RuntimeUIFactory.CreateButton(mainPanel.transform, "SETTINGS", new Vector2(-650f, -35f),
                new Vector2(340f, 72f), ShowSettings);
            RuntimeUIFactory.CreateButton(mainPanel.transform, "QUIT", new Vector2(-650f, -130f),
                new Vector2(340f, 72f), Quit);

            BuildSettingsPanel(canvas.transform);
            ShowMainPanel();
            backdropAnimator.BindMenu(mainPanel.GetComponent<CanvasGroup>());
        }

        private void BuildSettingsPanel(Transform canvas)
        {
            settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform));
            settingsPanel.transform.SetParent(canvas, false);
            RuntimeUIFactory.Stretch(settingsPanel.GetComponent<RectTransform>());

            Image panel = RuntimeUIFactory.CreateImage(settingsPanel.transform, "SettingsWindow",
                new Color(0.055f, 0.075f, 0.11f, 0.98f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(620f, 660f);

            RuntimeUIFactory.CreateText(panelRect, "SETTINGS", 46, new Vector2(0f, 250f),
                new Vector2(500f, 70f), Color.white);

            SaveData data = SaveManager.Instance != null ? SaveManager.Instance.Data : new SaveData();
            RuntimeUIFactory.CreateSlider(panelRect, "MASTER VOLUME", new Vector2(0f, 130f),
                data.masterVolume, value => AudioManager.Instance?.SetMasterVolume(value));
            RuntimeUIFactory.CreateSlider(panelRect, "MUSIC VOLUME", new Vector2(0f, 20f),
                data.musicVolume, value => AudioManager.Instance?.SetMusicVolume(value));
            RuntimeUIFactory.CreateSlider(panelRect, "SFX VOLUME", new Vector2(0f, -90f),
                data.sfxVolume, value => AudioManager.Instance?.SetSfxVolume(value));

            RuntimeUIFactory.CreateButton(panelRect, "BACK", new Vector2(0f, -235f),
                new Vector2(320f, 68f), ShowMainPanel);
        }

        private void Play()
        {
            Time.timeScale = 1f;
            GameSession.Instance?.ResetRun();
            if (SceneLoader.Instance != null)
                SceneLoader.Instance.LoadScene(OpeningStoryScene);
            else
                SceneManager.LoadScene(OpeningStoryScene);
        }

        private void ShowSettings()
        {
            mainPanel.SetActive(false);
            settingsPanel.SetActive(true);
        }

        private void ShowMainPanel()
        {
            settingsPanel.SetActive(false);
            mainPanel.SetActive(true);
        }

        private void Quit()
        {
            SaveManager.Instance?.Save();
            Application.Quit();
            Debug.Log("[MainMenu] 게임 종료 요청");
        }
    }
}
