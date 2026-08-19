using System.Collections;
using Game.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>플레이어 사망 시 현재 스테이지를 처음 상태로 다시 시작합니다.</summary>
    [RequireComponent(typeof(Health))]
    public sealed class PlayerRespawn : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float respawnDelay = 1.4f;

        [Header("전투 사망 폭발")]
        [SerializeField, Range(4, 24)] private int debrisCount = 12;
        [SerializeField, Min(0.1f)] private float debrisLifetime = 1.2f;
        [SerializeField, Min(0f)] private float explosionForce = 7f;
        [SerializeField, Min(0f)] private float upwardForce = 4f;
        [SerializeField, Min(0f)] private float debrisGravity = 2.5f;

        private Health health;
        private SpriteRenderer[] renderers;
        private bool respawning;

        private void Awake()
        {
            health = GetComponent<Health>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        private void OnEnable()
        {
            health.onDeath.AddListener(HandleDeath);
        }

        private void OnDisable()
        {
            health.onDeath.RemoveListener(HandleDeath);
        }

        private void HandleDeath()
        {
            if (!respawning)
                StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            respawning = true;
            SpawnExplosionDebris();
            SetVisuals(false);
            yield return new WaitForSecondsRealtime(respawnDelay);

            if (SceneLoader.Instance != null)
                SceneLoader.Instance.ReloadCurrentScene();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void SpawnExplosionDebris()
        {
            SpriteRenderer source = FindExplosionSource();
            if (source == null || source.sprite == null) return;

            Bounds bounds = source.bounds;
            Vector3 center = bounds.center;

            for (int i = 0; i < debrisCount; i++)
            {
                GameObject piece = new GameObject("PlayerDebris");
                piece.layer = gameObject.layer;

                Vector2 offset = new Vector2(
                    Random.Range(-bounds.extents.x, bounds.extents.x),
                    Random.Range(-bounds.extents.y, bounds.extents.y));
                piece.transform.position = center + (Vector3)offset;
                piece.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                float size = Random.Range(0.12f, 0.28f);
                piece.transform.localScale = new Vector3(size, size, 1f);

                SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
                renderer.sprite = source.sprite;
                renderer.sharedMaterial = source.sharedMaterial;
                renderer.color = source.color;
                renderer.sortingLayerID = source.sortingLayerID;
                renderer.sortingOrder = source.sortingOrder + 1;

                Rigidbody2D debrisBody = piece.AddComponent<Rigidbody2D>();
                debrisBody.gravityScale = debrisGravity;
                debrisBody.angularVelocity = Random.Range(-720f, 720f);

                Vector2 direction = Random.insideUnitCircle.normalized;
                if (direction == Vector2.zero) direction = Vector2.up;
                debrisBody.linearVelocity = direction * explosionForce + Vector2.up * upwardForce;

                PlayerDebrisPiece debris = piece.AddComponent<PlayerDebrisPiece>();
                debris.Initialize(renderer, debrisLifetime);
            }
        }

        private SpriteRenderer FindExplosionSource()
        {
            foreach (SpriteRenderer candidate in renderers)
            {
                if (candidate != null && candidate.enabled && candidate.sprite != null)
                    return candidate;
            }

            return null;
        }

        private void SetVisuals(bool visible)
        {
            foreach (SpriteRenderer target in renderers)
            {
                if (target != null) target.enabled = visible;
            }
        }
    }

    /// <summary>폭발 조각을 축소하고 투명하게 만든 뒤 제거합니다.</summary>
    internal sealed class PlayerDebrisPiece : MonoBehaviour
    {
        private SpriteRenderer targetRenderer;
        private Vector3 initialScale;
        private Color initialColor;
        private float lifetime;
        private float elapsed;

        public void Initialize(SpriteRenderer renderer, float duration)
        {
            targetRenderer = renderer;
            initialScale = transform.localScale;
            initialColor = renderer.color;
            lifetime = Mathf.Max(0.1f, duration);
            Destroy(gameObject, lifetime + 0.1f);
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / lifetime);

            transform.localScale = Vector3.Lerp(initialScale, Vector3.zero, progress);
            if (targetRenderer != null)
            {
                Color color = initialColor;
                color.a *= 1f - Mathf.InverseLerp(0.35f, 1f, progress);
                targetRenderer.color = color;
            }
        }
    }
}
