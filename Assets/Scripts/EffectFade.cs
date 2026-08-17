using UnityEngine;

namespace AdaptiveBossPrototype
{
    public sealed class EffectFade : MonoBehaviour
    {
        private SpriteRenderer sprite;
        private float duration;
        private float start;
        private bool grow;
        private Vector3 initialScale;

        public void Configure(float life, bool expand)
        {
            sprite = GetComponent<SpriteRenderer>();
            duration = Mathf.Max(0.05f, life);
            start = Time.time;
            grow = expand;
            initialScale = transform.localScale;
            if (grow) transform.localScale = initialScale * 0.15f;
        }

        private void Update()
        {
            float t = Mathf.Clamp01((Time.time - start) / duration);
            Color color = sprite.color;
            color.a *= 1f - t;
            sprite.color = color;
            if (grow) transform.localScale = Vector3.Lerp(initialScale * 0.15f, initialScale, t);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
