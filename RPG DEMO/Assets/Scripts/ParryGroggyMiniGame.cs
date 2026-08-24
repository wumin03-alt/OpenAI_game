using System;
using Game.Core;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ParryGroggyMiniGame : MonoBehaviour
{
    [SerializeField, Min(2)] private int sequenceLength = 3;

    private readonly KeyCode[] keys =
    {
        KeyCode.LeftArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow
    };

    private readonly string[] glyphs = { "←", "↑", "↓", "→" };
    private KeyCode[] sequence;
    private int progress;
    private float timeRemaining;
    private float timeLimit;
    private float previousTimeScale;
    private bool playerWasEnabled;
    private PlayerController player;
    private BossStaggerGauge gauge;
    private Canvas canvas;
    private Image timerFill;
    private Text timerText;
    private Text feedback;
    private Image[] cards;
    private Text[] cardTexts;

    public bool IsActive { get; private set; }

    public bool TryBegin(BossStaggerGauge targetGauge)
    {
        if (IsActive || targetGauge == null || targetGauge.IsStaggered) return false;

        gauge = targetGauge;
        player = FindAnyObjectByType<PlayerController>();
        if (player == null) return false;

        EnsureUi();
        BuildSequence();
        progress = 0;
        timeLimit = GameSession.Instance != null ? GameSession.Instance.ParryMiniGameDuration : 3f;
        timeRemaining = timeLimit;
        playerWasEnabled = player.enabled;
        player.enabled = false;
        previousTimeScale = Time.timeScale;
        Time.timeScale = 0f;
        IsActive = true;
        canvas.gameObject.SetActive(true);
        feedback.text = "패링 데이터 해킹 // 방향키를 순서대로 입력";
        feedback.color = new Color(0.22f, 0.95f, 1f);
        Refresh();
        return true;
    }

    private void Update()
    {
        if (!IsActive) return;

        timeRemaining -= Time.unscaledDeltaTime;
        RefreshTimer();
        if (timeRemaining <= 0f)
        {
            Resolve(false);
            return;
        }

        for (int i = 0; i < keys.Length; i++)
        {
            if (!Input.GetKeyDown(keys[i])) continue;
            HandleInput(keys[i]);
            break;
        }
    }

    private void HandleInput(KeyCode key)
    {
        if (key == sequence[progress])
        {
            progress++;
            if (progress >= sequence.Length)
            {
                Resolve(true);
                return;
            }

            feedback.text = "입력 확인 // 계속 진행";
            feedback.color = new Color(0.22f, 1f, 0.68f);
            Refresh();
            return;
        }

        progress = key == sequence[0] ? 1 : 0;
        feedback.text = "순서 오류 // 처음부터 다시 입력";
        feedback.color = new Color(1f, 0.3f, 0.48f);
        Refresh();
    }

    private void Resolve(bool success)
    {
        if (!IsActive) return;
        IsActive = false;

        if (success && gauge != null)
        {
            float damage = GameSession.Instance != null
                ? GameSession.Instance.GroggyDamagePerSuccess
                : 34f;
            gauge.ApplyGroggyDamage(damage);
        }

        RestoreGame();
        if (canvas != null) canvas.gameObject.SetActive(false);
        Debug.Log(success ? "[ParryMiniGame] SUCCESS" : "[ParryMiniGame] FAILED", this);
    }

    private void RestoreGame()
    {
        Time.timeScale = previousTimeScale;
        if (player != null && playerWasEnabled)
        {
            Health health = player.GetComponent<Health>();
            if (health == null || !health.IsDead) player.enabled = true;
        }
    }

    private void BuildSequence()
    {
        sequence = new KeyCode[Mathf.Max(2, sequenceLength)];
        int previous = -1;
        for (int i = 0; i < sequence.Length; i++)
        {
            int index;
            do index = UnityEngine.Random.Range(0, keys.Length);
            while (index == previous && keys.Length > 1);
            previous = index;
            sequence[i] = keys[index];
        }
    }

    private void EnsureUi()
    {
        if (canvas != null) return;
        canvas = RuntimeUIFactory.CreateCanvas("ParryGroggyMiniGameCanvas", null, 335);

        Image panel = RuntimeUIFactory.CreateImage(canvas.transform, "Panel", new Color(0.025f, 0.04f, 0.1f, 0.97f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(800f, 310f);
        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.71f, 0.17f, 1f, 0.95f);
        outline.effectDistance = new Vector2(4f, -4f);

        Text title = RuntimeUIFactory.CreateText(panelRect, "PARRY COUNTER // GROGGY HACK", 31,
            new Vector2(0f, 115f), new Vector2(720f, 52f), new Color(0.91f, 0.96f, 1f));
        title.fontStyle = FontStyle.Bold;

        cards = new Image[Mathf.Max(2, sequenceLength)];
        cardTexts = new Text[cards.Length];
        float spacing = 130f;
        float start = -(cards.Length - 1) * spacing * 0.5f;
        for (int i = 0; i < cards.Length; i++)
        {
            Image card = RuntimeUIFactory.CreateImage(panelRect, $"Input_{i + 1}", new Color(0.08f, 0.14f, 0.25f, 1f));
            RectTransform rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(start + i * spacing, 35f);
            rect.sizeDelta = new Vector2(98f, 78f);
            cards[i] = card;
            cardTexts[i] = RuntimeUIFactory.CreateText(rect, "?", 48, Vector2.zero, rect.sizeDelta, Color.white);
            RuntimeUIFactory.Stretch(cardTexts[i].rectTransform);
        }

        Image timerBack = RuntimeUIFactory.CreateImage(panelRect, "TimerBack", new Color(0.06f, 0.09f, 0.16f, 1f));
        RectTransform timerRect = timerBack.rectTransform;
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(0.5f, 0.5f);
        timerRect.anchoredPosition = new Vector2(0f, -42f);
        timerRect.sizeDelta = new Vector2(650f, 20f);
        timerFill = RuntimeUIFactory.CreateImage(timerRect, "TimerFill", new Color(0.15f, 0.9f, 1f));
        timerFill.type = Image.Type.Filled;
        timerFill.fillMethod = Image.FillMethod.Horizontal;
        RuntimeUIFactory.Stretch(timerFill.rectTransform, 2f, -2f, 2f, -2f);
        timerText = RuntimeUIFactory.CreateText(panelRect, "3.0s", 21,
            new Vector2(0f, -73f), new Vector2(150f, 34f), Color.white);
        feedback = RuntimeUIFactory.CreateText(panelRect, string.Empty, 20,
            new Vector2(0f, -111f), new Vector2(700f, 38f), Color.white);
        canvas.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (sequence == null || cards == null) return;
        for (int i = 0; i < cards.Length; i++)
        {
            int index = Array.IndexOf(keys, sequence[i]);
            cardTexts[i].text = index >= 0 ? glyphs[index] : "?";
            cards[i].color = i < progress
                ? new Color(0.15f, 0.8f, 0.5f, 1f)
                : i == progress
                    ? new Color(0.72f, 0.22f, 1f, 1f)
                    : new Color(0.08f, 0.14f, 0.25f, 1f);
        }
        RefreshTimer();
    }

    private void RefreshTimer()
    {
        if (timerFill == null) return;
        float normalized = Mathf.Clamp01(timeRemaining / Mathf.Max(0.1f, timeLimit));
        timerFill.fillAmount = normalized;
        timerFill.color = Color.Lerp(new Color(1f, 0.2f, 0.38f), new Color(0.15f, 0.9f, 1f), normalized);
        timerText.text = $"{Mathf.Max(0f, timeRemaining):0.0}s";
    }

    private void OnDestroy()
    {
        if (IsActive)
        {
            IsActive = false;
            RestoreGame();
        }
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
