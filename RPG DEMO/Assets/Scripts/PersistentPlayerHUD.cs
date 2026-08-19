using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Stage01의 기존 Canvas와 PlayerHPBar를 다음 스테이지에 유지하고 새 Player에 재연결합니다.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class PersistentPlayerHUD : MonoBehaviour
{
    private HealthBarUI playerHealthBar;
    private bool initialized;

    public void Initialize()
    {
        if (initialized) return;

        playerHealthBar = GetComponentInChildren<HealthBarUI>(true);
        if (playerHealthBar == null)
        {
            Debug.LogError("[PersistentPlayerHUD] Stage01 PlayerHPBar/HealthBarUI를 찾지 못했습니다.");
            return;
        }

        initialized = true;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSceneLoaded;
        StartCoroutine(RebindAfterSceneLoad());
    }

    private void OnDestroy()
    {
        if (initialized) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        StartCoroutine(RebindAfterSceneLoad());
    }

    private IEnumerator RebindAfterSceneLoad()
    {
        yield return null;

        HideScenePlayerHpBars();
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) yield break;

        Health health = player.GetComponent<Health>();
        if (health != null) playerHealthBar.SetTarget(health);
    }

    private void HideScenePlayerHpBars()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        foreach (GameObject root in activeScene.GetRootGameObjects())
        {
            foreach (HealthBarUI bar in root.GetComponentsInChildren<HealthBarUI>(true))
            {
                if (bar != null && bar != playerHealthBar && bar.gameObject.name == "PlayerHPBar")
                    bar.gameObject.SetActive(false);
            }
        }
    }
}
