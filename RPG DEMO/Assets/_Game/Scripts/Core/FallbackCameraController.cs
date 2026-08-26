using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Core
{
    /// <summary>
    /// 카메라가 없는 Bootstrap/MainMenu에서는 배경을 렌더링하고,
    /// 스테이지 카메라가 로드되면 자동으로 비활성화되는 공통 폴백 카메라입니다.
    /// </summary>
    [DefaultExecutionOrder(-1500)]
    public sealed class FallbackCameraController : MonoBehaviour
    {
        private Camera fallbackCamera;
        private AudioListener fallbackListener;

        private void Awake()
        {
            EnsureFallbackCamera();
            RefreshState();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            RefreshState();
            StartCoroutine(RefreshNextFrame());
        }

        private IEnumerator RefreshNextFrame()
        {
            yield return null;
            RefreshState();
        }

        private void EnsureFallbackCamera()
        {
            Transform existing = transform.Find("FallbackCamera");
            GameObject cameraObject;

            if (existing != null)
            {
                cameraObject = existing.gameObject;
            }
            else
            {
                cameraObject = new GameObject("FallbackCamera");
                cameraObject.transform.SetParent(transform, false);
            }

            fallbackCamera = cameraObject.GetComponent<Camera>();
            if (fallbackCamera == null)
                fallbackCamera = cameraObject.AddComponent<Camera>();

            fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
            fallbackCamera.backgroundColor = new Color(0.025f, 0.045f, 0.08f, 1f);
            fallbackCamera.cullingMask = 0;
            fallbackCamera.orthographic = true;
            fallbackCamera.depth = -100f;

            fallbackListener = cameraObject.GetComponent<AudioListener>();
            if (fallbackListener == null)
                fallbackListener = cameraObject.AddComponent<AudioListener>();
        }

        private void RefreshState()
        {
            bool needsFallback = !HasOtherEnabledCamera();
            fallbackCamera.enabled = needsFallback;
            fallbackListener.enabled = needsFallback;
        }

        private bool HasOtherEnabledCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

            foreach (Camera candidate in cameras)
            {
                if (candidate == fallbackCamera) continue;
                if (candidate.enabled && candidate.gameObject.activeInHierarchy)
                    return true;
            }

            return false;
        }
    }
}
