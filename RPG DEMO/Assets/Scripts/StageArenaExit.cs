using System.Collections;
using Game.Core;
using Game.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 일반 스테이지 웨이브 완료 후에만 열리는 보스전 출구입니다.
/// 씬 단독 실행에서는 SceneLoader가 없을 수 있으므로 기존 StageExit과 동일하게 직접 로딩을 보조로 사용합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public sealed class StageArenaExit : MonoBehaviour
{
    [SerializeField] private StageArenaWaveController waveController;
    [SerializeField] private string nextSceneName = "BossArena";
    [SerializeField, Min(0f)] private float delay = 0.35f;
    [SerializeField] private SpriteRenderer gateRenderer;
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.08f, 0.12f, 1f);
    [SerializeField] private Color unlockedColor = new Color(0.2f, 0.95f, 0.65f, 1f);

    public bool IsUnlocked { get; private set; }

    private bool isTransitioning;
    private bool rewardRequested;

    private void Awake()
    {
        SetGateVisual(false);
        if (waveController != null)
            waveController.OnStageCleared += BeginStageReward;
    }

    private void Start()
    {
        if (waveController != null && waveController.IsCleared)
            BeginStageReward();
    }

    private void Update()
    {
        if (!IsUnlocked && waveController != null && waveController.IsCleared)
            BeginStageReward();
    }

    private void BeginStageReward()
    {
        if (rewardRequested || IsUnlocked) return;
        rewardRequested = true;

        string sceneName = SceneManager.GetActiveScene().name;
        if (GameSession.Instance != null && GameSession.Instance.IsStageRewardResolved(sceneName))
        {
            Unlock();
            return;
        }

        Vector3 chestPosition = transform.position + new Vector3(-2.2f, 0.7f, 0f);
        StageRewardChest.Spawn(chestPosition, Unlock);
    }

    public void Unlock()
    {
        if (IsUnlocked) return;
        IsUnlocked = true;
        SetGateVisual(true);
        Debug.Log("[StageArenaExit] Exit unlocked.");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTransitioning || !IsUnlocked || !other.CompareTag("Player")) return;
        StartCoroutine(LoadBossArena());
    }

    private IEnumerator LoadBossArena()
    {
        isTransitioning = true;
        yield return new WaitForSeconds(delay);

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.LoadScene(nextSceneName);
        else
            SceneManager.LoadScene(nextSceneName);
    }

    private void SetGateVisual(bool unlocked)
    {
        if (gateRenderer != null)
            gateRenderer.color = unlocked ? unlockedColor : lockedColor;
    }

    private void OnDestroy()
    {
        if (waveController != null)
            waveController.OnStageCleared -= BeginStageReward;
    }
}
