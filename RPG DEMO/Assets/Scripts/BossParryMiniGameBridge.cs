using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BossStaggerGauge))]
public sealed class BossParryMiniGameBridge : MonoBehaviour
{
    private BossStaggerGauge gauge;
    private ParryGroggyMiniGame miniGame;
    private PlayerCombatTracker tracker;
    private bool suppressNextParryMiniGame;

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
        if (suppressNextParryMiniGame)
        {
            suppressNextParryMiniGame = false;
            return;
        }

        miniGame?.TryBegin(gauge);
    }

    /// <summary>
    /// 영양 블록처럼 패링 자체가 별도의 카운터 행동으로 이어지는 공격은
    /// 같은 패링에서 공용 방향키 미니게임이 중복 실행되지 않게 합니다.
    /// </summary>
    public void SuppressNextParryMiniGame()
    {
        suppressNextParryMiniGame = true;
    }

    public void ClearParryMiniGameSuppression()
    {
        suppressNextParryMiniGame = false;
    }

    private void OnDestroy()
    {
        if (tracker != null) tracker.ParrySucceeded -= HandleParrySucceeded;
    }
}
