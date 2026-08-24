using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 종류와 무관하게 연속형 그로기 게이지와 긴 그로기 시간을 관리합니다.
/// 일반 공격 적중과 패링/특수 반사 피해가 서로 다른 비율로 게이지를 소진합니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossStaggerGauge : MonoBehaviour
{
    [SerializeField, Min(1)] private int parriesRequired = 3;
    [SerializeField, Min(0.1f)] private float staggerDuration = 10f;
    [SerializeField, Min(1f)] private float maxGroggy = 100f;
    [SerializeField, Min(0f)] private float normalHitGroggyDamage = 2f;

    public event Action<float> GaugeChanged;
    public event Action<float> StaggerStarted;
    public event Action StaggerEnded;

    public int ParriesRequired => parriesRequired;
    public int RegisteredParries => Mathf.Clamp(parriesRequired - RemainingSegments, 0, parriesRequired);
    public int RemainingSegments => Mathf.Clamp(
        Mathf.CeilToInt(Normalized * parriesRequired - 0.0001f), 0, parriesRequired);
    public float CurrentGroggy { get; private set; }
    public float MaxGroggy => maxGroggy;
    public float NormalHitGroggyDamage => normalHitGroggyDamage;
    public int NormalHitsRequired => normalHitGroggyDamage <= 0f
        ? int.MaxValue
        : Mathf.CeilToInt(maxGroggy / normalHitGroggyDamage);
    public float Normalized => IsStaggered ? 0f : Mathf.Clamp01(CurrentGroggy / maxGroggy);
    public bool IsStaggered { get; private set; }
    public float StaggerDuration => staggerDuration;
    public float StaggerTimeRemaining { get; private set; }

    private Coroutine staggerRoutine;
    private Health trackedHealth;
    private float lastKnownHealth;

    private void Awake()
    {
        CurrentGroggy = maxGroggy;
    }

    private void Start()
    {
        trackedHealth = GetComponent<Health>();
        if (trackedHealth != null)
        {
            lastKnownHealth = trackedHealth.CurrentHP;
            trackedHealth.HealthChanged += HandleHealthChanged;
        }

        GaugeChanged?.Invoke(Normalized);
    }

    private void OnDestroy()
    {
        if (trackedHealth != null)
            trackedHealth.HealthChanged -= HandleHealthChanged;
    }

    public void Configure(int requiredParries, float duration)
    {
        parriesRequired = Mathf.Max(1, requiredParries);
        staggerDuration = Mathf.Max(0.1f, duration);
        ResetGauge();
    }

    /// <summary>패링 한 번을 등록합니다. 이미 그로기 중이면 입력을 소비하지 않습니다.</summary>
    public bool RegisterParry()
    {
        return ApplyGroggyDamage(maxGroggy / Mathf.Max(1, parriesRequired));
    }

    public bool ApplyGroggyDamage(float amount)
    {
        if (IsStaggered || amount <= 0f) return false;

        CurrentGroggy = Mathf.Max(0f, CurrentGroggy - amount);
        GaugeChanged?.Invoke(Normalized);
        Debug.Log($"[BossStaggerGauge] GROGGY -{amount:0.#} → {CurrentGroggy:0.#}/{maxGroggy:0.#}", this);

        if (CurrentGroggy > 0.01f) return true;

        if (staggerRoutine != null) StopCoroutine(staggerRoutine);
        staggerRoutine = StartCoroutine(StaggerRoutine());
        return true;
    }

    private void HandleHealthChanged(float currentHealth, float maxHealth)
    {
        float healthDamage = Mathf.Max(0f, lastKnownHealth - currentHealth);
        lastKnownHealth = currentHealth;

        if (healthDamage <= 0f || normalHitGroggyDamage <= 0f) return;

        // 공격력 강화가 일반 공격의 그로기 효율까지 올리지 않도록 적중당 고정값을 적용합니다.
        // 기본값 2 / 최대 게이지 100이므로 근접·원거리 모두 50회 적중이 필요합니다.
        ApplyGroggyDamage(normalHitGroggyDamage);
    }

    public void ResetGauge()
    {
        if (staggerRoutine != null)
        {
            StopCoroutine(staggerRoutine);
            staggerRoutine = null;
        }

        IsStaggered = false;
        StaggerTimeRemaining = 0f;
        CurrentGroggy = maxGroggy;
        GaugeChanged?.Invoke(Normalized);
    }

    private IEnumerator StaggerRoutine()
    {
        IsStaggered = true;
        StaggerTimeRemaining = staggerDuration;
        GaugeChanged?.Invoke(0f);
        StaggerStarted?.Invoke(staggerDuration);

        while (StaggerTimeRemaining > 0f)
        {
            StaggerTimeRemaining -= Time.deltaTime;
            yield return null;
        }

        StaggerTimeRemaining = 0f;
        IsStaggered = false;
        CurrentGroggy = maxGroggy;
        staggerRoutine = null;
        GaugeChanged?.Invoke(Normalized);
        StaggerEnded?.Invoke();
    }
}
