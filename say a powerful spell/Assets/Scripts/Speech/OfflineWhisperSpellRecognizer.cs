using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace PowerfulSpell
{
    /// <summary>Bundled whisper.cpp로 음성을 PC 안에서만 처리한다. API 키와 인터넷이 필요 없다.</summary>
    public sealed class OfflineWhisperSpellRecognizer : ISpellSpeechRecognizer
    {
        private const int SampleRate = 16000;
        private const int MaxSeconds = 35;
        private const int ServerPort = 17871;
        private readonly MonoBehaviour host;
        private AudioClip recording;
        private Action<string> resultCallback;
        private Action<string> errorCallback;
        private string expected;
        private string microphoneDevice;
        private bool processing;
        private int recordingToken;
        private Process serverProcess;
        private bool localServerReady;

        [Serializable]
        private sealed class ServerTranscript
        {
            public string text = string.Empty;
        }

        public string DisplayName => "오프라인 Whisper";
        public bool Available => string.IsNullOrEmpty(AvailabilityError);
        public string AvailabilityError => GetAvailabilityError();
        public bool IsListening => recording != null || processing;
        public bool IsProcessing => processing;
        public float InputLevel => MeasureCurrentLevel();
        public string LiveTranscript => processing ? "PC에서 한국어 음성을 분석하는 중…" : string.Empty;

        private static string RuntimeDirectory => Path.Combine(Application.streamingAssetsPath, "OfflineSpeech");
        private static string ExecutablePath => Path.Combine(RuntimeDirectory, "whisper-cli.exe");
        private static string ServerExecutablePath => Path.Combine(RuntimeDirectory, "whisper-server.exe");
        private static string SmallModelPath => Path.Combine(RuntimeDirectory, "ggml-small.bin");
        private static string BaseModelPath => Path.Combine(RuntimeDirectory, "ggml-base.bin");
        private static string ModelPath => File.Exists(SmallModelPath) ? SmallModelPath : BaseModelPath;
        private static int WorkerThreads => Mathf.Clamp(Environment.ProcessorCount - 2, 4, 10);

        public OfflineWhisperSpellRecognizer(MonoBehaviour coroutineHost)
        {
            host = coroutineHost;
            StartLocalServer();
        }

        public void SetMicrophoneDevice(string device) => microphoneDevice = string.IsNullOrWhiteSpace(device) ? null : device;

        public void Begin(string expectedPhrase, Action<string> onResult, Action<string> onError)
        {
            if (processing) return;
            if (!Available)
            {
                onError?.Invoke(AvailabilityError);
                return;
            }

            expected = expectedPhrase;
            resultCallback = onResult;
            errorCallback = onError;
            recording = Microphone.Start(microphoneDevice, false, MaxSeconds, SampleRate);
            if (recording == null)
            {
                errorCallback?.Invoke("오프라인 음성 녹음을 시작하지 못했습니다.");
                return;
            }
            recordingToken++;
            host.StartCoroutine(MonitorSilence(recordingToken));
        }

        private IEnumerator MonitorSilence(int token)
        {
            bool heardVoice = false;
            float silenceStarted = 0f;
            float voiceStartedAt = 0f;
            float timeoutAt = Time.realtimeSinceStartup + MaxSeconds - .2f;
            float silenceRequired = expected != null && expected.Length > 110 ? .9f : (expected != null && expected.Length > 65 ? .7f : .55f);
            int expectedCharacters = SpellPhraseMatcher.Normalize(expected).Replace(" ", string.Empty).Length;
            float minimumSpeechWindow = Mathf.Clamp(expectedCharacters / 9f, 1.6f, 13f);
            while (recording != null && token == recordingToken)
            {
                float level = InputLevel;
                if (level > .07f)
                {
                    if (!heardVoice) voiceStartedAt = Time.realtimeSinceStartup;
                    heardVoice = true;
                    silenceStarted = 0f;
                }
                else if (heardVoice)
                {
                    if (silenceStarted <= 0f) silenceStarted = Time.realtimeSinceStartup;
                    float spokenWindow = Time.realtimeSinceStartup - voiceStartedAt;
                    float requiredNow = spokenWindow >= minimumSpeechWindow ? silenceRequired : 1.15f;
                    if (Time.realtimeSinceStartup - silenceStarted >= requiredNow)
                    {
                        Stop();
                        yield break;
                    }
                }

                if (Time.realtimeSinceStartup >= timeoutAt)
                {
                    if (heardVoice) Stop(); else CancelWithoutVoice();
                    yield break;
                }
                yield return null;
            }
        }

        public void Stop()
        {
            if (recording == null || processing) return;
            int frames = Math.Max(0, Microphone.GetPosition(microphoneDevice));
            Microphone.End(microphoneDevice);
            if (frames < SampleRate / 4)
            {
                recording = null;
                errorCallback?.Invoke("녹음이 너무 짧습니다. Space를 한 번 누른 뒤 안내음 후 말하세요.");
                return;
            }

            float[] samples = new float[frames * recording.channels];
            recording.GetData(samples, 0);
            if (!NormalizeForRecognition(samples))
            {
                recording = null;
                errorCallback?.Invoke("목소리 음량이 너무 작아 분석하지 않았습니다. 입력 게이지가 움직이는지 확인하세요.");
                return;
            }
            byte[] wav = WavEncoder.Encode(samples, recording.channels, SampleRate);
            recording = null;
            host.StartCoroutine(TranscribeLocally(wav));
        }

        private void CancelWithoutVoice()
        {
            if (recording != null) Microphone.End(microphoneDevice);
            recording = null;
            errorCallback?.Invoke("12초 동안 목소리가 감지되지 않았습니다. 입력 게이지와 마이크를 확인하세요.");
        }

        private IEnumerator TranscribeLocally(byte[] wav)
        {
            processing = true;
            if (File.Exists(ServerExecutablePath))
            {
                localServerReady = false;
                yield return WaitForLocalServer(8f);
                if (localServerReady)
                {
                    using (var request = CreateServerRequest(wav))
                    {
                        request.timeout = 25;
                        yield return request.SendWebRequest();
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            processing = false;
                            var response = JsonUtility.FromJson<ServerTranscript>(request.downloadHandler.text);
                            HandleTranscript(response?.text?.Trim() ?? string.Empty, string.Empty, 0);
                            yield break;
                        }
                        UnityEngine.Debug.LogWarning("[Offline Whisper] 상주 서버 요청 실패, CLI로 대체: " + request.error);
                    }
                }
            }

            yield return TranscribeWithCli(wav);
        }

        private IEnumerator TranscribeWithCli(byte[] wav)
        {
            processing = true;
            string jobId = Guid.NewGuid().ToString("N");
            string wavPath = Path.Combine(Application.temporaryCachePath, "spell-" + jobId + ".wav");
            string outputBase = Path.Combine(Application.temporaryCachePath, "spell-" + jobId);
            string outputText = outputBase + ".txt";
            File.WriteAllBytes(wavPath, wav);

            string vocabularyPrompt = BuildVocabularyPrompt(expected).Replace("\"", string.Empty);
            var startInfo = new ProcessStartInfo
            {
                FileName = ExecutablePath,
                WorkingDirectory = RuntimeDirectory,
                // 정답 문장/순서는 주지 않고 혀 꼬임 고유 어휘만 제공해 받아쓰기 붕괴를 줄인다.
                Arguments = $"-m \"{ModelPath}\" -f \"{wavPath}\" -l ko --prompt \"{vocabularyPrompt}\" -t {WorkerThreads} -ac 1024 -bo 5 -bs 5 -sns -nt -np -otxt -of \"{outputBase}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            Process process = null;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                processing = false;
                TryDelete(wavPath);
                errorCallback?.Invoke("오프라인 Whisper 실행 실패: " + exception.Message);
                yield break;
            }

            float timeoutAt = Time.realtimeSinceStartup + 30f;
            while (process != null && !process.HasExited && Time.realtimeSinceStartup < timeoutAt) yield return null;

            if (process == null || !process.HasExited)
            {
                try { process?.Kill(); } catch { }
                processing = false;
                TryDelete(wavPath);
                errorCallback?.Invoke("오프라인 음성 분석 시간이 30초를 초과했습니다.");
                yield break;
            }

            string stderr = process.StandardError.ReadToEnd();
            int exitCode = process.ExitCode;
            process.Dispose();
            string transcript = File.Exists(outputText) ? File.ReadAllText(outputText).Trim() : string.Empty;
            TryDelete(wavPath);
            TryDelete(outputText);
            processing = false;
            HandleTranscript(transcript, stderr, exitCode);
        }

        private void HandleTranscript(string transcript, string stderr, int exitCode)
        {
            UnityEngine.Debug.Log($"[Offline Whisper] raw transcript=\"{transcript}\" model={Path.GetFileName(ModelPath)}");

            if (exitCode != 0 || string.IsNullOrWhiteSpace(transcript))
            {
                if (stderr.Length > 240) stderr = stderr.Substring(stderr.Length - 240);
                errorCallback?.Invoke("오프라인 음성에서 문장을 찾지 못했습니다. " + stderr);
            }
            else if (LooksLikeDegenerateOutput(transcript))
            {
                errorCallback?.Invoke($"음성 모델이 반복 잡음으로 잘못 인식하여 결과를 폐기했습니다: “{Shorten(transcript, 80)}” 다시 또박또박 말해주세요.");
            }
            else
            {
                resultCallback?.Invoke(transcript);
            }
        }

        private void StartLocalServer()
        {
            if (!File.Exists(ServerExecutablePath) || !HasUsableModel()) return;
            try
            {
                serverProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = ServerExecutablePath,
                    WorkingDirectory = RuntimeDirectory,
                    Arguments = $"--host 127.0.0.1 --port {ServerPort} -m \"{ModelPath}\" -l ko -t {WorkerThreads} -ac 1024 -bo 5 -bs 5 -sns -nt -fa",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning("[Offline Whisper] 상주 서버 시작 실패, CLI를 사용합니다: " + exception.Message);
            }
        }

        private static bool HasUsableModel()
        {
            try { return File.Exists(ModelPath) && new FileInfo(ModelPath).Length > 100_000_000L; }
            catch { return false; }
        }

        private static string GetAvailabilityError()
        {
            if (Microphone.devices.Length == 0)
                return "마이크 장치를 찾지 못했습니다. 음성 설정과 Windows 마이크 권한을 확인하세요.";
            if (!Directory.Exists(RuntimeDirectory))
                return "Whisper 폴더가 누락되었습니다: Assets/StreamingAssets/OfflineSpeech";
            if (!File.Exists(ExecutablePath) || !File.Exists(ServerExecutablePath))
                return "Whisper 실행 파일이 누락되었습니다. Git에서 프로젝트 파일을 다시 받아주세요.";
            string[] dlls = { "whisper.dll", "ggml.dll", "ggml-base.dll", "ggml-cpu.dll" };
            foreach (string dll in dlls)
                if (!File.Exists(Path.Combine(RuntimeDirectory, dll)))
                    return $"Whisper 필수 파일이 누락되었습니다: {dll}";
            if (!File.Exists(SmallModelPath) && !File.Exists(BaseModelPath))
                return "Whisper 한국어 모델이 설치되지 않았습니다. Unity 상단 메뉴의 Powerful Spell → Whisper 모델 설치 및 확인을 실행하세요.";
            if (!HasUsableModel())
                return "Whisper 모델이 미완료 또는 손상된 파일입니다. Powerful Spell → Whisper 모델 설치 및 확인에서 다시 설치하세요.";
            return string.Empty;
        }

        private IEnumerator WaitForLocalServer(float maxSeconds)
        {
            float deadline = Time.realtimeSinceStartup + maxSeconds;
            while (Time.realtimeSinceStartup < deadline)
            {
                using (var probe = UnityWebRequest.Get($"http://127.0.0.1:{ServerPort}/"))
                {
                    probe.timeout = 1;
                    yield return probe.SendWebRequest();
                    if (probe.result == UnityWebRequest.Result.Success)
                    {
                        localServerReady = true;
                        yield break;
                    }
                }
                yield return new WaitForSecondsRealtime(.08f);
            }
        }

        private UnityWebRequest CreateServerRequest(byte[] wav)
        {
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", wav, "spell.wav", "audio/wav"),
                new MultipartFormDataSection("language", "ko"),
                new MultipartFormDataSection("prompt", BuildVocabularyPrompt(expected)),
                new MultipartFormDataSection("temperature", "0.0"),
                new MultipartFormDataSection("temperature_inc", "0.2"),
                new MultipartFormDataSection("response_format", "json")
            };
            return UnityWebRequest.Post($"http://127.0.0.1:{ServerPort}/inference", form);
        }

        private static string BuildVocabularyPrompt(string phrase)
        {
            string normalized = SpellPhraseMatcher.Normalize(phrase);
            var unique = new HashSet<string>();
            var words = new List<string>();
            foreach (string word in normalized.Split(' '))
            {
                if (word.Length < 2 || !unique.Add(word)) continue;
                words.Add(word);
            }
            return "한국어 혀 꼬임 주문 어휘: " + string.Join(", ", words);
        }

        public void Dispose()
        {
            recordingToken++;
            if (recording != null) Microphone.End(microphoneDevice);
            recording = null;
            try { if (serverProcess != null && !serverProcess.HasExited) serverProcess.Kill(); } catch { }
            serverProcess?.Dispose();
            serverProcess = null;
        }

        private float MeasureCurrentLevel()
        {
            if (recording == null || processing) return 0f;
            int position = Microphone.GetPosition(microphoneDevice);
            if (position <= 0) return 0f;
            int count = Mathf.Min(512, position);
            int offset = Mathf.Max(0, position - count);
            var samples = new float[count * recording.channels];
            if (!recording.GetData(samples, offset)) return 0f;
            double sum = 0d;
            for (int i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            return Mathf.Clamp01(Mathf.Sqrt((float)(sum / Math.Max(1, samples.Length))) * 10f);
        }

        private static bool NormalizeForRecognition(float[] samples)
        {
            double sum = 0d;
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float value = samples[i];
                sum += value * value;
                peak = Mathf.Max(peak, Mathf.Abs(value));
            }
            float rms = Mathf.Sqrt((float)(sum / Math.Max(1, samples.Length)));
            if (rms < .0025f || peak < .006f) return false;
            float gain = Mathf.Min(8f, Mathf.Min(.9f / peak, .12f / rms));
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Clamp(samples[i] * gain, -.95f, .95f);
            return true;
        }

        private static bool LooksLikeDegenerateOutput(string text)
        {
            int rawJamo = 0;
            int rawLatin = 0;
            int rawVisible = 0;
            foreach (char c in text)
            {
                if (c >= 'ㄱ' && c <= 'ㅣ') rawJamo++;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')) rawLatin++;
                if (!char.IsWhiteSpace(c) && !char.IsPunctuation(c)) rawVisible++;
            }

            // Whisper가 잡음을 음소나 영문자로 붕괴시킨 결과는 주문 비교 전에 폐기한다.
            // 정규화하면 단독 자모가 사라질 수 있으므로 반드시 원문을 먼저 검사한다.
            if (rawJamo >= 4) return true;
            if (rawLatin >= 5 && rawLatin * 2 >= Math.Max(1, rawVisible)) return true;

            string compact = SpellPhraseMatcher.Normalize(text).Replace(" ", string.Empty);
            if (compact.Length == 0) return true;

            int korean = 0;
            int latin = 0;
            int longestRun = 1;
            int currentRun = 1;
            var unique = new System.Collections.Generic.HashSet<char>();
            for (int i = 0; i < compact.Length; i++)
            {
                char c = compact[i];
                unique.Add(c);
                if ((c >= '가' && c <= '힣') || (c >= 'ㄱ' && c <= 'ㅣ')) korean++;
                if (c >= 'a' && c <= 'z') latin++;
                if (i > 0 && c == compact[i - 1]) currentRun++; else currentRun = 1;
                longestRun = Math.Max(longestRun, currentRun);
            }

            float diversity = unique.Count / (float)compact.Length;
            if (longestRun >= 5) return true;
            if (compact.Length >= 12 && diversity < .16f) return true;
            if (latin >= 5 && latin > korean / 2) return true;
            return false;
        }

        private static string Shorten(string value, int maxLength) => value.Length <= maxLength ? value : value.Substring(0, maxLength) + "…";

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
