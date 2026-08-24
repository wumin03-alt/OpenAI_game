using System;
using System.Collections;
using Game.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class StageRewardChest : MonoBehaviour
{
    private const string ClosedPath = "Rewards/SPR_TreasureChest_Closed";
    private const string HitPath = "Rewards/SPR_TreasureChest_Hit";
    private const string OpenPath = "Rewards/SPR_TreasureChest_Open";

    private SpriteRenderer spriteRenderer;
    private Sprite closedSprite;
    private Sprite hitSprite;
    private Sprite openSprite;
    private Health health;
    private string sceneKey;
    private Action rewardResolved;
    private Coroutine hitRoutine;
    private bool opened;

    public static StageRewardChest Spawn(Vector3 position, Action onRewardResolved)
    {
        string activeScene = SceneManager.GetActiveScene().name;
        GameSession session = GameSession.Instance;
        if (session != null)
        {
            if (session.IsStageRewardResolved(activeScene))
            {
                onRewardResolved?.Invoke();
                return null;
            }

            if (!session.TryBeginStageReward(activeScene)) return null;
        }

        GameObject root = new GameObject("StageRewardChest");
        root.transform.position = position;
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) root.layer = enemyLayer;

        StageRewardChest chest = root.AddComponent<StageRewardChest>();
        chest.sceneKey = activeScene;
        chest.rewardResolved = onRewardResolved;
        chest.Build();
        return chest;
    }

    private void Build()
    {
        closedSprite = Resources.Load<Sprite>(ClosedPath);
        hitSprite = Resources.Load<Sprite>(HitPath);
        openSprite = Resources.Load<Sprite>(OpenPath);
        if (closedSprite == null || hitSprite == null || openSprite == null)
            Debug.LogError("[StageRewardChest] 보물상자 Resources 스프라이트를 불러오지 못했습니다.", this);

        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = closedSprite;
        spriteRenderer.sortingOrder = 45;
        FitSpriteToWidth(3.2f);

        BoxCollider2D collider = gameObject.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = closedSprite != null ? (Vector2)closedSprite.bounds.size : new Vector2(1.4f, 1f);

        health = gameObject.AddComponent<Health>();
        health.ConfigureRuntime(24f, false);
        // 런타임 AddComponent에서도 UnityEvent가 직렬화된 프리팹과 동일하게 존재해야 한다.
        if (health.onDamaged == null) health.onDamaged = new UnityEngine.Events.UnityEvent();
        if (health.onDeath == null) health.onDeath = new UnityEngine.Events.UnityEvent();
        health.onDamaged.AddListener(HandleDamaged);
        health.onDeath.AddListener(HandleBroken);
        Debug.Log($"[StageRewardChest] 보상 이벤트 연결 완료: {sceneKey}", this);
    }

    private void HandleDamaged()
    {
        if (opened || spriteRenderer == null || hitSprite == null) return;
        if (hitRoutine != null) StopCoroutine(hitRoutine);
        hitRoutine = StartCoroutine(HitReaction());
    }

    private IEnumerator HitReaction()
    {
        spriteRenderer.sprite = hitSprite;
        FitSpriteToWidth(3.2f);
        yield return new WaitForSeconds(0.12f);
        if (!opened)
        {
            spriteRenderer.sprite = closedSprite;
            FitSpriteToWidth(3.2f);
        }
        hitRoutine = null;
    }

    private void HandleBroken()
    {
        if (opened) return;
        opened = true;
        if (hitRoutine != null)
        {
            StopCoroutine(hitRoutine);
            hitRoutine = null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = openSprite;
            FitSpriteToWidth(3.35f);
        }

        // 스테이지 클리어/일시정지로 Time.timeScale이 0이어도 보상창은 반드시 떠야 한다.
        // 따라서 scaled-time 대기 코루틴을 사용하지 않고 상자가 깨진 프레임에 즉시 생성한다.
        Health playerHealth = FindAnyObjectByType<PlayerController>()?.GetComponent<Health>();
        Debug.Log($"[StageRewardChest] 보상 선택창 표시: {sceneKey}", this);
        RewardSelectionUI.Show(sceneKey, playerHealth, () =>
        {
            rewardResolved?.Invoke();
            Destroy(gameObject);
        });
    }

    private void FitSpriteToWidth(float width)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null) return;
        float spriteWidth = spriteRenderer.sprite.bounds.size.x;
        if (spriteWidth <= 0f) return;
        float scale = width / spriteWidth;
        transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void OnDestroy()
    {
        if (health != null)
        {
            // Play 종료/씬 언로드 중에는 Health의 직렬화 UnityEvent가 먼저 정리될 수 있다.
            if (health.onDamaged != null) health.onDamaged.RemoveListener(HandleDamaged);
            if (health.onDeath != null) health.onDeath.RemoveListener(HandleBroken);
        }

        if (!opened)
            GameSession.Instance?.CancelPendingStageReward(sceneKey);
    }
}
