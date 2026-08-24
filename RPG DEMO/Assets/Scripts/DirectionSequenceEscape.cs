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
            new Color(0.02f, 0.04f, 0.08f, 0.95f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.68f);
        panelRect.sizeDelta = new Vector2(760f, 250f);

        Outline outline = panel.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.67f, 0.16f, 0.95f);
        outline.effectDistance = new Vector2(3f, -3f);

        Text header = RuntimeUIFactory.CreateText(panelRect,
            "포획 해제 // 5초 안에 방향키를 순서대로 입력", 30,
            new Vector2(0f, 88f), new Vector2(710f, 48f), Color.white);
        header.fontStyle = FontStyle.Bold;

        directionCards = new Image[Mathf.Max(1, sequenceLength)];
        directionTexts = new Text[directionCards.Length];
        float spacing = 126f;
        float start = -(directionCards.Length - 1) * spacing * 0.5f;
        for (int i = 0; i < directionCards.Length; i++)
        {
            Image card = RuntimeUIFactory.CreateImage(panelRect, $"Direction_{i + 1}",
                new Color(0.09f, 0.16f, 0.24f, 1f));
            RectTransform rect = card.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(start + i * spacing, 20f);
            rect.sizeDelta = new Vector2(94f, 74f);
            directionCards[i] = card;

            Text arrow = RuntimeUIFactory.CreateText(rect, "?", 48, Vector2.zero,
                rect.sizeDelta, Color.white);
            RuntimeUIFactory.Stretch(arrow.rectTransform);
            arrow.fontStyle = FontStyle.Bold;
            directionTexts[i] = arrow;
        }

        Image timerBackground = RuntimeUIFactory.CreateImage(panelRect, "TimerBackground",
            new Color(0.07f, 0.1f, 0.16f, 1f));
        RectTransform timerRect = timerBackground.rectTransform;
        timerRect.anchorMin = timerRect.anchorMax = new Vector2(0.5f, 0.5f);
        timerRect.anchoredPosition = new Vector2(0f, -51f);
        timerRect.sizeDelta = new Vector2(620f, 18f);

        countdownFill = RuntimeUIFactory.CreateImage(timerRect, "TimerFill",
            new Color(1f, 0.7f, 0.2f, 1f));
        countdownFill.type = Image.Type.Filled;
        countdownFill.fillMethod = Image.FillMethod.Horizontal;
        countdownFill.fillOrigin = 0;
        RuntimeUIFactory.Stretch(countdownFill.rectTransform, 2f, -2f, 2f, -2f);

        countdownText = RuntimeUIFactory.CreateText(panelRect, "5.0", 21,
            new Vector2(0f, -78f), new Vector2(120f, 30f), Color.white);
        feedbackText = RuntimeUIFactory.CreateText(panelRect, string.Empty, 20,
            new Vector2(0f, -104f), new Vector2(650f, 30f), Color.white);

        panel.gameObject.SetActive(false);
    }

    private void RefreshCards()
    {
        if (sequence == null || directionCards == null) return;

        for (int i = 0; i < directionCards.Length; i++)
        {
            int keyIndex = Array.IndexOf(directionKeys, sequence[i]);
            directionTexts[i].text = keyIndex >= 0 ? directionGlyphs[keyIndex] : "?";
            directionCards[i].color = i < progress
                ? new Color(0.16f, 0.78f, 0.48f, 1f)
                : i == progress
                    ? new Color(1f, 0.52f, 0.12f, 1f)
                    : new Color(0.09f, 0.16f, 0.24f, 1f);
        }
    }

    private void RefreshTimer()
    {
        if (countdownFill == null) return;
        float normalized = Mathf.Clamp01(TimeRemaining / timeLimit);
        countdownFill.fillAmount = normalized;
        countdownFill.color = Color.Lerp(new Color(1f, 0.18f, 0.3f),
            new Color(1f, 0.72f, 0.2f), normalized);
        countdownText.text = $"{Mathf.Max(0f, TimeRemaining):0.0}s";
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
