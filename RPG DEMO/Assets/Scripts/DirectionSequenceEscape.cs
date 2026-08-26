using System;
using Game.UI;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 포획된 플레이어가 제한 시간 안에 네 방향키를 표시된 순서대로 입력하는 탈출 UI입니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DirectionSequenceEscape : MonoBehaviour
{
    [SerializeField, Min(1)] private int sequenceLength = 4;
    [SerializeField, Min(0.5f)] private float timeLimit = 5f;

    public event Action<bool> Resolved;

    public bool IsActive { get; private set; }
    public float TimeRemaining { get; private set; }
    public int SequenceLength => sequenceLength;
    public float TimeLimit => timeLimit;

    private readonly KeyCode[] directionKeys =
    {
        KeyCode.LeftArrow, KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.RightArrow
    };

    private readonly string[] directionGlyphs = { "←", "↑", "↓", "→" };
    private KeyCode[] sequence;
    private int progress;

    private Canvas canvas;
    private Image panel;
    private Image countdownFill;
    private Image[] directionCards;
    private Text[] directionTexts;
    private Text countdownText;
    private Text feedbackText;

    private PlayerController capturedController;
    private Health capturedHealth;
    private Rigidbody2D capturedBody;
    private bool controllerWasEnabled;
    private RigidbodyConstraints2D savedConstraints;
    private Action onSuccess;
    private Action onFailure;

    public bool BeginEscape(PlayerController player, Health health, Action success, Action failure)
    {
        if (IsActive || player == null || health == null || health.IsDead) return false;

        EnsureUi();
        capturedController = player;
        capturedHealth = health;
        capturedBody = player.GetComponent<Rigidbody2D>();
        onSuccess = success;
        onFailure = failure;

        controllerWasEnabled = player.enabled;
        player.enabled = false;
        if (capturedBody != null)
        {
            savedConstraints = capturedBody.constraints;
            capturedBody.linearVelocity = Vector2.zero;
            capturedBody.angularVelocity = 0f;
            capturedBody.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        BuildSequence();
        progress = 0;
        TimeRemaining = timeLimit;
        IsActive = true;
        panel.gameObject.SetActive(true);
        feedbackText.text = "방향키를 순서대로 입력하세요";
        feedbackText.color = new Color(1f, 0.82f, 0.32f);
        RefreshCards();
        RefreshTimer();
        return true;
    }

    public void Cancel(bool countAsSuccess)
    {
        if (!IsActive) return;
        Resolve(countAsSuccess);
    }

    private void Update()
    {
        if (!IsActive) return;

        TimeRemaining -= Time.unscaledDeltaTime;
        RefreshTimer();
        if (TimeRemaining <= 0f)
        {
            Resolve(false);
            return;
        }

        for (int i = 0; i < directionKeys.Length; i++)
        {
            if (!Input.GetKeyDown(directionKeys[i])) continue;
            HandleDirection(directionKeys[i]);
            break;
        }
    }

    private void HandleDirection(KeyCode key)
    {
        if (key == sequence[progress])
        {
            progress++;
            feedbackText.text = progress >= sequence.Length ? "포획 해제" : "입력 확인";
            feedbackText.color = new Color(0.22f, 1f, 0.68f);
            RefreshCards();
            if (progress >= sequence.Length) Resolve(true);
            return;
        }

        progress = key == sequence[0] ? 1 : 0;
        feedbackText.text = "순서 오류 // 처음부터 다시 입력";
        feedbackText.color = new Color(1f, 0.25f, 0.42f);
        RefreshCards();
    }

    private void BuildSequence()
    {
        sequence = new KeyCode[Mathf.Max(1, sequenceLength)];
        int previous = -1;
        for (int i = 0; i < sequence.Length; i++)
        {
            int index;
            do index = UnityEngine.Random.Range(0, directionKeys.Length);
            while (index == previous && directionKeys.Length > 1);
            previous = index;
            sequence[i] = directionKeys[index];
        }
    }

    private void Resolve(bool escaped)
    {
        if (!IsActive) return;
        IsActive = false;

        if (capturedBody != null)
        {
            capturedBody.constraints = savedConstraints;
            capturedBody.linearVelocity = Vector2.zero;
            capturedBody.angularVelocity = 0f;
        }

        if (capturedController != null && controllerWasEnabled &&
            capturedHealth != null && !capturedHealth.IsDead)
            capturedController.enabled = true;

        if (panel != null) panel.gameObject.SetActive(false);

        Action callback = escaped ? onSuccess : onFailure;
        ClearCaptureReferences();
        callback?.Invoke();
        Resolved?.Invoke(escaped);
    }

    private void ClearCaptureReferences()
    {
        capturedController = null;
        capturedHealth = null;
        capturedBody = null;
        onSuccess = null;
        onFailure = null;
    }

    private void EnsureUi()
    {
        if (canvas != null) return;

        canvas = RuntimeUIFactory.CreateCanvas("DirectionEscapeCanvas", null, 320);
        panel = RuntimeUIFactory.CreateImage(canvas.transform, "DirectionEscapePanel",
            new Color(0.29f, 0.11f, 0.08f, 0.98f));
        MiddleBossUIStyle.Rounded(panel, new Color(0.29f, 0.11f, 0.08f, 0.98f));
        MiddleBossUIStyle.Outline(panel, new Color(1f, 0.72f, 0.2f, 1f), 4f);
        MiddleBossUIStyle.Shadow(panel, new Color(0f, 0f, 0f, 0.78f), 8f);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.68f);
        panelRect.sizeDelta = new Vector2(650f, 245f);

        Image parchment = RuntimeUIFactory.CreateImage(panelRect, "ParchmentInlay",
            new Color(1f, 0.88f, 0.59f, 0.98f));
        MiddleBossUIStyle.Rounded(parchment, new Color(1f, 0.88f, 0.59f, 0.98f));
        RuntimeUIFactory.Stretch(parchment.rectTransform, 8f, -8f, 8f, -8f);

        Image headerRibbon = RuntimeUIFactory.CreateImage(parchment.rectTransform, "HeaderRibbon",
            new Color(0.47f, 0.16f, 0.12f, 1f));
        MiddleBossUIStyle.Rounded(headerRibbon, new Color(0.47f, 0.16f, 0.12f, 1f));
        RectTransform ribbonRect = headerRibbon.rectTransform;
        ribbonRect.anchorMin = ribbonRect.anchorMax = new Vector2(0.5f, 1f);
        ribbonRect.pivot = new Vector2(0.5f, 1f);
        ribbonRect.anchoredPosition = new Vector2(0f, -8f);
        ribbonRect.sizeDelta = new Vector2(600f, 48f);

        Text header = RuntimeUIFactory.CreateText(ribbonRect,
            "포획 탈출!  5초 안에 순서대로 입력", 25,
            Vector2.zero, new Vector2(570f, 42f), Color.white);
        header.fontStyle = FontStyle.Bold;

        directionCards = new Image[Mathf.Max(1, sequenceLength)];
        directionTexts = new Text[directionCards.Length];
        float spacing = 112f;
        float start = -(directionCards.Length - 1) * spacing * 0.5f;
        for (int i = 0; i < directionCards.Length; i++)
        {
            Image card = RuntimeUIFactory.CreateImage(parchment.rectTransform, $"Direction_{i + 1}",
                new Color(0.36f, 0.2f, 0.16f, 1f));
            MiddleBossUIStyle.Rounded(card, new Color(0.36f, 0.2f, 0.16f, 1f));
            MiddleBossUIStyle.Outline(card, new Color(1f, 0.66f, 0.18f, 0.9f), 2f);
            RectTransform rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(start + i * spacing, 18f);
            rect.sizeDelta = new Vector2(82f, 82f);
            directionCards[i] = card;

            Text arrow = RuntimeUIFactory.CreateText(rect, "?", 48, Vector2.zero,
                rect.sizeDelta, Color.white);
            RuntimeUIFactory.Stretch(arrow.rectTransform);
            arrow.fontStyle = FontStyle.Bold;
            directionTexts[i] = arrow;
        }

        Image timerBackground = RuntimeUIFactory.CreateImage(parchment.rectTransform, "TimerBackground",
            new Color(0.33f, 0.16f, 0.12f, 1f));
        MiddleBossUIStyle.Rounded(timerBackground, new Color(0.33f, 0.16f, 0.12f, 1f));
        RectTransform timerRect = timerBackground.rectTransform;
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(0.5f, 0.5f);
        timerRect.anchoredPosition = new Vector2(0f, -49f);
        timerRect.sizeDelta = new Vector2(545f, 22f);

        countdownFill = RuntimeUIFactory.CreateImage(timerRect, "TimerFill",
            new Color(1f, 0.7f, 0.2f, 1f));
        MiddleBossUIStyle.Rounded(countdownFill, new Color(1f, 0.7f, 0.2f, 1f));
        MiddleBossUIStyle.HorizontalFill(countdownFill, 1f, 3f);

        countdownText = RuntimeUIFactory.CreateText(parchment.rectTransform, "5.0초", 20,
            new Vector2(-245f, -78f), new Vector2(100f, 30f), new Color(0.34f, 0.13f, 0.09f));
        feedbackText = RuntimeUIFactory.CreateText(parchment.rectTransform, string.Empty, 19,
            new Vector2(35f, -78f), new Vector2(430f, 30f), new Color(0.34f, 0.13f, 0.09f));

        panel.gameObject.SetActive(false);
    }

    private void RefreshCards()
    {
        if (sequence == null || directionCards == null) return;

        for (int i = 0; i < directionCards.Length; i++)
        {
            int keyIndex = Array.IndexOf(directionKeys, sequence[i]);
            directionTexts[i].text = keyIndex >= 0 ? directionGlyphs[keyIndex] : "?";
            Color cardColor = i < progress
                ? new Color(0.16f, 0.72f, 0.45f, 1f)
                : i == progress
                    ? new Color(1f, 0.47f, 0.1f, 1f)
                    : new Color(0.36f, 0.2f, 0.16f, 1f);
            directionCards[i].color = cardColor;
        }
    }

    private void RefreshTimer()
    {
        if (countdownFill == null) return;
        float normalized = Mathf.Clamp01(TimeRemaining / timeLimit);
        MiddleBossUIStyle.HorizontalFill(countdownFill, normalized, 3f);
        countdownFill.color = Color.Lerp(new Color(1f, 0.18f, 0.3f),
            new Color(1f, 0.72f, 0.2f), normalized);
        countdownText.text = $"{Mathf.Max(0f, TimeRemaining):0.0}초";
    }

    private void OnDestroy()
    {
        if (IsActive)
        {
            onSuccess = null;
            onFailure = null;
            Resolve(true);
        }

        if (canvas != null) Destroy(canvas.gameObject);
    }
}
