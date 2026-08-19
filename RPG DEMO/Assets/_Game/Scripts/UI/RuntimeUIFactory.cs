using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.UI
{
    internal static class RuntimeUIFactory
    {
        private static Font cachedFont;

        public static Canvas CreateCanvas(string name, Transform parent, int sortingOrder)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            root.AddComponent<GraphicRaycaster>();
            Stretch(root.GetComponent<RectTransform>());
            return canvas;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
        }

        public static Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return image;
        }

        public static Text CreateText(Transform parent, string value, int fontSize,
            Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(value, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = Font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;

            Shadow shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0.02f, 0.05f, 0.8f);
            shadow.effectDistance = new Vector2(1.5f, -2f);
            shadow.useGraphicAlpha = true;
            return text;
        }

        public static Button CreateButton(Transform parent, string label, Vector2 position,
            Vector2 size, UnityAction onClick)
        {
            Image image = CreateImage(parent, label + "Button", new Color(0.13f, 0.17f, 0.24f, 0.96f));
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Button button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.75f, 0.88f, 1f);
            colors.pressedColor = new Color(0.48f, 0.68f, 0.9f);
            button.colors = colors;

            UIAudioFeedback audioFeedback = image.gameObject.AddComponent<UIAudioFeedback>();
            audioFeedback.Initialize(button);
            button.onClick.AddListener(onClick);

            Text text = CreateText(rect, label, 28, Vector2.zero, size, Color.white);
            Stretch(text.rectTransform);
            return button;
        }

        public static Slider CreateSlider(Transform parent, string label, Vector2 position,
            float initialValue, UnityAction<float> onChanged)
        {
            CreateText(parent, label, 23, position + new Vector2(0f, 32f),
                new Vector2(420f, 40f), Color.white);

            GameObject sliderObject = new GameObject(label + "Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);
            RectTransform root = sliderObject.GetComponent<RectTransform>();
            root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
            root.anchoredPosition = position;
            root.sizeDelta = new Vector2(360f, 30f);

            Image background = CreateImage(root, "Background", new Color(0.08f, 0.1f, 0.14f, 1f));
            Stretch(background.rectTransform, 0f, 0f, -9f, -9f);

            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(root, false);
            Stretch(fillArea.GetComponent<RectTransform>(), 10f, -10f, -9f, -9f);
            Image fill = CreateImage(fillArea.transform, "Fill", new Color(0.25f, 0.65f, 1f, 1f));
            Stretch(fill.rectTransform);

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root, false);
            Stretch(handleArea.GetComponent<RectTransform>(), 10f, -10f, 0f, 0f);
            Image handle = CreateImage(handleArea.transform, "Handle", Color.white);
            handle.rectTransform.sizeDelta = new Vector2(24f, 24f);

            Slider slider = sliderObject.GetComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(initialValue);
            slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static void Stretch(RectTransform rect, float left = 0f, float right = 0f,
            float bottom = 0f, float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        private static Font Font
        {
            get
            {
                if (cachedFont == null)
                {
                    cachedFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 32);

                    if (cachedFont == null)
                        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return cachedFont;
            }
        }
    }
}
