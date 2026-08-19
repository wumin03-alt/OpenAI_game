using Game.Audio;
using Game.Save;
using Game.SceneManagement;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// 게임에서 하나만 존재하는 공통 시스템 루트입니다.
    /// Bootstrap 씬이 최초로 실행되면 App 오브젝트를 씬 전환 후에도 유지합니다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class Bootstrapper : MonoBehaviour
    {
        public static Bootstrapper Instance { get; private set; }

        [Header("첫 실행")]
        [SerializeField] private string initialSceneName = "MainMenu";
        [SerializeField] private bool loadInitialSceneOnStart = true;

        private bool isPrimaryInstance;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            isPrimaryInstance = true;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (!isPrimaryInstance) return;

            SaveManager.Instance?.Load();
            AudioManager.Instance?.ApplySavedVolumes();

            if (loadInitialSceneOnStart && !string.IsNullOrWhiteSpace(initialSceneName))
                SceneLoader.Instance?.LoadScene(initialSceneName);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
