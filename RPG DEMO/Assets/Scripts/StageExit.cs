using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 스테이지 끝 트리거. 플레이어가 닿으면 다음 씬(보스 아레나)으로 이동합니다.
/// GameManager와 분리된 독립 스크립트입니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class StageExit : MonoBehaviour
{
    [Header("── 이동할 씬 ──")]
    [Tooltip("Build Profiles에 등록된 씬 이름 (예: BossArena)")]
    [SerializeField] private string nextSceneName = "BossArena";
    [Tooltip("트리거 후 씬 전환까지 대기 시간")]
    [SerializeField] private float delay = 0.4f;

    [Header("── 진입 조건 ──")]
    [Tooltip("체크하면 Tag가 Enemy인 오브젝트가 모두 사라져야 통과 가능")]
    [SerializeField] private bool requireAllEnemiesDead = false;

    [Header("── 연출 (선택) ──")]
    [Tooltip("통과 시 켤 오브젝트. 예: BOSS AHEAD 텍스트")]
    [SerializeField] private GameObject enterMessage;
    [Tooltip("조건 미달일 때 켤 오브젝트")]
    [SerializeField] private GameObject blockedMessage;

    private bool triggered;

    private void Awake()
    {
        // Stage01의 실제 Canvas/HP Bar를 이후 씬에도 그대로 유지합니다.
        if (SceneManager.GetActiveScene().name != "Stage01") return;

        Canvas stageCanvas = Object.FindFirstObjectByType<Canvas>();
        if (stageCanvas == null) return;

        PersistentPlayerHUD hud = stageCanvas.GetComponent<PersistentPlayerHUD>();
        if (hud == null) hud = stageCanvas.gameObject.AddComponent<PersistentPlayerHUD>();
        hud.Initialize();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        // 기존 Stage01은 이 스크립트가 이미 배치된 튜토리얼 씬이므로,
        // 별도 Inspector 작업 없이 적 처치 후 다음 스테이지로 진행하게 합니다.
        bool needsEnemiesCleared = requireAllEnemiesDead ||
                                  SceneManager.GetActiveScene().name == "Stage01";
        if (needsEnemiesCleared && GameObject.FindGameObjectWithTag("Enemy") != null)
        {
            Debug.Log("[StageExit] 남은 적이 있습니다.");
            if (blockedMessage != null) blockedMessage.SetActive(true);
            return;
        }

        triggered = true;
        Debug.Log($"[StageExit] {GetNextSceneName()} 으로 이동합니다.");

        if (blockedMessage != null) blockedMessage.SetActive(false);
        if (enterMessage != null) enterMessage.SetActive(true);

        Invoke(nameof(LoadNext), delay);
    }

    private void LoadNext()
    {
        string sceneToLoad = GetNextSceneName();
        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogError("[StageExit] nextSceneName이 비어 있습니다.");
            return;
        }
        SceneManager.LoadScene(sceneToLoad);
    }

    // Stage01은 기존 씬 이름을 유지하면서 신규 스테이지 흐름만 연결합니다.
    private string GetNextSceneName()
    {
        return SceneManager.GetActiveScene().name == "Stage01"
            ? "Stage_02_Combat"
            : nextSceneName;
    }

    private void OnGUI()
    {
        if (SceneManager.GetActiveScene().name != "Stage01") return;

        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        GUIStyle body = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            normal = { textColor = new Color(0.82f, 0.9f, 1f, 1f) }
        };

        GUI.Label(new Rect(500f, 24f, 600f, 30f), "STAGE 01 — TUTORIAL", title);
        GUI.Label(new Rect(500f, 57f, 900f, 26f),
                  "← → Move   ↑ Jump   R Dash   Q Melee   W Ranged   E Parry", body);
        GUI.Label(new Rect(500f, 83f, 900f, 26f),
                  "Defeat the enemies, then reach the exit on the right.", body);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.35f);
        Gizmos.DrawCube(transform.position, new Vector3(1.5f, 6f, 1f));
    }
}
