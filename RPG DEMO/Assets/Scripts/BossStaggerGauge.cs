using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 종류와 무관하게 패링 누적과 긴 그로기 시간을 관리합니다.
/// 패링 세 번으로 게이지가 소진되며, 그로기 종료 후 다시 충전됩니다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossStaggerGauge : MonoBehaviour
{
    [SerializeField, Min(1)] private int parriesRequired = 3;
    [SerializeField, Min(0.1f)] private float staggerDuration = 10f;

    public event Action<float> GaugeChanged;
    public event Action<float> StaggerStarted;
    public event Action StaggerEnded;

    public int ParriesRequired => parriesRequired;
    public int RegisteredParries { get; private set; }
    public int RemainingSegments => Mathf.Max(0, parriesRequired - RegisteredParries);
    public float Normalized => IsStaggered ? 0f : Mathf.Clamp01((float)RemainingSegments / parriesRequired);
    public bool IsStaggered { get; private set; }
    public float StaggerDuration => staggerDuration;
    public float StaggerTimeRemaining { get; private set; }

    private Coroutine staggerRoutine;

    private void Start()
    {
        GaugeChanged?.Invoke(Normalized);
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
        if (IsStaggered) return false;

        RegisteredParries = Mathf.Min(parriesRequired, RegisteredParries + 1);
        GaugeChanged?.Invoke(Normalized);

        if (RegisteredParries < parriesRequired) return true;

        if (staggerRoutine != null) StopCoroutine(staggerRoutine);
        staggerRoutine = StartCoroutine(StaggerRoutine());
        return true;
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
        RegisteredParries = 0;
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
        RegisteredParries = 0;
        staggerRoutine = null;
        GaugeChanged?.Invoke(Normalized);
        StaggerEnded?.Invoke();
    }
}
