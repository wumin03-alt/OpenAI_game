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

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        if (requireAllEnemiesDead && GameObject.FindGameObjectWithTag("Enemy") != null)
        {
            Debug.Log("[StageExit] 남은 적이 있습니다.");
            if (blockedMessage != null) blockedMessage.SetActive(true);
            return;
        }

        triggered = true;
        Debug.Log($"[StageExit] {nextSceneName} 으로 이동합니다.");

        if (blockedMessage != null) blockedMessage.SetActive(false);
        if (enterMessage != null) enterMessage.SetActive(true);

        Invoke(nameof(LoadNext), delay);
    }

    private void LoadNext()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogError("[StageExit] nextSceneName이 비어 있습니다.");
            return;
        }
        SceneManager.LoadScene(nextSceneName);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 1f, 0.4f, 0.35f);
        Gizmos.DrawCube(transform.position, new Vector3(1.5f, 6f, 1f));
    }
}