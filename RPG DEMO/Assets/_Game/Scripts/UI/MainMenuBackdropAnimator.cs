using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 플레이어 시점에서 하나의 적 군단과 대치하는 메인 메뉴 컷씬을 연출합니다.
    /// 통합 키아트 전체를 카메라처럼 움직여 캐릭터가 따로 노는 느낌을 방지합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainMenuBackdropAnimator : MonoBehaviour
    {
        private const float IntroDuration = 2.65f;
        private const float RestScale = 1.065f;

        private CanvasGroup backdropGroup;
        private CanvasGroup menuGroup;
        private RectTransform armyRect;
        private RectTransform scanLine;
        private RectTransform topLetterbox;
        private RectTransform bottomLetterbox;
        private Image coreGlow;
        private Image threatFlash;
        private float elapsed;
        private bool introFinished;

        public void Build(Transform canvas)
        {
            GameObject backdrop = new GameObject("ArmyConfrontationBackdrop",
                typeof(RectTransform), typeof(CanvasGroup));
            backdrop.transform.SetParent(canvas, false);
            RuntimeUIFactory.Stretch(backdrop.GetComponent<RectTransform>());
            backdropGroup = backdrop.GetComponent<CanvasGroup>();
            backdropGroup.alpha = 0f;
            backdropGroup.interactable = false;
            backdropGroup.blocksRaycasts = false;

            RawImage army = CreateRawImage(backdrop.transform, "EnemyArmy_KeyArt",
                LoadTexture("MainMenu/Menu_ArmyConfrontation_v3"));
            armyRect = army.rectTransform;
            RuntimeUIFactory.Stretch(armyRect, -20f, 20f, -12f, 12f);
            armyRect.localScale = Vector3.one * 1.18f;
            army.color = new Color(0.86f, 0.93f, 1f, 1f);

            Image menuShade = RuntimeUIFactory.CreateImage(backdrop.transform, "MenuSideShade",
                new Color(0.002f, 0.009f, 0.025f, 0.63f));
            RectTransform shadeRect = menuShade.rectTransform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = new Vector2(0.34f, 1f);
            shadeRect.offsetMin = shadeRect.offsetMax = Vector2.zero;
            menuShade.raycastTarget = false;

            Image lowerVignette = RuntimeUIFactory.CreateImage(backdrop.transform, "PlayerView_LowerVignette",
                new Color(0f, 0.006f, 0.02f, 0.4f));
            RectTransform lowerRect = lowerVignette.rectTransform;
            lowerRect.anchorMin = Vector2.zero;
            lowerRect.anchorMax = new Vector2(1f, 0.17f);
            lowerRect.offsetMin = lowerRect.offsetMax = Vector2.zero;
            lowerVignette.raycastTarget = false;

            coreGlow = RuntimeUIFactory.CreateImage(backdrop.transform, "FinalBoss_CorePulse",
                new Color(0.08f, 0.8f, 1f, 0f));
            RectTransform coreRect = coreGlow.rectTransform;
            coreRect.anchorMin = coreRect.anchorMax = new Vector2(0.5f, 0.5f);
            coreRect.anchoredPosition = new Vector2(102f, 190f);
            coreRect.sizeDelta = new Vector2(132f, 132f);
            Sprite glowSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
            if (glowSprite != null)
            {
                coreGlow.sprite = glowSprite;
                coreGlow.preserveAspect = true;
            }
            else
            {
                // 원형 기본 스프라이트가 없는 플랫폼에서는 사각형 광원이 노출되지 않게 합니다.
                coreGlow.enabled = false;
            }
            coreGlow.raycastTarget = false;

            threatFlash = RuntimeUIFactory.CreateImage(backdrop.transform, "ThreatScanFlash",
                new Color(0.08f, 0.86f, 1f, 0f));
            RuntimeUIFactory.Stretch(threatFlash.rectTransform);
            threatFlash.raycastTarget = false;

            Image scan = RuntimeUIFactory.CreateImage(backdrop.transform, "ThreatScanLine",
                new Color(0.16f, 0.95f, 1f, 0.72f));
            scanLine = scan.rectTransform;
            scanLine.anchorMin = scanLine.anchorMax = new Vector2(0.5f, 0.5f);
            scanLine.sizeDelta = new Vector2(1920f, 3f);
            scanLine.anchoredPosition = new Vector2(0f, 430f);
            scan.raycastTarget = false;

            topLetterbox = CreateLetterbox(canvas, "ConfrontationLetterbox_Top", true);
            bottomLetterbox = CreateLetterbox(canvas, "ConfrontationLetterbox_Bottom", false);
        }

        public void BindMenu(CanvasGroup group)
        {
            menuGroup = group;
            if (menuGroup == null) return;
            menuGroup.alpha = 0f;
            menuGroup.interactable = false;
            menuGroup.blocksRaycasts = false;
        }

        private void Update()
        {
            elapsed += Time.unscaledDeltaTime;
            if (!introFinished && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
                elapsed = IntroDuration;

            float reveal = Smooth01(elapsed / 0.58f);
            if (backdropGroup != null) backdropGroup.alpha = reveal;

            float cameraReveal = Smooth01((elapsed - 0.12f) / 1.55f);
            float idleBreath = Mathf.Sin(Time.unscaledTime * 0.72f);
            if (armyRect != null)
            {
                float scale = Mathf.Lerp(1.18f, RestScale, cameraReveal)
                              + idleBreath * 0.0025f * cameraReveal;
                armyRect.localScale = Vector3.one * scale;
                armyRect.anchoredPosition = new Vector2(
                    Mathf.Sin(Time.unscaledTime * 0.31f) * 0.8f * cameraReveal,
                    Mathf.Lerp(-24f, 0f, cameraReveal) + idleBreath * 1.8f * cameraReveal);
            }

            if (scanLine != null)
            {
                float scanProgress = Mathf.Clamp01((elapsed - 0.62f) / 0.9f);
                scanLine.anchoredPosition = new Vector2(0f, Mathf.Lerp(430f, -430f, scanProgress));
                scanLine.gameObject.SetActive(scanProgress < 1f);
            }

            if (threatFlash != null)
            {
                float cyanFlash = Pulse(elapsed, 0.66f, 0.16f) * 0.1f;
                float redFlash = Pulse(elapsed, 1.48f, 0.13f) * 0.075f;
                threatFlash.color = new Color(
                    Mathf.Lerp(0.08f, 1f, redFlash > 0f ? 1f : 0f),
                    redFlash > 0f ? 0.08f : 0.86f,
                    redFlash > 0f ? 0.24f : 1f,
                    Mathf.Max(cyanFlash, redFlash));
            }

            if (coreGlow != null)
            {
                float introCore = Smooth01((elapsed - 0.88f) / 0.55f);
                float pulse = Mathf.Sin(Time.unscaledTime * 2.15f) * 0.5f + 0.5f;
                Color color = coreGlow.color;
                color.a = introCore * (0.055f + pulse * 0.085f);
                coreGlow.color = color;
                coreGlow.rectTransform.localScale = Vector3.one * (0.92f + pulse * 0.14f);
            }

            float menuFade = Smooth01((elapsed - 1.78f) / 0.55f);
            if (menuGroup != null)
            {
                menuGroup.alpha = menuFade;
                menuGroup.interactable = menuFade >= 0.98f;
                menuGroup.blocksRaycasts = menuFade >= 0.98f;
            }

            float bars = 1f - Smooth01((elapsed - 1.72f) / 0.78f);
            SetLetterboxHeight(topLetterbox, 104f * bars, true);
            SetLetterboxHeight(bottomLetterbox, 104f * bars, false);

            if (!introFinished && elapsed >= IntroDuration)
                introFinished = true;
        }

        private static float Pulse(float time, float center, float halfWidth)
        {
            float distance = Mathf.Abs(time - center);
            return distance >= halfWidth ? 0f : 1f - distance / halfWidth;
        }

        private static RawImage CreateRawImage(Transform parent, string objectName, Texture texture)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            go.transform.SetParent(parent, false);
            RawImage image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        private static Texture2D LoadTexture(string path)
        {
            Texture2D texture = Resources.Load<Texture2D>(path);
            if (texture != null) return texture;
            Sprite sprite = Resources.Load<Sprite>(path);
            return sprite != null ? sprite.texture : null;
        }

        private static RectTransform CreateLetterbox(Transform parent, string objectName, bool top)
        {
            Image image = RuntimeUIFactory.CreateImage(parent, objectName, Color.black);
            image.raycastTarget = false;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rect.sizeDelta = new Vector2(0f, 104f);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        private static void SetLetterboxHeight(RectTransform rect, float height, bool top)
        {
            if (rect == null) return;
            rect.sizeDelta = new Vector2(0f, height);
            rect.pivot = new Vector2(0.5f, top ? 1f : 0f);
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }
    }
}
