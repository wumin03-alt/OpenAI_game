#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class BossArenaPresentationSetup
{
    private const string ScenePath = "Assets/Scenes/BossArena.unity";
    private const string ArtRoot = "Assets/Art/BossArena";
    private const string FontSourcePath = "Assets/Art/Common/Fonts/NotoSansKR-Variable.ttf";
    private const string FontAssetPath = "Assets/Art/Common/Fonts/NotoSansKR-BossArena SDF.asset";

    [MenuItem("Tools/Boss Arena/Apply Korean AI Core Presentation")]
    public static void Apply()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigureImports();
        TMP_FontAsset koreanFont = EnsureKoreanFont();

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        RemovePreviousPresentation(scene);

        BuildEnvironment(scene);
        ConfigureActors(scene);
        ConfigureUi(scene, koreanFont);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[BossArenaPresentationSetup] 한국어 AI 코어룸 프레젠테이션 적용 완료");
    }

    private static void ConfigureImports()
    {
        ConfigureSprite(ArtRoot + "/Environment/SPR_AICoreChamber_Background.png", 67.2f, new Vector2(0.5f, 0.5f), 2048);

        for (int i = 1; i <= 6; i++)
            ConfigureSprite($"{ArtRoot}/Boss/SPR_CyberDragon_Frame_{i:00}.png", 100f, new Vector2(0.5f, 0f), 1024);
        for (int i = 1; i <= 3; i++)
            ConfigureSprite($"{ArtRoot}/Platforms/SPR_AICorePlatform_{i:00}.png", 100f, new Vector2(0.5f, 0f), 1024);

        ConfigureSprite(ArtRoot + "/VFX/SPR_Boss_CoreCharge.png", 100f, new Vector2(0.5f, 0.5f), 1024);
    }

    private static void ConfigureSprite(string path, float pixelsPerUnit, Vector2 pivot, int maxSize)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        TextureImporterSettings spriteSettings = new TextureImporterSettings();
        importer.ReadTextureSettings(spriteSettings);
        spriteSettings.spriteAlignment = (int)SpriteAlignment.Custom;
        spriteSettings.spritePivot = pivot;
        importer.SetTextureSettings(spriteSettings);
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = maxSize;
        importer.SaveAndReimport();
    }

    private static TMP_FontAsset EnsureKoreanFont()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null) return existing;

        Font source = AssetDatabase.LoadAssetAtPath<Font>(FontSourcePath);
        if (source == null) throw new System.InvalidOperationException("Noto Sans KR 폰트를 찾지 못했습니다.");

        TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(source, 72, 8, GlyphRenderMode.SDFAA,
            2048, 2048, AtlasPopulationMode.Dynamic, true);
        asset.name = "NotoSansKR-BossArena SDF";
        asset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        asset.isMultiAtlasTexturesEnabled = true;

        AssetDatabase.CreateAsset(asset, FontAssetPath);
        asset.atlasTexture.name = asset.name + " Atlas";
        asset.material.name = asset.name + " Material";
        AssetDatabase.AddObjectToAsset(asset.atlasTexture, asset);
        AssetDatabase.AddObjectToAsset(asset.material, asset);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        return asset;
    }

    private static void RemovePreviousPresentation(Scene scene)
    {
        GameObject old = FindSceneObject(scene, "BossArenaPresentation");
        if (old != null) Object.DestroyImmediate(old);
    }

    private static void BuildEnvironment(Scene scene)
    {
        GameObject root = new GameObject("BossArenaPresentation");
        SceneManager.MoveGameObjectToScene(root, scene);

        GameObject background = new GameObject("AI_Core_Chamber_Background");
        background.transform.SetParent(root.transform, false);
        background.transform.position = new Vector3(0f, 0f, 4f);
        SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
        backgroundRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Environment/SPR_AICoreChamber_Background.png");
        backgroundRenderer.sortingOrder = -50;

        Transform platforms = new GameObject("AttackPlatforms").transform;
        platforms.SetParent(root.transform, false);
        // Player의 최대 점프 정점은 약 2.87u(15 / gravity 4)지만 짧게 누르면 상승 속도가
        // 즉시 45%로 줄어듭니다. 짧은 입력도 허용하도록 지면→측면 단차를 약 1.1u,
        // 측면→중앙 단차를 1.5u로 제한합니다.
        CreatePlatform(platforms, 1, new Vector2(-5.6f, -2.10f));
        CreatePlatform(platforms, 2, new Vector2(0f, -0.60f));
        CreatePlatform(platforms, 3, new Vector2(5.6f, -2.10f));
    }

    private static void CreatePlatform(Transform parent, int variant, Vector2 surfacePosition)
    {
        string path = $"{ArtRoot}/Platforms/SPR_AICorePlatform_{variant:00}.png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        float visualHeight = sprite != null ? sprite.bounds.size.y : 2.4f;
        const float alphaTopPadding = 0.04f;

        GameObject platform = new GameObject($"AI_Core_Platform_{variant:00}");
        platform.transform.SetParent(parent, false);
        platform.transform.position = new Vector3(
            surfacePosition.x,
            surfacePosition.y - visualHeight + alphaTopPadding,
            0f);
        int groundLayer = LayerMask.NameToLayer("Ground");
        platform.layer = groundLayer >= 0 ? groundLayer : 6;

        SpriteRenderer renderer = platform.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 2;

        BoxCollider2D collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(4.65f, 0.34f);
        collider.offset = new Vector2(0f, visualHeight - alphaTopPadding - 0.17f);
        collider.usedByEffector = true;

        PlatformEffector2D effector = platform.AddComponent<PlatformEffector2D>();
        effector.surfaceArc = 165f;
        effector.useSideFriction = false;
        effector.useSideBounce = false;
    }

    private static void ConfigureActors(Scene scene)
    {
        BossController boss = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<BossController>(true)).FirstOrDefault();
        if (boss != null)
        {
            SpriteRenderer renderer = boss.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer != null)
            {
                Sprite[] frames = LoadFrames("Boss/SPR_CyberDragon_Frame_", 6);
                renderer.sprite = frames[0];
                renderer.color = Color.white;
                renderer.sortingOrder = 4;
                renderer.transform.localPosition = new Vector3(0f, -2.05f, 0f);
                renderer.transform.localScale = new Vector3(1.32f, 1.32f, 1f);

                BossArenaSpriteAnimator animator = renderer.GetComponent<BossArenaSpriteAnimator>();
                if (animator == null) animator = renderer.gameObject.AddComponent<BossArenaSpriteAnimator>();
                animator.ConfigureBoss(renderer, boss, frames.Take(2).ToArray(),
                    frames.Skip(2).Take(2).ToArray(), frames.Skip(4).Take(2).ToArray());
            }

            SerializedObject serializedBoss = new SerializedObject(boss);
            serializedBoss.FindProperty("flipTowardPlayer").boolValue = false;
            serializedBoss.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject presentation = FindSceneObject(scene, "BossArenaPresentation");
        if (presentation != null)
        {
            BossArenaCombatVFX vfx = presentation.AddComponent<BossArenaCombatVFX>();
            vfx.Configure(boss,
                AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/VFX/SPR_Boss_CoreCharge.png"));
        }
    }

    private static Sprite[] LoadFrames(string relativePrefix, int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{relativePrefix}{i:00}.png"))
            .ToArray();
    }

    private static void ConfigureUi(Scene scene, TMP_FontAsset font)
    {
        Canvas canvas = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Canvas>(true)).FirstOrDefault();
        if (canvas == null) return;

        // Keep this scene generator safe to run repeatedly from the editor menu.
        foreach (Transform generated in canvas.GetComponentsInChildren<Transform>(true)
                     .Where(item => item != canvas.transform && item.name.StartsWith("BossArena_"))
                     .ToArray())
        {
            if (generated != null)
            {
                Object.DestroyImmediate(generated.gameObject);
            }
        }

        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        foreach (TMP_Text text in canvas.GetComponentsInChildren<TMP_Text>(true))
        {
            text.font = font;
            text.raycastTarget = false;
        }

        SetText(scene, "BossName", "AI 코어 수호자 · 사이버 드래곤", 22f, new Color(0.88f, 0.96f, 1f));
        SetText(scene, "PhaseLabel", "PHASE 01  /  전투 습관 분석 중", 19f, new Color(0.16f, 0.9f, 1f));

        AnalysisUI analysis = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<AnalysisUI>(true)).FirstOrDefault();
        if (analysis != null)
        {
            SerializedObject so = new SerializedObject(analysis);
            so.FindProperty("titleLine").stringValue = "root@ai-core:~/combat $ analyze --live";
            so.FindProperty("loadingLine").stringValue = "$ countermeasure --build";
            so.FindProperty("completeLine").stringValue = "[OK] 대응 프로토콜 배포 완료";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        StyleHealthBar(scene, "BossHPBar", new Vector2(0f, -42f), new Vector2(940f, 34f),
            null, font, new Color(1f, 0.22f, 0.45f), new Color(0.52f, 0.08f, 0.18f));
        StyleAnalysisPanel(scene);
    }

    private static void SetText(Scene scene, string objectName, string value, float size, Color color)
    {
        GameObject go = FindSceneObject(scene, objectName);
        TMP_Text text = go != null ? go.GetComponent<TMP_Text>() : null;
        if (text == null) return;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.fontStyle = FontStyles.Bold;
    }

    private static void StyleHealthBar(Scene scene, string name, Vector2 position, Vector2 size,
        string title, TMP_FontAsset font, Color highColor, Color lowColor)
    {
        GameObject go = FindSceneObject(scene, name);
        if (go == null) return;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image frame = go.GetComponent<Image>();
        if (frame != null)
        {
            frame.color = new Color(0.08f, 0.78f, 0.92f, 0.96f);
            frame.raycastTarget = false;
        }

        GameObject plate = CreateUiImage(go.transform, "BossArena_InnerPlate", new Color(0.008f, 0.02f, 0.05f, 0.98f));
        Stretch(plate.GetComponent<RectTransform>(), 3f);
        plate.transform.SetAsFirstSibling();

        HealthBarUI healthBar = go.GetComponent<HealthBarUI>();
        if (healthBar != null)
        {
            SerializedObject so = new SerializedObject(healthBar);
            so.FindProperty("highColor").colorValue = highColor;
            so.FindProperty("lowColor").colorValue = lowColor;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        AddCornerGem(go.transform, new Vector2(2f, -2f), new Color(0.15f, 0.9f, 1f));
        AddCornerGem(go.transform, new Vector2(size.x - 2f, -2f), new Color(0.72f, 0.22f, 1f));
        AddBarTicks(go.transform, size.x, size.y);

        if (string.IsNullOrEmpty(title)) return;
        TextMeshProUGUI titleText = CreateUiText(go.transform, "BossArena_PlayerTitle", title, font, 20f,
            new Color(0.92f, 0.96f, 1f), TextAlignmentOptions.Left);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 12f);
        titleRect.sizeDelta = new Vector2(size.x, 26f);
        titleText.fontStyle = FontStyles.Bold;
    }

    private static void BuildControlsBadge(Transform parent, TMP_FontAsset font)
    {
        GameObject frame = CreateUiImage(parent, "BossArena_KoreanControls", new Color(0.08f, 0.7f, 0.86f, 0.88f));
        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 22f);
        rect.sizeDelta = new Vector2(1080f, 62f);

        GameObject inner = CreateUiImage(frame.transform, "Inner", new Color(0.006f, 0.018f, 0.043f, 0.96f));
        Stretch(inner.GetComponent<RectTransform>(), 3f);

        TextMeshProUGUI header = CreateUiText(frame.transform, "InputHeader", "COMBAT INPUT",
            font, 11f, new Color(0.26f, 0.76f, 0.9f), TextAlignmentOptions.Left);
        SetRect(header.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 0f), new Vector2(90f, 42f));
        header.fontStyle = FontStyles.Bold;

        CreateControlChip(frame.transform, 132f, 138f, "← →", "이동", font, new Color(0.2f, 0.86f, 1f));
        CreateControlChip(frame.transform, 270f, 126f, "↑", "점프", font, new Color(0.2f, 0.86f, 1f));
        CreateControlChip(frame.transform, 396f, 154f, "Q", "근접", font, new Color(0.38f, 1f, 0.72f));
        CreateControlChip(frame.transform, 550f, 164f, "W", "원거리", font, new Color(0.2f, 0.86f, 1f));
        CreateControlChip(frame.transform, 714f, 146f, "E", "패링", font, new Color(0.72f, 0.44f, 1f));
        CreateControlChip(frame.transform, 860f, 130f, "R", "대시", font, new Color(1f, 0.38f, 0.56f));
    }

    private static void CreateControlChip(Transform parent, float x, float width, string key,
        string label, TMP_FontAsset font, Color accent)
    {
        GameObject group = CreateUiImage(parent, "ControlChip", new Color(0.03f, 0.07f, 0.13f, 0.96f));
        RectTransform rect = group.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
        rect.sizeDelta = new Vector2(width - 7f, 42f);

        GameObject keycap = CreateUiImage(group.transform, "Keycap", accent);
        RectTransform keyRect = keycap.GetComponent<RectTransform>();
        keyRect.anchorMin = keyRect.anchorMax = new Vector2(0f, 0.5f);
        keyRect.pivot = new Vector2(0f, 0.5f);
        keyRect.anchoredPosition = new Vector2(5f, 0f);
        keyRect.sizeDelta = new Vector2(key.Length > 1 ? 48f : 34f, 32f);

        TextMeshProUGUI keyText = CreateUiText(keycap.transform, "Key", key, font, 15f,
            new Color(0.015f, 0.035f, 0.07f), TextAlignmentOptions.Center);
        Stretch(keyText.rectTransform, 1f);
        keyText.fontStyle = FontStyles.Bold;

        TextMeshProUGUI labelText = CreateUiText(group.transform, "Label", label, font, 15f,
            new Color(0.86f, 0.94f, 1f), TextAlignmentOptions.Left);
        RectTransform labelRect = labelText.rectTransform;
        labelRect.anchorMin = labelRect.anchorMax = new Vector2(0f, 0.5f);
        labelRect.pivot = new Vector2(0f, 0.5f);
        labelRect.anchoredPosition = new Vector2(key.Length > 1 ? 61f : 47f, 0f);
        labelRect.sizeDelta = new Vector2(width - 62f, 34f);
        labelText.fontStyle = FontStyles.Bold;
    }

    private static void AddBarTicks(Transform parent, float width, float height)
    {
        for (int i = 1; i < 10; i++)
        {
            GameObject tick = CreateUiImage(parent, "BarTick", new Color(0.55f, 0.82f, 0.92f, 0.2f));
            RectTransform rect = tick.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(width * i / 10f, 0f);
            rect.sizeDelta = new Vector2(1f, Mathf.Max(8f, height - 10f));
        }
    }

    private static void StyleAnalysisPanel(Scene scene)
    {
        GameObject panel = FindSceneObject(scene, "AnalysisPanel");
        if (panel == null) return;
        Image dim = panel.GetComponent<Image>();
        if (dim != null) dim.color = new Color(0.002f, 0.008f, 0.022f, 0.9f);

        GameObject frame = CreateUiImage(panel.transform, "BossArena_AnalysisFrame", new Color(0.08f, 0.82f, 0.94f, 0.98f));
        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1180f, 700f);
        frame.transform.SetAsFirstSibling();

        GameObject inner = CreateUiImage(frame.transform, "Inner", new Color(0.006f, 0.018f, 0.04f, 0.985f));
        Stretch(inner.GetComponent<RectTransform>(), 4f);

        GameObject header = CreateUiImage(frame.transform, "BossArena_AnalysisHeader", new Color(0.018f, 0.09f, 0.16f, 0.99f));
        RectTransform headerRect = header.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = Vector2.one;
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -4f);
        headerRect.sizeDelta = new Vector2(-8f, 64f);

        AddTerminalDot(frame.transform, new Vector2(22f, -32f), new Color(1f, 0.3f, 0.4f));
        AddTerminalDot(frame.transform, new Vector2(44f, -32f), new Color(1f, 0.72f, 0.24f));
        AddTerminalDot(frame.transform, new Vector2(66f, -32f), new Color(0.22f, 0.95f, 0.66f));

        GameObject divider = CreateUiImage(frame.transform, "BossArena_AnalysisDivider", new Color(0.08f, 0.62f, 0.76f, 0.42f));
        RectTransform dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.anchoredPosition = new Vector2(12f, -22f);
        dividerRect.sizeDelta = new Vector2(2f, 470f);

        TextMeshProUGUI footer = CreateUiText(frame.transform, "BossArena_AnalysisFooter",
            "telemetry://player/session   |   status: STREAMING   |   integrity-check: ACTIVE",
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath), 16f,
            new Color(0.32f, 0.72f, 0.82f), TextAlignmentOptions.Center);
        SetRect(footer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -305f), new Vector2(1080f, 28f));

        StyleTerminalText(scene, "TitleText", new Vector2(0f, 286f), new Vector2(1000f, 44f),
            28f, new Color(0.28f, 1f, 0.68f), TextAlignmentOptions.Left, false, true);
        StyleTerminalText(scene, "StatsText", new Vector2(-295f, -14f), new Vector2(500f, 430f),
            21f, new Color(0.78f, 0.9f, 1f), TextAlignmentOptions.TopLeft, false, false);
        StyleTerminalText(scene, "StyleLabel", new Vector2(280f, 132f), new Vector2(430f, 34f),
            18f, new Color(0.3f, 0.82f, 0.95f), TextAlignmentOptions.Left, false, false);
        StyleTerminalText(scene, "StyleValue", new Vector2(280f, 72f), new Vector2(430f, 66f),
            38f, new Color(0.2f, 0.88f, 1f), TextAlignmentOptions.Left, false, true);
        StyleTerminalText(scene, "ProtocolText", new Vector2(280f, -72f), new Vector2(430f, 190f),
            21f, new Color(0.3f, 1f, 0.62f), TextAlignmentOptions.TopLeft, true, false);
    }

    private static void StyleTerminalText(Scene scene, string objectName, Vector2 position, Vector2 size,
        float fontSize, Color color, TextAlignmentOptions alignment, bool wrap, bool bold)
    {
        GameObject go = FindSceneObject(scene, objectName);
        TMP_Text text = go != null ? go.GetComponent<TMP_Text>() : null;
        RectTransform rect = go != null ? go.GetComponent<RectTransform>() : null;
        if (text == null || rect == null) return;

        SetRect(rect, new Vector2(0.5f, 0.5f), position, size);
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.lineSpacing = objectName == "StatsText" ? 14f : 4f;
        text.raycastTarget = false;
    }

    private static GameObject CreateUiImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static TextMeshProUGUI CreateUiText(Transform parent, string name, string value,
        TMP_FontAsset font, float size, Color color, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void AddCornerGem(Transform parent, Vector2 position, Color color)
    {
        GameObject gem = CreateUiImage(parent, "BossArena_CornerGem", color);
        RectTransform rect = gem.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(13f, 13f);
        rect.localEulerAngles = new Vector3(0f, 0f, 45f);
    }

    private static void AddTerminalDot(Transform parent, Vector2 position, Color color)
    {
        GameObject dot = CreateUiImage(parent, "BossArena_TerminalDot", color);
        RectTransform rect = dot.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(11f, 11f);
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    private static GameObject FindSceneObject(Scene scene, string name)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => transform.gameObject)
            .FirstOrDefault(go => go.name == name);
    }
}
#endif
