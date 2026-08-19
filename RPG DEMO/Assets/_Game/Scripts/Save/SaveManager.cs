using System;
using UnityEngine;

namespace Game.Save
{
    [Serializable]
    public sealed class SaveData
    {
        public int highestUnlockedStage = 1;
        public float masterVolume = 1f;
        public float musicVolume = 0.2f;
        public float sfxVolume = 0.15f;
        public int audioSettingsVersion = 2;
    }

    /// <summary>초기 버전의 진행도와 오디오 설정 저장소입니다.</summary>
    public sealed class SaveManager : MonoBehaviour
    {
        private const string SaveKey = "RPG_DEMO_SAVE_V1";
        private const float AutoSaveDelay = 0.5f;

        public static SaveManager Instance { get; private set; }
        public SaveData Data { get; private set; } = new SaveData();

        private bool savePending;
        private float saveAtUnscaledTime;

        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
        }

        public void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                Data = new SaveData();
                return;
            }

            string json = PlayerPrefs.GetString(SaveKey);
            Data = JsonUtility.FromJson<SaveData>(json) ?? new SaveData();
            bool migrated = false;

            // 기존 저장 데이터에는 버전 필드가 없으므로 UI/SFX 100% 값을 새 기본값으로 한 번만 변경합니다.
            if (Data.audioSettingsVersion < 1)
            {
                Data.sfxVolume = 0.15f;
                Data.audioSettingsVersion = 1;
                migrated = true;
            }

            // 기존 BGM 100% 저장값을 새 기본값인 20%로 한 번만 낮춥니다.
            if (Data.audioSettingsVersion < 2)
            {
                Data.musicVolume = 0.2f;
                Data.audioSettingsVersion = 2;
                migrated = true;
            }

            if (migrated) Save();
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
            savePending = false;
        }

        public void UnlockStage(int stageNumber)
        {
            Data.highestUnlockedStage = Mathf.Max(Data.highestUnlockedStage, stageNumber);
            Save();
        }

        public void SetMasterVolume(float value)
        {
            Data.masterVolume = Mathf.Clamp01(value);
            QueueAutoSave();
        }

        public void SetMusicVolume(float value)
        {
            Data.musicVolume = Mathf.Clamp01(value);
            QueueAutoSave();
        }

        public void SetSfxVolume(float value)
        {
            Data.sfxVolume = Mathf.Clamp01(value);
            QueueAutoSave();
        }

        private void QueueAutoSave()
        {
            savePending = true;
            saveAtUnscaledTime = Time.unscaledTime + AutoSaveDelay;
        }

        private void Update()
        {
            if (savePending && Time.unscaledTime >= saveAtUnscaledTime)
                Save();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && savePending) Save();
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void OnDestroy()
        {
            if (savePending) Save();
            if (Instance == this) Instance = null;
        }
    }
}
