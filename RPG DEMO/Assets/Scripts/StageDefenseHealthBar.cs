using UnityEngine;

/// <summary>기존 플레이어 HP 바를 복제해 방어 대상의 HP를 같은 HUD 스타일로 표시합니다.</summary>
[RequireComponent(typeof(Health))]
public sealed class StageDefenseHealthBar : MonoBehaviour
{
    [SerializeField] private HealthBarUI playerHealthBar;

    private void Start()
    {
        if (playerHealthBar == null || playerHealthBar.transform.parent == null)
        {
            Debug.LogError("[StageDefenseHealthBar] Player HP bar is missing.", this);
            return;
        }

        GameObject defenseBarObject = Instantiate(playerHealthBar.gameObject, playerHealthBar.transform.parent);
        defenseBarObject.name = "DefenseCoreHPBar";

        RectTransform barRect = defenseBarObject.transform as RectTransform;
        barRect.anchorMin = barRect.anchorMax = new Vector2(0f, 1f);
        barRect.pivot = new Vector2(0f, 1f);
        barRect.anchoredPosition = new Vector2(40f, -82f);
        barRect.sizeDelta = new Vector2(420f, 34f);

        HealthBarUI defenseBar = defenseBarObject.GetComponent<HealthBarUI>();
        defenseBar.SetVisualRole(HealthBarUI.VisualRole.Defense);
        defenseBar.SetTarget(GetComponent<Health>());
    }
}
