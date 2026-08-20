using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 일반 스테이지용 순차 웨이브 진행기입니다.
/// Stage01의 배치값은 코드 안에 두어, Stage02~04에서는 이 컴포넌트를 복제한 뒤
/// 웨이브 좌표/구성만 바꿔 사용할 수 있습니다.
/// </summary>
public sealed class StageArenaWaveController : MonoBehaviour
{
    [Serializable]
    private sealed class WaveDefinition
    {
        public Vector2[] spawnPoints;
    }

    [SerializeField] private GameObject gruntPrefab;
    [SerializeField] private Transform enemyContainer;
    [SerializeField, Min(0f)] private float nextWaveDelay = 1.25f;
    [SerializeField] private WaveDefinition[] waves = CreateDefaultWaves();

    public int CurrentWave { get; private set; }
    public bool IsCleared { get; private set; }

    private readonly List<GameObject> livingEnemies = new List<GameObject>();
    private bool waitingForWaveClear;

    private void Awake()
    {
        Debug.Log("[Stage01] WaveController Awake", this);

        // 수동 YAML 연결이나 씬 복제 과정에서 하위 배열이 누락된 경우에도 Stage01은 막히지 않습니다.
        if (!HasValidWaveData())
        {
            Debug.LogWarning("[Stage01] Wave data was invalid; restoring defaults.", this);
            waves = CreateDefaultWaves();
        }
    }

    private void Start()
    {
        Debug.Log("[Stage01] WaveController Start", this);

        if (gruntPrefab == null || waves == null || waves.Length == 0)
        {
            Debug.LogError("[Stage01] Wave start aborted: Grunt prefab or wave definitions are missing.", this);
            return;
        }

        if (enemyContainer == null)
        {
            Debug.LogError("[Stage01] Wave start aborted: Enemies container is missing.", this);
            return;
        }

        ClearScenePlacedEnemies();
        StartWave(0);
    }

    private void Update()
    {
        if (!waitingForWaveClear || IsCleared) return;

        livingEnemies.RemoveAll(enemy => enemy == null);
        if (livingEnemies.Count != 0) return;

        waitingForWaveClear = false;
        if (CurrentWave >= waves.Length)
        {
            IsCleared = true;
            Debug.Log("[StageArenaWaveController] Stage clear. Exit unlocked.");
            return;
        }

        StartCoroutine(BeginNextWaveAfterDelay());
    }

    private IEnumerator BeginNextWaveAfterDelay()
    {
        yield return new WaitForSeconds(nextWaveDelay);
        StartWave(CurrentWave);
    }

    private void StartWave(int waveIndex)
    {
        Debug.Log($"[Stage01] Start Wave {waveIndex + 1}", this);

        if (waves == null || waveIndex < 0 || waveIndex >= waves.Length)
        {
            Debug.LogError($"[Stage01] Wave start aborted: index {waveIndex} is outside the wave data.", this);
            enabled = false;
            return;
        }

        if (waves[waveIndex].spawnPoints == null || waves[waveIndex].spawnPoints.Length == 0)
        {
            Debug.LogError($"[Stage01] Wave {waveIndex + 1} has no spawn points.", this);
            enabled = false;
            return;
        }

        CurrentWave = waveIndex + 1;
        Debug.Log($"[Stage01] Spawn count = {waves[waveIndex].spawnPoints.Length}", this);
        foreach (Vector2 spawnPoint in waves[waveIndex].spawnPoints)
        {
            Debug.Log($"[Stage01] Instantiate Grunt at ({spawnPoint.x}, {spawnPoint.y})", this);

            GameObject enemy;
            try
            {
                enemy = Instantiate(gruntPrefab, spawnPoint, Quaternion.identity, enemyContainer);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
                return;
            }

            enemy.name = $"Wave_{CurrentWave}_Grunt";
            livingEnemies.Add(enemy);
        }

        waitingForWaveClear = true;
        Debug.Log($"[StageArenaWaveController] Wave {CurrentWave}/{waves.Length} started.");
    }

    private void ClearScenePlacedEnemies()
    {
        if (enemyContainer == null) return;

        // 문서 확정 전 Stage01에 남아 있던 고정 배치 적은 웨이브 시작 전에 제거합니다.
        for (int i = enemyContainer.childCount - 1; i >= 0; i--)
            Destroy(enemyContainer.GetChild(i).gameObject);
    }

    private bool HasValidWaveData()
    {
        if (waves == null || waves.Length == 0) return false;
        for (int i = 0; i < waves.Length; i++)
        {
            if (waves[i] == null || waves[i].spawnPoints == null || waves[i].spawnPoints.Length == 0)
                return false;
        }

        return true;
    }

    private static WaveDefinition[] CreateDefaultWaves()
    {
        return new[]
        {
            new WaveDefinition { spawnPoints = new[] { new Vector2(-8.5f, -2.0f), new Vector2(8.5f, -2.0f) } },
            new WaveDefinition { spawnPoints = new[] { new Vector2(-7.5f, 2.85f), new Vector2(0f, 0.6f), new Vector2(9.5f, -2.0f) } },
            new WaveDefinition
            {
                spawnPoints = new[]
                {
                    new Vector2(-10.5f, -2.0f), new Vector2(10.5f, -2.0f),
                    new Vector2(-7.2f, 2.85f), new Vector2(7.2f, 2.85f)
                }
            }
        };
    }
}
