using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using UnityEngine.Windows.Speech;
#endif

namespace PowerfulSpell
{
    public interface ISpellSpeechRecognizer : IDisposable
    {
        bool Available { get; }
        bool IsListening { get; }
        bool IsProcessing { get; }
        string DisplayName { get; }
        float InputLevel { get; }
        string LiveTranscript { get; }
        void Begin(string expectedPhrase, Action<string> onResult, Action<string> onError);
        void Stop();
    }

    public sealed class WindowsDictationSpellRecognizer : ISpellSpeechRecognizer
    {
        public string DisplayName => "Windows 음성";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private DictationRecognizer recognizer;
        private Action<string> resultCallback;
        private Action<string> errorCallback;
        private bool terminalEventSent;
        private string liveTranscript = string.Empty;
        private long lastHypothesisTicks;
        public bool Available => Microphone.devices.Length > 0;
        public bool IsListening => recognizer != null && recognizer.Status == SpeechSystemStatus.Running;
        public bool IsProcessing => false;
        public float InputLevel
        {
            get
            {
                if (lastHypothesisTicks == 0) return 0f;
                double elapsed = TimeSpan.FromTicks(DateTime.UtcNow.Ticks - lastHypothesisTicks).TotalSeconds;
                return Mathf.Clamp01(1f - (float)elapsed / .75f) * .82f;
            }
        }
        public string LiveTranscript => liveTranscript;

        public void Begin(string expectedPhrase, Action<string> onResult, Action<string> onError)
        {
            if (!Available)
            {
                onError?.Invoke("사용 가능한 마이크가 없습니다. Windows 설정의 마이크 권한과 입력 장치를 확인하세요.");
                return;
            }
            Stop();
            DisposeRecognizer();
            resultCallback = onResult;
            errorCallback = onError;
            terminalEventSent = false;
            liveTranscript = string.Empty;
            lastHypothesisTicks = 0;
            try
            {
                recognizer = new DictationRecognizer(ConfidenceLevel.Low);
                recognizer.AutoSilenceTimeoutSeconds = 2.2f;
                recognizer.InitialSilenceTimeoutSeconds = 5f;
                recognizer.DictationResult += OnResult;
                recognizer.DictationHypothesis += OnHypothesis;
                recognizer.DictationError += OnError;
                recognizer.DictationComplete += OnComplete;
                recognizer.Start();
            }
            catch (Exception exception)
            {
                DisposeRecognizer();
                errorCallback?.Invoke("Windows 음성 인식을 시작하지 못했습니다: " + exception.Message);
            }
        }

        private void OnResult(string text, ConfidenceLevel confidence)
        {
            if (terminalEventSent) return;
            terminalEventSent = true;
            resultCallback?.Invoke(text);
            Stop();
        }

        private void OnHypothesis(string text)
        {
            liveTranscript = text;
            lastHypothesisTicks = DateTime.UtcNow.Ticks;
        }

        private void OnError(string error, int hresult)
        {
            ReportErrorOnce($"Windows 음성 인식 오류: {error} ({hresult}). 온라인 음성 인식과 기본 마이크를 확인하거나 OpenAI STT를 사용하세요.");
            Stop();
        }

        private void OnComplete(DictationCompletionCause cause)
        {
            if (cause != DictationCompletionCause.Complete && cause != DictationCompletionCause.Canceled)
                ReportErrorOnce("음성을 인식하지 못했습니다. Windows 온라인 음성 인식, 한국어 음성 팩, 기본 마이크를 확인하세요.");
        }

        private void ReportErrorOnce(string message)
        {
            if (terminalEventSent) return;
            terminalEventSent = true;
            errorCallback?.Invoke(message);
        }

        public void Stop()
        {
            if (recognizer != null && recognizer.Status == SpeechSystemStatus.Running)
                recognizer.Stop();
        }

        public void Dispose()
        {
            Stop();
            DisposeRecognizer();
        }

        private void DisposeRecognizer()
        {
            if (recognizer == null) return;
            recognizer.DictationResult -= OnResult;
            recognizer.DictationHypothesis -= OnHypothesis;
            recognizer.DictationError -= OnError;
            recognizer.DictationComplete -= OnComplete;
            recognizer.Dispose();
            recognizer = null;
        }
#else
        public bool Available => false;
        public bool IsListening => false;
        public bool IsProcessing => false;
        public float InputLevel => 0f;
        public string LiveTranscript => string.Empty;
        public void Begin(string expectedPhrase, Action<string> onResult, Action<string> onError) => onError?.Invoke("Windows 에디터/빌드에서만 사용할 수 있습니다.");
        public void Stop() { }
        public void Dispose() { }
#endif
    }

    public sealed class WindowsKeywordSpellRecognizer : ISpellSpeechRecognizer
    {
        public string DisplayName => "Windows 주문 인식";
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        private KeywordRecognizer recognizer;
        private Action<string> resultCallback;
        private Action<string> errorCallback;
        private string expected;
        public bool Available => Microphone.devices.Length > 0;
        public bool IsListening => recognizer != null && recognizer.IsRunning;
        public bool IsProcessing => false;
        public float InputLevel => IsListening ? .18f : 0f;
        public string LiveTranscript => IsListening ? "주문 문장을 기다리는 중" : string.Empty;

        public void Begin(string expectedPhrase, Action<string> onResult, Action<string> onError)
        {
            Stop();
            DisposeRecognizer();
            expected = expectedPhrase;
            resultCallback = onResult;
            errorCallback = onError;
            try
            {
                recognizer = new KeywordRecognizer(new[] { expectedPhrase }, ConfidenceLevel.Low);
                recognizer.OnPhraseRecognized += OnPhraseRecognized;
                recognizer.Start();
            }
            catch (Exception exception)
            {
                DisposeRecognizer();
                errorCallback?.Invoke("Windows 주문 인식기도 시작하지 못했습니다: " + exception.Message);
            }
        }

        private void OnPhraseRecognized(PhraseRecognizedEventArgs args)
        {
            resultCallback?.Invoke(args.text);
            Stop();
        }

        public void Stop()
        {
            if (recognizer != null && recognizer.IsRunning) recognizer.Stop();
        }

        public void Dispose()
        {
            Stop();
            DisposeRecognizer();
        }

        private void DisposeRecognizer()
        {
            if (recognizer == null) return;
            recognizer.OnPhraseRecognized -= OnPhraseRecognized;
            recognizer.Dispose();
            recognizer = null;
        }
#else
        public bool Available => false;
        public bool IsListening => false;
        public bool IsProcessing => false;
        public float InputLevel => 0f;
        public string LiveTranscript => string.Empty;
        public void Begin(string expectedPhrase, Action<string> onResult, Action<string> onError) => onError?.Invoke("Windows 에디터/빌드에서만 사용할 수 있습니다.");
        public void Stop() { }
        public void Dispose() { }
#endif
    }

    public sealed class OpenAITranscriptionSpellRecognizer : ISpellSpeechRecognizer
    {
        private const int SampleRate = 16000;
        private const int MaxSeconds = 12;
        private readonly MonoBehaviour host;
        private AudioClip recording;
        private Action<string> resultCallback;
        private Action<string> errorCallback;
        private string expected;
        private string apiKey;
        private string microphoneDevice;
        private bool uploading;
        private int recordingToken;

        public string DisplayName => "OpenAI STT";
        public bool Available => Microphone.devices.Length > 0;
        public bool IsListening => recording != null || uploading;
        public bool IsProcessing => uploading;
        public string LiveTranscript => uploading ? "음성을 텍스트로 변환하는 중…" : string.Empty;
        public float InputLevel => MeasureCurrentLevel();

        public OpenAITranscriptionSpellRecognizer(MonoBehaviour coroutineHost, string key)
        {
            host = coroutineHost;
            apiKey = key?.Trim();
        }

        public void SetApiKey(string key) => apiKey = key?.Trim();
        public void SetMicrophoneDevice(string device) => microphoneDevice = string.IsNullOrWhiteSpace(device) ? null : device;

        public void Begin(string expectedPhrase, Action<string> onResult, Action<string> onError)
        {
            if (uploading) return;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                onError?.Invoke("설정에서 OpenAI API 키를 입력하세요. 키는 이 실행 중에만 메모리에 보관됩니다.");
                return;
            }
            if (!Available)
            {
                onError?.Invoke("사용 가능한 마이크가 없습니다.");
                return;
            }

            resultCallback = onResult;
            errorCallback = onError;
            expected = expectedPhrase;
            recording = Microphone.Start(microphoneDevice, false, MaxSeconds, SampleRate);
            if (recording == null)
            {
                errorCallback?.Invoke("마이크 녹음을 시작하지 못했습니다.");
                return;
            }
            recordingToken++;
            host.StartCoroutine(AutoStop(recordingToken));
            host.StartCoroutine(AutoStopAfterSilence(recordingToken));
        }

        private IEnumerator AutoStopAfterSilence(int token)
        {
            bool heardVoice = false;
            float silenceStarted = 0f;
            float started = Time.realtimeSinceStartup;
            while (recording != null && !uploading && token == recordingToken)
            {
                float level = InputLevel;
                if (level > .075f)
                {
                    heardVoice = true;
                    silenceStarted = 0f;
                }
                else if (heardVoice && Time.realtimeSinceStartup - started > .8f)
                {
                    if (silenceStarted <= 0f) silenceStarted = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - silenceStarted >= .65f)
                    {
                        Stop();
                        yield break;
                    }
                }
                yield return null;
            }
        }

        private IEnumerator AutoStop(int token)
        {
            yield return new WaitForSecondsRealtime(MaxSeconds - .25f);
            if (recording != null && !uploading && token == recordingToken)
            {
                errorCallback?.Invoke("최대 녹음 시간에 도달하여 자동으로 판정합니다.");
                Stop();
            }
        }

        public void Stop()
        {
            if (recording == null || uploading) return;
            int samples = Math.Max(0, Microphone.GetPosition(microphoneDevice));
            Microphone.End(microphoneDevice);
            if (samples < SampleRate / 5)
            {
                recording = null;
                errorCallback?.Invoke("녹음이 너무 짧습니다. 버튼을 누르고 주문을 말한 뒤 다시 눌러주세요.");
                return;
            }

            float[] data = new float[samples * recording.channels];
            recording.GetData(data, 0);
            byte[] wav = WavEncoder.Encode(data, recording.channels, SampleRate);
            recording = null;
            host.StartCoroutine(Transcribe(wav));
        }

        private IEnumerator Transcribe(byte[] wav)
        {
            uploading = true;
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", wav, "spell.wav", "audio/wav"),
                new MultipartFormDataSection("model", "gpt-4o-transcribe"),
                new MultipartFormDataSection("language", "ko"),
                new MultipartFormDataSection("prompt", "한국어 판타지 주문입니다. 예상 문장: " + expected)
            };

            using var request = UnityWebRequest.Post("https://api.openai.com/v1/audio/transcriptions", form);
            request.SetRequestHeader("Authorization", "Bearer " + apiKey);
            request.timeout = 25;
            yield return request.SendWebRequest();
            uploading = false;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string details = request.downloadHandler?.text;
                if (!string.IsNullOrWhiteSpace(details) && details.Length > 220) details = details.Substring(0, 220) + "…";
                errorCallback?.Invoke($"OpenAI STT 요청 실패 ({request.responseCode}): {request.error} {details}");
                yield break;
            }

            var response = JsonUtility.FromJson<TranscriptionResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.text))
                errorCallback?.Invoke("OpenAI가 빈 인식 결과를 반환했습니다.");
            else
                resultCallback?.Invoke(response.text.Trim());
        }

        public void Dispose()
        {
            recordingToken++;
            if (recording != null) Microphone.End(microphoneDevice);
            recording = null;
        }

        private float MeasureCurrentLevel()
        {
            if (recording == null || uploading) return 0f;
            int position = Microphone.GetPosition(microphoneDevice);
            if (position <= 0) return 0f;
            int sampleCount = Mathf.Min(512, position);
            int offset = Mathf.Max(0, position - sampleCount);
            var samples = new float[sampleCount * recording.channels];
            if (!recording.GetData(samples, offset)) return 0f;
            double sum = 0d;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            float rms = Mathf.Sqrt((float)(sum / Math.Max(1, samples.Length)));
            return Mathf.Clamp01(rms * 9f);
        }

        [Serializable]
        private sealed class TranscriptionResponse { public string text = string.Empty; }
    }

    internal static class WavEncoder
    {
        public static byte[] Encode(float[] samples, int channels, int sampleRate)
        {
            const int bitsPerSample = 16;
            int dataSize = samples.Length * 2;
            using var stream = new MemoryStream(44 + dataSize);
            using var writer = new BinaryWriter(stream, Encoding.ASCII);
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write((short)bitsPerSample);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            foreach (float sample in samples)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue));
            writer.Flush();
            return stream.ToArray();
        }
    }
}
