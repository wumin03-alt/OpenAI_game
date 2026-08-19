using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Health 하나를 연결해두면 알아서 채워지는 HP 바.
/// 플레이어 / 보스 양쪽에 그대로 재사용합니다.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.025f, 0.045f, 0.075f, 0.94f);
    private static readonly Color PlayerHighColor = new Color(0.12f, 0.86f, 0.76f, 1f);
    private static readonly Color PlayerLowColor = new Color(1f, 0.25f, 0.4f, 1f);
    private static readonly Color BossHighColor = new Color(1f, 0.48f, 0.16f, 1f);
    private static readonly Color BossLowColor = new Color(0.88f, 0.07f, 0.25f, 1f);
    private static readonly Color PrimaryTextColor = new Color(0.91f, 0.96f, 1f, 1f);
    private static readonly Color BossTitleColor = new Color(1f, 0.72f, 0.32f, 1f);
    private static readonly Color PlayerAccentColor = new Color(0.2f, 0.82f, 0.95f, 0.9f);
    private static readonly Color BossAccentColor = new Color(1f, 0.32f, 0.2f, 0.9f);

    [Header("── 연결 ──")]
    [Tooltip("표시할 대상의 Health 컴포넌트")]
    [SerializeField] private Health target;
    [Tooltip("Image Type = Filled 로 설정된 Fill 이미지")]
    [SerializeField] private Image fillImage;
    [Tooltip("선택 사항. 숫자 표시용 텍스트")]
    [SerializeField] private TMP_Text label;

    [Header("── 연출 ──")]
    [Tooltip("0이면 즉시 반영, 크면 부드럽게 줄어듦")]
    [SerializeField] private float lerpSpeed = 8f;
    [SerializeField] private bool colorByAmount = true;
    [SerializeField] private Color highColor = new Color(0.3f, 0.9f, 0.4f);
    [SerializeField] private Color lowColor = new Color(0.9f, 0.25f, 0.25f);

    private float displayed = 1f;

    /// <summary>보스처럼 런타임에 생성/연결되는 대상용</summary>
    public void SetTarget(Health newTarget)
    {
        target = newTarget;
        ApplyVisualStyle();
        displayed = target != null ? target.Normalized : 0f;
        Apply();
    }

    private void Start()
    {
        ApplyVisualStyle();
        displayed = target != null ? target.Normalized : 0f;
        Apply();
    }

    private void ApplyVisualStyle()
    {
        bool isBoss = target != null && target.CompareTag("Boss");
        highColor = isBoss ? BossHighColor : PlayerHighColor;
        lowColor = isBoss ? BossLowColor : PlayerLowColor;

        Image background = GetComponent<Image>();
        if (background != null)
        {
            background.color = PanelColor;
            background.raycastTarget = false;
        }

        if (fillImage != null)
            fillImage.raycastTarget = false;

        Outline border = GetComponent<Outline>();
        if (border == null)
            border = gameObject.AddComponent<Outline>();
        border.effectColor = isBoss ? BossAccentColor : PlayerAccentColor;
        border.effectDistance = new Vector2(2f, -2f);
        border.useGraphicAlpha = true;

        RectTransform barRect = transform as RectTransform;
        if (barRect != null)
        {
            Vector2 size = barRect.sizeDelta;
            size.y = isBoss ? Mathf.Max(size.y, 34f) : Mathf.Max(size.y, 28f);
            if (!isBoss)
                size.x = Mathf.Max(size.x, 260f);
            barRect.sizeDelta = size;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            bool isValueLabel = text == label;
            text.color = !isValueLabel && isBoss ? BossTitleColor : PrimaryTextColor;
            text.fontStyle |= FontStyles.Bold;
            text.outlineColor = new Color32(4, 10, 20, 235);
            text.outlineWidth = isValueLabel ? 0.14f : 0.2f;
            text.raycastTarget = false;

            if (isValueLabel)
                text.fontSize = Mathf.Max(text.fontSize, isBoss ? 20f : 18f);
            else if (isBoss)
            {
                text.fontSize = Mathf.Max(text.fontSize, 26f);
                text.characterSpacing = 2f;
            }
        }
    }

    private void Update()
    {
        if (target == null || fillImage == null) return;

        float goal = target.Normalized;
        displayed = lerpSpeed <= 0f
            ? goal
            : Mathf.MoveTowards(displayed, goal, Time.unscaledDeltaTime * lerpSpeed);

        Apply();
    }

    private void Apply()
    {
        if (fillImage == null) return;

        fillImage.fillAmount = displayed;

        if (colorByAmount)
            fillImage.color = Color.Lerp(lowColor, highColor, displayed);

        if (label != null && target != null)
            label.text = $"{Mathf.CeilToInt(target.CurrentHP)} / {Mathf.CeilToInt(target.MaxHP)}";
    }
}
