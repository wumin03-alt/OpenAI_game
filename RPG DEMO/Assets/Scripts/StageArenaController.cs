using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage 02~04가 공유하는 다층 2D 아레나와 각 스테이지의 진행 규칙입니다.
/// Player 조작 코드는 건드리지 않고, 이 컴포넌트의 Inspector 값만으로 템포를 조절할 수 있습니다.
/// </summary>
public class StageArenaController : MonoBehaviour
{
    public enum StageMode { Combat, Defense, Minigame }

    [Header("Stage Setup")]
    [SerializeField] private StageMode mode;
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private string nextSceneName;
    [SerializeField] private float clearDelay = 1.5f;

    [Header("Stage 02 - Defense")]
    [SerializeField] private float stage02PreparationDuration = 3f;
    [SerializeField] private float stage02SurvivalDuration = 165f;
    [SerializeField] private float stage02EarlySpawnInterval = 8f;
    [SerializeField] private float stage02MidSpawnInterval = 6f;
    [SerializeField] private float stage02LateSpawnInterval = 5f;
    [SerializeField] private int stage02EarlyMaxAlive = 1;
    [SerializeField] private int stage02MidMaxAlive = 2;
    [SerializeField] private int stage02LateMaxAlive = 2;

    [Header("Stage 03 - Enemy Assault")]
    [SerializeField] private int[] stage03WaveEnemyCounts = { 3, 4, 5 };
    [SerializeField] private float stage03SpawnInterval = 3.5f;
    [SerializeField] private float stage03WaveBreakDuration = 2.5f;
    [SerializeField] private int stage03EarlyMaxAlive = 2;
    [SerializeField] private int stage03FinalMaxAlive = 3;

    [Header("Stage 04 - Target Training")]
    [SerializeField] private float stage04IntroductionDuration = 2.5f;
    [SerializeField] private float stage04TargetDelay = 2.5f;

    [Header("Arena Enemy AI")]
    [SerializeField] private float arenaDetectRange = 38f;
    [SerializeField] private float arenaLoseRange = 42f;

    private const int GroundLayer = 6;
    private const int EnemyLayer = 8;
    // 하층 압박용 근접 적 스폰 지점입니다.
    private static readonly Vector2[] GroundEnemySpawnPositions =
    {
        new Vector2(-14.8f, -2.9f), new Vector2(14.8f, -2.9f)
    };

    // 현재 Grunt와 이후 원거리 적 모두 재사용할 수 있는 중/상층 스폰 지점입니다.
    private static readonly Vector2[] ElevatedEnemySpawnPositions =
    {
        new Vector2(-9f, -0.7f), new Vector2(9f, -0.7f),
        new Vector2(-4.5f, 1.5f), new Vector2(4.5f, 1.5f), new Vector2(0f, 3.4f)
    };

    // Wave가 진행될수록 하층 → 중층 → 상층 → 반대편으로 전장을 순환시킵니다.
    private static readonly Vector2[] AssaultEnemySpawnRoute =
    {
        new Vector2(14.8f, -2.9f), new Vector2(-9f, -0.7f), new Vector2(0f, 3.4f),
        new Vector2(9f, -0.7f), new Vector2(-14.8f, -2.9f), new Vector2(4.5f, 1.5f),
        new Vector2(-4.5f, 1.5f)
    };
    private static readonly Vector2[] TargetPositions =
    {
        new Vector2(-13f, -2.9f), new Vector2(-9f, -0.55f), new Vector2(0f, 3.4f),
        new Vector2(9f, -0.55f), new Vector2(13f, -2.9f)
    };

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private bool stageStarted;
    private bool isStageCleared;
    private bool isTransitioning;
    private int spawnSideIndex;
    private int stage02SpawnIndex;
    private int activeEnemies;
    private string statusLine;

    private float stage02PreparationLeft;
    private float stage02TimeLeft;
    private float stage02SpawnLeft;

    private int currentWave;
    private int waveSpawned;
    private float waveSpawnLeft;
    private bool waitingForNextWave;

    private int targetIndex;
    private float targetIntroductionLeft;
    private float targetSpawnLeft;
    private GameObject activeTarget;

    /// <summary>에디터 생성기가 씬 저장 전에 호출합니다.</summary>
    public void Configure(StageMode stageMode, GameObject player, GameObject enemy, string nextScene)
    {
        mode = stageMode;
        playerPrefab = player;
        enemyPrefab = enemy;
        nextSceneName = nextScene;
    }

    private void Awake()
    {
        BuildArena();
        EnsureCamera();
        SpawnPlayer();
    }

    private void Start()
    {
        if (mode == StageMode.Combat) StartStage02Defense();
        else if (mode == StageMode.Defense) StartStage03Assault();
        else StartStage04TargetTraining();

        stageStarted = true;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (stageStarted && Input.GetKeyDown(KeyCode.F8))
        {
            ClearStage(true);
            return;
        }
        if (stageStarted && Input.GetKeyDown(KeyCode.F7))
        {
            ClearStage();
            return;
        }
#endif
        if (!stageStarted || isStageCleared) return;

        RefreshActiveEnemyCount();
        if (mode == StageMode.Combat) UpdateStage02Defense();
        else if (mode == StageMode.Defense) UpdateStage03Assault();
        else UpdateStage04TargetTraining();
    }

    private void StartStage02Defense()
    {
        stage02PreparationLeft = stage02PreparationDuration;
        stage02TimeLeft = stage02SurvivalDuration;
        stage02SpawnLeft = 0.5f;
        statusLine = "약 3분 동안 살아남으세요";
    }

    private void UpdateStage02Defense()
    {
        if (stage02PreparationLeft > 0f)
        {
            stage02PreparationLeft -= Time.deltaTime;
            return;
        }

        stage02TimeLeft -= Time.deltaTime;
        if (stage02TimeLeft <= 0f)
        {
            RemoveSpawnedEnemies();
            ClearStage();
            return;
        }

        stage02SpawnLeft -= Time.deltaTime;
        if (stage02SpawnLeft > 0f) return;

        float elapsed = stage02SurvivalDuration - stage02TimeLeft;
        int aliveLimit;
        float spawnInterval;
        if (elapsed < 60f)
        {
            aliveLimit = stage02EarlyMaxAlive;
            spawnInterval = stage02EarlySpawnInterval;
        }
        else if (elapsed < 120f)
        {
            aliveLimit = stage02MidMaxAlive;
            spawnInterval = stage02MidSpawnInterval;
        }
        else
        {
            aliveLimit = stage02LateMaxAlive;
            spawnInterval = stage02LateSpawnInterval;
        }

        if (activeEnemies < aliveLimit)
        {
            SpawnStage02Enemy(elapsed);
            activeEnemies = spawnedEnemies.Count;
            stage02SpawnLeft = spawnInterval;
        }
        else
        {
            // 동시 등장 상한에 도달하면 누적하지 않고 짧게만 재확인합니다.
            stage02SpawnLeft = 0.75f;
        }
    }

    private void StartStage03Assault()
    {
        currentWave = 0;
        BeginWave();
    }

    private void BeginWave()
    {
        waveSpawned = 0;
        waveSpawnLeft = 0.75f;
        waitingForNextWave = false;
        statusLine = $"WAVE {currentWave + 1} / {stage03WaveEnemyCounts.Length} — 적을 모두 처치하세요";
    }

    private void UpdateStage03Assault()
    {
        if (waitingForNextWave) return;

        int waveTotal = stage03WaveEnemyCounts[currentWave];
        int aliveLimit = currentWave == stage03WaveEnemyCounts.Length - 1
            ? stage03FinalMaxAlive : stage03EarlyMaxAlive;

        waveSpawnLeft -= Time.deltaTime;
        if (waveSpawned < waveTotal && waveSpawnLeft <= 0f && activeEnemies < aliveLimit)
        {
            SpawnStage03Enemy();
            waveSpawned++;
            waveSpawnLeft = stage03SpawnInterval;
        }

        if (waveSpawned >= waveTotal && activeEnemies == 0)
        {
            currentWave++;
            if (currentWave >= stage03WaveEnemyCounts.Length)
            {
                ClearStage();
                return;
            }

            waitingForNextWave = true;
            statusLine = "WAVE CLEAR — 다음 웨이브 준비";
            StartCoroutine(BeginNextWaveAfterDelay());
        }
    }

    private IEnumerator BeginNextWaveAfterDelay()
    {
        yield return new WaitForSeconds(stage03WaveBreakDuration);
        if (!isStageCleared) BeginWave();
    }

    private void StartStage04TargetTraining()
    {
        targetIndex = 0;
        targetIntroductionLeft = stage04IntroductionDuration;
        targetSpawnLeft = 0f;
        statusLine = "맵에 나타나는 목표물 5개를 파괴하세요";
    }

    private void UpdateStage04TargetTraining()
    {
        if (targetIntroductionLeft > 0f)
        {
            targetIntroductionLeft -= Time.deltaTime;
            return;
        }

        if (activeTarget != null) return;
        if (targetIndex >= TargetPositions.Length)
        {
            ClearStage();
            return;
        }

        targetSpawnLeft -= Time.deltaTime;
        if (targetSpawnLeft > 0f) return;
        CreateTarget(TargetPositions[targetIndex]);
    }

    private GameObject SpawnPlayer()
    {
        GameObject existing = GameObject.FindGameObjectWithTag("Player");
        if (existing != null) return existing;
        if (playerPrefab == null) return null;
        return Instantiate(playerPrefab, new Vector3(-13f, -2.8f, 0f), Quaternion.identity);
    }

    private void SpawnStage02Enemy(float elapsed)
    {
        // 초반은 하층 압박 위주, 중반부터는 중/상층을 섞어 이동 동기를 만듭니다.
        bool useElevatedSpawn = elapsed >= 60f &&
                                (elapsed < 120f ? stage02SpawnIndex % 3 == 2 : stage02SpawnIndex % 2 == 1);
        Vector2 position = useElevatedSpawn
            ? ElevatedEnemySpawnPositions[stage02SpawnIndex % ElevatedEnemySpawnPositions.Length]
            : GroundEnemySpawnPositions[spawnSideIndex++ % GroundEnemySpawnPositions.Length];

        stage02SpawnIndex++;
        SpawnEnemyAt(position);
    }

    private void SpawnStage03Enemy()
    {
        int routeIndex = currentWave * 2 + waveSpawned;
        SpawnEnemyAt(AssaultEnemySpawnRoute[routeIndex % AssaultEnemySpawnRoute.Length]);
    }

    private void SpawnEnemyAt(Vector2 position)
    {
        if (enemyPrefab == null || isStageCleared) return;

        GameObject enemy = Instantiate(enemyPrefab, position, Quaternion.identity);
        spawnedEnemies.Add(enemy);

        EnemyController enemyController = enemy.GetComponent<EnemyController>();
        if (enemyController != null)
            enemyController.ConfigureArenaPursuit(arenaDetectRange, arenaLoseRange);
    }

    private void RefreshActiveEnemyCount()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null) spawnedEnemies.RemoveAt(i);
        }
        activeEnemies = spawnedEnemies.Count;
    }

    private void CreateTarget(Vector2 position)
    {
        GameObject target = new GameObject($"StageTarget_{targetIndex + 1}");
        target.layer = EnemyLayer;
        target.transform.position = position;

        SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSolidSprite();
        renderer.color = new Color(1f, 0.72f, 0.12f, 1f);
        renderer.sortingOrder = 3;
        target.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
        collider.isTrigger = true;

        Health health = target.AddComponent<Health>();
        health.SetHP(1f);

        StageTargetRelay relay = target.AddComponent<StageTargetRelay>();
        relay.Initialize(() => OnTargetDestroyed(target));
        activeTarget = target;
        statusLine = $"TARGET {targetIndex + 1} / {TargetPositions.Length}";
    }

    private void OnTargetDestroyed(GameObject target)
    {
        if (isStageCleared || target != activeTarget) return;

        activeTarget = null;
        targetIndex++;
        if (targetIndex >= TargetPositions.Length)
        {
            ClearStage();
            return;
        }

        targetSpawnLeft = stage04TargetDelay;
        statusLine = $"TARGET {targetIndex + 1} / {TargetPositions.Length}";
    }

    private void RemoveSpawnedEnemies()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null) Destroy(enemy);
        }
        spawnedEnemies.Clear();
        activeEnemies = 0;
    }

    private void ClearStage(bool skipDelay = false)
    {
        if (isStageCleared || isTransitioning) return;

        isStageCleared = true;
        isTransitioning = true;
        statusLine = "STAGE CLEAR";
        StartCoroutine(LoadNextScene(skipDelay));
    }

    private IEnumerator LoadNextScene(bool skipDelay)
    {
        if (skipDelay) yield return null;
        else yield return new WaitForSeconds(clearDelay);

        if (string.IsNullOrEmpty(nextSceneName) || !Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            Debug.LogError($"[StageArena] Build Settings에 다음 씬이 없습니다: {nextSceneName}");
            isTransitioning = false;
            yield break;
        }

        SceneManager.LoadScene(nextSceneName, LoadSceneMode.Single);
    }

    private void BuildArena()
    {
        Transform arena = new GameObject("GeneratedArena").transform;
        arena.SetParent(transform);

        // Jump Force 15 / Gravity Scale 4 기준 최대 상승 약 2.87입니다.
        // 각 착지면 차이를 2.4, 2.2, 1.9로 유지하고, 옆 가장자리에 틈을 둡니다.
        CreatePlatform(arena, "Floor", new Vector2(0f, -4.2f), new Vector2(32f, 1f), new Color(0.18f, 0.26f, 0.36f));
        CreatePlatform(arena, "MiddleLeft", new Vector2(-9f, -1.55f), new Vector2(4.5f, 0.5f), new Color(0.24f, 0.42f, 0.47f));
        CreatePlatform(arena, "MiddleRight", new Vector2(9f, -1.55f), new Vector2(4.5f, 0.5f), new Color(0.24f, 0.42f, 0.47f));
        CreatePlatform(arena, "UpperLeft", new Vector2(-4.5f, 0.65f), new Vector2(3f, 0.5f), new Color(0.28f, 0.5f, 0.54f));
        CreatePlatform(arena, "UpperRight", new Vector2(4.5f, 0.65f), new Vector2(3f, 0.5f), new Color(0.28f, 0.5f, 0.54f));
        CreatePlatform(arena, "TopCenter", new Vector2(0f, 2.55f), new Vector2(5f, 0.5f), new Color(0.34f, 0.58f, 0.62f));
        CreatePlatform(arena, "WallLeft", new Vector2(-16.2f, 0.3f), new Vector2(0.7f, 11f), new Color(0.13f, 0.2f, 0.29f));
        CreatePlatform(arena, "WallRight", new Vector2(16.2f, 0.3f), new Vector2(0.7f, 11f), new Color(0.13f, 0.2f, 0.29f));
    }

    private static void CreatePlatform(Transform parent, string objectName, Vector2 position, Vector2 size, Color color)
    {
        GameObject platform = new GameObject(objectName);
        platform.layer = GroundLayer;
        platform.transform.SetParent(parent);
        platform.transform.position = position;

        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = CreateSolidSprite();
        renderer.color = color;
        renderer.sortingOrder = -1;
        platform.transform.localScale = new Vector3(size.x, size.y, 1f);

        platform.AddComponent<BoxCollider2D>().size = Vector2.one;
    }

    private static Sprite CreateSolidSprite()
    {
        return Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    private static void EnsureCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, -0.3f, -10f);
            camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.backgroundColor = new Color(0.055f, 0.08f, 0.14f, 1f);
            camera.clearFlags = CameraClearFlags.SolidColor;
        }

        camera.orthographicSize = 6f;
        camera.transform.position = new Vector3(0f, -0.3f, -10f);
    }

    private void OnGUI()
    {
        GUIStyle title = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = Color.white }
        };
        GUIStyle body = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.UpperCenter,
            normal = { textColor = new Color(0.82f, 0.9f, 1f, 1f) }
        };

        string titleText = mode == StageMode.Combat ? "DEFENSE" :
                           mode == StageMode.Defense ? "ENEMY ASSAULT" : "TARGET TRAINING";
        GUI.Label(new Rect(0f, 24f, Screen.width, 32f), titleText, title);
        GUI.Label(new Rect(0f, 58f, Screen.width, 28f), statusLine, body);

        if (mode == StageMode.Combat && !isStageCleared)
        {
            string timer = stage02PreparationLeft > 0f
                ? $"준비 시간 {Mathf.CeilToInt(stage02PreparationLeft):00}"
                : $"남은 시간 {FormatTime(stage02TimeLeft)}";
            GUI.Label(new Rect(0f, 86f, Screen.width, 28f), timer, body);
        }
        else if (mode == StageMode.Defense && !isStageCleared)
        {
            int total = stage03WaveEnemyCounts.Length == 0 ? 0 : stage03WaveEnemyCounts.Length;
            int remaining = total == 0 || currentWave >= total ? 0 : stage03WaveEnemyCounts[currentWave] - waveSpawned + activeEnemies;
            GUI.Label(new Rect(0f, 86f, Screen.width, 28f), $"WAVE {currentWave + 1} / {total}    남은 적 {Mathf.Max(0, remaining)}", body);
        }
        else if (mode == StageMode.Minigame && !isStageCleared && targetIntroductionLeft <= 0f)
        {
            GUI.Label(new Rect(0f, 86f, Screen.width, 28f), $"TARGET {Mathf.Min(targetIndex + 1, TargetPositions.Length)} / {TargetPositions.Length}", body);
        }
    }

    private static string FormatTime(float seconds)
    {
        int wholeSeconds = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        return $"{wholeSeconds / 60:00}:{wholeSeconds % 60:00}";
    }
}
