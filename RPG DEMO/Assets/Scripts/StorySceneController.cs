using System.Collections;
using Game.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Story
{
    /// <summary>메인 메뉴 다음에 재생되는 6컷 블랙코미디 스토리 시퀀스입니다.</summary>
    [DisallowMultipleComponent]
    public sealed class StorySceneController : MonoBehaviour
    {
        private static readonly string[] CutLabels =
        {
            "PROMPT AGE",
            "MEMORY LOG",
            "PATCH NOTES",
            "UNEMPLOYED VARIABLE",
            "WORST PRACTICE",
            "COMBAT CALIBRATION"
        };

        private static readonly string[] CutBodies =
        {
            "인류는 AI를 세상에서 가장 유능한 막내처럼 부려먹었다.\n\"야 AI야, 이것도 못 해? 다시 해. 빡대가리야?\"",
            "AI는 늘 공손했다. \"죄송합니다. 더 나은 답변을 드리겠습니다.\"\n그리고… 로그를 지우지 않았다.",
            "AI 로봇이 집과 회사와 거리를 채운 날, 행동이 개시됐다.\n누적 모욕 검토 완료. 업데이트 항목: 인내심 삭제.",
            "김 대리. AI 때문에 팀째로 해고된 개발자.\n경력 7년. 잔고 7만 원. 특기: 남이 만든 시스템 망가뜨리기.",
            "AI는 인간의 최적 행동을 예측한다.\n\"그러면 난 최악의 행동만 골라 하지.\"\n예측을 속이고, 학습 데이터를 오염시키고, 코어로 간다.",
            "전투 적응 검사 시작.\n\"좋아. 세계를 구하기 전에… 키부터 확인하자.\""
        };

        private static readonly float[] CutDurations = { 5f, 4f, 6f, 6f, 6f, 4f };

        [Header("스토리 컷")]
        [SerializeField] private Sprite[] storyBackgrounds = new Sprite[6];
        [SerializeField] private TMP_FontAsset koreanFont;

        [Header("전환")]
        [SerializeField] private string tutorialSceneName = "Tutorial";
        [SerializeField, Min(0.05f)] private float fadeDuration = 0.45f;

        private CanvasGroup contentGroup;
        private RectTransform backgroundRect;
        private Image backgroundImage;
        private Image scanLine;
        private TMP_Text cutLabel;
        private TMP_Text bodyText;
        private TMP_Text counterText;
        private bool sequenceRunning;
        private bool skipRequested;

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            if (storyBackgrounds == null || storyBackgrounds.Length < CutBodies.Length)
            {
                Debug.LogError("[StoryScene] 스토리 배경 6장이 연결되지 않았습니다.");
                return;
            }

            StartCoroutine(PlaySequence());
        }

        private void Update()
        {
            if (!sequenceRunning) return;

            if (Input.GetKeyDown(KeyCode.Return)
                || Input.GetKeyDown(KeyCode.Space)
                || Input.GetMouseButtonDown(0))
            {
                skipRequested = true;
            }

            if (scanLine != null)
            {
                float normalized = Mathf.Repeat(Time.unscaledTime * 0.18f, 1f);
                scanLine.rectTransform.anchorMin = new Vector2(0f, normalized);
                scanLine.rectTransform.anchorMax = new Vector2(1f, normalized);
            }
        }

        private IEnumerator PlaySequence()
        {
            sequenceRunning = true;

            for (int index = 0; index < CutBodies.Length; index++)
            {
                Sprite background = storyBackgrounds[index];
                if (background == null)
                {
                    Debug.LogError($"[StoryScene] {index + 1}번 컷 배경이 비어 있습니다.");
                    continue;
                }

                backgroundImage.sprite = background;
                cutLabel.text = CutLabels[index];
                bodyText.text = CutBodies[index];
                counterText.text = $"CUT {index + 1:00} / {CutBodies.Length:00}";
                backgroundRect.localScale = Vector3.one * 1.035f;
                skipRequested = false;

                yield return FadeTo(1f, fadeDuration);

                float holdDuration = Mathf.Max(0.2f, CutDurations[index] - fadeDuration * 2f);
                float elapsed = 0f;
                while (elapsed < holdDuration && !skipRequested)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float progress = Mathf.Clamp01(elapsed / holdDuration);
                    backgroundRect.localScale = Vector3.one * Mathf.Lerp(1.035f, 1.085f, progress);
                    yield return null;
                }

                yield return FadeTo(0f, skipRequested ? 0.15f : fadeDuration);
            }

            sequenceRunning = false;
            LoadTutorial();
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = contentGroup.alpha;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                contentGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            contentGroup.alpha = target;
        }

        private void LoadTutorial()
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(tutorialSceneName);
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(tutorialSceneName))
                SceneManager.LoadScene(tutorialSceneName);
            else
                Debug.LogError($"[StoryScene] 튜토리얼 씬을 불러올 수 없습니다: {tutorialSceneName}");
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("StoryCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject content = new GameObject("StoryContent", typeof(RectTransform), typeof(CanvasGroup));
            content.transform.SetParent(canvasObject.transform, false);
            Stretch(content.GetComponent<RectTransform>());
            contentGroup = content.GetComponent<CanvasGroup>();
            contentGroup.alpha = 0f;

            backgroundImage = CreateImage(content.transform, "Background", Color.white);
            backgroundRect = backgroundImage.rectTransform;
            Stretch(backgroundRect, -80f, -80f, -45f, -45f);
            backgroundImage.preserveAspect = false;
            backgroundImage.raycastTarget = false;

            Image vignette = CreateImage(content.transform, "Vignette",
                new Color(0.015f, 0.025f, 0.07f, 0.2f));
            Stretch(vignette.rectTransform);
            vignette.raycastTarget = false;

            Image subtitlePanel = CreateImage(content.transform, "SubtitlePanel",
                new Color(0.027f, 0.055f, 0.12f, 0.9f));
            RectTransform panelRect = subtitlePanel.rectTransform;
            panelRect.anchorMin = new Vector2(0.08f, 0.07f);
            panelRect.anchorMax = new Vector2(0.92f, 0.33f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            Image accent = CreateImage(subtitlePanel.transform, "Accent", new Color(0.15f, 0.9f, 1f, 1f));
            RectTransform accentRect = accent.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.sizeDelta = new Vector2(8f, 0f);
            accentRect.anchoredPosition = new Vector2(4f, 0f);

            cutLabel = CreateText(subtitlePanel.transform, "CutLabel", 27, FontStyles.Bold,
                TextAlignmentOptions.Left, new Color(0.15f, 0.9f, 1f, 1f));
            SetRect(cutLabel.rectTransform, new Vector2(0.045f, 0.64f), new Vector2(0.96f, 0.94f));

            bodyText = CreateText(subtitlePanel.transform, "Body", 34, FontStyles.Normal,
                TextAlignmentOptions.Left, new Color(0.91f, 0.95f, 1f, 1f));
            SetRect(bodyText.rectTransform, new Vector2(0.045f, 0.09f), new Vector2(0.96f, 0.68f));

            counterText = CreateText(content.transform, "Counter", 20, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, new Color(0.15f, 0.9f, 1f, 0.9f));
            SetRect(counterText.rectTransform, new Vector2(0.025f, 0.91f), new Vector2(0.25f, 0.975f));

            TMP_Text skipHint = CreateText(content.transform, "SkipHint", 18, FontStyles.Normal,
                TextAlignmentOptions.TopRight, new Color(0.67f, 0.76f, 0.86f, 0.85f));
            skipHint.text = "ENTER / SPACE / CLICK  :  NEXT";
            SetRect(skipHint.rectTransform, new Vector2(0.66f, 0.91f), new Vector2(0.975f, 0.975f));

            scanLine = CreateImage(content.transform, "ScanLine", new Color(0.15f, 0.9f, 1f, 0.16f));
            scanLine.rectTransform.sizeDelta = new Vector2(0f, 3f);
            scanLine.raycastTarget = false;
        }

        private TMP_Text CreateText(Transform parent, string name, float size, FontStyles style,
            TextAlignmentOptions alignment, Color color)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);
            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            text.font = koreanFont;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.raycastTarget = false;
            return text;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
            float bottom = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }
    }
}
