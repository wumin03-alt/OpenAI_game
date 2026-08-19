using UnityEngine;
using UnityEngine.SceneManagement;
using Game.SceneManagement;
using Game.Core;
using Game.Save;
using Game.UI;
using Game.Audio;
using UnityEngine.UI;

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
    [SerializeField] private bool requireAllEnemiesDead = true;

    [Header("── 연출 (선택) ──")]
    [Tooltip("통과 시 켤 오브젝트. 예: BOSS AHEAD 텍스트")]
    [SerializeField] private GameObject enterMessage;
    [Tooltip("조건 미달일 때 켤 오브젝트")]
    [SerializeField] private GameObject blockedMessage;

    [Header("── 클리어 안내 ──")]
    [SerializeField] private string clearGuideMessage = "오른쪽으로 이동하여 다음 스테이지로 이동하시오.";
    [SerializeField, Min(0.05f)] private float enemyCheckInterval = 0.25f;

    private bool triggered;
    private bool stageCleared;
    private float nextEnemyCheckTime;

    private void Start()
    {
        if (enterMessage != null) enterMessage.SetActive(false);

        stageCleared = !requireAllEnemiesDead;
        if (stageCleared)
            ShowClearGuide();
        else
            CheckForStageClear();
    }

    private void Update()
    {
        if (!requireAllEnemiesDead || stageCleared || triggered) return;
        if (Time.unscaledTime < nextEnemyCheckTime) return;

        nextEnemyCheckTime = Time.unscaledTime + enemyCheckInterval;
        CheckForStageClear();
    }

    private void CheckForStageClear()
    {
        if (GameObject.FindGameObjectWithTag("Enemy") != null) return;

        stageCleared = true;
        ShowClearGuide();
        Debug.Log("[StageExit] 스테이지 클리어. 출구가 열렸습니다.");
    }

    private void ShowClearGuide()
    {
        if (enterMessage != null)
        {
            enterMessage.SetActive(true);
            return;
        }

        Canvas canvas = RuntimeUIFactory.CreateCanvas("StageClearGuideCanvas", null, 250);
        Image panel = RuntimeUIFactory.CreateImage(canvas.transform, "GuidePanel",
            new Color(0.02f, 0.04f, 0.08f, 0.88f));
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -70f);
        panelRect.sizeDelta = new Vector2(1100f, 72f);

        Text guide = RuntimeUIFactory.CreateText(panelRect, clearGuideMessage, 30,
            Vector2.zero, panelRect.sizeDelta, new Color(0.76f, 0.9f, 1f));
        guide.fontStyle = FontStyle.Bold;
        RuntimeUIFactory.Stretch(guide.rectTransform);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        if (requireAllEnemiesDead && !stageCleared)
        {
            CheckForStageClear();
        }

        if (requireAllEnemiesDead && !stageCleared)
        {
            Debug.Log("[StageExit] 남은 적이 있습니다.");
            if (blockedMessage != null) blockedMessage.SetActive(true);
            return;
        }

        triggered = true;
        Debug.Log($"[StageExit] {nextSceneName} 으로 이동합니다.");

        FreezePlayerForTransition(other);

        if (blockedMessage != null) blockedMessage.SetActive(false);
        if (enterMessage != null) enterMessage.SetActive(true);

        // 공통 AudioManager는 씬이 바뀌어도 유지되므로 징글의 끝부분도 자연스럽게 이어집니다.
        AudioManager.Instance?.PlayStageTransition();

        Invoke(nameof(LoadNext), delay);
    }

    private static void FreezePlayerForTransition(Collider2D playerCollider)
    {
        PlayerController controller = playerCollider.GetComponentInParent<PlayerController>();
        if (controller != null)
            controller.enabled = false;

        Rigidbody2D body = playerCollider.attachedRigidbody;
        if (body == null)
            body = playerCollider.GetComponentInParent<Rigidbody2D>();
        if (body == null) return;

        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
    }

    private void LoadNext()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("[StageExit] nextSceneName이 비어 있습니다.");
            return;
        }

        if (GameSession.Instance != null && SaveManager.Instance != null)
            SaveManager.Instance.UnlockStage(GameSession.Instance.CurrentStage + 1);

        // 정식 실행에서는 Bootstrap의 공통 로더를 사용합니다.
        // Stage01을 에디터에서 단독 실행할 때는 기존 직접 로딩으로 대체합니다.
        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(nextSceneName);
        else
            SceneManager.LoadScene(nextSceneName);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.35f);
        Gizmos.DrawCube(transform.position, new Vector3(1.5f, 6f, 1f));
    }
}
