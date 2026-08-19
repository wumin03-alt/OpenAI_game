using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메뉴 또는 batch mode에서 Stage 02~04 씬과 Build Settings를 재생성합니다.
/// 프리팹 연결은 이 코드가 처리하므로 Inspector 수동 설정이 필요 없습니다.
/// </summary>
public static class StageSceneGenerator
{
    private const string Stage01Path = "Assets/Scenes/Stage01.unity";
    private const string Stage02Path = "Assets/Scenes/Stage_02_Combat.unity";
    private const string Stage03Path = "Assets/Scenes/Stage_03_Defense.unity";
    private const string Stage04Path = "Assets/Scenes/Stage_04_Minigame.unity";
    private const string BossPath = "Assets/Scenes/BossArena.unity";

    [MenuItem("Tools/RPG/Generate Stage 01-05 Flow")]
    public static void GenerateAll()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab");
        GameObject enemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Grunt.prefab");
        if (playerPrefab == null || enemyPrefab == null)
        {
            Debug.LogError("[StageSceneGenerator] Player 또는 Grunt 프리팹을 찾지 못했습니다.");
            return;
        }

        CreateArenaScene(Stage02Path, "Stage 02 - Defense", StageArenaController.StageMode.Combat,
                         playerPrefab, enemyPrefab, "Stage_03_Defense");
        CreateArenaScene(Stage03Path, "Stage 03 - Enemy Assault", StageArenaController.StageMode.Defense,
                         playerPrefab, enemyPrefab, "Stage_04_Minigame");
        CreateArenaScene(Stage04Path, "Stage 04 - Target Training", StageArenaController.StageMode.Minigame,
                         playerPrefab, enemyPrefab, "BossArena");
        UpdateTutorialScene();
        UpdateBuildSettings();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[StageSceneGenerator] Stage 01 → 02 → 03 → 04 → BossArena flow is ready.");
    }

    private static void CreateArenaScene(string path, string sceneName, StageArenaController.StageMode mode,
                                         GameObject playerPrefab, GameObject enemyPrefab, string nextScene)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        GameObject root = new GameObject(sceneName);
        StageArenaController controller = root.AddComponent<StageArenaController>();
        controller.Configure(mode, playerPrefab, enemyPrefab, nextScene);
        EditorSceneManager.SaveScene(scene, path);
    }

    private static void UpdateTutorialScene()
    {
        Scene scene = EditorSceneManager.OpenScene(Stage01Path, OpenSceneMode.Single);
        StageExit exit = Object.FindFirstObjectByType<StageExit>();
        if (exit == null)
        {
            Debug.LogError("[StageSceneGenerator] Stage01의 StageExit을 찾지 못했습니다.");
            return;
        }

        SerializedObject serializedExit = new SerializedObject(exit);
        serializedExit.FindProperty("nextSceneName").stringValue = "Stage_02_Combat";
        serializedExit.FindProperty("requireAllEnemiesDead").boolValue = true;
        serializedExit.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene);
    }

    private static void UpdateBuildSettings()
    {
        EditorBuildSettings.scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(Stage01Path, true),
            new EditorBuildSettingsScene(Stage02Path, true),
            new EditorBuildSettingsScene(Stage03Path, true),
            new EditorBuildSettingsScene(Stage04Path, true),
            new EditorBuildSettingsScene(BossPath, true)
        }.ToArray();
    }
}
