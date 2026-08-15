using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DemonCompany.Phase1
{
    public sealed class InterviewSpriteAnimator : MonoBehaviour
    {
        private static readonly Dictionary<string, Sprite[]> CachedFrames = new Dictionary<string, Sprite[]>();

        private Image target;
        private Sprite[] frames;
        private int currentFrame;
        private float nextFrameAt;
        private float talkingUntil;

        public void Configure(Image image, string resourcePath)
        {
            target = image;
            frames = LoadFrames(resourcePath);
            if (frames == null || frames.Length < 4) return;

            currentFrame = 0;
            target.sprite = frames[0];
            target.preserveAspect = true;
            nextFrameAt = Time.unscaledTime + Random.Range(1.7f, 3.2f);
        }

        public void PlayTalking(float duration = 2.6f)
        {
            if (frames == null || frames.Length < 4) return;
            talkingUntil = Time.unscaledTime + duration;
            SetFrame(2);
            nextFrameAt = Time.unscaledTime + 0.22f;
        }

        private void Update()
        {
            if (target == null || frames == null || frames.Length < 4) return;

            float now = Time.unscaledTime;
            if (now < talkingUntil)
            {
                if (now >= nextFrameAt)
                {
                    SetFrame(currentFrame == 2 ? 3 : 2);
                    nextFrameAt = now + 0.22f;
                }
                return;
            }

            if (currentFrame >= 2)
            {
                SetFrame(0);
                nextFrameAt = now + Random.Range(1.7f, 3.2f);
                return;
            }

            if (now < nextFrameAt) return;
            if (currentFrame == 0)
            {
                SetFrame(1);
                nextFrameAt = now + 0.14f;
            }
            else
            {
                SetFrame(0);
                nextFrameAt = now + Random.Range(1.7f, 3.2f);
            }
        }

        private void SetFrame(int index)
        {
            currentFrame = index;
            target.sprite = frames[index];
        }

        private static Sprite[] LoadFrames(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath)) return null;
            if (CachedFrames.TryGetValue(resourcePath, out Sprite[] cached)) return cached;

            Texture2D sheet = Resources.Load<Texture2D>(resourcePath);
            if (sheet == null || sheet.width < 4 || sheet.height < 1) return null;
            sheet.filterMode = FilterMode.Point;
            sheet.wrapMode = TextureWrapMode.Clamp;

            int frameWidth = sheet.width / 4;
            Sprite[] loaded = new Sprite[4];
            for (int index = 0; index < loaded.Length; index++)
            {
                Rect rect = new Rect(index * frameWidth, 0f, frameWidth, sheet.height);
                loaded[index] = Sprite.Create(sheet, rect, new Vector2(0.5f, 0f), frameWidth, 0, SpriteMeshType.FullRect);
                loaded[index].name = resourcePath + "_frame_" + index;
            }

            CachedFrames[resourcePath] = loaded;
            return loaded;
        }
    }
}
