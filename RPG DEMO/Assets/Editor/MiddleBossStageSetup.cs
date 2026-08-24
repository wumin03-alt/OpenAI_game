#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>F.E.E.D.-6 중간보스 씬을 재현 가능하게 생성하고 Stage03 뒤에 연결합니다.</summary>
public static class MiddleBossStageSetup
{
    private const string ScenePath = "Assets/Scenes/MiddleBoss.unity";
    private const string Stage03Path = "Assets/Scenes/Stage03.unity";
    private const string BossSpritePath = "Assets/Art/MiddleBoss/SPR_FEED6_Idle.png";
    private const string RuinedCityPath = "Assets/Art/MiddleBoss/Environment/SPR_MiddleBoss_RuinedCity.png";
    private const string AnimationRoot = "Assets/Art/MiddleBoss/Animation/Frames";
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";

    [MenuItem("Tools/Middle Boss/Build F.E.E.D.-6 Stage")]
    public static void Apply()
    {
        ConfigureArtAssets();
        BuildMiddleBossScene();
        UpdateBuildSettings();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MiddleBossStageSetup] Stage03 -> MiddleBoss -> Stage04 연결 및 씬 생성 완료");
    }

    private static void ConfigureArtAssets()
    {
        ConfigureSprite(BossSpritePath, 180f);
        ConfigureSprite(RuinedCityPath, 128f);
        foreach (string part in new[] { "Claw", "Piston", "Track" })
        {
            for (int i = 1; i <= 4; i++)
                ConfigureSprite($"{AnimationRoot}/{part}/{i:00}.png", 120f);
        }
    }

    private static void ConfigureSprite(string assetPath, float pixelsPerUnit)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[MiddleBossStageSetup] 스프라이트를 찾을 수 없습니다: {assetPath}");
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void BuildMiddleBossScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Sprite square = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite bossSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BossSpritePath);
        Sprite ruinedCity = AssetDatabase.LoadAssetAtPath<Sprite>(RuinedCityPath);
        Sprite[] clawFrames = LoadFrames("Claw");
        Sprite[] pistonFrames = LoadFrames("Piston");
        Sprite[] trackFrames = LoadFrames("Track");

        CreateCamera();
        CreateEnvironment(square, ruinedCity);

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
            throw new MissingReferenceException($"Player prefab missing: {PlayerPrefabPath}");
        GameObject player = PrefabUtility.InstantiatePrefab(playerPrefab, scene) as GameObject;
        player.name = "Player";
        player.transform.position = new Vector3(-7.4f, -3.15f, 0f);

        GameObject exitGate = CreateExitGate(square);
        GameObject boss = CreateBoss(bossSprite, square, exitGate, clawFrames, pistonFrames, trackFrames);

        Selection.activeGameObject = boss;
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void CreateCamera()
    {
        GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0.25f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.015f, 0.025f, 0.055f, 1f);
    }

    private static void CreateEnvironment(Sprite square, Sprite ruinedCity)
    {
        GameObject root = new GameObject("MiddleBossEnvironment_RuinedCity");

        CreatePanel(root.transform, ruinedCity != null ? ruinedCity : square, "RuinedCityBackdrop",
            new Vector3(0f, 0.25f, 4f), new Vector2(24f, 12f), Color.white, -20);

        CreatePanel(root.transform, square, "DustHaze", new Vector3(0f, -1.3f, 2f),
            new Vector2(24f, 3.8f), new Color(0.3f, 0.22f, 0.16f, 0.13f), -8);

        CreateSolid(root.transform, square, "Ground", new Vector3(0f, -4.35f, 0f),
            new Vector2(24f, 1.2f), new Color(0.16f, 0.14f, 0.13f, 1f));
        CreateSolid(root.transform, square, "LeftWall", new Vector3(-11.7f, 0f, 0f),
            new Vector2(1f, 10f), new Color(0.13f, 0.12f, 0.12f, 1f));
        CreateSolid(root.transform, square, "RightWall", new Vector3(11.7f, 0f, 0f),
            new Vector2(1f, 10f), new Color(0.13f, 0.12f, 0.12f, 1f));

        GameObject platform = CreateSolid(root.transform, square, "EscapePlatform_RubbleSlab",
            new Vector3(-1.25f, -1.55f, 0f), new Vector2(3.6f, 0.42f),
            new Color(0.38f, 0.33f, 0.28f, 1f));
        SpriteRenderer platformRenderer = platform.GetComponent<SpriteRenderer>();
        platformRenderer.sortingOrder = 4;
        CreatePanel(root.transform, square, "PlatformWarningStripe", new Vector3(-1.25f, -1.31f, -0.1f),
            new Vector2(3.25f, 0.08f), new Color(1f, 0.6f, 0.16f, 0.85f), 5);
    }

    private static GameObject CreateBoss(Sprite bossSprite, Sprite square, GameObject exitGate,
        Sprite[] clawFrames, Sprite[] pistonFrames, Sprite[] trackFrames)
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        GameObject boss = new GameObject("FEED6_MiddleBoss");
        boss.tag = "Enemy";
        if (enemyLayer >= 0) boss.layer = enemyLayer;
        boss.transform.position = new Vector3(7.15f, -1.35f, 0f);

        Rigidbody2D body = boss.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;

        BoxCollider2D collider = boss.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(8.4f, 5.25f);
        collider.offset = new Vector2(0.05f, -0.1f);

        GameObject visualObject = new GameObject("SPR_FEED6_Body", typeof(SpriteRenderer));
        visualObject.transform.SetParent(boss.transform, false);
        visualObject.transform.localScale = Vector3.one * 1.18f;
        SpriteRenderer visual = visualObject.GetComponent<SpriteRenderer>();
        visual.sprite = bossSprite;
        visual.sortingOrder = 8;

        CreateAnimatedPart(boss.transform, "AnimatedClaw", clawFrames,
            new Vector3(-2f, 1.45f, 0f), 0.72f, 13, 4.2f, true, 0.08f, 2.2f);
        CreateAnimatedPart(boss.transform, "AnimatedPiston", pistonFrames,
            new Vector3(-2.55f, -0.35f, 0f), 0.68f, 12, 3.2f, true, 0.045f, 2.8f);
        CreateAnimatedPart(boss.transform, "AnimatedTrack", trackFrames,
            new Vector3(2.55f, -0.45f, 0f), 1.05f, 11, 5.5f, false, 0.025f, 3.2f);

        GameObject aim = new GameObject("BossAimTarget");
        aim.tag = "Boss";
        aim.transform.SetParent(boss.transform, false);
        aim.transform.localPosition = new Vector3(-0.65f, 0.55f, 0f);

        Health health = boss.AddComponent<Health>();
        SerializedObject healthObject = new SerializedObject(health);
        healthObject.FindProperty("maxHP").floatValue = 400f;
        healthObject.FindProperty("invincibleTime").floatValue = 0f;
        healthObject.FindProperty("destroyOnDeath").boolValue = false;
        healthObject.FindProperty("stopMovementOnDeath").boolValue = true;
        healthObject.FindProperty("freezePositionOnDeath").boolValue = true;
        healthObject.ApplyModifiedPropertiesWithoutUndo();

        BossStaggerGauge gauge = boss.AddComponent<BossStaggerGauge>();
        SerializedObject gaugeObject = new SerializedObject(gauge);
        gaugeObject.FindProperty("parriesRequired").intValue = 3;
        gaugeObject.FindProperty("staggerDuration").floatValue = 10f;
        gaugeObject.ApplyModifiedPropertiesWithoutUndo();

        boss.AddComponent<DirectionSequenceEscape>();
        MiddleBossController controller = boss.AddComponent<MiddleBossController>();
        SerializedObject controllerObject = new SerializedObject(controller);
        controllerObject.FindProperty("visual").objectReferenceValue = visual;
        controllerObject.FindProperty("attackSprite").objectReferenceValue = square;
        controllerObject.FindProperty("exitGate").objectReferenceValue = exitGate;
        controllerObject.FindProperty("aimTarget").objectReferenceValue = aim.transform;
        controllerObject.ApplyModifiedPropertiesWithoutUndo();
        return boss;
    }

    private static Sprite[] LoadFrames(string part)
    {
        Sprite[] frames = new Sprite[4];
        for (int i = 0; i < frames.Length; i++)
            frames[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"{AnimationRoot}/{part}/{i + 1:00}.png");
        return frames;
    }

    private static void CreateAnimatedPart(Transform parent, string objectName, Sprite[] frames,
        Vector3 localPosition, float scale, int sortingOrder, float fps, bool pingPong,
        float bobAmplitude, float bobSpeed)
    {
        GameObject part = new GameObject(objectName, typeof(SpriteRenderer), typeof(MiddleBossPartAnimator));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
        renderer.sprite = frames != null && frames.Length > 0 ? frames[0] : null;
        renderer.sortingOrder = sortingOrder;
        part.GetComponent<MiddleBossPartAnimator>().Configure(renderer, frames, fps, pingPong,
            bobAmplitude, bobSpeed);
    }

    private static GameObject CreateExitGate(Sprite square)
    {
        GameObject gate = new GameObject("MiddleBossExit_ToStage04", typeof(SpriteRenderer), typeof(BoxCollider2D));
        gate.transform.position = new Vector3(10.45f, -1.6f, 0f);
        SpriteRenderer renderer = gate.GetComponent<SpriteRenderer>();
        renderer.sprite = square;
        SetWorldSize(gate.transform, square, new Vector2(0.8f, 4.7f));
        renderer.color = new Color(0.15f, 1f, 0.65f, 0.5f);
        renderer.sortingOrder = 6;
        BoxCollider2D collider = gate.GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
        collider.size = square != null ? square.bounds.size : Vector2.one;

        StageExit exit = gate.AddComponent<StageExit>();
        SerializedObject exitObject = new SerializedObject(exit);
        exitObject.FindProperty("nextSceneName").stringValue = "Stage04";
        exitObject.FindProperty("delay").floatValue = 0.35f;
        exitObject.FindProperty("requireAllEnemiesDead").boolValue = false;
        exitObject.FindProperty("clearGuideMessage").stringValue = "F.E.E.D.-6 정지 완료 // 우측 출구로 Stage04에 진입하세요.";
        exitObject.ApplyModifiedPropertiesWithoutUndo();
        gate.SetActive(false);
        return gate;
    }

    private static void UpdateBuildSettings()
    {
        string sceneGuid = AssetDatabase.AssetPathToGUID(ScenePath);
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes
            .Where(scene =>
                !scene.path.Equals(ScenePath, System.StringComparison.OrdinalIgnoreCase) &&
                !scene.path.Equals(sceneGuid, System.StringComparison.OrdinalIgnoreCase))
            .ToList();

        int stage03Index = scenes.FindIndex(scene =>
            scene.path.Equals(Stage03Path, System.StringComparison.OrdinalIgnoreCase));
        if (stage03Index < 0)
            throw new MissingReferenceException("Build Settings에서 Stage03을 찾지 못했습니다.");

        scenes.Insert(stage03Index + 1, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static GameObject CreatePanel(Transform parent, Sprite sprite, string objectName,
        Vector3 position, Vector2 size, Color color, int sortingOrder)
    {
        GameObject panel = new GameObject(objectName, typeof(SpriteRenderer));
        panel.transform.SetParent(parent, false);
        panel.transform.position = position;
        SpriteRenderer renderer = panel.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        SetWorldSize(panel.transform, sprite, size);
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return panel;
    }

    private static GameObject CreateSolid(Transform parent, Sprite sprite, string objectName,
        Vector3 position, Vector2 size, Color color)
    {
        GameObject solid = CreatePanel(parent, sprite, objectName, position, size, color, 1);
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) solid.layer = groundLayer;
        BoxCollider2D collider = solid.AddComponent<BoxCollider2D>();
        collider.size = sprite != null ? sprite.bounds.size : Vector2.one;
        return solid;
    }

    private static GameObject CreateWorldLabel(Sprite sprite, Vector3 position, Vector2 size,
        Color color, string objectName)
    {
        GameObject go = new GameObject(objectName, typeof(SpriteRenderer));
        go.transform.position = position;
        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        SetWorldSize(go.transform, sprite, size);
        renderer.color = color;
        renderer.sortingOrder = 2;
        return go;
    }

    private static void SetWorldSize(Transform target, Sprite sprite, Vector2 worldSize)
    {
        Vector2 nativeSize = sprite != null ? sprite.bounds.size : Vector2.one;
        target.localScale = new Vector3(
            worldSize.x / Mathf.Max(nativeSize.x, 0.001f),
            worldSize.y / Mathf.Max(nativeSize.y, 0.001f),
            1f);
    }
}
#endif
