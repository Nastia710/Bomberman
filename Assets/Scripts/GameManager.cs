using System;
using System.Collections.Generic;
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
        [SerializeField]
        private GameObject _bomberManPrefab;

        private readonly Vector2[] _arenaSpawnPositions = new Vector2[]
        {
            new Vector2(-7f, 5f),
            new Vector2(-7f, 3f),
            new Vector2(-7f, -1f),
            new Vector2(-7f, -5f),
            new Vector2(-5f, 1f)
        };

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
        private float _baseFontSize;
        private bool _showingGo;
        private float _goTimer;

        private void Awake()
        {
            Instance = this;
            if (_countdownText != null)
            {
                _baseFontSize = _countdownText.fontSize;
            }
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

                SpawnArenaPlayers();
            }

            _showingGo = false;
            UpdateCountdownDisplay();
        }

        public override void OnNetworkDespawn()
        {
            _gameState.OnValueChanged -= OnGameStateChanged;
            _countdownTimer.OnValueChanged -= OnCountdownTimerChanged;
        }

        private void Update()
        {
            if (_showingGo)
            {
                _goTimer -= Time.deltaTime;
                if (_goTimer <= 0f)
                {
                    _showingGo = false;
                    _countdownText.gameObject.SetActive(false);
                    GameStartedEvent?.Invoke();
                }
                return;
            }

            if (_gameState.Value == GameState.Countdown && _countdownTimer.Value > 0f)
            {
                AnimateCountdownText();
            }

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

        private void SpawnArenaPlayers()
        {
            List<ulong> clientIds = new List<ulong>(NetworkManager.Singleton.ConnectedClientsIds);

            for (int i = 0; i < clientIds.Count && i < _arenaSpawnPositions.Length; i++)
            {
                Vector2 spawnPos = _arenaSpawnPositions[i];
                GameObject player = Instantiate(
                    _bomberManPrefab,
                    new Vector3(spawnPos.x, spawnPos.y, 0f),
                    Quaternion.identity);

                NetworkObject networkObject = player.GetComponent<NetworkObject>();
                networkObject.SpawnWithOwnership(clientIds[i]);
            }
        }

        private void OnGameStateChanged(GameState oldValue, GameState newValue)
        {
            if (newValue == GameState.Playing)
            {
                _countdownText.text = "GO!";
                _countdownText.fontSize = _baseFontSize;
                Color goColor = _countdownText.color;
                goColor.a = 1f;
                _countdownText.color = goColor;
                _showingGo = true;
                _goTimer = 1f;
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
            if (displayNumber > 0)
            {
                _countdownText.text = displayNumber.ToString();
            }
        }

        private void AnimateCountdownText()
        {
            if (_countdownText == null)
            {
                return;
            }

            float fractional = _countdownTimer.Value % 1f;
            if (fractional == 0f && _countdownTimer.Value > 0f)
            {
                fractional = 1f;
            }

            float progress = 1f - fractional;

            _countdownText.fontSize = _baseFontSize + (50f * progress);

            Color c = _countdownText.color;
            c.a = Mathf.Lerp(1f, 0.333f, progress);
            _countdownText.color = c;
        }

        public bool IsPlaying()
        {
            return _gameState.Value == GameState.Playing;
        }
    }
}
