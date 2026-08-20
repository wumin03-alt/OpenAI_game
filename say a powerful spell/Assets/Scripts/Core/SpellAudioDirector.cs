using System;
using UnityEngine;

namespace PowerfulSpell
{
    /// <summary>외부 에셋 없이 프로토타입에서 사용할 마법/전투 사운드를 합성한다.</summary>
    public sealed class SpellAudioDirector : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private AudioSource sfxSource;
        private AudioSource ambienceSource;
        private AudioClip click;
        private AudioClip listen;
        private AudioClip cast;
        private AudioClip fizzle;
        private AudioClip enemyAttack;
        private AudioClip victory;
        private AudioClip defeat;

        private void Awake()
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = .34f;

            ambienceSource = gameObject.AddComponent<AudioSource>();
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
            ambienceSource.volume = .075f;

            click = CreateTone("UI Rune Click", .11f, (t, p) => Envelope(p) * (Mathf.Sin(t * 740f * Mathf.PI * 2f) + Mathf.Sin(t * 1110f * Mathf.PI * 2f) * .35f) * .34f);
            listen = CreateTone("Listening Pulse", .42f, (t, p) => Envelope(p) * Mathf.Sin(t * Mathf.Lerp(180f, 520f, p) * Mathf.PI * 2f) * .42f);
            cast = CreateTone("Spell Cast", .82f, SpellCastSample);
            fizzle = CreateTone("Spell Fizzle", .38f, (t, p) => Envelope(p) * Mathf.Sin(t * Mathf.Lerp(210f, 70f, p) * Mathf.PI * 2f) * .22f);
            enemyAttack = CreateTone("Enemy Attack", .52f, (t, p) => Envelope(p) * Mathf.Sin(t * Mathf.Lerp(95f, 42f, p) * Mathf.PI * 2f) * .28f);
            victory = CreateTone("Stage Victory", 1.35f, VictorySample);
            defeat = CreateTone("Stage Defeat", 1.1f, (t, p) => Envelope(p) * (Mathf.Sin(t * Mathf.Lerp(180f, 52f, p) * Mathf.PI * 2f) * .4f + Noise(t, 13) * .08f));
            ambienceSource.clip = CreateTone("Arcane Ambience", 8f, AmbienceSample);
            ambienceSource.Play();
        }

        public void PlayClick() => Play(click, .55f, UnityEngine.Random.Range(.97f, 1.04f));
        public void PlayListen() => Play(listen, .7f, 1f);
        public void PlayCast(float pitch = 1f) => Play(cast, .42f, pitch);
        public void PlayFizzle() => Play(fizzle, .34f, 1f);
        public void PlayEnemyAttack() => Play(enemyAttack, .36f, UnityEngine.Random.Range(.92f, 1.04f));
        public void PlayVictory() => Play(victory, .48f, 1f);
        public void PlayDefeat() => Play(defeat, .42f, 1f);
        public void PlayMicrophonePreview(AudioClip clip)
        {
            if (clip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(clip, 1f);
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (clip == null) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, volume);
        }

        private static AudioClip CreateTone(string name, float duration, Func<float, float, float> sampler)
        {
            int count = Mathf.CeilToInt(duration * SampleRate);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float progress = i / (float)(count - 1);
                samples[i] = Mathf.Clamp(sampler(t, progress), -.95f, .95f);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float SpellCastSample(float t, float p)
        {
            float rise = Mathf.Sin(t * Mathf.Lerp(120f, 880f, p) * Mathf.PI * 2f) * .28f;
            float voiceLike = Mathf.Sin(t * 230f * Mathf.PI * 2f) * Mathf.Sin(t * 730f * Mathf.PI * 2f) * .18f;
            return Envelope(p) * (rise + voiceLike);
        }

        private static float VictorySample(float t, float p)
        {
            int step = Mathf.Min(3, Mathf.FloorToInt(p * 4f));
            float[] notes = { 261.63f, 329.63f, 392f, 523.25f };
            float local = (p * 4f) % 1f;
            return Mathf.Sin(t * notes[step] * Mathf.PI * 2f) * Envelope(local) * .36f;
        }

        private static float AmbienceSample(float t, float p)
        {
            float fade = Mathf.Min(1f, Mathf.Min(p * 25f, (1f - p) * 25f));
            float drone = Mathf.Sin(t * 43.65f * Mathf.PI * 2f) * .3f + Mathf.Sin(t * 65.41f * Mathf.PI * 2f) * .16f;
            float shimmer = Mathf.Sin(t * 523.25f * Mathf.PI * 2f + Mathf.Sin(t * .4f) * 2f) * .025f;
            return (drone + shimmer) * fade;
        }

        private static float Envelope(float p)
        {
            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / .08f));
            float release = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - p) / .2f));
            return attack * release;
        }

        private static float Noise(float t, int seed)
        {
            float value = Mathf.Sin((t * 12345.678f + seed * 17.13f) * 78.233f) * 43758.5453f;
            return Mathf.Repeat(value, 1f) * 2f - 1f;
        }
    }
}
