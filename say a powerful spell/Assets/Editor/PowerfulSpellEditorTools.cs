#if UNITY_EDITOR
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace PowerfulSpell.Editor
{
    public static class PowerfulSpellEditorTools
    {
        [MenuItem("Powerful Spell/프로토타입 진행도 초기화")]
        private static void ResetProgress()
        {
            PlayerPrefs.DeleteKey("PowerfulSpell.HighestUnlockedStage");
            PlayerPrefs.Save();
            Debug.Log("[Powerful Spell] 스테이지/주문 해금 진행도를 초기화했습니다.");
        }

        [MenuItem("Powerful Spell/모든 스테이지와 주문 해금")]
        private static void UnlockAll()
        {
            PlayerPrefs.SetInt("PowerfulSpell.HighestUnlockedStage", 5);
            PlayerPrefs.Save();
            Debug.Log("[Powerful Spell] 5개 스테이지와 모든 주문을 해금했습니다.");
        }
    }

    public sealed class WhisperModelInstallerWindow : EditorWindow
    {
        private const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin";
        private const string ExpectedSha1 = "55356645c2b361a969dfd0ef2c5a50d530afd8d5";
        private const long MinimumModelBytes = 480_000_000L;
        private UnityWebRequest request;
        private string status = string.Empty;
        private bool verifying;

        private static string ModelDirectory => Path.Combine(Application.dataPath, "StreamingAssets", "OfflineSpeech");
        private static string ModelPath => Path.Combine(ModelDirectory, "ggml-small.bin");
        private static string DownloadPath => ModelPath + ".download";

        [MenuItem("Powerful Spell/Whisper 모델 설치 및 확인", priority = 1)]
        private static void OpenWindow()
        {
            var window = GetWindow<WhisperModelInstallerWindow>(true, "Whisper 모델 설치", true);
            window.minSize = new Vector2(540, 270);
            window.RefreshStatus();
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(14);
            EditorGUILayout.LabelField("오프라인 Whisper 한국어 모델", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "모델은 GitHub에 저장하지 않습니다. 각 팀원은 프로젝트를 받은 뒤 이 창에서 한 번 설치하면 됩니다. " +
                "공식 whisper.cpp 다국어 small 모델(약 466MiB)을 내려받고 SHA-1을 검증합니다.", MessageType.Info);
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("설치 위치", ModelPath);
            EditorGUILayout.LabelField("상태", status);

            if (request != null)
            {
                Rect progressRect = EditorGUILayout.GetControlRect(false, 24);
                EditorGUI.ProgressBar(progressRect, request.downloadProgress,
                    $"다운로드 중... {request.downloadProgress * 100f:0.0}%");
                if (GUILayout.Button("다운로드 취소")) CancelDownload();
            }
            else
            {
                EditorGUI.BeginDisabledGroup(verifying);
                if (GUILayout.Button(File.Exists(ModelPath) ? "모델 다시 설치" : "공식 모델 다운로드 및 설치", GUILayout.Height(38)))
                    BeginDownload();
                if (File.Exists(ModelPath) && GUILayout.Button("설치된 모델 무결성 다시 확인")) VerifyInstalledModel();
                EditorGUI.EndDisabledGroup();
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("설치 후 Play를 완전히 종료했다가 다시 시작하면 Whisper가 모델을 읽습니다.", MessageType.None);
        }

        private void BeginDownload()
        {
            Directory.CreateDirectory(ModelDirectory);
            TryDelete(DownloadPath);
            status = "공식 모델 다운로드를 준비하는 중...";
            request = UnityWebRequest.Get(ModelUrl);
            request.downloadHandler = new DownloadHandlerFile(DownloadPath);
            request.SendWebRequest();
            EditorApplication.update += PollDownload;
        }

        private void PollDownload()
        {
            if (request == null) return;
            Repaint();
            if (!request.isDone) return;

            EditorApplication.update -= PollDownload;
            if (request.result != UnityWebRequest.Result.Success)
            {
                status = "다운로드 실패: " + request.error;
                request.Dispose();
                request = null;
                TryDelete(DownloadPath);
                return;
            }

            request.Dispose();
            request = null;
            if (!VerifyFile(DownloadPath, out string detail))
            {
                status = "다운로드한 모델 검증 실패: " + detail;
                TryDelete(DownloadPath);
                return;
            }

            TryDelete(ModelPath);
            File.Move(DownloadPath, ModelPath);
            AssetDatabase.Refresh();
            status = "설치 완료 · SHA-1 검증 정상 · Play를 다시 시작하세요.";
            Debug.Log("[Powerful Spell] Whisper small 한국어 모델 설치 및 SHA-1 검증을 완료했습니다.");
        }

        private void VerifyInstalledModel()
        {
            verifying = true;
            status = "모델 무결성을 확인하는 중...";
            Repaint();
            try
            {
                status = VerifyFile(ModelPath, out string detail)
                    ? "설치 정상 · SHA-1 검증 완료"
                    : "모델 검증 실패: " + detail;
            }
            finally
            {
                verifying = false;
            }
        }

        private static bool VerifyFile(string path, out string detail)
        {
            if (!File.Exists(path))
            {
                detail = "파일이 없습니다.";
                return false;
            }
            var info = new FileInfo(path);
            if (info.Length < MinimumModelBytes)
            {
                detail = $"파일 크기가 너무 작습니다 ({info.Length / 1024 / 1024}MiB).";
                return false;
            }
            using var sha1 = SHA1.Create();
            using var stream = File.OpenRead(path);
            string actual = BitConverter.ToString(sha1.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
            detail = actual;
            return actual == ExpectedSha1;
        }

        private void RefreshStatus()
        {
            if (!File.Exists(ModelPath)) status = "미설치 · 다운로드가 필요합니다.";
            else if (new FileInfo(ModelPath).Length < MinimumModelBytes) status = "미완료 또는 손상된 모델";
            else status = "모델 파일 발견 · 무결성 확인을 권장합니다.";
        }

        private void CancelDownload()
        {
            EditorApplication.update -= PollDownload;
            request?.Abort();
            request?.Dispose();
            request = null;
            TryDelete(DownloadPath);
            status = "다운로드를 취소했습니다.";
        }

        private void OnDisable()
        {
            if (request != null) CancelDownload();
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }
}
#endif
