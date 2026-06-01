using System;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Bomberman
{
	public class LobbyManager : NetworkBehaviour
	{
		public static string PlayerName { get; private set; } = "Player";
		public static bool HasBeenDisconnectedFromGame = false;

		[SerializeField]
		private GameObject _lobbyPlayerPrefab;
		[SerializeField]
		private Button _hostButton;
		[SerializeField]
		private Button _joinButton;
		[SerializeField]
		private Button _startButton;
		[SerializeField]
		private Button _disconnectButton;
		[SerializeField]
		private TMP_InputField _nicknameInput;
		[SerializeField]
		private TMP_InputField _passwordInput;
		[SerializeField]
		private TMP_Text _statusText;
		[SerializeField]
		private TMP_Text _errorText;
		[SerializeField]
		private GameObject _menuPanel;
		[SerializeField]
		private GameObject _lobbyPanel;
		[SerializeField]
		private GameObject _disconnectedPanel;
		[SerializeField]
		private string _gameSceneName = "SampleScene";

		private NetworkVariable<int> _playerCount = new NetworkVariable<int>(
			0,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Server);

		private Dictionary<ulong, GameObject> _lobbyPlayers = new Dictionary<ulong, GameObject>();
		private Dictionary<ulong, int> _playerSlots = new Dictionary<ulong, int>();
		private bool[] _occupiedSlots = new bool[5];

		private string _localNickname = "Player";
		private string _hostPassword = "";

		private const int MAX_PLAYERS = 5;
		private const string REASON_WRONG_PASSWORD = "Wrong password";
		private const string REASON_NO_ROOM = "No room with this password";
		private const string REASON_ROOM_FULL = "Room is full";

		private readonly Vector2[] _lobbyPositions = new Vector2[]
		{
			new Vector2(0f, 0f),
			new Vector2(-3.52f, 1.42f),
			new Vector2(-3.52f, -1.42f),
			new Vector2(3.52f, -1.42f),
			new Vector2(3.52f, 1.42f)
		};

		private void OnEnable()
		{
			SubscribeEvents();
		}

		private void Start()
		{
			if (NetworkManager.Singleton != null)
			{
				NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;
			}

			if (HasBeenDisconnectedFromGame)
			{
				ShowDisconnectedPanel();
				HasBeenDisconnectedFromGame = false;
			}
			else
			{
				ShowMenuPanel();
			}

			if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
			{
				ShowLobbyPanel();
				
				if (NetworkManager.Singleton.IsServer)
				{
					SetupServerHost();
				}
				else
				{
					SetupClient();
				}
			}
		}

		private void OnDisable()
		{
			UnsubscribeEvents();
		}

		public override void OnDestroy()
		{
			base.OnDestroy();
			UnsubscribeNetworkEvents();
		}

		public void ShowMenuPanel()
		{
			_menuPanel.SetActive(true);
			_lobbyPanel.SetActive(false);
			_disconnectedPanel.SetActive(false);
			_startButton.gameObject.SetActive(false);
			HideError();
		}

		private void ShowLobbyPanel()
		{
			_menuPanel.SetActive(false);
			_lobbyPanel.SetActive(true);
			_disconnectedPanel.SetActive(false);
			HideError();
		}

		private void ShowDisconnectedPanel()
		{
			_menuPanel.SetActive(false);
			_lobbyPanel.SetActive(false);
			_disconnectedPanel.SetActive(true);
		}

		private void ShowError(string message)
		{
			if (_errorText != null)
			{
				_errorText.gameObject.SetActive(true);
				_errorText.text = message;
			}
		}

		private void HideError()
		{
			if (_errorText != null)
			{
				_errorText.gameObject.SetActive(false);
			}
		}

		private void SetupServerHost()
		{
			NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;
			NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
			
			foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
			{
				SpawnLobbyPlayer(clientId);
			}
			_playerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
			UpdateStatusText();
			UpdateStartButton();
		}

		private void SetupClient()
		{
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
			_statusText.text = "Connected";
		}

		private bool IsRejectionReason(string reason)
		{
			return reason == REASON_WRONG_PASSWORD
				|| reason == REASON_NO_ROOM
				|| reason == REASON_ROOM_FULL;
		}

		private void OnHostClicked()
		{
			_localNickname = GetNickname();
			_hostPassword = _passwordInput.text.Trim();
			PlayerName = _localNickname;
			
			NetworkManager.Singleton.ConnectionApprovalCallback = OnConnectionApproval;
			NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
			NetworkManager.Singleton.StartHost();
			
			ShowLobbyPanel();
			UpdateStatusText();
		}

		private void OnJoinClicked()
		{
			_localNickname = GetNickname();
			string password = _passwordInput.text.Trim();
			PlayerName = _localNickname;
			
			NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(password);
			NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
			NetworkManager.Singleton.StartClient();
			
			ShowLobbyPanel();
			_statusText.text = "Connecting...";
		}

		private void OnStartClicked()
		{
			if (!IsServer)
			{
				return;
			}

			if (NetworkManager.Singleton.ConnectedClientsIds.Count < 2)
			{
				return;
			}

			_startButton.interactable = false;
			ClearLobbyPlayers();
			NetworkManager.Singleton.SceneManager.LoadScene(_gameSceneName, LoadSceneMode.Single);
		}

		private void OnDisconnectClicked()
		{
			if (NetworkManager.Singleton != null)
			{
				NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
				NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
				NetworkManager.Singleton.ConnectionApprovalCallback = null;
				NetworkManager.Singleton.Shutdown();
			}

			ClearLobbyPlayers();
			ShowMenuPanel();
		}

		private void OnConnectionApproval(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
		{
			if (NetworkManager.Singleton.ConnectedClientsIds.Count >= MAX_PLAYERS)
			{
				RejectConnection(response, REASON_ROOM_FULL);
				return;
			}

			string clientPassword = GetClientPassword(request);

			if (request.ClientNetworkId == NetworkManager.Singleton.LocalClientId)
			{
				ApproveConnection(response);
				return;
			}

			ValidatePasswordAndApprove(clientPassword, response);
		}

		private string GetClientPassword(NetworkManager.ConnectionApprovalRequest request)
		{
			if (request.Payload != null)
			{
				return Encoding.UTF8.GetString(request.Payload);
			}
			return "";
		}

		private void RejectConnection(NetworkManager.ConnectionApprovalResponse response, string reason)
		{
			response.Approved = false;
			response.Reason = reason;
		}

		private void ApproveConnection(NetworkManager.ConnectionApprovalResponse response)
		{
			response.Approved = true;
			response.CreatePlayerObject = false;
		}

		private void ValidatePasswordAndApprove(string clientPassword, NetworkManager.ConnectionApprovalResponse response)
		{
			if (!string.IsNullOrEmpty(_hostPassword))
			{
				if (clientPassword != _hostPassword)
				{
					RejectConnection(response, REASON_WRONG_PASSWORD);
					return;
				}
			}
			else
			{
				if (!string.IsNullOrEmpty(clientPassword))
				{
					RejectConnection(response, REASON_NO_ROOM);
					return;
				}
			}

			ApproveConnection(response);
		}

		private void OnClientConnected(ulong clientId)
		{
			if (!IsServer)
			{
				return;
			}

			_playerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
			SpawnLobbyPlayer(clientId);
			UpdateStatusText();
			UpdateStartButton();
		}

		private void OnClientDisconnected(ulong clientId)
		{
			if (IsServer)
			{
				HandleServerClientDisconnect(clientId);
			}
			else
			{
				HandleLocalClientDisconnect();
			}
		}

		private void HandleServerClientDisconnect(ulong clientId)
		{
			RemoveLobbyPlayer(clientId);
			FreePlayerSlot(clientId);

			_playerCount.Value = NetworkManager.Singleton.ConnectedClientsIds.Count;
			UpdateStatusText();
			UpdateStartButton();
		}

		private void RemoveLobbyPlayer(ulong clientId)
		{
			if (_lobbyPlayers.ContainsKey(clientId))
			{
				GameObject playerObj = _lobbyPlayers[clientId];
				if (playerObj != null)
				{
					NetworkObject networkObj = playerObj.GetComponent<NetworkObject>();
					if (networkObj != null && networkObj.IsSpawned)
					{
						networkObj.Despawn(true);
					}
				}
				_lobbyPlayers.Remove(clientId);
			}
		}

		private void FreePlayerSlot(ulong clientId)
		{
			if (_playerSlots.ContainsKey(clientId))
			{
				int slot = _playerSlots[clientId];
				if (slot >= 0 && slot < MAX_PLAYERS)
				{
					_occupiedSlots[slot] = false;
				}
				_playerSlots.Remove(clientId);
			}
		}

		private void HandleLocalClientDisconnect()
		{
			string reason = NetworkManager.Singleton.DisconnectReason;
			NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
			NetworkManager.Singleton.Shutdown();
			ClearLobbyPlayers();

			if (IsRejectionReason(reason))
			{
				ShowMenuPanel();
				ShowError(reason);
			}
			else
			{
				ShowDisconnectedPanel();
			}
		}

		private void SpawnLobbyPlayer(ulong clientId)
		{
			int slot = GetFirstAvailableSlot();
			if (slot == -1)
			{
				return;
			}

			_occupiedSlots[slot] = true;
			_playerSlots[clientId] = slot;

			Vector2 position = _lobbyPositions[slot];
			GameObject lobbyPlayer = Instantiate(
				_lobbyPlayerPrefab,
				new Vector3(position.x, position.y, 0f),
				Quaternion.identity);

			NetworkObject networkObject = lobbyPlayer.GetComponent<NetworkObject>();
			networkObject.SpawnWithOwnership(clientId);
			_lobbyPlayers[clientId] = lobbyPlayer;
		}

		private int GetFirstAvailableSlot()
		{
			for (int i = 0; i < MAX_PLAYERS; i++)
			{
				if (!_occupiedSlots[i])
				{
					return i;
				}
			}
			return -1;
		}

		private void UpdateStatusText()
		{
			int count = _playerCount.Value;
			_statusText.text = "Players: " + count;
		}

		private void UpdateStartButton()
		{
			if (!IsServer)
			{
				return;
			}

			_startButton.gameObject.SetActive(_playerCount.Value >= 2);
			_startButton.interactable = true;
		}

		private void ClearLobbyPlayers()
		{
			DespawnOrDestroyPlayers();
			_lobbyPlayers.Clear();
			_playerSlots.Clear();
			Array.Clear(_occupiedSlots, 0, _occupiedSlots.Length);
		}

		private void DespawnOrDestroyPlayers()
		{
			foreach (KeyValuePair<ulong, GameObject> pair in _lobbyPlayers)
			{
				if (pair.Value != null)
				{
					NetworkObject networkObj = pair.Value.GetComponent<NetworkObject>();
					if (networkObj != null && networkObj.IsSpawned && IsServer)
					{
						networkObj.Despawn(true);
					}
					else
					{
						Destroy(pair.Value);
					}
				}
			}
		}

		private string GetNickname()
		{
			string nickname = _nicknameInput.text.Trim();
			if (string.IsNullOrEmpty(nickname))
			{
				nickname = "Player";
			}
			return nickname;
		}

		private void SubscribeEvents()
		{
			_playerCount.OnValueChanged += OnPlayerCountChanged;
			
			if (_hostButton != null)
			{
				_hostButton.onClick.AddListener(OnHostClicked);
			}
			if (_joinButton != null)
			{
				_joinButton.onClick.AddListener(OnJoinClicked);
			}
			if (_startButton != null)
			{
				_startButton.onClick.AddListener(OnStartClicked);
			}
			if (_disconnectButton != null)
			{
				_disconnectButton.onClick.AddListener(OnDisconnectClicked);
			}
		}

		private void UnsubscribeEvents()
		{
			_playerCount.OnValueChanged -= OnPlayerCountChanged;

			if (_hostButton != null)
			{
				_hostButton.onClick.RemoveListener(OnHostClicked);
			}
			if (_joinButton != null)
			{
				_joinButton.onClick.RemoveListener(OnJoinClicked);
			}
			if (_startButton != null)
			{
				_startButton.onClick.RemoveListener(OnStartClicked);
			}
			if (_disconnectButton != null)
			{
				_disconnectButton.onClick.RemoveListener(OnDisconnectClicked);
			}
		}

		private void UnsubscribeNetworkEvents()
		{
			if (NetworkManager.Singleton != null)
			{
				NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
				NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
				NetworkManager.Singleton.ConnectionApprovalCallback = null;
			}
		}

		private void OnPlayerCountChanged(int oldValue, int newValue)
		{
			UpdateStatusText();
		}
	}
}
