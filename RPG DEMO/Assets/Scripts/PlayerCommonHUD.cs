using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>PlayerCommonHUD 프리팹의 씬 독립적인 런타임 연결을 담당합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerCommonHUD : MonoBehaviour
{
    [SerializeField] private HealthBarUI healthBar;
    [SerializeField] private TMP_Text parryNotice;

    private PlayerCombatTracker tracker;
    private Coroutine noticeRoutine;

    public void Configure(HealthBarUI playerHealthBar, TMP_Text notice)
    {
        healthBar = playerHealthBar;
        parryNotice = notice;
    }

    private void Start()
    {
        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player != null && healthBar != null)
            healthBar.SetTarget(player.GetComponent<Health>());

        tracker = player != null ? player.GetComponent<PlayerCombatTracker>() : PlayerCombatTracker.Instance;
        if (tracker != null) tracker.ParrySucceeded += ShowParryNotice;

        if (parryNotice != null) parryNotice.color = Color.clear;
    }

    private void OnDestroy()
    {
        if (tracker != null) tracker.ParrySucceeded -= ShowParryNotice;
    }

    private void ShowParryNotice()
    {
        if (parryNotice == null) return;
        if (noticeRoutine != null) StopCoroutine(noticeRoutine);
        noticeRoutine = StartCoroutine(AnimateNotice());
    }

    private IEnumerator AnimateNotice()
    {
        float elapsed = 0f;
        const float duration = 0.95f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = t < 0.18f ? t / 0.18f : 1f - Mathf.InverseLerp(0.62f, 1f, t);
            parryNotice.color = new Color(0.38f, 1f, 0.76f, alpha);
            parryNotice.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.12f, 1f, Mathf.Clamp01(t * 5f));
            yield return null;
        }

        parryNotice.color = Color.clear;
        noticeRoutine = null;
    }
}
