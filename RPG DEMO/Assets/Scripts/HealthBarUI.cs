using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Health 하나를 연결해두면 알아서 채워지는 HP 바.
/// 플레이어 / 보스 양쪽에 그대로 재사용합니다.
/// </summary>
public class HealthBarUI : MonoBehaviour
{
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
        displayed = target != null ? target.Normalized : 0f;
        Apply();
    }

    private void Start()
    {
        displayed = target != null ? target.Normalized : 0f;
        Apply();
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
