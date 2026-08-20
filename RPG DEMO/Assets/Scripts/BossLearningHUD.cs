using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 보스의 관찰 항목, 데이터 무결성, 장착 카운터와 교란 정도를 실시간 공개합니다.
/// 승패 화면도 이 컴포넌트가 런타임에 구성하므로 기존 씬 UI와 충돌하지 않습니다.
/// </summary>
public class BossLearningHUD : MonoBehaviour
{
    private BossController boss;
    private Health bossHealth;
    private Health playerHealth;
    private PlayerCombatTracker tracker;
    private TMP_FontAsset font;

    private TMP_Text titleText;
    private TMP_Text profileText;
    private TMP_Text protocolText;
    private TMP_Text integrityLabel;
    private TMP_Text liveStatusText;
    private Image integrityFill;
    private GameObject resultRoot;
    private Image resultBackground;
    private Image resultGlow;
    private Image resultTopLine;
    private Image resultBottomLine;
    private TMP_Text resultSymbol;
    private TMP_Text resultTitle;
    private TMP_Text resultSubtitle;
    private TMP_Text resultTip;
    private TMP_Text resultPrompt;
    private bool initialized;
    private bool resultShown;

    public void Initialize(BossController owner, Health ownerHealth, PlayerCombatTracker combatTracker, Health player)
    {
        if (initialized) return;
        initialized = true;
        boss = owner;
        bossHealth = ownerHealth;
        tracker = combatTracker;
        playerHealth = player;

        TMP_Text template = FindAnyObjectByType<TMP_Text>();
        if (template != null) font = template.font;

        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[BossLearningHUD] Canvas를 찾지 못했습니다.");
            return;
        }

        BuildLearningPanel(canvas.transform);
        BuildResultPanel(canvas.transform);

        if (bossHealth != null && bossHealth.onDeath != null)
            bossHealth.onDeath.AddListener(OnBossDefeated);
        if (playerHealth != null && playerHealth.onDeath != null)
            playerHealth.onDeath.AddListener(OnPlayerDefeated);
    }

    private void OnDestroy()
    {
        if (bossHealth != null && bossHealth.onDeath != null)
            bossHealth.onDeath.RemoveListener(OnBossDefeated);
        if (playerHealth != null && playerHealth.onDeath != null)
            playerHealth.onDeath.RemoveListener(OnPlayerDefeated);
    }

    private void Update()
    {
        if (!initialized || titleText == null || tracker == null || boss == null) return;

        if (resultShown)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            return;
        }

        float integrity = boss.Phase <= 1 ? tracker.DataIntegrity : boss.AdaptationStrength;
        integrityFill.fillAmount = integrity;
        integrityFill.color = Color.Lerp(new Color(1f, 0.2f, 0.35f), new Color(0.1f, 0.9f, 1f), integrity);

        if (boss.Phase <= 1)
        {
            titleText.text = "AI CORE // TELEMETRY";
            titleText.color = new Color(0.15f, 0.9f, 1f);
            liveStatusText.text = "● LIVE / PHASE 01";
            profileText.text = $"ATTACK   {tracker.GetStyleLabel(),-12}  M {tracker.MeleeCount:00} / R {tracker.RangedCount:00}\n" +
                               $"MOTION   {tracker.GetMobilityLabel()}\n" +
                               $"EVADE    {tracker.GetEvasionLabel()}";
            protocolText.text = "[E] 패링으로 공격을 무효화하고 학습 데이터를 교란";
            integrityLabel.text = $"DATA INTEGRITY                                      {tracker.DataIntegrity * 100f:F0}%";
        }
        else
        {
            titleText.text = "AI CORE // ADAPTIVE MODEL";
            titleText.color = new Color(1f, 0.2f, 0.48f);
            liveStatusText.text = "◆ LOCKED / PHASE 02";
            profileText.text = $"ATTACK   {tracker.GetStyleLabel(),-12}  M {tracker.MeleeCount:00} / R {tracker.RangedCount:00}\n" +
                               $"MOTION   {tracker.GetMobilityLabel()}\n" +
                               $"EVADE    {tracker.GetEvasionLabel()}";
            protocolText.text = "COUNTER // " + boss.CounterProtocol;
            integrityLabel.text = $"ADAPTATION STRENGTH                             {boss.AdaptationStrength * 100f:F0}%";
        }
    }

    private void BuildLearningPanel(Transform parent)
    {
        GameObject root = CreateUIObject("BossLearningPanel", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-32f, -112f);
        rect.sizeDelta = new Vector2(438f, 246f);
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.72f, 0.88f, 0.92f);
        bg.raycastTarget = false;

        Image inner = CreateImage(root.transform, "InnerPanel", new Color(0.012f, 0.025f, 0.055f, 0.98f));
        inner.rectTransform.anchorMin = Vector2.zero;
        inner.rectTransform.anchorMax = Vector2.one;
        inner.rectTransform.offsetMin = new Vector2(3f, 3f);
        inner.rectTransform.offsetMax = new Vector2(-3f, -3f);

        Image leftRail = CreateImage(root.transform, "SignalRail", new Color(0.16f, 0.92f, 1f, 0.94f));
        leftRail.rectTransform.anchorMin = new Vector2(0f, 0f);
        leftRail.rectTransform.anchorMax = new Vector2(0f, 1f);
        leftRail.rectTransform.pivot = new Vector2(0f, 0.5f);
        leftRail.rectTransform.anchoredPosition = new Vector2(5f, 0f);
        leftRail.rectTransform.sizeDelta = new Vector2(4f, -10f);

        Image header = CreateImage(root.transform, "HeaderBand", new Color(0.025f, 0.11f, 0.2f, 0.99f));
        header.rectTransform.anchorMin = new Vector2(0f, 1f);
        header.rectTransform.anchorMax = Vector2.one;
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        header.rectTransform.sizeDelta = new Vector2(-8f, 48f);

        AddTerminalDot(root.transform, new Vector2(18f, -24f), new Color(1f, 0.3f, 0.4f));
        AddTerminalDot(root.transform, new Vector2(37f, -24f), new Color(1f, 0.72f, 0.24f));
        AddTerminalDot(root.transform, new Vector2(56f, -24f), new Color(0.22f, 0.95f, 0.66f));

        titleText = CreateText(root.transform, "Title", new Vector2(76f, -13f), new Vector2(230f, 30f), 16f, TextAlignmentOptions.TopLeft);
        titleText.fontStyle = FontStyles.Bold;
        liveStatusText = CreateText(root.transform, "LiveStatus", new Vector2(302f, -14f), new Vector2(116f, 28f), 13f, TextAlignmentOptions.TopRight);
        liveStatusText.fontStyle = FontStyles.Bold;
        liveStatusText.color = new Color(0.32f, 1f, 0.72f);

        TMP_Text section = CreateText(root.transform, "ProfileCaption", new Vector2(22f, -60f), new Vector2(394f, 20f), 12f, TextAlignmentOptions.TopLeft);
        section.text = "PLAYER BEHAVIOR PROFILE  /  실시간 표본";
        section.color = new Color(0.35f, 0.64f, 0.78f);
        section.fontStyle = FontStyles.Bold;

        profileText = CreateText(root.transform, "Profile", new Vector2(22f, -84f), new Vector2(394f, 78f), 15.5f, TextAlignmentOptions.TopLeft);
        profileText.color = new Color(0.83f, 0.9f, 1f);
        profileText.lineSpacing = 8f;

        integrityLabel = CreateText(root.transform, "IntegrityLabel", new Vector2(22f, -168f), new Vector2(394f, 20f), 12.5f, TextAlignmentOptions.TopLeft);
        integrityLabel.color = new Color(0.7f, 0.8f, 0.92f);

        GameObject barBg = CreateUIObject("IntegrityBar", root.transform);
        RectTransform barRect = barBg.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(22f, -191f);
        barRect.sizeDelta = new Vector2(394f, 12f);
        Image barBackground = barBg.AddComponent<Image>();
        barBackground.color = new Color(0.02f, 0.03f, 0.07f, 1f);
        barBackground.raycastTarget = false;

        GameObject fill = CreateUIObject("Fill", barBg.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(2f, 2f);
        fillRect.offsetMax = new Vector2(-2f, -2f);
        integrityFill = fill.AddComponent<Image>();
        integrityFill.type = Image.Type.Filled;
        integrityFill.fillMethod = Image.FillMethod.Horizontal;
        integrityFill.fillOrigin = 0;
        integrityFill.raycastTarget = false;

        Image protocolBand = CreateImage(root.transform, "ProtocolBand", new Color(0.02f, 0.12f, 0.17f, 0.96f));
        protocolBand.rectTransform.anchorMin = new Vector2(0f, 1f);
        protocolBand.rectTransform.anchorMax = new Vector2(1f, 1f);
        protocolBand.rectTransform.pivot = new Vector2(0.5f, 1f);
        protocolBand.rectTransform.anchoredPosition = new Vector2(0f, -211f);
        protocolBand.rectTransform.sizeDelta = new Vector2(-10f, 30f);

        protocolText = CreateText(root.transform, "Protocol", new Vector2(22f, -217f), new Vector2(394f, 22f), 12.5f, TextAlignmentOptions.TopLeft);
        protocolText.color = new Color(0.35f, 1f, 0.68f);
        protocolText.fontStyle = FontStyles.Bold;
    }

    private void BuildResultPanel(Transform parent)
    {
        resultRoot = CreateUIObject("BossResultPanel", parent);
        RectTransform rect = resultRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        resultBackground = resultRoot.AddComponent<Image>();
        resultBackground.raycastTarget = false;

        resultGlow = CreateImage(resultRoot.transform, "ResultGlow", Color.clear);
        resultGlow.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        resultGlow.rectTransform.anchorMax = new Vector2(1f, 0.5f);
        resultGlow.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        resultGlow.rectTransform.anchoredPosition = Vector2.zero;
        resultGlow.rectTransform.sizeDelta = new Vector2(0f, 360f);

        resultTopLine = CreateImage(resultRoot.transform, "ResultTopLine", Color.white);
        SetCenteredLine(resultTopLine.rectTransform, 178f);
        resultBottomLine = CreateImage(resultRoot.transform, "ResultBottomLine", Color.white);
        SetCenteredLine(resultBottomLine.rectTransform, -178f);

        resultSymbol = CreateCenteredText(resultRoot.transform, "ResultSymbol", new Vector2(0f, 142f), new Vector2(160f, 52f), 34f);
        resultTitle = CreateCenteredText(resultRoot.transform, "ResultTitle", new Vector2(0f, 72f), new Vector2(1400f, 90f), 62f);
        resultTitle.fontStyle = FontStyles.Bold;
        resultSubtitle = CreateCenteredText(resultRoot.transform, "ResultSubtitle", new Vector2(0f, -4f), new Vector2(1320f, 58f), 25f);
        resultTip = CreateCenteredText(resultRoot.transform, "ResultTip", new Vector2(0f, -72f), new Vector2(1260f, 48f), 19f);
        resultPrompt = CreateCenteredText(resultRoot.transform, "ResultPrompt", new Vector2(0f, -142f), new Vector2(620f, 40f), 18f);
        resultPrompt.fontStyle = FontStyles.Bold;
        resultRoot.SetActive(false);
    }

    private TMP_Text CreateCenteredText(Transform parent, string objectName, Vector2 position, Vector2 size, float fontSize)
    {
        TMP_Text text = CreateText(parent, objectName, Vector2.zero, size, fontSize, TextAlignmentOptions.Center);
        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        return text;
    }

    private static void SetCenteredLine(RectTransform rect, float y)
    {
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(1180f, 2f);
    }

    private Image CreateImage(Transform parent, string objectName, Color color)
    {
        GameObject go = CreateUIObject(objectName, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void AddGem(Transform parent, Vector2 position, Color color)
    {
        Image gem = CreateImage(parent, "CornerGem", color);
        RectTransform rect = gem.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(14f, 14f);
        rect.localEulerAngles = new Vector3(0f, 0f, 45f);
    }

    private void AddTerminalDot(Transform parent, Vector2 position, Color color)
    {
        Image dot = CreateImage(parent, "TerminalDot", color);
        RectTransform rect = dot.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(10f, 10f);
    }

    private GameObject CreateUIObject(string objectName, Transform parent)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);
        return go;
    }

    private TMP_Text CreateText(Transform parent, string objectName, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = CreateUIObject(objectName, parent);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;
        return text;
    }

    private void OnBossDefeated()
    {
        ShowResult(
            "◇  SYSTEM LIBERATED  ◇",
            "AI 코어 해체 완료",
            "예측 순환을 오염시키고 사이버 드래곤을 정지시켰습니다.",
            "REPORT · 대응 모델 무력화 / 데이터 주권 회수",
            new Color(0.2f, 1f, 0.76f),
            new Color(0.01f, 0.18f, 0.18f, 0.72f),
            new Color(0.005f, 0.018f, 0.035f, 0.92f));
    }

    private void OnPlayerDefeated()
    {
        ShowResult(
            "◆  MODEL LOCKED  ◆",
            "행동 패턴이 간파되었습니다",
            "AI 코어가 당신의 전투 데이터를 완전히 학습했습니다.",
            "TIP · 같은 행동을 반복하지 말고 패링으로 학습 데이터를 교란하세요.",
            new Color(1f, 0.26f, 0.34f),
            new Color(0.42f, 0.015f, 0.025f, 0.66f),
            new Color(0.055f, 0.004f, 0.012f, 0.91f));
    }

    private void ShowResult(string symbol, string title, string subtitle, string tip,
        Color accent, Color glow, Color background)
    {
        if (resultShown || resultRoot == null) return;
        resultShown = true;
        resultRoot.SetActive(true);
        resultRoot.transform.SetAsLastSibling();
        resultBackground.color = background;
        resultGlow.color = glow;
        resultTopLine.color = accent;
        resultBottomLine.color = accent;
        resultSymbol.text = symbol;
        resultSymbol.color = accent;
        resultTitle.text = title;
        resultTitle.color = Color.white;
        resultSubtitle.text = subtitle;
        resultSubtitle.color = new Color(0.93f, 0.95f, 1f);
        resultTip.text = tip;
        resultTip.color = Color.Lerp(accent, Color.white, 0.35f);
        resultPrompt.text = "[ ENTER ]  전투 재시작";
        resultPrompt.color = accent;
        Time.timeScale = 0f;
    }
}
