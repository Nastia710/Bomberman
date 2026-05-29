using System;
using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace Bomberman
{
    public enum GameState
    {
        Waiting = 0,
        Countdown = 1,
        Playing = 2
    }

    public class GameManager : NetworkBehaviour
    {
        [SerializeField]
        private TMP_Text _countdownText;
        [SerializeField]
        private float _countdownDuration = 3f;

        private NetworkVariable<GameState> _gameState = new NetworkVariable<GameState>(
            GameState.Countdown,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private NetworkVariable<float> _countdownTimer = new NetworkVariable<float>(
            3f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public static GameManager Instance { get; private set; }
        public static event Action GameStartedEvent;

        private bool _countdownFinished;

        private void Awake()
        {
            Instance = this;
        }

        public override void OnNetworkSpawn()
        {
            _gameState.OnValueChanged += OnGameStateChanged;
            _countdownTimer.OnValueChanged += OnCountdownTimerChanged;

            if (IsServer)
            {
                _gameState.Value = GameState.Countdown;
                _countdownTimer.Value = _countdownDuration;
                _countdownFinished = false;
            }

            UpdateCountdownDisplay();
        }

        public override void OnNetworkDespawn()
        {
            _gameState.OnValueChanged -= OnGameStateChanged;
            _countdownTimer.OnValueChanged -= OnCountdownTimerChanged;
        }

        private void Update()
        {
            if (!IsServer)
            {
                return;
            }

            if (_gameState.Value == GameState.Countdown && !_countdownFinished)
            {
                _countdownTimer.Value -= Time.deltaTime;

                if (_countdownTimer.Value <= 0f)
                {
                    _countdownFinished = true;
                    _countdownTimer.Value = 0f;
                    _gameState.Value = GameState.Playing;
                }
            }
        }

        private void OnGameStateChanged(GameState oldValue, GameState newValue)
        {
            if (newValue == GameState.Playing)
            {
                _countdownText.gameObject.SetActive(false);
                GameStartedEvent?.Invoke();
            }
        }

        private void OnCountdownTimerChanged(float oldValue, float newValue)
        {
            UpdateCountdownDisplay();
        }

        private void UpdateCountdownDisplay()
        {
            if (_gameState.Value != GameState.Countdown)
            {
                return;
            }

            int displayNumber = Mathf.CeilToInt(_countdownTimer.Value);
            if (displayNumber <= 0)
            {
                _countdownText.text = "GO!";
            }
            else
            {
                _countdownText.text = displayNumber.ToString();
            }
        }

        public bool IsPlaying()
        {
            return _gameState.Value == GameState.Playing;
        }
    }
}
