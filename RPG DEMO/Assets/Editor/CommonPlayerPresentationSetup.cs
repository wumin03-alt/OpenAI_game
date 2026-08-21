#if UNITY_EDITOR
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 보스 씬에서 검증된 플레이어 아트/UI를 공통 프리팹으로 승격하고 각 게임 씬에 설치합니다.
/// 반복 실행해도 동일한 결과가 나오도록 구성합니다.
/// </summary>
public static class CommonPlayerPresentationSetup
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Player.prefab";
    private const string HudPrefabPath = "Assets/Prefabs/UI/PlayerCommonHUD.prefab";
    private const string PlayerArtRoot = "Assets/Art/Player";
    private const string PlayerSpriteRoot = PlayerArtRoot + "/Sprites";
    private const string PlayerVfxRoot = PlayerArtRoot + "/VFX";
    private const string CommonFontRoot = "Assets/Art/Common/Fonts";
    private const string FontAssetPath = CommonFontRoot + "/NotoSansKR-BossArena SDF.asset";

    private static readonly string[] TargetScenes =
    {
        "Assets/Scenes/Stage01.unity",
        "Assets/Scenes/BossArena.unity"
    };

    [MenuItem("Tools/Common Player/Apply Shared Player Art And HUD")]
    public static void Apply()
    {
        MovePlayerAssetsToCommonFolders();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ConfigurePlayerSpriteImports();
        ConfigurePlayerPrefab();
        BuildHudPrefab();

        foreach (string scenePath in TargetScenes)
            InstallIntoScene(scenePath);

        AssetDatabase.SaveAssets();
        Debug.Log("[CommonPlayerPresentationSetup] Player 아트/VFX/HUD 공통 프리팹 적용 완료");
    }

    public static void ApplyFromCommandLine()
    {
        Apply();
    }

    private static void MovePlayerAssetsToCommonFolders()
    {
        EnsureFolder("Assets/Art", "Player");
        EnsureFolder("Assets/Art", "Common");
        EnsureFolder("Assets/Art/Common", "Raw");
        EnsureFolder(PlayerArtRoot, "VFX");
        EnsureFolder(PlayerArtRoot, "Previews");
        EnsureFolder(PlayerArtRoot, "Raw");

        MoveAssetIfNeeded("Assets/Art/BossArena/Player", PlayerSpriteRoot);
        MoveAssetIfNeeded("Assets/Art/BossArena/Fonts", CommonFontRoot);

        MoveAssetIfNeeded("Assets/Art/BossArena/VFX/SPR_Player_RangedBurst.png",
            PlayerVfxRoot + "/SPR_Player_RangedBurst.png");
        MoveAssetIfNeeded("Assets/Art/BossArena/VFX/SPR_Parry_SuccessRing.png",
            PlayerVfxRoot + "/SPR_Parry_SuccessRing.png");

        MoveAssetIfNeeded("Assets/Art/BossArena/SPR_OfficeWorker_Preview.png",
            PlayerArtRoot + "/Previews/SPR_OfficeWorker_Preview.png");
        MoveAssetIfNeeded("Assets/Art/BossArena/SPR_OfficeWorker_Crouch_Preview.png",
            PlayerArtRoot + "/Previews/SPR_OfficeWorker_Crouch_Preview.png");
        MoveAssetIfNeeded("Assets/Art/BossArena/Raw/SPR_OfficeWorker_AnimationStrip_Raw.png",
            PlayerArtRoot + "/Raw/SPR_OfficeWorker_AnimationStrip_Raw.png");
        MoveAssetIfNeeded("Assets/Art/BossArena/Raw/SPR_OfficeWorker_CrouchStrip.png",
            PlayerArtRoot + "/Raw/SPR_OfficeWorker_CrouchStrip.png");
        MoveAssetIfNeeded("Assets/Art/BossArena/VFX/SPR_CombatVFX_Atlas_Raw.png",
            "Assets/Art/Common/Raw/SPR_CombatVFX_Atlas_Raw.png");
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    private static void MoveAssetIfNeeded(string source, string destination)
    {
        if (AssetDatabase.LoadMainAssetAtPath(destination) != null || !File.Exists(source) && !Directory.Exists(source))
            return;

        string error = AssetDatabase.MoveAsset(source, destination);
        if (!string.IsNullOrEmpty(error))
            throw new System.InvalidOperationException($"Asset 이동 실패: {source} -> {destination}\n{error}");
    }

    private static void ConfigurePlayerSpriteImports()
    {
        for (int i = 1; i <= 8; i++)
            ConfigureSprite($"{PlayerSpriteRoot}/SPR_OfficeWorker_Frame_{i:00}.png");
        for (int i = 1; i <= 2; i++)
            ConfigureSprite($"{PlayerSpriteRoot}/SPR_OfficeWorker_Crouch_{i:00}.png");
        ConfigureSprite(PlayerVfxRoot + "/SPR_Player_RangedBurst.png");
        ConfigureSprite(PlayerVfxRoot + "/SPR_Parry_SuccessRing.png");
    }

    private static void ConfigureSprite(string path)
    {
        if (AssetImporter.GetAtPath(path) is not TextureImporter importer) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }

    private static void ConfigurePlayerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            PlayerController player = root.GetComponent<PlayerController>();
            PlayerCombatTracker tracker = root.GetComponent<PlayerCombatTracker>();
            Rigidbody2D body = root.GetComponent<Rigidbody2D>();
            SpriteRenderer renderer = root.GetComponentInChildren<SpriteRenderer>(true);
            if (player == null || tracker == null || renderer == null)
                throw new System.InvalidOperationException("Player.prefab의 필수 컴포넌트를 찾지 못했습니다.");

            renderer.sprite = LoadPlayerFrames("SPR_OfficeWorker_Frame_", 8)[0];
            renderer.sortingOrder = 5;
            renderer.transform.localPosition = new Vector3(0f, -0.72f, 0f);
            renderer.transform.localScale = new Vector3(0.78f, 0.78f, 1f);

            foreach (BossArenaSpriteAnimator oldAnimator in root.GetComponentsInChildren<BossArenaSpriteAnimator>(true))
                Object.DestroyImmediate(oldAnimator);

            PlayerSpriteAnimator animator = renderer.GetComponent<PlayerSpriteAnimator>();
            if (animator == null) animator = renderer.gameObject.AddComponent<PlayerSpriteAnimator>();
            Sprite[] frames = LoadPlayerFrames("SPR_OfficeWorker_Frame_", 8);
            Sprite[] crouch = LoadPlayerFrames("SPR_OfficeWorker_Crouch_", 2);
            animator.Configure(renderer, player, body, frames.Take(2).ToArray(),
                frames.Skip(2).Take(4).ToArray(), frames.Skip(6).Take(2).ToArray(), crouch);

            PlayerCombatVFX vfx = root.GetComponent<PlayerCombatVFX>();
            if (vfx == null) vfx = root.AddComponent<PlayerCombatVFX>();
            vfx.Configure(player, tracker,
                AssetDatabase.LoadAssetAtPath<Sprite>(PlayerVfxRoot + "/SPR_Player_RangedBurst.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>(PlayerVfxRoot + "/SPR_Parry_SuccessRing.png"));

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite[] LoadPlayerFrames(string prefix, int count)
    {
        return Enumerable.Range(1, count)
            .Select(i => AssetDatabase.LoadAssetAtPath<Sprite>($"{PlayerSpriteRoot}/{prefix}{i:00}.png"))
            .ToArray();
    }

    private static void BuildHudPrefab()
    {
        EnsureFolder("Assets/Prefabs", "UI");
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (font == null) throw new System.InvalidOperationException("공통 HUD용 Noto Sans KR 폰트를 찾지 못했습니다.");

        GameObject root = new GameObject("PlayerCommonHUD", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PlayerCommonHUD));
        try
        {
            root.layer = 5;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 60;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            HealthBarUI healthBar = BuildPlayerHealthBar(root.transform, font);
            BuildControlsBadge(root.transform, font);
            TMP_Text notice = BuildParryNotice(root.transform, font);
            root.GetComponent<PlayerCommonHUD>().Configure(healthBar, notice);

            PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static HealthBarUI BuildPlayerHealthBar(Transform parent, TMP_FontAsset font)
    {
        GameObject bar = CreateImage(parent, "PlayerHPBar", new Color(0.025f, 0.045f, 0.075f, 0.94f));
        RectTransform rect = bar.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(48f, -70f);
        rect.sizeDelta = new Vector2(360f, 30f);

        GameObject fill = CreateImage(bar.transform, "Fill", new Color(0.22f, 0.95f, 0.68f));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        Stretch(fillRect, 4f);
        Image fillImage = fill.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;

        TMP_Text title = CreateText(bar.transform, "PlayerTitle", "해고된 직장인 · 생존력", font,
            20f, new Color(0.92f, 0.96f, 1f), TextAlignmentOptions.Left);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 0f);
        titleRect.anchoredPosition = new Vector2(0f, 12f);
        titleRect.sizeDelta = new Vector2(360f, 28f);

        TMP_Text value = CreateText(bar.transform, "Value", "100 / 100", font,
            17f, Color.white, TextAlignmentOptions.Center);
        Stretch(value.rectTransform, 0f);

        HealthBarUI healthBar = bar.AddComponent<HealthBarUI>();
        SerializedObject serialized = new SerializedObject(healthBar);
        serialized.FindProperty("fillImage").objectReferenceValue = fillImage;
        serialized.FindProperty("label").objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        return healthBar;
    }

    private static void BuildControlsBadge(Transform parent, TMP_FontAsset font)
    {
        GameObject frame = CreateImage(parent, "PlayerControls", new Color(0.08f, 0.7f, 0.86f, 0.88f));
        RectTransform rect = frame.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 22f);
        rect.sizeDelta = new Vector2(1080f, 62f);

        GameObject inner = CreateImage(frame.transform, "Inner", new Color(0.006f, 0.018f, 0.043f, 0.96f));
        Stretch(inner.GetComponent<RectTransform>(), 3f);

        TMP_Text text = CreateText(frame.transform, "ControlsText",
            "COMBAT INPUT     ← →  이동     ↑  점프     ↓  엎드리기     Q  근접     W  원거리     E  패링     R  대시",
            font, 18f, new Color(0.86f, 0.96f, 1f), TextAlignmentOptions.Center);
        Stretch(text.rectTransform, 10f);
        text.fontStyle = FontStyles.Bold;
    }

    private static TMP_Text BuildParryNotice(Transform parent, TMP_FontAsset font)
    {
        TMP_Text notice = CreateText(parent, "ParryNotice", "PARRY SUCCESS  //  COUNTER",
            font, 31f, Color.clear, TextAlignmentOptions.Center);
        RectTransform rect = notice.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, 190f);
        rect.sizeDelta = new Vector2(860f, 64f);
        notice.fontStyle = FontStyles.Bold;
        return notice;
    }

    private static void InstallIntoScene(string scenePath)
    {
        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        RemoveSceneObjects(scene, "PlayerHPBar", "BossArena_KoreanControls");

        PlayerController player = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerController>(true)).FirstOrDefault();
        if (player == null) throw new System.InvalidOperationException($"{scenePath}에서 Player를 찾지 못했습니다.");

        SpriteRenderer renderer = player.GetComponentInChildren<SpriteRenderer>(true);
        if (renderer != null)
        {
            if (PrefabUtility.IsPartOfPrefabInstance(renderer))
                PrefabUtility.RevertObjectOverride(renderer, InteractionMode.AutomatedAction);
            if (PrefabUtility.IsPartOfPrefabInstance(renderer.transform))
                PrefabUtility.RevertObjectOverride(renderer.transform, InteractionMode.AutomatedAction);
        }

        foreach (BossArenaSpriteAnimator oldAnimator in player.GetComponentsInChildren<BossArenaSpriteAnimator>(true))
            Object.DestroyImmediate(oldAnimator);

        foreach (Image image in player.GetComponents<Image>())
        {
            if (PrefabUtility.IsAddedComponentOverride(image)) Object.DestroyImmediate(image);
        }

        bool hasCommonHud = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerCommonHUD>(true)).Any();
        if (!hasCommonHud)
        {
            GameObject hudPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);
            PrefabUtility.InstantiatePrefab(hudPrefab, scene);
        }

        if (scenePath.EndsWith("BossArena.unity"))
        {
            BossController boss = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BossController>(true)).FirstOrDefault();
            BossArenaCombatVFX bossVfx = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<BossArenaCombatVFX>(true)).FirstOrDefault();
            if (bossVfx != null)
            {
                bossVfx.Configure(boss,
                    AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/BossArena/VFX/SPR_Boss_CoreCharge.png"));
                EditorUtility.SetDirty(bossVfx);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void RemoveSceneObjects(Scene scene, params string[] names)
    {
        foreach (GameObject gameObject in scene.GetRootGameObjects()
                     .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                     .Select(transform => transform.gameObject)
                     .Where(gameObject => names.Contains(gameObject.name) &&
                                          gameObject.GetComponentInParent<PlayerCommonHUD>(true) == null)
                     .ToArray())
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static GameObject CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string value,
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

    private static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }
}
#endif
