using Game.Save;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>BGM과 효과음을 공통으로 재생하는 간단한 오디오 진입점입니다.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;

            if (musicSource == null)
                musicSource = CreateSource("MusicSource", true);
            if (sfxSource == null)
                sfxSource = CreateSource("SfxSource", false);
        }

        public void ApplySavedVolumes()
        {
            SaveData data = SaveManager.Instance?.Data;
            if (data == null) return;

            AudioListener.volume = Mathf.Clamp01(data.masterVolume);
            musicSource.volume = Mathf.Clamp01(data.musicVolume);
            sfxSource.volume = Mathf.Clamp01(data.sfxVolume);
        }

        public void SetMasterVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
            SaveManager.Instance?.SetMasterVolume(value);
        }

        public void SetMusicVolume(float value)
        {
            musicSource.volume = Mathf.Clamp01(value);
            SaveManager.Instance?.SetMusicVolume(value);
        }

        public void SetSfxVolume(float value)
        {
            sfxSource.volume = Mathf.Clamp01(value);
            SaveManager.Instance?.SetSfxVolume(value);
        }

        public void PlayMusic(AudioClip clip, bool restartIfSame = false)
        {
            if (clip == null) return;
            if (!restartIfSame && musicSource.clip == clip && musicSource.isPlaying) return;

            musicSource.clip = clip;
            musicSource.Play();
        }

        public void StopMusic() => musicSource.Stop();

        public void PlaySfx(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null)
                sfxSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale));
        }

        private AudioSource CreateSource(string objectName, bool loop)
        {
            GameObject child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            return source;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
