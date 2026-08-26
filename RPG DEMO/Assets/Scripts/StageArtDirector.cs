using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage01~03, Stage05~07 전용 아트 연결기입니다. 웨이브/피격 판정은 그대로 두고 런타임에
/// 배경과 캐릭터 스프라이트만 최종 아트로 교체합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StageArtDirector : MonoBehaviour
{
    private const float ScanInterval = 0.15f;

    private readonly HashSet<int> decoratedObjects = new HashSet<int>();
    private int stageNumber;
    private bool backdropCreated;
    private bool terrainDecorated;

    private IEnumerator Start()
    {
        stageNumber = ParseStageNumber(SceneManager.GetActiveScene().name);
        if (stageNumber == 0)
        {
            enabled = false;
            yield break;
        }

        while (enabled)
        {
            EnsureBackdrop();
            EnsureTerrainArt();
            if (stageNumber == 3) DecorateDefenseDog();
            yield return new WaitForSeconds(ScanInterval);
        }
    }

    // 적은 웨이브의 Update/코루틴에서 생성되므로, 렌더링 직전인 LateUpdate에서 아트를
    // 입혀야 스폰된 프레임에 기본 프리팹 스프라이트가 노출되지 않습니다.
    private void LateUpdate()
    {
        DecorateEnemies();
    }

    internal static int ParseStageNumber(string sceneName)
    {
        if (sceneName == "Stage01") return 1;
        if (sceneName == "Stage02") return 2;
        if (sceneName == "Stage03") return 3;
        if (sceneName == "Stage05") return 5;
        if (sceneName == "Stage06") return 6;
        if (sceneName == "Stage07") return 7;
        return 0;
    }

    private void EnsureBackdrop()
    {
        if (backdropCreated) return;

        Camera stageCamera = Camera.main;
        Texture2D texture = StageArtLibrary.LoadTexture($"StageArt/Stage_Background_0{stageNumber}");
        if (stageCamera == null || texture == null) return;

        GameObject backdrop = new GameObject($"Stage0{stageNumber}_FinalBackdrop");
        backdrop.transform.SetParent(stageCamera.transform, false);
        backdrop.transform.localPosition = new Vector3(0f, 0f, 20f);

        SpriteRenderer renderer = backdrop.AddComponent<SpriteRenderer>();
        renderer.sprite = StageArtLibrary.CreateFullSprite(texture, 100f, new Vector2(0.5f, 0.5f));
        renderer.sortingOrder = -1000;
        // 원본 배경의 네온 대비는 유지하되 장시간 플레이 시 눈부심을 줄입니다.
        renderer.color = new Color(0.43f, 0.46f, 0.52f, 1f);

        StageBackdropFollower follower = backdrop.AddComponent<StageBackdropFollower>();
        follower.Configure(stageCamera, renderer);
        backdropCreated = true;
    }

    private void DecorateEnemies()
    {
        EnemyController[] meleeEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (EnemyController enemy in meleeEnemies)
        {
            if (enemy == null || !decoratedObjects.Add(enemy.gameObject.GetInstanceID())) continue;
            StageEnemyArtAnimator animator = enemy.gameObject.AddComponent<StageEnemyArtAnimator>();
            animator.ConfigureMelee(enemy);
        }

        StageRangedEnemyController[] rangedEnemies =
            FindObjectsByType<StageRangedEnemyController>(FindObjectsSortMode.None);
        foreach (StageRangedEnemyController enemy in rangedEnemies)
        {
            if (enemy == null || !decoratedObjects.Add(enemy.gameObject.GetInstanceID())) continue;
            StageEnemyArtAnimator animator = enemy.gameObject.AddComponent<StageEnemyArtAnimator>();
            animator.ConfigureRanged();
        }
    }

    private void EnsureTerrainArt()
    {
        if (terrainDecorated) return;

        Texture2D groundTexture = StageArtLibrary.LoadTexture("StageArt/Stage_Ground_Long");
        Texture2D platformTexture = StageArtLibrary.LoadTexture("StageArt/Stage_Platform_Short");
        if (groundTexture == null || platformTexture == null) return;

        Sprite groundSprite = StageArtLibrary.CreateFullSprite(groundTexture, 100f, new Vector2(0.5f, 0.5f));
        Sprite platformSprite = StageArtLibrary.CreateFullSprite(platformTexture, 100f, new Vector2(0.5f, 0.5f));
        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        int decoratedCount = 0;

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || renderer.gameObject.layer != 6) continue;

            string objectName = renderer.gameObject.name;
            if (objectName.StartsWith("ArenaWall_"))
            {
                // 양쪽 벽은 충돌 경계만 유지하고 프로토타입 파란 도형은 숨깁니다.
                renderer.enabled = false;
                continue;
            }

            bool isGround = objectName.StartsWith("Ground_");
            bool isPlatform = objectName.StartsWith("Platform_");
            if (!isGround && !isPlatform) continue;

            BoxCollider2D collider = renderer.GetComponent<BoxCollider2D>();
            renderer.sprite = isGround ? groundSprite : platformSprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = collider != null ? collider.size : renderer.size;
            renderer.color = Color.white;
            renderer.sortingOrder = 1;
            decoratedCount++;
        }

        // StageArenaLayout.Awake 이후 실행되므로 여섯 개 런타임 구조물이 모두 있어야 합니다.
        terrainDecorated = decoratedCount > 0;
    }

    private void DecorateDefenseDog()
    {
        DefenseTargetPatrol[] targets = FindObjectsByType<DefenseTargetPatrol>(FindObjectsSortMode.None);
        foreach (DefenseTargetPatrol target in targets)
        {
            if (target == null || !decoratedObjects.Add(target.gameObject.GetInstanceID())) continue;
            StageEnemyArtAnimator animator = target.gameObject.AddComponent<StageEnemyArtAnimator>();
            animator.ConfigureDefenseDog();
        }
    }
}

internal static class StageArtLibrary
{
    private static readonly Dictionary<string, Sprite[]> FrameCache = new Dictionary<string, Sprite[]>();
    private static Material checkerKeyMaterial;

    public static Material CheckerKeyMaterial
    {
        get
        {
            if (checkerKeyMaterial == null)
                checkerKeyMaterial = Resources.Load<Material>("StageArt/MAT_StageArtColorKey");
            return checkerKeyMaterial;
        }
    }

    public static Texture2D LoadTexture(string resourcePath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null) texture.filterMode = FilterMode.Point;
        else Debug.LogError($"[StageArtDirector] Missing texture: Resources/{resourcePath}");
        return texture;
    }

    public static Sprite CreateFullSprite(Texture2D texture, float pixelsPerUnit, Vector2 pivot)
    {
        return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), pivot,
            pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    public static Sprite[] LoadFrames(string resourcePath, int columns, float normalizedBottom,
        float normalizedTop, float pixelsPerUnit, Vector2 pivot, float[] framePivotXs = null)
    {
        string pivotKey = framePivotXs == null ? "shared" : string.Join(",", framePivotXs);
        string cacheKey = $"{resourcePath}:{columns}:{normalizedBottom}:{normalizedTop}:{pixelsPerUnit}:{pivot}:{pivotKey}";
        if (FrameCache.TryGetValue(cacheKey, out Sprite[] cached)) return cached;

        Texture2D texture = LoadTexture(resourcePath);
        if (texture == null) return new Sprite[0];

        float cellWidth = texture.width / (float)columns;
        float bottom = texture.height * normalizedBottom;
        float height = texture.height * Mathf.Clamp01(normalizedTop - normalizedBottom);
        Sprite[] frames = new Sprite[columns];
        for (int i = 0; i < columns; i++)
        {
            Rect rect = new Rect(i * cellWidth, bottom, cellWidth, height);
            float pivotX = framePivotXs != null && i < framePivotXs.Length
                ? Mathf.Clamp01(framePivotXs[i])
                : pivot.x;
            frames[i] = Sprite.Create(texture, rect, new Vector2(pivotX, pivot.y),
                pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frames[i].name = $"{resourcePath.Replace('/', '_')}_{i:00}";
        }

        FrameCache[cacheKey] = frames;
        return frames;
    }
}

internal sealed class StageBackdropFollower : MonoBehaviour
{
    private Camera stageCamera;
    private SpriteRenderer targetRenderer;

    public void Configure(Camera cameraToFollow, SpriteRenderer renderer)
    {
        stageCamera = cameraToFollow;
        targetRenderer = renderer;
        ResizeToCamera();
    }

    private void LateUpdate()
    {
        ResizeToCamera();
    }

    private void ResizeToCamera()
    {
        if (stageCamera == null || targetRenderer == null || targetRenderer.sprite == null) return;

        float viewHeight = stageCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * stageCamera.aspect;
        Vector2 spriteSize = targetRenderer.sprite.bounds.size;
        float scale = Mathf.Max(viewWidth / spriteSize.x, viewHeight / spriteSize.y);
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}

internal sealed class StageEnemyArtAnimator : MonoBehaviour
{
    private enum VisualKind { Melee, Ranged, DefenseDog }

    private SpriteRenderer targetRenderer;
    private Rigidbody2D body;
    private Health health;
    private EnemyController meleeController;
    private VisualKind kind;
    private Sprite[] idleFrames;
    private Sprite[] walkFrames;
    private Sprite[] attackFrames;
    private Sprite[] hitFrames;
    private Sprite[] jumpFrames;
    private Sprite[] deathFrames;
    private Sprite[] activeFrames;
    private float frameDuration;
    private float frameTimer;
    private float hitUntil;
    private int frameIndex;
    private bool configured;

    public void ConfigureMelee(EnemyController controller)
    {
        kind = VisualKind.Melee;
        meleeController = controller;
        idleFrames = StageArtLibrary.LoadFrames("StageArt/Melee_Idle", 6, 0.21f, 0.82f, 176f,
            new Vector2(0.5f, 0f), new[] { 0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.49f });
        walkFrames = StageArtLibrary.LoadFrames("StageArt/Melee_Walk", 6, 0.25f, 0.82f, 176f,
            new Vector2(0.5f, 0f), new[] { 0.54f, 0.50f, 0.50f, 0.50f, 0.50f, 0.44f });
        attackFrames = StageArtLibrary.LoadFrames("StageArt/Melee_Attack", 6, 0.29f, 0.76f, 176f,
            new Vector2(0.5f, 0f), new[] { 0.53f, 0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.49f });
        hitFrames = StageArtLibrary.LoadFrames("StageArt/Melee_Hit", 6, 0.18f, 0.82f, 176f,
            new Vector2(0.5f, 0f), new[] { 0.50f, 0.50f, 0.50f, 0.50f, 0.50f, 0.49f });
        deathFrames = StageArtLibrary.LoadFrames("StageArt/Melee_Death", 6, 0.25f, 0.77f, 176f,
            new Vector2(0.5f, 0f), new[] { 0.53f, 0.50f, 0.50f, 0.50f, 0.50f, 0.44f, 0.46f });
        InitializeRenderer(true, 1f);
        SetVisualGroundOffset(-0.66f);
        SetState(idleFrames, 0.18f);
    }

    public void ConfigureRanged()
    {
        kind = VisualKind.Ranged;
        idleFrames = StageArtLibrary.LoadFrames("StageArt/Ranged_Idle", 6, 0.30f, 0.72f, 145f,
            new Vector2(0.5f, 0.5f));
        deathFrames = StageArtLibrary.LoadFrames("StageArt/Ranged_Death", 6, 0.12f, 0.88f, 145f,
            new Vector2(0.5f, 0.5f));
        InitializeRenderer(false, 1f);
        SetState(idleFrames, 0.12f);
    }

    public void ConfigureDefenseDog()
    {
        kind = VisualKind.DefenseDog;
        idleFrames = StageArtLibrary.LoadFrames("StageArt/Dog_Idle", 6, 0.32f, 0.70f, 132f,
            new Vector2(0.5f, 0f));
        jumpFrames = StageArtLibrary.LoadFrames("StageArt/Dog_Jump", 6, 0.19f, 0.82f, 132f,
            new Vector2(0.5f, 0f));
        InitializeRenderer(false, 1f);
        SetVisualGroundOffset(-1.12f);
        SetState(idleFrames, 0.13f);
    }

    private void InitializeRenderer(bool useCheckerKey, float uniformScale)
    {
        targetRenderer = GetComponentInChildren<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();

        if (targetRenderer == null)
        {
            Debug.LogError($"[StageArtDirector] SpriteRenderer missing on {name}.", this);
            enabled = false;
            return;
        }

        targetRenderer.drawMode = SpriteDrawMode.Simple;
        targetRenderer.color = Color.white;
        if (useCheckerKey && StageArtLibrary.CheckerKeyMaterial != null)
            targetRenderer.sharedMaterial = StageArtLibrary.CheckerKeyMaterial;

        Vector3 localScale = targetRenderer.transform.localScale;
        targetRenderer.transform.localScale = new Vector3(
            Mathf.Sign(localScale.x == 0f ? 1f : localScale.x) * uniformScale,
            uniformScale, 1f);

        if (health != null)
        {
            health.onDamaged.AddListener(HandleDamaged);
            health.onDeath.AddListener(HandleDeath);
        }

        configured = true;
    }

    private void SetVisualGroundOffset(float localY)
    {
        if (targetRenderer == null) return;
        Vector3 position = targetRenderer.transform.localPosition;
        position.y = localY;
        targetRenderer.transform.localPosition = position;
    }

    private void Update()
    {
        if (!configured || targetRenderer == null || (health != null && health.IsDead)) return;

        SelectState();
        if (activeFrames == null || activeFrames.Length == 0) return;

        frameTimer += Time.deltaTime;
        if (frameTimer < frameDuration) return;

        frameTimer -= frameDuration;
        frameIndex = (frameIndex + 1) % activeFrames.Length;
        targetRenderer.sprite = activeFrames[frameIndex];
    }

    private void LateUpdate()
    {
        if (!configured || targetRenderer == null) return;
        targetRenderer.color = Color.white;

        Vector3 scale = targetRenderer.transform.localScale;
        float facing = Mathf.Sign(scale.x == 0f ? 1f : scale.x);
        if (kind == VisualKind.DefenseDog && body != null && Mathf.Abs(body.linearVelocity.x) > 0.05f)
        {
            facing = Mathf.Sign(body.linearVelocity.x);
            // Dog_Idle 시트는 왼쪽, Dog_Jump 시트는 오른쪽을 보고 그려져 있어
            // 지상(걷기) 프레임에서만 미러 방향이 반대다.
            if (ReferenceEquals(activeFrames, idleFrames)) facing = -facing;
        }
        targetRenderer.transform.localScale = new Vector3(facing, 1f, 1f);
    }

    private void SelectState()
    {
        if (kind == VisualKind.DefenseDog)
        {
            bool airborne = body != null && Mathf.Abs(body.linearVelocity.y) > 0.35f;
            SetState(airborne ? jumpFrames : idleFrames, airborne ? 0.10f : 0.13f);
            return;
        }

        if (kind == VisualKind.Ranged)
        {
            SetState(idleFrames, 0.12f);
            return;
        }

        if (Time.time < hitUntil)
        {
            SetState(hitFrames, 0.07f);
            return;
        }

        bool moving = body != null && Mathf.Abs(body.linearVelocity.x) > 0.15f;
        bool attacking = meleeController != null && meleeController.IsChasing && !moving;
        if (attacking) SetState(attackFrames, 0.12f);
        else if (moving) SetState(walkFrames, 0.13f);
        else SetState(idleFrames, 0.18f);
    }

    private void SetState(Sprite[] frames, float secondsPerFrame)
    {
        if (frames == null || frames.Length == 0 || ReferenceEquals(activeFrames, frames)) return;
        activeFrames = frames;
        frameDuration = secondsPerFrame;
        frameTimer = 0f;
        frameIndex = 0;
        if (targetRenderer != null) targetRenderer.sprite = frames[0];
    }

    private void HandleDamaged()
    {
        if (kind == VisualKind.Melee) hitUntil = Time.time + 0.32f;
    }

    private void HandleDeath()
    {
        if (deathFrames == null || deathFrames.Length == 0 || targetRenderer == null) return;

        GameObject ghost = new GameObject($"{name}_DeathArt");
        ghost.transform.position = targetRenderer.transform.position;
        ghost.transform.rotation = targetRenderer.transform.rotation;
        ghost.transform.localScale = targetRenderer.transform.lossyScale;

        SpriteRenderer ghostRenderer = ghost.AddComponent<SpriteRenderer>();
        ghostRenderer.sprite = deathFrames[0];
        ghostRenderer.sharedMaterial = targetRenderer.sharedMaterial;
        ghostRenderer.sortingLayerID = targetRenderer.sortingLayerID;
        ghostRenderer.sortingOrder = targetRenderer.sortingOrder;
        ghostRenderer.color = Color.white;

        StageDeathArtPlayback playback = ghost.AddComponent<StageDeathArtPlayback>();
        playback.Configure(ghostRenderer, deathFrames, kind == VisualKind.Ranged ? 0.11f : 0.09f);
    }

    private void OnDestroy()
    {
        if (health == null) return;
        health.onDamaged.RemoveListener(HandleDamaged);
        health.onDeath.RemoveListener(HandleDeath);
    }
}

internal sealed class StageDeathArtPlayback : MonoBehaviour
{
    private SpriteRenderer targetRenderer;
    private Sprite[] frames;
    private float frameDuration;

    public void Configure(SpriteRenderer renderer, Sprite[] animationFrames, float secondsPerFrame)
    {
        targetRenderer = renderer;
        frames = animationFrames;
        frameDuration = secondsPerFrame;
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        for (int i = 0; i < frames.Length; i++)
        {
            if (targetRenderer == null) yield break;
            targetRenderer.sprite = frames[i];
            yield return new WaitForSeconds(frameDuration);
        }
        Destroy(gameObject);
    }
}

/// <summary>Stage01~03, Stage05~07의 원거리 적이 생성한 투사체에만 빨간 총알 프레임을 적용합니다.</summary>
internal sealed class StageRangedProjectileArt : MonoBehaviour
{
    private SpriteRenderer targetRenderer;
    private Sprite[] frames;
    private int frameIndex;
    private float frameTimer;

    public static void Apply(Projectile projectile)
    {
        if (projectile == null) return;

        string sceneName = SceneManager.GetActiveScene().name;
        if (StageArtDirector.ParseStageNumber(sceneName) == 0) return;
        if (projectile.GetComponent<StageRangedProjectileArt>() != null) return;

        projectile.gameObject.AddComponent<StageRangedProjectileArt>().Configure();
    }

    private void Configure()
    {
        targetRenderer = GetComponentInChildren<SpriteRenderer>();
        frames = StageArtLibrary.LoadFrames("StageArt/Stage_Projectile_Red", 4, 0f, 1f, 20f,
            new Vector2(0.5f, 0.5f));

        if (targetRenderer == null || frames.Length == 0)
        {
            enabled = false;
            return;
        }

        targetRenderer.drawMode = SpriteDrawMode.Simple;
        targetRenderer.color = Color.white;
        targetRenderer.sortingOrder = 10;
        targetRenderer.sprite = frames[0];
    }

    private void Update()
    {
        if (targetRenderer == null || frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        if (frameTimer < 0.06f) return;
        frameTimer -= 0.06f;
        frameIndex = (frameIndex + 1) % frames.Length;
        targetRenderer.sprite = frames[frameIndex];
    }
}
