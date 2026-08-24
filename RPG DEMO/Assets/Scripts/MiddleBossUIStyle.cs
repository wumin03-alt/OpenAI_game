using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>중간보스 전용 런타임 HUD의 둥근 프레임과 수평 게이지 표현을 통일합니다.</summary>
    internal static class MiddleBossUIStyle
    {
        private static Sprite roundedSprite;

        public static void Rounded(Image image, Color color)
        {
            if (image == null) return;
            if (roundedSprite == null)
                roundedSprite = BuildRoundedSprite();

            image.sprite = roundedSprite;
            image.type = roundedSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
        }

        private static Sprite BuildRoundedSprite()
        {
            const int size = 32;
            const int radius = 9;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "MiddleBossRoundedUI",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float cornerX = x < radius ? radius - x : x >= size - radius ? x - (size - radius - 1) : 0f;
                    float cornerY = y < radius ? radius - y : y >= size - radius ? y - (size - radius - 1) : 0f;
                    float distance = Mathf.Sqrt(cornerX * cornerX + cornerY * cornerY);
                    byte alpha = distance <= radius - 0.75f ? (byte)255 : distance <= radius + 0.25f
                        ? (byte)Mathf.RoundToInt(Mathf.Clamp01(radius + 0.25f - distance) * 255f)
                        : (byte)0;
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = "MiddleBossRoundedUI";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        public static void Outline(Graphic graphic, Color color, float distance = 2f)
        {
            if (graphic == null) return;
            UnityEngine.UI.Outline outline = graphic.GetComponent<UnityEngine.UI.Outline>();
            if (outline == null) outline = graphic.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = color;
            outline.effectDistance = new Vector2(distance, -distance);
            outline.useGraphicAlpha = true;
        }

        public static void Shadow(Graphic graphic, Color color, float distance = 3f)
        {
            if (graphic == null) return;
            UnityEngine.UI.Shadow shadow = graphic.GetComponent<UnityEngine.UI.Shadow>();
            if (shadow == null) shadow = graphic.gameObject.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(distance, -distance);
            shadow.useGraphicAlpha = true;
        }

        /// <summary>
        /// 스프라이트가 없는 런타임 Image에서도 게이지가 확실히 줄도록
        /// fillAmount 대신 RectTransform의 우측 앵커를 직접 이동합니다.
        /// </summary>
        public static void HorizontalFill(Image image, float normalized, float inset = 3f)
        {
            if (image == null) return;
            float value = Mathf.Clamp01(normalized);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = new Vector2(Mathf.Max(0.001f, value), 1f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            image.enabled = value > 0.001f;
        }
    }
}
