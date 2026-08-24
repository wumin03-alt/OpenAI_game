using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BossStaggerGauge))]
public sealed class BossParryMiniGameBridge : MonoBehaviour
{
    private BossStaggerGauge gauge;
    private ParryGroggyMiniGame miniGame;
    private PlayerCombatTracker tracker;

    private void Awake()
    {
        gauge = GetComponent<BossStaggerGauge>();
        miniGame = GetComponent<ParryGroggyMiniGame>();
        if (miniGame == null) miniGame = gameObject.AddComponent<ParryGroggyMiniGame>();
    }

    private IEnumerator Start()
    {
        yield return null;
        tracker = PlayerCombatTracker.Instance;
        if (tracker == null)
        {
            Debug.LogError("[BossParryMiniGameBridge] PlayerCombatTracker를 찾지 못했습니다.", this);
            yield break;
        }
        tracker.ParrySucceeded += HandleParrySucceeded;
    }

    private void HandleParrySucceeded()
    {
        miniGame?.TryBegin(gauge);
    }

    private void OnDestroy()
    {
        if (tracker != null) tracker.ParrySucceeded -= HandleParrySucceeded;
    }
}
