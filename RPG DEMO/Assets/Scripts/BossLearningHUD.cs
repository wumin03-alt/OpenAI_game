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
    private Image integrityFill;
    private GameObject resultRoot;
    private TMP_Text resultText;
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
            titleText.text = "DEEP//SICK  [OBSERVING]";
            titleText.color = new Color(0.15f, 0.9f, 1f);
            profileText.text = $"ATTACK  {tracker.GetStyleLabel()}\n" +
                               $"MOTION  {tracker.GetMobilityLabel()}\n" +
                               $"EVASION {tracker.GetEvasionLabel()}";
            protocolText.text = "PARRY [E] TO INJECT FALSE DATA";
            integrityLabel.text = $"DATA INTEGRITY  {tracker.DataIntegrity * 100f:F0}%";
        }
        else
        {
            titleText.text = "DEEP//SICK  [ADAPTED]";
            titleText.color = new Color(1f, 0.2f, 0.48f);
            profileText.text = $"LEARNED {tracker.GetStyleLabel()}\n" +
                               $"MOTION  {tracker.GetMobilityLabel()}\n" +
                               $"PREDICT {tracker.GetEvasionLabel()}";
            protocolText.text = "COUNTER: " + boss.CounterProtocol;
            integrityLabel.text = $"ADAPTATION STRENGTH  {boss.AdaptationStrength * 100f:F0}%";
        }
    }

    private void BuildLearningPanel(Transform parent)
    {
        GameObject root = CreateUIObject("BossLearningPanel", parent);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-22f, -22f);
        rect.sizeDelta = new Vector2(360f, 196f);
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.025f, 0.04f, 0.085f, 0.9f);
        bg.raycastTarget = false;

        titleText = CreateText(root.transform, "Title", new Vector2(14f, -12f), new Vector2(332f, 30f), 18f, TextAlignmentOptions.TopLeft);
        profileText = CreateText(root.transform, "Profile", new Vector2(14f, -48f), new Vector2(332f, 74f), 15f, TextAlignmentOptions.TopLeft);
        profileText.color = new Color(0.83f, 0.9f, 1f);

        integrityLabel = CreateText(root.transform, "IntegrityLabel", new Vector2(14f, -124f), new Vector2(332f, 22f), 13f, TextAlignmentOptions.TopLeft);
        integrityLabel.color = new Color(0.7f, 0.8f, 0.92f);

        GameObject barBg = CreateUIObject("IntegrityBar", root.transform);
        RectTransform barRect = barBg.GetComponent<RectTransform>();
        barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(14f, -148f);
        barRect.sizeDelta = new Vector2(332f, 10f);
        Image barBackground = barBg.AddComponent<Image>();
        barBackground.color = new Color(0.16f, 0.2f, 0.3f, 0.9f);
        barBackground.raycastTarget = false;

        GameObject fill = CreateUIObject("Fill", barBg.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = fillRect.offsetMax = Vector2.zero;
        integrityFill = fill.AddComponent<Image>();
        integrityFill.type = Image.Type.Filled;
        integrityFill.fillMethod = Image.FillMethod.Horizontal;
        integrityFill.fillOrigin = 0;
        integrityFill.raycastTarget = false;

        protocolText = CreateText(root.transform, "Protocol", new Vector2(14f, -166f), new Vector2(332f, 24f), 12f, TextAlignmentOptions.TopLeft);
        protocolText.color = new Color(1f, 0.72f, 0.25f);
    }

    private void BuildResultPanel(Transform parent)
    {
        resultRoot = CreateUIObject("BossResultPanel", parent);
        RectTransform rect = resultRoot.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        Image background = resultRoot.AddComponent<Image>();
        background.color = new Color(0.015f, 0.02f, 0.05f, 0.86f);

        resultText = CreateText(resultRoot.transform, "Result", Vector2.zero, new Vector2(780f, 260f), 31f, TextAlignmentOptions.Center);
        RectTransform textRect = resultText.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.anchoredPosition = Vector2.zero;
        resultRoot.SetActive(false);
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
        ShowResult("ADAPTIVE CORE SHUT DOWN\n\nYOU CORRUPTED THE PREDICTION LOOP\n\nPRESS ENTER TO RETRY", new Color(0.2f, 1f, 0.75f));
    }

    private void OnPlayerDefeated()
    {
        ShowResult("BEHAVIOR PREDICTED\n\nTHE CORE COMPLETED ITS COUNTER MODEL\n\nPRESS ENTER TO RETRY", new Color(1f, 0.25f, 0.42f));
    }

    private void ShowResult(string message, Color color)
    {
        if (resultShown || resultRoot == null) return;
        resultShown = true;
        resultRoot.SetActive(true);
        resultRoot.transform.SetAsLastSibling();
        resultText.text = message;
        resultText.color = color;
        Time.timeScale = 0f;
    }
}
