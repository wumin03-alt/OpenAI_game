using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage01~03, Stage05~07에서 현재 스테이지 번호를 화면 상단 중앙에 항상 표시합니다.
/// 씬 파일 번호(Stage05)가 아니라 실제 진행 순서(STAGE 4)를 보여 줍니다.
/// 씬 데이터를 건드리지 않도록 BossStageProgression과 같은 방식으로 런타임에 자동 설치됩니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageIndicatorUI : MonoBehaviour
{
    [Header("── 연결 ──")]
    [SerializeField] private Canvas stageCanvas;

    [Header("── 표시 ──")]
    [Tooltip("0이면 씬 이름에서 진행 번호를 자동으로 계산합니다.")]
    [SerializeField, Min(0)] private int displayNumberOverride;

    private TextMeshProUGUI label;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneLoadInstaller()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid() || GetProgressNumber(scene.name) == 0) return;

        // 씬에 이미 수동 배치된 인디케이터가 있으면 그쪽 설정을 존중합니다.
        Canvas stageCanvas = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.GetComponentInChildren<StageIndicatorUI>(true) != null) return;
            if (stageCanvas == null) stageCanvas = root.GetComponentInChildren<Canvas>(true);
        }

        if (stageCanvas == null)
        {
            Debug.LogWarning($"[StageIndicatorUI] {scene.name}에 Canvas가 없어 스테이지 표시를 건너뜁니다.");
            return;
        }

        stageCanvas.gameObject.AddComponent<StageIndicatorUI>();
    }

    /// <summary>
    /// 씬 이름을 일반 스테이지 진행 번호로 바꿉니다.
    /// 중간보스(Stage04 자리)와 최종보스는 카운트하지 않으므로 Stage05~07이 4~6이 됩니다.
    /// </summary>
    internal static int GetProgressNumber(string sceneName)
    {
        switch (StageArtDirector.ParseStageNumber(sceneName))
        {
            case 1: return 1;
            case 2: return 2;
            case 3: return 3;
            case 5: return 4;
            case 6: return 5;
            case 7: return 6;
            default: return 0;
        }
    }

    private void Awake()
    {
        int displayNumber = displayNumberOverride > 0
            ? displayNumberOverride
            : GetProgressNumber(gameObject.scene.name);

        if (displayNumber == 0)
        {
            enabled = false;
            return;
        }

        CreateLabel();
        if (label != null) label.text = $"STAGE {displayNumber}";
    }

    private void CreateLabel()
    {
        Canvas canvas = stageCanvas != null ? stageCanvas : GetComponent<Canvas>();
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[StageIndicatorUI] Canvas가 없어 스테이지 표시를 만들 수 없습니다.", this);
            enabled = false;
            return;
        }

        GameObject labelObject = new GameObject("StageIndicator",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(canvas.transform, false);

        label = labelObject.GetComponent<TextMeshProUGUI>();
        // 기존 HUD와 같은 폰트를 그대로 쓰도록 씬에 있는 TMP 텍스트를 템플릿으로 삼습니다.
        TMP_Text template = FindFirstObjectByType<TMP_Text>();
        label.font = template != null ? template.font : TMP_Settings.defaultFontAsset;
        label.fontSize = 34f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.characterSpacing = 5f;
        label.color = new Color(0.91f, 0.95f, 1f, 1f);
        label.outlineColor = new Color(0.02f, 0.05f, 0.1f, 0.95f);
        label.outlineWidth = 0.18f;
        label.raycastTarget = false;

        // 상단 중앙 고정: HP바(좌상단), 스피드런 HUD(우상단), 방어 경고문(y -120)과 겹치지 않습니다.
        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0.5f, 1f);
        labelRect.pivot = new Vector2(0.5f, 1f);
        labelRect.anchoredPosition = new Vector2(0f, -24f);
        labelRect.sizeDelta = new Vector2(360f, 52f);
    }
}
