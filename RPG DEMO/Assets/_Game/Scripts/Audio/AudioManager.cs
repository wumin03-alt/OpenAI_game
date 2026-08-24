using Game.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Audio
{
    /// <summary>BGM과 효과음을 공통으로 재생하는 간단한 오디오 진입점입니다.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        [Header("공통 UI 효과음")]
        [SerializeField] private AudioClip uiHoverClip;
        [SerializeField] private AudioClip uiClickClip;

        [Header("플레이어 전투 효과음")]
        [SerializeField] private AudioClip playerMeleeSwingClip;
        [SerializeField] private AudioClip combatHitClip;
        [SerializeField] private AudioClip playerRangedShotClip;
        [SerializeField] private AudioClip playerDashClip;
        [SerializeField] private AudioClip playerDeathExplosionClip;

        [Header("씬 배경음")]
        [SerializeField] private AudioClip mainMenuMusicClip;
        [SerializeField] private AudioClip normalStageMusicClip;
        [SerializeField] private AudioClip midBossMusicClip;
        [SerializeField] private AudioClip finalBossMusicClip;

        [Header("씬 전환 효과음")]
        [SerializeField] private AudioClip stageTransitionClip;

        private float lastStageTransitionTime = float.NegativeInfinity;

        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;

            if (musicSource == null)
                musicSource = CreateSource("MusicSource", true);
            if (sfxSource == null)
                sfxSource = CreateSource("SfxSource", false);

            musicSource.playOnAwake = false;
            musicSource.loop = true;
            sfxSource.playOnAwake = false;
            sfxSource.loop = false;

            // Bootstrap 씬에서 별도 연결을 하지 않아도 공통 UI 음원을 사용할 수 있게 합니다.
            if (uiHoverClip == null)
                uiHoverClip = Resources.Load<AudioClip>("Audio/UI/rollover2");
            if (uiClickClip == null)
                uiClickClip = Resources.Load<AudioClip>("Audio/UI/click1");

            if (playerMeleeSwingClip == null)
                playerMeleeSwingClip = Resources.Load<AudioClip>("Audio/SFX/Player/Player_Melee_Swing_01");
            if (combatHitClip == null)
                combatHitClip = Resources.Load<AudioClip>("Audio/SFX/Player/Combat_Hit_01");
            if (playerRangedShotClip == null)
                playerRangedShotClip = Resources.Load<AudioClip>("Audio/SFX/Player/Player_Ranged_Shot_01");
            if (playerDashClip == null)
                playerDashClip = Resources.Load<AudioClip>("Audio/SFX/Player/Player_Dash_01");
            if (playerDeathExplosionClip == null)
                playerDeathExplosionClip = Resources.Load<AudioClip>("Audio/SFX/Player/Player_Death_Explosion_01");

            if (mainMenuMusicClip == null)
                mainMenuMusicClip = Resources.Load<AudioClip>("Audio/Music/BGM_MainMenu_Loop");
            if (normalStageMusicClip == null)
                normalStageMusicClip = Resources.Load<AudioClip>("Audio/Music/BGM_Stage01_Loop");
            if (midBossMusicClip == null)
                midBossMusicClip = Resources.Load<AudioClip>("Audio/Music/BGM_MidBoss_Loop");
            if (finalBossMusicClip == null)
                finalBossMusicClip = Resources.Load<AudioClip>("Audio/Music/BGM_FinalBoss_Loop");
            if (stageTransitionClip == null)
                stageTransitionClip = Resources.Load<AudioClip>("Audio/SFX/System/Stage_Transition_01");

            SceneManager.sceneLoaded += HandleSceneLoaded;
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

        public void PlayUiHover() => PlaySfx(uiHoverClip);

        public void PlayUiClick() => PlaySfx(uiClickClip);

        public void PlayPlayerMeleeSwing() => PlaySfx(playerMeleeSwingClip);

        public void PlayCombatHit() => PlaySfx(combatHitClip);

        public void PlayPlayerRangedShot() => PlaySfx(playerRangedShotClip);

        public void PlayPlayerDash() => PlaySfx(playerDashClip);

        public void PlayPlayerDeathExplosion() => PlaySfx(playerDeathExplosionClip);

        public void PlayStageTransition()
        {
            // 기존 스테이지 출구와 공통 SceneLoader가 같은 전환을 연속 요청해도
            // 징글이 겹쳐 재생되지 않게 짧은 재생 방지 구간을 둡니다.
            float guardTime = stageTransitionClip != null
                ? Mathf.Max(0.5f, stageTransitionClip.length * 0.5f)
                : 0.5f;
            if (Time.unscaledTime - lastStageTransitionTime < guardTime) return;

            lastStageTransitionTime = Time.unscaledTime;
            PlaySfx(stageTransitionClip);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "MainMenu")
            {
                PlayMusic(mainMenuMusicClip);
                return;
            }

            if (IsNormalStage(scene.name))
            {
                PlayMusic(normalStageMusicClip);
                return;
            }

            if (IsMidBossStage(scene.name))
            {
                if (midBossMusicClip != null)
                    PlayMusic(midBossMusicClip);
                else
                    StopMusic();
                return;
            }

            if (IsFinalBossStage(scene.name))
            {
                if (finalBossMusicClip != null)
                    PlayMusic(finalBossMusicClip);
                else
                    StopMusic();
                return;
            }

            // 보스전 등 별도 BGM이 아직 없는 씬에서는 이전 씬 음악이 잘못 이어지지 않게 합니다.
            if (scene.name != "Bootstrap")
                StopMusic();
        }

        private static bool IsNormalStage(string sceneName)
        {
            if (!TryGetStageNumber(sceneName, out int stageNumber)) return false;
            // 실제 런 순서의 일반 전투 씬은 Stage01~03, Stage05~07이다.
            // 중간보스는 번호형 Stage가 아니라 별도 MiddleBoss 씬이므로 Stage05도 일반 BGM을 쓴다.
            return stageNumber >= 1 && stageNumber <= 9;
        }

        private static bool IsMidBossStage(string sceneName)
        {
            return string.Equals(sceneName, "MiddleBoss", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(sceneName, "MidBoss", System.StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFinalBossStage(string sceneName)
        {
            if (sceneName == "BossArena") return true;
            return TryGetStageNumber(sceneName, out int stageNumber)
                   && stageNumber == 10;
        }

        private static bool TryGetStageNumber(string sceneName, out int stageNumber)
        {
            stageNumber = 0;
            if (!sceneName.StartsWith("Stage", System.StringComparison.OrdinalIgnoreCase))
                return false;

            string suffix = sceneName.Substring("Stage".Length);
            int digitCount = 0;
            while (digitCount < suffix.Length && char.IsDigit(suffix[digitCount]))
                digitCount++;

            return digitCount > 0
                   && int.TryParse(suffix.Substring(0, digitCount), out stageNumber);
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
            if (Instance != this) return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            Instance = null;
        }
    }
}
