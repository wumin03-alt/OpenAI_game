using System.Collections;
using UnityEngine;

namespace Game.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class ScreenFader : MonoBehaviour
    {
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        public IEnumerator FadeOut(float duration)
        {
            yield return FadeTo(1f, duration, true);
        }

        public IEnumerator FadeIn(float duration)
        {
            yield return FadeTo(0f, duration, false);
        }

        private IEnumerator FadeTo(float target, float duration, bool blockInput)
        {
            canvasGroup.blocksRaycasts = blockInput;
            float start = canvasGroup.alpha;

            if (duration <= 0f)
            {
                canvasGroup.alpha = target;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    canvasGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
                    yield return null;
                }
                canvasGroup.alpha = target;
            }

            canvasGroup.blocksRaycasts = blockInput && target > 0f;
        }
    }
}
