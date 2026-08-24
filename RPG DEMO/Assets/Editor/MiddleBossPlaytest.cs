#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Game.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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
        string[] expectedFlow =
        {
            "Assets/Scenes/Stage03.unity",
            MiddleBossScene,
            "Assets/Scenes/Stage05.unity",
            "Assets/Scenes/Stage06.unity",
            "Assets/Scenes/Stage07.unity",
            FinalBossScene
        };

        int startIndex = Array.IndexOf(paths, expectedFlow[0]);
        bool isContiguous = startIndex >= 0 &&
                            startIndex + expectedFlow.Length <= paths.Length &&
                            expectedFlow.SequenceEqual(paths.Skip(startIndex).Take(expectedFlow.Length));
        bool removedScenesAbsent = Array.IndexOf(paths, "Assets/Scenes/Stage04.unity") < 0 &&
                                   Array.IndexOf(paths, "Assets/Scenes/Stage08.unity") < 0;

        Require(isContiguous && removedScenesAbsent,
            "Build Settings 순서가 Stage03 -> MiddleBoss -> Stage05 -> Stage06 -> Stage07 -> BossArena가 아니거나 삭제된 Stage04/Stage08이 남아 있습니다.");
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
        Health bossHealth = boss.GetComponent<Health>();
        Health playerHealth = player.GetComponent<Health>();
        Require(gauge != null && parryBridge != null && parryMiniGame != null
                && escape != null && bossHealth != null && playerHealth != null,
            "중간보스 공용 그로기/패링 미니게임/QTE/플레이어 Health 연결이 누락됐습니다.");
        if (failure != null) return;
        ValidateGroggyHudLayout(boss.GetComponent<BossStaggerHUD>(), 790f, -131f);
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

        float bossHealthBeforeAttack = bossHealth.CurrentHP;
        bossHealth.TakeDamage(10f, true);
        Require(Mathf.Approximately(bossHealth.CurrentHP, bossHealthBeforeAttack - 10f)
                && Mathf.Approximately(gauge.CurrentGroggy,
                    gauge.MaxGroggy - gauge.NormalHitGroggyDamage)
                && gauge.NormalHitsRequired == 50,
            "일반 공격 그로기가 적중당 고정값으로 감소하거나 50회 조건을 만족하지 않았습니다.");
        gauge.ResetGauge();

        ValidateNutrientBlockReflection(boss, player, gauge, parryMiniGame);
        Require(gauge.IsStaggered && Mathf.Approximately(gauge.StaggerDuration, 10f),
            "반사된 영양 블록 두 발로 10초 그로기가 시작되지 않았습니다.");

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

    private static void ValidateNutrientBlockReflection(MiddleBossController boss,
        PlayerController player, BossStaggerGauge gauge, ParryGroggyMiniGame miniGame)
    {
        FieldInfo parryState = typeof(PlayerController).GetField(
            "<IsParrying>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo trigger = typeof(NutrientBlockProjectile).GetMethod(
            "OnTriggerEnter2D", BindingFlags.Instance | BindingFlags.NonPublic);
        Collider2D playerCollider = player.GetComponent<Collider2D>();
        Collider2D bossCollider = boss.GetComponent<Collider2D>();
        Health bossHealth = boss.GetComponent<Health>();
        BossStaggerHUD hud = boss.GetComponent<BossStaggerHUD>();
        Require(parryState != null && trigger != null && playerCollider != null
                && bossCollider != null && bossHealth != null && hud != null,
            "영양 블록 반사 테스트 진입점 또는 충돌체가 누락됐습니다.");
        if (failure != null) return;

        float healthBeforeReflection = bossHealth.CurrentHP;
        for (int hit = 0; hit < 2; hit++)
        {
            GameObject block = new GameObject($"NutrientBlockReflectionTest_{hit + 1}");
            Rigidbody2D body = block.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            BoxCollider2D collider = block.AddComponent<BoxCollider2D>();
            collider.isTrigger = true;
            NutrientBlockProjectile projectile = block.AddComponent<NutrientBlockProjectile>();
            projectile.Initialize(gauge, Vector2.left, 7.2f, 9f);

            parryState.SetValue(player, true);
            trigger.Invoke(projectile, new object[] { playerCollider });
            parryState.SetValue(player, false);

            Require(projectile.IsReflected && !miniGame.IsActive,
                "영양 블록 패링 시 미니게임 없이 보스 방향으로 반사되지 않았습니다.");
            if (failure != null)
            {
                UnityEngine.Object.Destroy(block);
                return;
            }

            trigger.Invoke(projectile, new object[] { bossCollider });
            float expected = hit == 0 ? gauge.MaxGroggy * 0.5f : 0f;
            Require(Mathf.Approximately(gauge.CurrentGroggy, expected),
                $"반사 영양 블록 {hit + 1}회 명중 후 그로기 게이지가 50%씩 감소하지 않았습니다.");
            Require(Mathf.Approximately(
                    bossHealth.CurrentHP,
                    healthBeforeReflection - projectile.ReflectedBossDamage * (hit + 1)),
                $"반사 영양 블록 {hit + 1}회 명중 후 보스 HP 피해가 적용되지 않았습니다.");
            ValidateGroggyHudFill(hud, hit == 0 ? 0.5f : 0f);
            if (failure != null) return;
        }
    }

    private static void ValidateGroggyHudLayout(BossStaggerHUD hud,
        float expectedWidth, float expectedY)
    {
        Require(hud != null, "보스 HP 아래의 연속형 그로기 HUD가 생성되지 않았습니다.");
        if (failure != null) return;

        FieldInfo trackField = typeof(BossStaggerHUD).GetField(
            "track", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo fillField = typeof(BossStaggerHUD).GetField(
            "fill", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo trailField = typeof(BossStaggerHUD).GetField(
            "damageTrail", BindingFlags.Instance | BindingFlags.NonPublic);
        Image track = trackField?.GetValue(hud) as Image;
        Image fill = fillField?.GetValue(hud) as Image;
        Image damageTrail = trailField?.GetValue(hud) as Image;

        Require(track != null && fill != null && damageTrail != null,
            "보스 HP 아래의 연속형 그로기 HUD가 생성되지 않았습니다.");
        if (failure != null) return;

        Require(Mathf.Approximately(track.rectTransform.sizeDelta.x, expectedWidth)
                && Mathf.Approximately(track.rectTransform.sizeDelta.y, 14f)
                && Mathf.Approximately(track.rectTransform.anchoredPosition.y, expectedY)
                && fill.type == Image.Type.Simple
                && Mathf.Approximately(fill.rectTransform.anchorMax.x, 1f),
            "그로기 HUD가 보스 HP 아래의 얇은 연속형 게이지 규격과 일치하지 않습니다.");
    }

    private static void ValidateGroggyHudFill(BossStaggerHUD hud, float expectedNormalized)
    {
        Require(hud != null, "그로기 HUD를 찾지 못했습니다.");
        if (failure != null) return;

        FieldInfo fillField = typeof(BossStaggerHUD).GetField(
            "fill", BindingFlags.Instance | BindingFlags.NonPublic);
        Image fill = fillField?.GetValue(hud) as Image;
        Require(fill != null, "그로기 HUD의 실제 감소 바를 찾지 못했습니다.");
        if (failure != null) return;

        float visibleNormalized = fill.enabled ? fill.rectTransform.anchorMax.x : 0f;
        Require(Mathf.Approximately(visibleNormalized, expectedNormalized),
            $"그로기 HUD 표시량({visibleNormalized:0.##})이 실제 게이지({expectedNormalized:0.##})와 다릅니다.");
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
        Health bossHealth = boss.GetComponent<Health>();
        Require(gauge != null && hud != null && parryBridge != null && parryMiniGame != null
                && bossHealth != null,
            "최종보스에 공용 그로기 게이지/HUD/패링 미니게임이 런타임 연결되지 않았습니다.");
        if (failure != null) return;
        ValidateGroggyHudLayout(hud, 940f, -68f);
        if (failure != null) return;

        float bossHealthBeforeAttack = bossHealth.CurrentHP;
        bossHealth.TakeDamage(10f, true);
        Require(Mathf.Approximately(bossHealth.CurrentHP, bossHealthBeforeAttack - 10f)
                && Mathf.Approximately(gauge.CurrentGroggy, 90f),
            "최종보스 공격 피해가 그로기 게이지에 함께 반영되지 않았습니다.");
        gauge.ResetGauge();

        gauge.RegisterParry();
        gauge.RegisterParry();
        gauge.RegisterParry();
        Require(gauge.IsStaggered, "최종보스 세 번째 패링 후 그로기가 시작되지 않았습니다.");
        Require(boss.State == BossController.BossState.Staggered,
            "최종보스 AI가 공용 그로기 상태를 구독하지 않았습니다.");
        ValidateGroggyHudFill(hud, 0f);
        if (failure != null) return;

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
            Require(Mathf.Approximately(gauge.CurrentGroggy, gauge.MaxGroggy),
                "중간보스 10초 그로기 종료 후 게이지가 완전히 재충전되지 않았습니다.");
            BossStaggerHUD hud = UnityEngine.Object.FindAnyObjectByType<MiddleBossController>()
                ?.GetComponent<BossStaggerHUD>();
            ValidateGroggyHudFill(hud, 1f);
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
            Require(Mathf.Approximately(gauge.CurrentGroggy, gauge.MaxGroggy),
                "최종보스 10초 그로기 종료 후 게이지가 완전히 재충전되지 않았습니다.");
            BossStaggerHUD hud = UnityEngine.Object.FindAnyObjectByType<BossController>()
                ?.GetComponent<BossStaggerHUD>();
            ValidateGroggyHudFill(hud, 1f);
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
            Debug.Log("[MiddleBossPlaytest] PASS: boss attack-linked groggy, nutrient-block reflection without mini-game, QTE, and 10-second stagger verified.");
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
