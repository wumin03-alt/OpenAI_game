#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>배치 모드에서도 실제 Play Mode로 중간보스와 최종보스 그로기를 검증합니다.</summary>
public static class MiddleBossPlaytest
{
    private enum TestState
    {
        None,
        MiddleBossEntering,
        MiddleBossWaitingForRecovery,
        MiddleBossLeaving,
        FinalBossEntering,
        FinalBossWaitingForRecovery,
        FinalBossLeaving
    }

    private const string MiddleBossScene = "Assets/Scenes/MiddleBoss.unity";
    private const string FinalBossScene = "Assets/Scenes/BossArena.unity";

    private static TestState state;
    private static double stateStartedAt;
    private static string failure;
    private static bool playModeOptionsCaptured;
    private static bool previousEnterPlayModeOptionsEnabled;
    private static EnterPlayModeOptions previousEnterPlayModeOptions;

    public static void Run()
    {
        previousEnterPlayModeOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
        previousEnterPlayModeOptions = EditorSettings.enterPlayModeOptions;
        playModeOptionsCaptured = true;
        EditorSettings.enterPlayModeOptionsEnabled = true;
        EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;

        failure = null;
        ValidateSceneOrder();
        if (failure != null)
        {
            Finish(false);
            return;
        }

        EditorApplication.playModeStateChanged += HandlePlayModeChanged;
        EditorApplication.update += Tick;
        EditorSceneManager.OpenScene(MiddleBossScene, OpenSceneMode.Single);
        state = TestState.MiddleBossEntering;
        stateStartedAt = EditorApplication.timeSinceStartup;
        EditorApplication.isPlaying = true;
    }

    private static void ValidateSceneOrder()
    {
        string[] paths = EditorBuildSettings.scenes.Where(scene => scene.enabled)
            .Select(scene => scene.path).ToArray();
        int stage03 = Array.IndexOf(paths, "Assets/Scenes/Stage03.unity");
        int middle = Array.IndexOf(paths, MiddleBossScene);
        int stage04 = Array.IndexOf(paths, "Assets/Scenes/Stage04.unity");
        Require(stage03 >= 0 && middle == stage03 + 1 && stage04 == middle + 1,
            "Build Settings 순서가 Stage03 -> MiddleBoss -> Stage04가 아닙니다.");
    }

    private static void HandlePlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            if (state == TestState.MiddleBossEntering) BeginMiddleBossAssertions();
            else if (state == TestState.FinalBossEntering) BeginFinalBossAssertions();
            return;
        }

        if (change != PlayModeStateChange.EnteredEditMode) return;

        if (failure != null)
        {
            Finish(false);
            return;
        }

        if (state == TestState.MiddleBossLeaving)
        {
            EditorSceneManager.OpenScene(FinalBossScene, OpenSceneMode.Single);
            state = TestState.FinalBossEntering;
            stateStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = true;
        }
        else if (state == TestState.FinalBossLeaving)
        {
            Finish(true);
        }
    }

    private static void BeginMiddleBossAssertions()
    {
        MiddleBossController boss = UnityEngine.Object.FindAnyObjectByType<MiddleBossController>();
        PlayerController player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        Require(boss != null, "MiddleBossController가 Play Mode에서 생성되지 않았습니다.");
        Require(player != null, "PlayerController가 MiddleBoss 씬에 없습니다.");
        if (failure != null) return;

        BossStaggerGauge gauge = boss.GetComponent<BossStaggerGauge>();
        BossParryMiniGameBridge parryBridge = boss.GetComponent<BossParryMiniGameBridge>();
        ParryGroggyMiniGame parryMiniGame = boss.GetComponent<ParryGroggyMiniGame>();
        DirectionSequenceEscape escape = boss.GetComponent<DirectionSequenceEscape>();
        Health playerHealth = player.GetComponent<Health>();
        Require(gauge != null && parryBridge != null && parryMiniGame != null
                && escape != null && playerHealth != null,
            "중간보스 공용 그로기/패링 미니게임/QTE/플레이어 Health 연결이 누락됐습니다.");
        if (failure != null) return;

        Require(escape.SequenceLength == 4 && Mathf.Approximately(escape.TimeLimit, 5f),
            "QTE가 방향키 4개/제한시간 5초로 설정되지 않았습니다.");

        GameSession session = GameSession.Instance;
        if (session == null)
            session = new GameObject("RunItemSession_Playtest").AddComponent<GameSession>();
        session.ApplyToPlayer(playerHealth);
        session.AcquireItem(new RunItemOffer(RunItemType.AttackBoost, 1), playerHealth);
        session.AcquireItem(new RunItemOffer(RunItemType.AttackBoost, 1), playerHealth);
        session.AcquireItem(new RunItemOffer(RunItemType.GroggyDamageBoost, 1), playerHealth);
        session.AcquireItem(new RunItemOffer(RunItemType.ParryTimeBoost, 1), playerHealth);
        float maxBeforeItem = playerHealth.MaxHP;
        session.AcquireItem(new RunItemOffer(RunItemType.MaxHealthBoost, 20), playerHealth);
        Require(Mathf.Approximately(session.AttackDamageMultiplier, 1.3f)
                && Mathf.Approximately(session.GroggyDamagePerSuccess, 44f)
                && Mathf.Approximately(session.ParryMiniGameDuration, 4f),
            "공격/그로기/미니게임 아이템의 합산 수치가 계약과 다릅니다.");
        Require(Mathf.Approximately(playerHealth.MaxHP, maxBeforeItem + 20f),
            "최대 체력 아이템이 플레이어 최대 체력을 20 증가시키지 않았습니다.");

        ValidateCompressionDodgeGeometry(boss, player);
        if (failure != null) return;

        MethodInfo forcedImpactMethod = typeof(MiddleBossController).GetMethod(
            "ApplyForcedTransferImpact", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(forcedImpactMethod != null, "강제 이송 충돌 피해 진입점을 찾지 못했습니다.");
        if (failure != null) return;

        float hpBeforeImpact = playerHealth.CurrentHP;
        forcedImpactMethod.Invoke(boss, null);
        Require(playerHealth.CurrentHP <= hpBeforeImpact - 11.9f,
            "강제 이송으로 보스와 충돌했을 때 12 피해가 적용되지 않았습니다.");
        Require(escape.IsActive && !player.enabled,
            "강제 이송 충돌 후 기존 포획 QTE가 이어지지 않았습니다.");
        escape.Cancel(true);
        Require(player.enabled, "강제 이송 충돌 QTE 종료 후 플레이어 입력이 복구되지 않았습니다.");

        MethodInfo captureMethod = typeof(MiddleBossController).GetMethod(
            "TryCapturePlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo captureDamageField = typeof(MiddleBossController).GetField(
            "captureFailureDamage", BindingFlags.Instance | BindingFlags.NonPublic);
        Require(captureMethod != null && captureDamageField != null,
            "중간보스 포획 진입점 또는 실패 피해 설정을 찾지 못했습니다.");
        if (failure != null) return;

        float expectedCaptureDamage = (float)captureDamageField.GetValue(boss);
        float hpBeforeFailure = playerHealth.CurrentHP;
        bool captured = (bool)captureMethod.Invoke(boss, null);
        Require(captured && escape.IsActive && !player.enabled,
            "중간보스 포획 시 QTE와 플레이어 입력 잠금이 시작되지 않았습니다.");
        escape.Cancel(false);
        Require(playerHealth.CurrentHP <= hpBeforeFailure - expectedCaptureDamage + 0.1f,
            $"QTE 실패 시 설정된 큰 데미지({expectedCaptureDamage:0})가 적용되지 않았습니다.");
        Require(player.enabled, "QTE 실패 처리 후 플레이어 입력이 복구되지 않았습니다.");

        Require(escape.BeginEscape(player, playerHealth, null, null), "QTE 시작에 실패했습니다.");
        Require(!player.enabled, "QTE 중 플레이어 입력이 잠기지 않았습니다.");
        escape.Cancel(true);
        Require(player.enabled, "QTE 성공 후 플레이어 입력이 복구되지 않았습니다.");

        gauge.ApplyGroggyDamage(44f);
        Require(Mathf.Approximately(gauge.CurrentGroggy, 56f) && gauge.RemainingSegments == 2,
            "아이템 합산 그로기 피해가 연속 게이지에 반영되지 않았습니다.");
        gauge.ResetGauge();

        gauge.RegisterParry();
        Require(gauge.RemainingSegments == 2, "첫 패링 후 그로기 게이지가 1/3 감소하지 않았습니다.");
        gauge.RegisterParry();
        Require(gauge.RemainingSegments == 1, "두 번째 패링 후 그로기 게이지가 2/3 감소하지 않았습니다.");
        gauge.RegisterParry();
        Require(gauge.IsStaggered && Mathf.Approximately(gauge.StaggerDuration, 10f),
            "세 번째 패링 후 10초 그로기가 시작되지 않았습니다.");

        Time.timeScale = 20f;
        state = TestState.MiddleBossWaitingForRecovery;
        stateStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void ValidateCompressionDodgeGeometry(MiddleBossController boss,
        PlayerController player)
    {
        MethodInfo createCargo = typeof(MiddleBossController).GetMethod(
            "CreateCompressionCargo", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo overlapsPlayer = typeof(MiddleBossController).GetMethod(
            "IsCargoOverlappingPlayer", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo destroyVisual = typeof(MiddleBossController).GetMethod(
            "DestroyAttackVisual", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo setCrouchVisual = typeof(PlayerController).GetMethod(
            "SetCrouchVisual", BindingFlags.Instance | BindingFlags.NonPublic);
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();

        Require(createCargo != null && overlapsPlayer != null && destroyVisual != null
                && setCrouchVisual != null && playerCollider != null && playerBody != null,
            "교차 압축물의 점프/엎드리기 판정 테스트 진입점이 누락됐습니다.");
        if (failure != null) return;

        Vector2 originalPosition = playerBody.position;
        float groundLevel = playerCollider.bounds.min.y;

        object upperCargo = createCargo.Invoke(boss, new object[]
        {
            new Vector2(originalPosition.x, groundLevel + 1.15f), 1f, 1, 90
        });
        Physics2D.SyncTransforms();
        bool standingHitsUpper = (bool)overlapsPlayer.Invoke(boss, new[] { upperCargo });
        setCrouchVisual.Invoke(player, new object[] { true });
        Physics2D.SyncTransforms();
        bool crouchingHitsUpper = (bool)overlapsPlayer.Invoke(boss, new[] { upperCargo });
        setCrouchVisual.Invoke(player, new object[] { false });
        DestroyReflectedCargo(upperCargo, destroyVisual, boss);

        object lowerCargo = createCargo.Invoke(boss, new object[]
        {
            new Vector2(originalPosition.x, groundLevel + 0.45f), 1f, 0, 91
        });
        Physics2D.SyncTransforms();
        bool groundedHitsLower = (bool)overlapsPlayer.Invoke(boss, new[] { lowerCargo });
        playerBody.position = originalPosition + Vector2.up * 1.8f;
        Physics2D.SyncTransforms();
        bool jumpingHitsLower = (bool)overlapsPlayer.Invoke(boss, new[] { lowerCargo });
        playerBody.position = originalPosition;
        Physics2D.SyncTransforms();
        DestroyReflectedCargo(lowerCargo, destroyVisual, boss);

        Require(standingHitsUpper && !crouchingHitsUpper,
            "상·중단 압축물이 서 있는 플레이어는 맞히고 엎드린 플레이어는 통과하지 못했습니다.");
        Require(groundedHitsLower && !jumpingHitsLower,
            "하단 압축물이 지상 플레이어는 맞히고 점프한 플레이어는 통과하지 못했습니다.");
    }

    private static void DestroyReflectedCargo(object cargoFlight, MethodInfo destroyVisual,
        MiddleBossController boss)
    {
        GameObject visual = cargoFlight?.GetType().GetProperty("Visual")
            ?.GetValue(cargoFlight) as GameObject;
        if (visual != null) destroyVisual.Invoke(boss, new object[] { visual });
    }

    private static void BeginFinalBossAssertions()
    {
        BossController boss = UnityEngine.Object.FindAnyObjectByType<BossController>();
        Require(boss != null, "BossArena에서 BossController를 찾지 못했습니다.");
        if (failure != null) return;

        BossStaggerGauge gauge = boss.GetComponent<BossStaggerGauge>();
        BossStaggerHUD hud = boss.GetComponent<BossStaggerHUD>();
        BossParryMiniGameBridge parryBridge = boss.GetComponent<BossParryMiniGameBridge>();
        ParryGroggyMiniGame parryMiniGame = boss.GetComponent<ParryGroggyMiniGame>();
        Require(gauge != null && hud != null && parryBridge != null && parryMiniGame != null,
            "최종보스에 공용 그로기 게이지/HUD/패링 미니게임이 런타임 연결되지 않았습니다.");
        if (failure != null) return;

        gauge.RegisterParry();
        gauge.RegisterParry();
        gauge.RegisterParry();
        Require(gauge.IsStaggered, "최종보스 세 번째 패링 후 그로기가 시작되지 않았습니다.");
        Require(boss.State == BossController.BossState.Staggered,
            "최종보스 AI가 공용 그로기 상태를 구독하지 않았습니다.");

        Time.timeScale = 20f;
        state = TestState.FinalBossWaitingForRecovery;
        stateStartedAt = EditorApplication.timeSinceStartup;
    }

    private static void Tick()
    {
        if (state == TestState.None) return;

        double elapsed = EditorApplication.timeSinceStartup - stateStartedAt;
        if (elapsed > 30d)
        {
            Fail($"테스트 상태 {state}가 30초 안에 끝나지 않았습니다.");
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            else Finish(false);
            return;
        }

        if (state == TestState.MiddleBossWaitingForRecovery)
        {
            BossStaggerGauge gauge = UnityEngine.Object.FindAnyObjectByType<MiddleBossController>()
                ?.GetComponent<BossStaggerGauge>();
            if (gauge == null || gauge.IsStaggered) return;
            Require(gauge.RemainingSegments == 3,
                "중간보스 10초 그로기 종료 후 게이지가 3칸으로 재충전되지 않았습니다.");
            Time.timeScale = 1f;
            state = TestState.MiddleBossLeaving;
            stateStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = false;
        }
        else if (state == TestState.FinalBossWaitingForRecovery)
        {
            BossStaggerGauge gauge = UnityEngine.Object.FindAnyObjectByType<BossController>()
                ?.GetComponent<BossStaggerGauge>();
            if (gauge == null || gauge.IsStaggered) return;
            Require(gauge.RemainingSegments == 3,
                "최종보스 10초 그로기 종료 후 게이지가 3칸으로 재충전되지 않았습니다.");
            Time.timeScale = 1f;
            state = TestState.FinalBossLeaving;
            stateStartedAt = EditorApplication.timeSinceStartup;
            EditorApplication.isPlaying = false;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition && failure == null) Fail(message);
    }

    private static void Fail(string message)
    {
        failure = message;
        Debug.LogError($"[MiddleBossPlaytest] FAIL: {message}");
    }

    private static void Finish(bool passed)
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeChanged;
        EditorApplication.update -= Tick;
        Time.timeScale = 1f;
        state = TestState.None;

        if (playModeOptionsCaptured)
        {
            EditorSettings.enterPlayModeOptionsEnabled = previousEnterPlayModeOptionsEnabled;
            EditorSettings.enterPlayModeOptions = previousEnterPlayModeOptions;
            playModeOptionsCaptured = false;
        }

        if (passed && failure == null)
        {
            Debug.Log("[MiddleBossPlaytest] PASS: scene order, QTE lock/release, parry mini-game bridge, additive groggy damage, and 10-second stagger verified.");
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError($"[MiddleBossPlaytest] FAILED: {failure ?? "unknown failure"}");
            EditorApplication.Exit(1);
        }
    }
}
#endif
