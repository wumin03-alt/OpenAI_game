using System;
using UnityEngine;

/// <summary>Health 사망 뒤 GameObject가 파괴될 때 Stage04 진행을 정확히 한 번 전달합니다.</summary>
public class StageTargetRelay : MonoBehaviour
{
    private Action onDestroyed;
    private bool initialized;

    public void Initialize(Action callback)
    {
        onDestroyed = callback;
        initialized = true;
    }

    private void OnDestroy()
    {
        if (!initialized) return;
        initialized = false;
        onDestroyed?.Invoke();
    }
}
