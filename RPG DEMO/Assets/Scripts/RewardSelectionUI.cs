using System;
using System.Collections.Generic;
using Game.Core;
using Game.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RewardSelectionUI : MonoBehaviour
{
    private static readonly RunItemType[] AllTypes =
    {
        RunItemType.HealthRecovery,
        RunItemType.AttackBoost,
        RunItemType.GroggyDamageBoost,
        RunItemType.ParryTimeBoost,
        RunItemType.MaxHealthBoost
    };

    private readonly Button[] optionButtons = new Button[2];
    private RunItemOffer[] offers;
    private string sceneKey;
    private Health playerHealth;
    private PlayerController playerController;
    private bool playerWasEnabled;
    private Action completed;
    private int selectedIndex;
    private bool resolved;

    public static void Show(string rewardSceneKey, Health health, Action onCompleted)
    {
        GameObject root = new GameObject("RewardSelectionFlow");
        RewardSelectionUI selection = root.AddComponent<RewardSelectionUI>();
        selection.sceneKey = rewardSceneKey;
        selection.playerHealth = health;
        selection.completed = onCompleted;
        selection.Build();
    }

    private void Build()
    {
        offers = CreateDistinctOffers();
        playerController = playerHealth != null ? playerHealth.GetComponent<PlayerController>() : null;
        if (playerController != null)
        {
            playerWasEnabled = playerController.enabled;
            playerController.enabled = false;
        }

        RuntimeUIFactory.EnsureEventSystem();
        Canvas canvas = RuntimeUIFactory.CreateCanvas("StageRewardSelectionCanvas", transform, 340);
        Image dim = RuntimeUIFactory.CreateImage(canvas.transform, "Dim", new Color(0.01f, 0.02f, 0.06f, 0.78f));
        RuntimeUIFactory.Stretch(dim.rectTransform);

        Image panel = RuntimeUIFactory.CreateImage(dim.transform, "RewardPanel", new Color(0.05f, 0.09f, 0.18f, 0.98f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1120f, 650f);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.15f, 0.9f, 1f, 0.9f);
        outline.effectDistance = new Vector2(4f, -4f);

        Text title = RuntimeUIFactory.CreateText(panelRect, "STAGE CLEAR // 보상 아이템 선택", 38,
            new Vector2(0f, 265f), new Vector2(1000f, 70f), new Color(0.91f, 0.96f, 1f));
        title.fontStyle = FontStyle.Bold;
        RuntimeUIFactory.CreateText(panelRect, "두 아이템 중 하나를 선택하면 이번 런 동안 즉시 적용됩니다.", 23,
            new Vector2(0f, 210f), new Vector2(980f, 48f), new Color(0.66f, 0.78f, 0.9f));

        BuildOfferCard(panelRect, 0, new Vector2(-275f, -15f));
        BuildOfferCard(panelRect, 1, new Vector2(275f, -15f));
        RuntimeUIFactory.CreateText(panelRect, "마우스 클릭 또는 ← → + ENTER", 20,
            new Vector2(0f, -285f), new Vector2(800f, 42f), new Color(0.22f, 0.95f, 0.68f));

        SelectIndex(0);
    }

    private void BuildOfferCard(RectTransform parent, int index, Vector2 position)
    {
        RunItemOffer offer = offers[index];
        Image card = RuntimeUIFactory.CreateImage(parent, $"RewardCard_{index + 1}", new Color(0.07f, 0.13f, 0.24f, 1f));
        RectTransform cardRect = card.rectTransform;
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = position;
        cardRect.sizeDelta = new Vector2(470f, 420f);

        Button button = card.gameObject.AddComponent<Button>();
        button.targetGraphic = card;
        int capturedIndex = index;
        button.onClick.AddListener(() => Resolve(capturedIndex));
        UIAudioFeedback feedback = card.gameObject.AddComponent<UIAudioFeedback>();
        feedback.Initialize(button);
        optionButtons[index] = button;

        Image icon = RuntimeUIFactory.CreateImage(cardRect, "ItemIcon", Color.white);
        icon.sprite = Resources.Load<Sprite>(RunItemCatalog.GetSpriteResourcePath(offer.Type));
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
        iconRect.anchoredPosition = new Vector2(0f, 78f);
        iconRect.sizeDelta = new Vector2(220f, 220f);

        Text nameText = RuntimeUIFactory.CreateText(cardRect, RunItemCatalog.GetTitle(offer.Type), 29,
            new Vector2(0f, -78f), new Vector2(420f, 52f), new Color(1f, 0.72f, 0.24f));
        nameText.fontStyle = FontStyle.Bold;
        RuntimeUIFactory.CreateText(cardRect, RunItemCatalog.GetDescription(offer), 21,
            new Vector2(0f, -137f), new Vector2(420f, 76f), new Color(0.87f, 0.94f, 1f));
    }

    private static RunItemOffer[] CreateDistinctOffers()
    {
        List<RunItemType> pool = new List<RunItemType>(AllTypes);
        int first = UnityEngine.Random.Range(0, pool.Count);
        RunItemType firstType = pool[first];
        pool.RemoveAt(first);
        RunItemType secondType = pool[UnityEngine.Random.Range(0, pool.Count)];
        return new[] { RunItemCatalog.CreateOffer(firstType), RunItemCatalog.CreateOffer(secondType) };
    }

    private void Update()
    {
        if (resolved) return;
        if (Input.GetKeyDown(KeyCode.LeftArrow)) SelectIndex(0);
        else if (Input.GetKeyDown(KeyCode.RightArrow)) SelectIndex(1);
        else if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            Resolve(selectedIndex);
    }

    private void SelectIndex(int index)
    {
        selectedIndex = Mathf.Clamp(index, 0, optionButtons.Length - 1);
        Button selected = optionButtons[selectedIndex];
        if (selected != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(selected.gameObject);
    }

    private void Resolve(int index)
    {
        if (resolved || index < 0 || index >= offers.Length) return;
        resolved = true;
        foreach (Button button in optionButtons)
            if (button != null) button.interactable = false;

        GameSession session = GameSession.Instance;
        if (session != null)
        {
            session.AcquireItem(offers[index], playerHealth);
            session.ResolveStageReward(sceneKey, playerHealth);
        }
        else
        {
            ApplyStandalone(offers[index]);
        }

        RestorePlayer();
        completed?.Invoke();
        Destroy(gameObject);
    }

    private void ApplyStandalone(RunItemOffer offer)
    {
        if (playerHealth == null) return;
        if (offer.Type == RunItemType.HealthRecovery)
            playerHealth.Heal(offer.Magnitude);
        else if (offer.Type == RunItemType.MaxHealthBoost)
            playerHealth.ApplyRunState(offer.Magnitude, playerHealth.CurrentHP + offer.Magnitude);
        playerHealth.Heal(20f);
    }

    private void RestorePlayer()
    {
        if (playerController != null && playerWasEnabled && playerHealth != null && !playerHealth.IsDead)
            playerController.enabled = true;
    }

    private void OnDestroy()
    {
        if (!resolved) RestorePlayer();
    }
}
