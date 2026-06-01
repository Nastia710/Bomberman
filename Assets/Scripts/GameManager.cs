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
		public static GameManager Instance { get; private set; }

		[SerializeField]
		private TMP_Text _countdownText;
		[SerializeField]
		private float _countdownDuration = 3f;
		[SerializeField]
		private GameObject _bomberManPrefab;
		[SerializeField]
		private GameObject[] _powerUpPrefabs;
		[SerializeField]
		private GameObject _powerUpTextPrefab;
		[SerializeField]
		private Transform _powerUpContainer;

		public Transform PowerUpContainer
		{
			get
			{
				return _powerUpContainer;
			}
		}

		private int _powerUpsToSpawn;
		private Dictionary<Vector2, int> _powerUpLocations = new Dictionary<Vector2, int>();

		private bool _gameEnded = false;
		private bool _allClientsLoaded = false;

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

		private bool _countdownFinished;
		private float _baseFontSize;
		private bool _showingGo;
		private float _goTimer;

		public static event Action GameStartedEvent;

		private void Awake()
		{
			Instance = this;
			if (_countdownText != null)
			{
				_baseFontSize = _countdownText.fontSize;
			}
		}

		private void OnEnable()
		{
			SubscribeEvents();
		}

		private void OnDisable()
		{
			UnsubscribeEvents();
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
				_allClientsLoaded = false;
				_gameEnded = false;

				if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.SceneManager != null)
				{
					Unity.Netcode.NetworkManager.Singleton.SceneManager.OnLoadEventCompleted += OnLoadEventCompleted;
				}
				else
				{
					_allClientsLoaded = true;
				}

				_powerUpLocations.Clear();
				
				GameObject[] bricks = GameObject.FindGameObjectsWithTag("Brick");
				List<int> availableIndices = new List<int>() { 0, 1, 2, 3, 4, 5, 6 };
				
				if (bricks.Length >= 7)
				{
					List<GameObject> shuffledBricks = new System.Collections.Generic.List<GameObject>(bricks);
					for (int i = 0; i < shuffledBricks.Count; i++)
					{
						GameObject temp = shuffledBricks[i];
						int randomIndex = UnityEngine.Random.Range(i, shuffledBricks.Count);
						shuffledBricks[i] = shuffledBricks[randomIndex];
						shuffledBricks[randomIndex] = temp;
					}

					for (int i = 0; i < 7; i++)
					{
						Vector2 pos = new Vector2(
							Mathf.Round(shuffledBricks[i].transform.position.x),
							Mathf.Round(shuffledBricks[i].transform.position.y)
						);
						_powerUpLocations[pos] = availableIndices[i];
					}
				}

				SpawnArenaPlayers();
			}

			_showingGo = false;
			UpdateCountdownDisplay();
		}

		public override void OnNetworkDespawn()
		{
			_gameState.OnValueChanged -= OnGameStateChanged;
			_countdownTimer.OnValueChanged -= OnCountdownTimerChanged;

			if (Unity.Netcode.NetworkManager.Singleton != null)
			{
				if (IsServer && Unity.Netcode.NetworkManager.Singleton.SceneManager != null)
				{
					Unity.Netcode.NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
				}
			}
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
					if (GameStartedEvent != null)
					{
						GameStartedEvent.Invoke();
					}
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

			if (_gameState.Value == GameState.Countdown && !_countdownFinished && _allClientsLoaded)
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

		public void OnBrickDestroyed(Vector3 position)
		{
			if (!IsServer)
			{
				return;
			}

			Vector2 key = new Vector2(Mathf.Round(position.x), Mathf.Round(position.y));
			if (_powerUpLocations.TryGetValue(key, out int powerUpIndex))
			{
				_powerUpLocations.Remove(key);
				if (_powerUpPrefabs != null && powerUpIndex < _powerUpPrefabs.Length && _powerUpPrefabs[powerUpIndex] != null)
				{
					GameObject powerUp = Instantiate(_powerUpPrefabs[powerUpIndex], position, Quaternion.identity);
					powerUp.GetComponent<NetworkObject>().Spawn();
				}
			}
		}

		public void NotifyPlayerDied(string deadPlayerName)
		{
			if (!IsServer)
			{
				return;
			}

			ShowGlobalMessageClientRpc($"{deadPlayerName} LOSE!", 5f);

			if (!_gameEnded)
			{
				StartCoroutine(CheckWinConditionRoutine());
			}
		}

		public bool IsPlaying()
		{
			return _gameState.Value == GameState.Playing;
		}

		private void SubscribeEvents()
		{
			if (Unity.Netcode.NetworkManager.Singleton != null)
			{
				Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
			}
		}

		private void UnsubscribeEvents()
		{
			if (Unity.Netcode.NetworkManager.Singleton != null)
			{
				Unity.Netcode.NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
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

		private void OnLoadEventCompleted(string sceneName, UnityEngine.SceneManagement.LoadSceneMode loadSceneMode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
		{
			if (sceneName == "SampleScene")
			{
				_allClientsLoaded = true;
				if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.SceneManager != null)
				{
					Unity.Netcode.NetworkManager.Singleton.SceneManager.OnLoadEventCompleted -= OnLoadEventCompleted;
				}
			}
		}

		private System.Collections.IEnumerator CheckWinConditionRoutine()
		{
			yield return new WaitForSeconds(0.2f);

			if (_gameEnded)
			{
				yield break;
			}

			int aliveCount = 0;
			BomberMan winner = null;

			foreach (var player in BomberMan.AllPlayers)
			{
				if (player != null && player.GetComponent<NetworkObject>().IsSpawned)
				{
					aliveCount++;
					winner = player;
				}
			}

			if (aliveCount == 1 && winner != null)
			{
				_gameEnded = true;
				ShowGlobalMessageClientRpc($"{winner.Nickname} WIN!", 0f);
				StartCoroutine(ReturnToLobbyRoutine());
			}
			else if (aliveCount == 0)
			{
				_gameEnded = true;
				ShowGlobalMessageClientRpc("DRAW!", 0f);
				StartCoroutine(ReturnToLobbyRoutine());
			}
		}

		private System.Collections.IEnumerator ReturnToLobbyRoutine()
		{
			yield return new WaitForSeconds(5f);
			if (IsServer && Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.SceneManager != null)
			{
				foreach (var obj in FindObjectsByType<NetworkObject>(FindObjectsSortMode.None))
				{
					if (obj.IsSpawned && (obj.GetComponent<BomberMan>() != null || obj.GetComponent<Enemy>() != null || obj.GetComponent<Bomb>() != null || obj.GetComponent<PowerUp>() != null))
					{
						obj.Despawn(true);
					}
				}

				Unity.Netcode.NetworkManager.Singleton.SceneManager.LoadScene("LobbyScene", UnityEngine.SceneManagement.LoadSceneMode.Single);
			}
		}

		[ClientRpc]
		private void ShowGlobalMessageClientRpc(string message, float duration)
		{
			if (_powerUpTextPrefab == null || _powerUpContainer == null)
			{
				return;
			}

			GameObject textObj = Instantiate(_powerUpTextPrefab, _powerUpContainer);
			TMPro.TMP_Text uiText = textObj.GetComponent<TMPro.TMP_Text>();
			if (uiText != null)
			{
				uiText.text = message;
			}
			
			if (duration > 0)
			{
				Destroy(textObj, duration);
			}
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (!IsServer && (clientId == 0 || clientId == Unity.Netcode.NetworkManager.Singleton.LocalClientId))
			{
				LobbyManager.HasBeenDisconnectedFromGame = true;
				UnityEngine.SceneManagement.SceneManager.LoadScene("LobbyScene");
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
	}
}
