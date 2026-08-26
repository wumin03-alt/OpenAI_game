using System.Collections;
using Game.Core;
using Game.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>PlayerCommonHUD 프리팹의 씬 독립적인 런타임 연결을 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerCommonHUD : MonoBehaviour
{
    [SerializeField] private HealthBarUI healthBar;
    [SerializeField] private TMP_Text parryNotice;

    private PlayerCombatTracker tracker;
    private Coroutine noticeRoutine;
    private RectTransform heldItemsRoot;

    public void Configure(HealthBarUI playerHealthBar, TMP_Text notice)
    {
        healthBar = playerHealthBar;
        parryNotice = notice;
    }

    private void Start()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null && healthBar != null)
            healthBar.SetTarget(player.GetComponent<Health>());

        tracker = player != null ? player.GetComponent<PlayerCombatTracker>() : PlayerCombatTracker.Instance;
        if (tracker != null) tracker.ParrySucceeded += ShowParryNotice;

        BuildHeldItemHud();
        if (GameSession.Instance != null)
            GameSession.Instance.RunStateChanged += RefreshHeldItems;

        if (parryNotice != null) parryNotice.color = Color.clear;
    }

    private void OnDestroy()
    {
        if (tracker != null) tracker.ParrySucceeded -= ShowParryNotice;
        if (GameSession.Instance != null)
            GameSession.Instance.RunStateChanged -= RefreshHeldItems;
    }

    private void ShowParryNotice()
    {
        if (parryNotice == null) return;
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(AnimateNotice());
    }

    private void BuildHeldItemHud()
    {
        GameObject root = new GameObject("HeldRunItems", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(transform, false);
        heldItemsRoot = root.GetComponent<RectTransform>();
        heldItemsRoot.anchorMin = heldItemsRoot.anchorMax = new Vector2(0f, 0f);
        heldItemsRoot.pivot = new Vector2(0f, 0f);
        heldItemsRoot.anchoredPosition = new Vector2(48f, 105f);
        heldItemsRoot.sizeDelta = new Vector2(500f, 76f);
        Image background = root.GetComponent<Image>();
        background.color = new Color(0.025f, 0.055f, 0.11f, 0.88f);
        background.raycastTarget = false;
        RefreshHeldItems();
    }

    private void RefreshHeldItems()
    {
        if (heldItemsRoot == null) return;
        for (int i = heldItemsRoot.childCount - 1; i >= 0; i--)
            Destroy(heldItemsRoot.GetChild(i).gameObject);

        GameSession session = GameSession.Instance;
        if (session == null)
        {
            heldItemsRoot.gameObject.SetActive(false);
            return;
        }

        RunItemType[] persistentTypes =
        {
            RunItemType.AttackBoost,
            RunItemType.GroggyDamageBoost,
            RunItemType.ParryTimeBoost,
            RunItemType.MaxHealthBoost
        };

        int visible = 0;
        foreach (RunItemType type in persistentTypes)
        {
            int count = session.GetItemCount(type);
            if (count <= 0) continue;

            Image icon = RuntimeUIFactory.CreateImage(heldItemsRoot, type.ToString(), Color.white);
            icon.sprite = Resources.Load<Sprite>(RunItemCatalog.GetSpriteResourcePath(type));
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            RectTransform rect = icon.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(12f + visible * 116f, 0f);
            rect.sizeDelta = new Vector2(68f, 68f);

            Text stack = RuntimeUIFactory.CreateText(icon.transform, $"x{count}", 18,
                new Vector2(35f, -24f), new Vector2(60f, 28f), Color.white);
            stack.alignment = TextAnchor.MiddleRight;
            visible++;
        }

        heldItemsRoot.gameObject.SetActive(visible > 0);
        heldItemsRoot.sizeDelta = new Vector2(Mathf.Max(120f, 20f + visible * 116f), 76f);
    }

    private IEnumerator AnimateNotice()
    {
        float elapsed = 0f;
        const float duration = 0.95f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = t < 0.18f ? t / 0.18f : 1f - Mathf.InverseLerp(0.62f, 1f, t);
            parryNotice.color = new Color(0.38f, 1f, 0.76f, alpha);
            parryNotice.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(t * 5f));
            yield return null;
        }

        parryNotice.color = Color.clear;
        noticeRoutine = null;
    }
}
