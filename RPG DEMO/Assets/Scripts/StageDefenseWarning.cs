using TMPro;
using UnityEngine;

/// <summary>방어 대상 체력이 임계치에 도달했을 때만 최소 경고를 표시합니다.</summary>
[RequireComponent(typeof(Health))]
public sealed class StageDefenseWarning : MonoBehaviour
{
    [SerializeField] private Canvas stageCanvas;
    [SerializeField, Range(0.01f, 1f)] private float warningThreshold = 0.3f;

    private Health defenseHealth;
    private GameObject warningRoot;
    private CanvasGroup warningGroup;

    private void Awake()
    {
        defenseHealth = GetComponent<Health>();
        CreateWarning();
    }

    private void Update()
    {
        if (defenseHealth == null || warningRoot == null) return;

        bool isCritical = !defenseHealth.IsDead && defenseHealth.Normalized <= warningThreshold;
        warningRoot.SetActive(isCritical);
        if (isCritical)
            warningGroup.alpha = 0.65f + Mathf.PingPong(Time.unscaledTime * 2.5f, 0.35f);
    }

    private void CreateWarning()
    {
        Canvas canvas = stageCanvas != null ? stageCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[StageDefenseWarning] Canvas is missing.", this);
            enabled = false;
            return;
        }

        warningRoot = new GameObject("DefenseCriticalWarning", typeof(RectTransform), typeof(CanvasGroup));
        warningRoot.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = warningRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 1f);
        rootRect.anchoredPosition = new Vector2(0f, -120f);
        rootRect.sizeDelta = new Vector2(780f, 56f);

        warningGroup = warningRoot.GetComponent<CanvasGroup>();
        warningGroup.blocksRaycasts = false;
        warningGroup.interactable = false;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(warningRoot.transform, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text template = FindFirstObjectByType<TMP_Text>();
        text.font = template != null ? template.font : TMP_Settings.defaultFontAsset;
        text.text = "DEFENSE CORE CRITICAL";
        text.fontSize = 34f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(1f, 0.2f, 0.3f, 1f);
        text.outlineColor = new Color(0.08f, 0.01f, 0.03f, 0.95f);
        text.outlineWidth = 0.2f;
        text.raycastTarget = false;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
        warningRoot.SetActive(false);
    }
}
