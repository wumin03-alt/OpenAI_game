using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>기존 플레이어 HP 바를 복제해 방어 대상의 HP를 같은 HUD 스타일로 표시합니다.</summary>
[RequireComponent(typeof(Health))]
public sealed class StageDefenseHealthBar : MonoBehaviour
{
    [SerializeField] private HealthBarUI playerHealthBar;

    private IEnumerator Start()
    {
        // PlayerController가 씬의 구형 HUD를 비활성화하고 PlayerCommonHUD를 생성한 뒤,
        // 실제로 표시 중인 플레이어 HP 바를 기준으로 복제합니다.
        yield return null;

        HealthBarUI sourceBar = FindVisiblePlayerHealthBar();
        if (sourceBar == null)
            sourceBar = playerHealthBar;

        if (sourceBar == null || sourceBar.transform.parent == null)
        {
            Debug.LogError("[StageDefenseHealthBar] Player HP bar is missing.", this);
            yield break;
        }

        GameObject defenseBarObject = Instantiate(sourceBar.gameObject, sourceBar.transform.parent);
        defenseBarObject.name = "DefenseCoreHPBar";
        defenseBarObject.SetActive(true);

        RectTransform barRect = defenseBarObject.transform as RectTransform;
        RectTransform sourceRect = sourceBar.transform as RectTransform;
        barRect.anchorMin = sourceRect.anchorMin;
        barRect.anchorMax = sourceRect.anchorMax;
        barRect.pivot = sourceRect.pivot;
        barRect.localScale = sourceRect.localScale;
        barRect.anchoredPosition = sourceRect.anchoredPosition + new Vector2(0f, -70f);
        barRect.sizeDelta = sourceRect.sizeDelta;

        HealthBarUI defenseBar = defenseBarObject.GetComponent<HealthBarUI>();
        defenseBar.SetVisualRole(HealthBarUI.VisualRole.Defense);
        defenseBar.SetTarget(GetComponent<Health>());
        SetDefenseTitle(defenseBar);
    }

    private static HealthBarUI FindVisiblePlayerHealthBar()
    {
        PlayerCommonHUD commonHud = FindAnyObjectByType<PlayerCommonHUD>();
        if (commonHud == null) return null;

        foreach (HealthBarUI healthBar in commonHud.GetComponentsInChildren<HealthBarUI>(true))
        {
            if (healthBar.gameObject.name == "PlayerHPBar" && healthBar.gameObject.activeInHierarchy)
                return healthBar;
        }

        return null;
    }

    private static void SetDefenseTitle(HealthBarUI defenseBar)
    {
        foreach (TMP_Text text in defenseBar.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.gameObject.name != "PlayerTitle") continue;

            text.gameObject.name = "DefenseTitle";
            text.text = "DEFENSE CORE // PROTECT TARGET";
            return;
        }
    }
}
