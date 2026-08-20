using UnityEngine;

namespace Game.Core
{
    public enum GameState
    {
        Booting,
        Loading,
        Playing,
        Paused,
        GameOver
    }

    /// <summary>게임 전체 상태와 일시정지를 관리합니다.</summary>
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public GameState State { get; private set; } = GameState.Booting;

        public event System.Action<GameState> StateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this) return;
            Instance = this;
        }

        public void SetState(GameState nextState)
        {
            if (State == nextState) return;
            State = nextState;
            StateChanged?.Invoke(State);
        }

        public void Pause()
        {
            if (State != GameState.Playing) return;
            Time.timeScale = 0f;
            SetState(GameState.Paused);
        }

        public void Resume()
        {
            if (State != GameState.Paused) return;
            Time.timeScale = 1f;
            SetState(GameState.Playing);
        }

        public void GameOver()
        {
            Time.timeScale = 1f;
            SetState(GameState.GameOver);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
