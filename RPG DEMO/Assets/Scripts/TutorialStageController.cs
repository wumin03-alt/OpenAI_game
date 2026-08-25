using System.Collections;
using Game.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.Tutorial
{
    /// <summary>공용 Player 프리팹의 실제 입력을 단계별로 안내하는 짧은 튜토리얼 스테이지입니다.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialStageController : MonoBehaviour
    {
        private enum TutorialStep
        {
            Move,
            Ranged,
            Melee,
            Parry,
            Dash,
            Complete
        }

        [Header("씬 참조")]
        [SerializeField] private GameObject playerPrefab;
        [SerializeField] private Sprite stageBackground;
        [SerializeField] private TMP_FontAsset koreanFont;

        [Header("전환")]
        [SerializeField] private string nextSceneName = "Stage01";
        [SerializeField, Min(0.2f)] private float confirmDuration = 0.7f;

        private readonly Color observation = new Color(0.15f, 0.9f, 1f, 1f);
        private readonly Color success = new Color(0.22f, 0.95f, 0.68f, 1f);
        private readonly Color warning = new Color(1f, 0.72f, 0.24f, 1f);

        private TutorialStep currentStep;
        private TMP_Text stepCounter;
        private TMP_Text keyText;
        private TMP_Text instructionText;
        private TMP_Text detailText;
        private Image accentBar;
        private bool inputLocked;
        private bool movedLeft;
        private bool movedRight;
        private Sprite whiteSprite;

        private void Awake()
        {
            BuildWorld();
            BuildUI();
            ShowStep(TutorialStep.Move);
        }

        private void Update()
        {
            if (inputLocked) return;

            switch (currentStep)
            {
                case TutorialStep.Move:
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) movedLeft = true;
                    if (Input.GetKeyDown(KeyCode.RightArrow)) movedRight = true;

                    detailText.text = movedLeft && movedRight
                        ? "좌우 이동 확인 완료"
                        : $"왼쪽 [{(movedLeft ? "완료" : "대기")}]   오른쪽 [{(movedRight ? "완료" : "대기")}]";

                    if (movedLeft && movedRight) ConfirmStep();
                    break;

                case TutorialStep.Ranged:
                    if (Input.GetKeyDown(KeyCode.W)) ConfirmStep();
                    break;

                case TutorialStep.Melee:
                    if (Input.GetKeyDown(KeyCode.Q)) ConfirmStep();
                    break;

                case TutorialStep.Parry:
                    if (Input.GetKeyDown(KeyCode.E)) ConfirmStep();
                    break;

                case TutorialStep.Dash:
                    if (Input.GetKeyDown(KeyCode.R)) ConfirmStep();
                    break;
            }
        }

        private void ConfirmStep()
        {
            if (inputLocked) return;
            StartCoroutine(ConfirmAndAdvance());
        }

        private IEnumerator ConfirmAndAdvance()
        {
            inputLocked = true;
            accentBar.color = success;
            instructionText.text = "입력 확인";
            detailText.text = "다음 전투 모듈을 불러오는 중...";
            yield return new WaitForSecondsRealtime(confirmDuration);

            TutorialStep next = (TutorialStep)((int)currentStep + 1);
            if (next == TutorialStep.Complete)
            {
                ShowComplete();
                yield return new WaitForSecondsRealtime(1.6f);
                LoadStageOne();
                yield break;
            }

            ShowStep(next);
            inputLocked = false;
        }

        private void ShowStep(TutorialStep step)
        {
            currentStep = step;
            accentBar.color = observation;
            stepCounter.text = $"COMBAT CALIBRATION  //  {(int)step + 1:00} / 05";

            switch (step)
            {
                case TutorialStep.Move:
                    keyText.text = "←  →";
                    instructionText.text = "방향키로 좌우 이동";
                    detailText.text = "왼쪽과 오른쪽 방향키를 각각 입력하세요.";
                    break;
                case TutorialStep.Ranged:
                    keyText.text = "W";
                    instructionText.text = "원거리 공격";
                    detailText.text = "W를 눌러 전방으로 에너지 탄환을 발사하세요.";
                    break;
                case TutorialStep.Melee:
                    keyText.text = "Q";
                    instructionText.text = "근접 공격";
                    detailText.text = "Q를 눌러 가까운 적을 공격하세요.";
                    break;
                case TutorialStep.Parry:
                    keyText.text = "E";
                    instructionText.text = "패링";
                    detailText.text = "E를 눌러 짧은 패링 판정을 활성화하세요.";
                    break;
                case TutorialStep.Dash:
                    keyText.text = "R";
                    instructionText.text = "대시";
                    detailText.text = "R을 눌러 바라보는 방향으로 빠르게 이동하세요.";
                    break;
            }
        }

        private void ShowComplete()
        {
            currentStep = TutorialStep.Complete;
            accentBar.color = success;
            stepCounter.text = "COMBAT CALIBRATION  //  COMPLETE";
            keyText.text = "✓";
            keyText.color = success;
            instructionText.text = "전투 적응 검사 완료";
            detailText.text = "실전 구역 STAGE 01로 이동합니다.";
        }

        private void LoadStageOne()
        {
            if (SceneLoader.Instance != null)
            {
                SceneLoader.Instance.LoadScene(nextSceneName);
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(nextSceneName))
                SceneManager.LoadScene(nextSceneName);
            else
                Debug.LogError($"[Tutorial] 다음 씬을 불러올 수 없습니다: {nextSceneName}");
        }

        private void BuildWorld()
        {
            whiteSprite = CreateWhiteSprite();

            if (stageBackground != null)
            {
                GameObject backgroundObject = new GameObject("TutorialLabBackground", typeof(SpriteRenderer));
                SpriteRenderer renderer = backgroundObject.GetComponent<SpriteRenderer>();
                renderer.sprite = stageBackground;
                renderer.sortingOrder = -100;
                backgroundObject.transform.position = new Vector3(20f, 0f, 5f);

                Vector2 size = stageBackground.bounds.size;
                if (size.x > 0f && size.y > 0f)
                    backgroundObject.transform.localScale = new Vector3(64f / size.x, 14f / size.y, 1f);
            }
            else
            {
                Debug.LogWarning("[Tutorial] 실험실 배경 스프라이트가 연결되지 않았습니다.");
            }

            CreateBlock("Prototype_Ground", new Vector2(20f, -4.3f), new Vector2(48f, 1.8f),
                new Color(0.05f, 0.1f, 0.18f, 1f), true);
            CreateBlock("Prototype_LeftBoundary", new Vector2(-2f, 0f), new Vector2(0.5f, 12f),
                observation, true);
            CreateBlock("Prototype_RightBoundary", new Vector2(42f, 0f), new Vector2(0.5f, 12f),
                observation, true);

            CreateTarget(new Vector2(10f, -2.35f), warning);
            CreateTarget(new Vector2(20f, -2.35f), new Color(1f, 0.2f, 0.48f, 1f));
            CreateTarget(new Vector2(30f, -2.35f), observation);

            if (playerPrefab != null)
            {
                GameObject player = Instantiate(playerPrefab, new Vector3(2f, -2.7f, 0f), Quaternion.identity);
                player.name = "Player";
            }
            else
            {
                Debug.LogError("[Tutorial] 공용 Player 프리팹이 연결되지 않았습니다.");
            }

            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener),
                typeof(CameraFollow));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(3.5f, 0f, -10f);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.027f, 0.043f, 0.095f, 1f);
        }

        private void BuildUI()
        {
            GameObject canvasObject = new GameObject("TutorialCanvas", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 150;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            stepCounter = CreateText(canvasObject.transform, "StepCounter", 22, FontStyles.Bold,
                TextAlignmentOptions.TopLeft, observation);
            SetRect(stepCounter.rectTransform, new Vector2(0.035f, 0.9f), new Vector2(0.54f, 0.965f));

            Image panel = CreateImage(canvasObject.transform, "InstructionPanel",
                new Color(0.027f, 0.065f, 0.14f, 0.92f));
            RectTransform panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.16f, 0.055f);
            panelRect.anchorMax = new Vector2(0.84f, 0.265f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            accentBar = CreateImage(panel.transform, "Accent", observation);
            RectTransform accentRect = accentBar.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.sizeDelta = new Vector2(9f, 0f);
            accentRect.anchoredPosition = new Vector2(4.5f, 0f);

            Image keyPlate = CreateImage(panel.transform, "KeyPlate", new Color(0.04f, 0.11f, 0.21f, 1f));
            SetRect(keyPlate.rectTransform, new Vector2(0.035f, 0.16f), new Vector2(0.21f, 0.84f));

            keyText = CreateText(keyPlate.transform, "Key", 54, FontStyles.Bold,
                TextAlignmentOptions.Center, observation);
            Stretch(keyText.rectTransform);

            instructionText = CreateText(panel.transform, "Instruction", 39, FontStyles.Bold,
                TextAlignmentOptions.Left, new Color(0.91f, 0.95f, 1f, 1f));
            SetRect(instructionText.rectTransform, new Vector2(0.25f, 0.47f), new Vector2(0.96f, 0.87f));

            detailText = CreateText(panel.transform, "Detail", 25, FontStyles.Normal,
                TextAlignmentOptions.Left, new Color(0.67f, 0.76f, 0.86f, 1f));
            SetRect(detailText.rectTransform, new Vector2(0.25f, 0.13f), new Vector2(0.96f, 0.5f));
        }

        private void CreateTarget(Vector2 position, Color color)
        {
            GameObject target = new GameObject("Prototype_Target", typeof(SpriteRenderer));
            target.transform.position = position;
            target.transform.localScale = new Vector3(1.1f, 2.2f, 1f);
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = new Color(color.r, color.g, color.b, 0.65f);
            renderer.sortingOrder = 2;

            GameObject core = new GameObject("TargetCore", typeof(SpriteRenderer));
            core.transform.SetParent(target.transform, false);
            core.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
            SpriteRenderer coreRenderer = core.GetComponent<SpriteRenderer>();
            coreRenderer.sprite = whiteSprite;
            coreRenderer.color = Color.white;
            coreRenderer.sortingOrder = 3;
        }

        private void CreateBlock(string name, Vector2 position, Vector2 size, Color color, bool collider)
        {
            GameObject block = new GameObject(name, typeof(SpriteRenderer));
            block.transform.position = position;
            block.transform.localScale = new Vector3(size.x, size.y, 1f);
            SpriteRenderer renderer = block.GetComponent<SpriteRenderer>();
            renderer.sprite = whiteSprite;
            renderer.color = color;
            renderer.sortingOrder = 1;

            if (!collider) return;
            int groundLayer = LayerMask.NameToLayer("Ground");
            block.layer = groundLayer >= 0 ? groundLayer : 6;
            block.AddComponent<BoxCollider2D>();
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
            image.raycastTarget = false;
            return image;
        }

        private static Sprite CreateWhiteSprite()
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = "Prototype_WhiteTexture",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 2f);
        }

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
