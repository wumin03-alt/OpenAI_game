using System;
using System.Collections;
using System.Collections.Generic;
using Game.Core;
using Game.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 일반 스테이지용 순차 웨이브 진행기입니다.
/// Stage01의 배치값은 코드 안에 두어, Stage02~04에서는 이 컴포넌트를 복제한 뒤
/// 웨이브 좌표/구성만 바꿔 사용할 수 있습니다.
/// </summary>
public sealed class StageArenaWaveController : MonoBehaviour
{
    private enum EnemyType
    {
        Grunt,
        Ranged
    }

    [Serializable]
    private sealed class SpawnDefinition
    {
        public EnemyType enemyType;
        public Vector2 spawnPoint;
        [Min(0.01f)] public float healthMultiplier = 1f;
        [Min(0f)] public float movementSpeedMultiplier = 1f;
    }

    [Serializable]
    private sealed class WaveDefinition
    {
        public SpawnDefinition[] spawns;
    }

    [SerializeField] private GameObject gruntPrefab;
    [SerializeField] private GameObject rangedPrefab;
    [SerializeField] private Transform enemyContainer;
    [SerializeField, Min(0f)] private float nextWaveDelay = 1.25f;
    [SerializeField] private WaveDefinition[] waves = CreateDefaultWaves();
    [Header("── 방어 스테이지 (선택) ──")]
    [SerializeField] private Health defenseTarget;
    [SerializeField, Min(1f)] private float defenseDuration;
    [SerializeField] private Canvas stageCanvas;
    [SerializeField] private TMP_FontAsset defenseHudFont;
    [Header("── 스피드런 스테이지 (선택) ──")]
    [SerializeField] private bool speedrunMode;
    [SerializeField, Min(1f)] private float speedrunTimeLimit = 60f;
    [SerializeField, Min(1)] private int speedrunKillGoal = 15;
    [SerializeField, Min(0.05f)] private float speedrunSpawnInterval = 1.4f;
    [SerializeField, Min(1)] private int speedrunMaxConcurrentEnemies = 6;
    [SerializeField, Min(1f)] private float speedrunEnemyMaxHP = 20f;
    [SerializeField] private Vector2[] speedrunSpawnPoints;
    [SerializeField] private TMP_FontAsset speedrunHudFont;
    [SerializeField, Min(0f)] private float speedrunFailureReloadDelay = 1.5f;

    public int CurrentWave { get; private set; }
    public bool IsCleared { get; private set; }

    /// <summary>정의된 총 웨이브 수입니다. 안내문이 마지막 웨이브를 판별하는 데만 사용합니다.</summary>
    public int TotalWaves => waves != null ? waves.Length : 0;
    public event Action OnWaveCleared;
    public event Action OnStageCleared;

    private readonly List<GameObject> livingEnemies = new List<GameObject>();
    private bool waitingForWaveClear;
    private bool defenseMode;
    private bool defenseFailed;
    private PlayerController defensePlayer;
    private Health playerHealth;
    private GameObject failureRoot;
    private TextMeshProUGUI failureText;
    private float speedrunTimeRemaining;
    private float nextSpeedrunSpawnTime;
    private int speedrunKills;
    private int speedrunSpawnIndex;
    private int lastSpeedrunSpawnPointIndex = -1;
    private bool speedrunFailed;
    private TextMeshProUGUI speedrunKillText;
    private TextMeshProUGUI speedrunTimeText;

    private void Awake()
    {
        Debug.Log("[Stage01] WaveController Awake", this);

        // 수동 YAML 연결이나 씬 복제 과정에서 하위 배열이 누락된 경우에도 Stage01은 막히지 않습니다.
        if (!speedrunMode && !HasValidWaveData())
        {
            Debug.LogWarning("[Stage01] Wave data was invalid; restoring defaults.", this);
            waves = CreateDefaultWaves();
        }
    }

    private void Start()
    {
        Debug.Log("[Stage01] WaveController Start", this);

        if (speedrunMode)
        {
            StartSpeedrun();
            return;
        }

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
        defenseMode = defenseTarget != null && defenseDuration > 0f;
        if (defenseMode)
        {
            InitializeDefenseFailureHandling();
            StartCoroutine(RunDefenseWaves());
        }
        else
            StartWave(0);
    }

    private void Update()
    {
        if (speedrunMode)
        {
            UpdateSpeedrun();
            return;
        }

        if (defenseMode)
        {
            if (!IsCleared && !defenseFailed && defenseTarget != null && defenseTarget.IsDead)
                FailDefense();
            return;
        }

        if (!waitingForWaveClear || IsCleared) return;

        livingEnemies.RemoveAll(enemy => enemy == null);
        if (livingEnemies.Count != 0) return;

        waitingForWaveClear = false;
        OnWaveCleared?.Invoke();
        if (CurrentWave >= waves.Length)
        {
            IsCleared = true;
            OnStageCleared?.Invoke();
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

    private void StartSpeedrun()
    {
        if (gruntPrefab == null || enemyContainer == null || speedrunSpawnPoints == null || speedrunSpawnPoints.Length == 0)
        {
            Debug.LogError("[StageArenaWaveController] Speedrun start aborted: Grunt prefab, enemy container, or spawn points are missing.", this);
            enabled = false;
            return;
        }

        ClearScenePlacedEnemies();
        CurrentWave = 1;
        speedrunTimeRemaining = speedrunTimeLimit;
        nextSpeedrunSpawnTime = Time.time;
        CreateSpeedrunHud();
        UpdateSpeedrunHud();
    }

    private void UpdateSpeedrun()
    {
        if (IsCleared || speedrunFailed) return;

        livingEnemies.RemoveAll(enemy => enemy == null);
        speedrunTimeRemaining -= Time.deltaTime;
        if (speedrunTimeRemaining <= 0f)
        {
            FailSpeedrun();
            return;
        }

        if (Time.time >= nextSpeedrunSpawnTime && livingEnemies.Count < speedrunMaxConcurrentEnemies)
        {
            SpawnSpeedrunEnemy();
            nextSpeedrunSpawnTime = Time.time + speedrunSpawnInterval;
        }

        UpdateSpeedrunHud();
    }

    private void SpawnSpeedrunEnemy()
    {
        int spawnPointIndex = ChooseSpeedrunSpawnPoint();
        Vector2 spawnPoint = speedrunSpawnPoints[spawnPointIndex];
        speedrunSpawnIndex++;

        GameObject enemy = Instantiate(gruntPrefab, spawnPoint, Quaternion.identity, enemyContainer);
        enemy.name = $"Speedrun_Grunt_{speedrunSpawnIndex}";

        Health health = enemy.GetComponent<Health>();
        if (health != null)
        {
            float healthMultiplier = speedrunEnemyMaxHP / Mathf.Max(0.01f, health.MaxHP);
            health.ApplyMaxHPMultiplier(healthMultiplier);
            health.onDeath.AddListener(HandleSpeedrunEnemyDefeated);
        }

        livingEnemies.Add(enemy);
    }

    private int ChooseSpeedrunSpawnPoint()
    {
        if (speedrunSpawnPoints.Length == 1) return 0;

        if (lastSpeedrunSpawnPointIndex < 0)
        {
            lastSpeedrunSpawnPointIndex = UnityEngine.Random.Range(0, speedrunSpawnPoints.Length);
            return lastSpeedrunSpawnPointIndex;
        }

        int pointIndex = UnityEngine.Random.Range(0, speedrunSpawnPoints.Length - 1);
        if (pointIndex >= lastSpeedrunSpawnPointIndex) pointIndex++;
        lastSpeedrunSpawnPointIndex = pointIndex;
        return pointIndex;
    }

    private void HandleSpeedrunEnemyDefeated()
    {
        if (IsCleared || speedrunFailed || speedrunKills >= speedrunKillGoal) return;

        speedrunKills = Mathf.Min(speedrunKills + 1, speedrunKillGoal);
        if (speedrunKills < speedrunKillGoal)
        {
            UpdateSpeedrunHud();
            return;
        }

        IsCleared = true;
        OnWaveCleared?.Invoke();
        OnStageCleared?.Invoke();
        UpdateSpeedrunHud();
        Debug.Log("[StageArenaWaveController] Speedrun stage clear. Exit unlocked.");
    }

    private void FailSpeedrun()
    {
        if (speedrunFailed) return;

        speedrunFailed = true;
        FreezeSpeedrunEnemies();
        UpdateSpeedrunHud();
        StartCoroutine(ReloadSpeedrunAfterDelay());
    }

    private void FreezeSpeedrunEnemies()
    {
        foreach (Transform enemyTransform in enemyContainer)
        {
            EnemyController controller = enemyTransform.GetComponent<EnemyController>();
            if (controller != null) controller.enabled = false;
            FreezeBody(enemyTransform.GetComponent<Rigidbody2D>());
        }
    }

    private IEnumerator ReloadSpeedrunAfterDelay()
    {
        yield return new WaitForSecondsRealtime(speedrunFailureReloadDelay);

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.ReloadCurrentScene();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void UpdateSpeedrunHud()
    {
        if (speedrunKillText == null || speedrunTimeText == null) return;

        if (IsCleared)
        {
            speedrunKillText.text = $"처치수 {speedrunKillGoal}/{speedrunKillGoal}";
            speedrunTimeText.text = "목표 달성! 출구로 이동";
        }
        else if (speedrunFailed)
        {
            speedrunKillText.text = "실패! 재도전...";
            speedrunTimeText.text = string.Empty;
        }
        else
        {
            speedrunKillText.text = $"처치수 {speedrunKills}/{speedrunKillGoal}";
            speedrunTimeText.text = $"남은 시간 {Mathf.CeilToInt(speedrunTimeRemaining)}초";
        }
    }

    private void CreateSpeedrunHud()
    {
        if (speedrunKillText != null) return;

        Canvas canvas = stageCanvas != null ? stageCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null || speedrunHudFont == null)
        {
            Debug.LogError("[StageArenaWaveController] Speedrun HUD cannot initialize: Canvas or Korean TMP font is missing.", this);
            return;
        }

        GameObject panelObject = new GameObject("Stage04SpeedrunHUD", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        panelObject.transform.SetParent(canvas.transform, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(1f, 1f);
        panelRect.pivot = new Vector2(1f, 1f);
        panelRect.anchoredPosition = new Vector2(-40f, -40f);
        panelRect.sizeDelta = new Vector2(360f, 104f);

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.051f, 0.09f, 0.188f, 0.9f);
        panelImage.raycastTarget = false;

        speedrunKillText = CreateSpeedrunHudText(panelObject.transform, "KillText", new Vector2(18f, -14f),
            new Color(0.22f, 0.95f, 0.68f, 1f));
        speedrunTimeText = CreateSpeedrunHudText(panelObject.transform, "TimeText", new Vector2(18f, -59f),
            new Color(0.91f, 0.95f, 1f, 1f));
    }

    private TextMeshProUGUI CreateSpeedrunHudText(Transform parent, string objectName, Vector2 anchoredPosition, Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = speedrunHudFont;
        text.fontSize = 25f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Left;
        text.color = color;
        text.raycastTarget = false;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 1f);
        textRect.pivot = new Vector2(0f, 1f);
        textRect.anchoredPosition = anchoredPosition;
        textRect.sizeDelta = new Vector2(324f, 34f);
        return text;
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

        if (waves[waveIndex].spawns == null || waves[waveIndex].spawns.Length == 0)
        {
            Debug.LogError($"[Stage01] Wave {waveIndex + 1} has no spawn points.", this);
            enabled = false;
            return;
        }

        CurrentWave = waveIndex + 1;
        Debug.Log($"[Stage01] Spawn count = {waves[waveIndex].spawns.Length}", this);
        foreach (SpawnDefinition spawn in waves[waveIndex].spawns)
        {
            GameObject prefab = GetPrefab(spawn.enemyType);
            if (prefab == null)
            {
                Debug.LogError($"[Stage01] Wave {CurrentWave} cannot spawn {spawn.enemyType}: prefab is missing.", this);
                enabled = false;
                return;
            }

            Debug.Log($"[Stage01] Instantiate {spawn.enemyType} at ({spawn.spawnPoint.x}, {spawn.spawnPoint.y})", this);

            GameObject enemy;
            try
            {
                enemy = Instantiate(prefab, spawn.spawnPoint, Quaternion.identity, enemyContainer);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                enabled = false;
                return;
            }

            enemy.name = $"Wave_{CurrentWave}_{spawn.enemyType}";
            ApplySpawnStats(enemy, spawn);
            livingEnemies.Add(enemy);
        }

        waitingForWaveClear = !defenseMode;
        Debug.Log($"[StageArenaWaveController] Wave {CurrentWave}/{waves.Length} started.");
    }

    private IEnumerator RunDefenseWaves()
    {
        float waveDuration = defenseDuration / waves.Length;
        for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
        {
            if (defenseFailed) yield break;

            StartWave(waveIndex);
            float elapsed = 0f;
            while (elapsed < waveDuration)
            {
                if (defenseTarget == null || defenseTarget.IsDead)
                {
                    FailDefense();
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            OnWaveCleared?.Invoke();
        }

        if (defenseTarget == null || defenseTarget.IsDead)
        {
            FailDefense();
            yield break;
        }

        IsCleared = true;
        OnStageCleared?.Invoke();
        Debug.Log("[StageArenaWaveController] Defense stage clear. Exit unlocked.");
    }

    private void FailDefense()
    {
        if (defenseFailed) return;
        defenseFailed = true;
        waitingForWaveClear = false;
        Debug.Log("[StageArenaWaveController] Defense failed. Restart countdown started.");
        FreezeDefenseActors();
        StartCoroutine(ShowFailureCountdownAndReload());
    }

    private void InitializeDefenseFailureHandling()
    {
        defensePlayer = FindFirstObjectByType<PlayerController>();
        if (defensePlayer == null)
        {
            Debug.LogError("[StageArenaWaveController] Defense mode cannot find PlayerController.", this);
            return;
        }

        playerHealth = defensePlayer.GetComponent<Health>();
        if (playerHealth != null)
            playerHealth.onDeath.AddListener(HandlePlayerDeath);
        else
            Debug.LogError("[StageArenaWaveController] Defense mode player Health is missing.", this);

        PlayerRespawn playerRespawn = defensePlayer.GetComponent<PlayerRespawn>();
        if (playerRespawn != null)
            playerRespawn.enabled = false;
    }

    private void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.onDeath.RemoveListener(HandlePlayerDeath);
    }

    private void HandlePlayerDeath()
    {
        if (defenseMode)
            FailDefense();
    }

    private void FreezeDefenseActors()
    {
        FreezeBody(defensePlayer != null ? defensePlayer.GetComponent<Rigidbody2D>() : null);
        if (defensePlayer != null) defensePlayer.enabled = false;

        if (defenseTarget != null)
        {
            DefenseTargetPatrol patrol = defenseTarget.GetComponent<DefenseTargetPatrol>();
            if (patrol != null) patrol.enabled = false;
            FreezeBody(defenseTarget.GetComponent<Rigidbody2D>());
        }

        if (enemyContainer == null) return;
        foreach (Transform enemyTransform in enemyContainer)
        {
            EnemyController controller = enemyTransform.GetComponent<EnemyController>();
            if (controller != null) controller.enabled = false;
            FreezeBody(enemyTransform.GetComponent<Rigidbody2D>());
        }
    }

    private static void FreezeBody(Rigidbody2D body)
    {
        if (body == null) return;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.simulated = false;
    }

    private IEnumerator ShowFailureCountdownAndReload()
    {
        CreateFailureVisual();
        for (int seconds = 5; seconds > 0; seconds--)
        {
            if (failureText != null)
                failureText.text = $"대상을 지키지 못했습니다.\n해당 스테이지를 재시작합니다.\n\n{seconds}";
            yield return new WaitForSecondsRealtime(1f);
        }

        if (SceneLoader.Instance != null)
            SceneLoader.Instance.ReloadCurrentScene();
        else
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void CreateFailureVisual()
    {
        if (failureRoot != null) return;

        Canvas canvas = stageCanvas != null ? stageCanvas : FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[StageArenaWaveController] Defense failure UI cannot find a Canvas.", this);
            return;
        }

        failureRoot = new GameObject("DefenseFailureNotice", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        failureRoot.transform.SetParent(canvas.transform, false);
        failureRoot.transform.SetAsLastSibling();

        Image panel = failureRoot.GetComponent<Image>();
        panel.color = new Color(0.03f, 0.02f, 0.08f, 0.92f);
        panel.raycastTarget = false;

        RectTransform rootRect = failureRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(780f, 230f);

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(failureRoot.transform, false);
        failureText = textObject.GetComponent<TextMeshProUGUI>();
        TMP_Text template = FindFirstObjectByType<TMP_Text>();
        failureText.font = defenseHudFont != null
            ? defenseHudFont
            : template != null ? template.font : TMP_Settings.defaultFontAsset;
        failureText.fontSize = 34f;
        failureText.fontStyle = FontStyles.Bold;
        failureText.alignment = TextAlignmentOptions.Center;
        failureText.color = new Color(1f, 0.32f, 0.45f, 1f);
        failureText.outlineColor = new Color(0.04f, 0.01f, 0.05f, 1f);
        failureText.outlineWidth = 0.2f;
        failureText.raycastTarget = false;

        RectTransform textRect = failureText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 18f);
        textRect.offsetMax = new Vector2(-24f, -18f);
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
            if (waves[i] == null || waves[i].spawns == null || waves[i].spawns.Length == 0)
                return false;

            for (int j = 0; j < waves[i].spawns.Length; j++)
            {
                if (waves[i].spawns[j] == null)
                    return false;
            }
        }

        return true;
    }

    private static WaveDefinition[] CreateDefaultWaves()
    {
        return new[]
        {
            new WaveDefinition
            {
                spawns = new[]
                {
                    Spawn(EnemyType.Grunt, -8.5f, -2.0f),
                    Spawn(EnemyType.Grunt, 8.5f, -2.0f)
                }
            },
            new WaveDefinition
            {
                spawns = new[]
                {
                    Spawn(EnemyType.Grunt, -7.5f, 2.85f),
                    Spawn(EnemyType.Grunt, 0f, 0.6f),
                    Spawn(EnemyType.Ranged, 9.5f, -2.0f)
                }
            },
            new WaveDefinition
            {
                spawns = new[]
                {
                    Spawn(EnemyType.Grunt, -10.5f, -2.0f),
                    Spawn(EnemyType.Grunt, 10.5f, -2.0f),
                    Spawn(EnemyType.Ranged, -7.2f, 2.85f),
                    Spawn(EnemyType.Ranged, 7.2f, 2.85f)
                }
            }
        };
    }

    private GameObject GetPrefab(EnemyType enemyType)
    {
        return enemyType == EnemyType.Ranged ? rangedPrefab : gruntPrefab;
    }

    private void ApplySpawnStats(GameObject enemy, SpawnDefinition spawn)
    {
        float healthMultiplier = spawn.healthMultiplier > 0f ? spawn.healthMultiplier : 1f;
        Health health = enemy.GetComponent<Health>();
        if (health != null && !Mathf.Approximately(healthMultiplier, 1f))
            health.ApplyMaxHPMultiplier(healthMultiplier);

        float speedMultiplier = spawn.movementSpeedMultiplier > 0f ? spawn.movementSpeedMultiplier : 1f;
        EnemyController controller = enemy.GetComponent<EnemyController>();
        if (controller != null)
        {
            if (!Mathf.Approximately(speedMultiplier, 1f))
                controller.ApplyMovementSpeedMultiplier(speedMultiplier);

            if (defenseMode && defenseTarget != null)
                controller.SetTarget(defenseTarget.transform);
        }

        StageRangedEnemyController rangedController = enemy.GetComponent<StageRangedEnemyController>();
        if (defenseMode && defenseTarget != null && rangedController != null)
            rangedController.SetTarget(defenseTarget.transform);
    }

    private static SpawnDefinition Spawn(EnemyType enemyType, float x, float y,
        float healthMultiplier = 1f, float movementSpeedMultiplier = 1f)
    {
        return new SpawnDefinition
        {
            enemyType = enemyType,
            spawnPoint = new Vector2(x, y),
            healthMultiplier = healthMultiplier,
            movementSpeedMultiplier = movementSpeedMultiplier
        };
    }
}
