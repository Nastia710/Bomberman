using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace Bomberman
{
	public class BomberMan : NetworkBehaviour
	{
		public static List<BomberMan> AllPlayers = new List<BomberMan>();

		public string Nickname
		{
			get
			{
				return _nickname.Value.ToString();
			}
		}

		public int FireLength
		{
			get
			{
				return _fireLength;
			}
		}

		[SerializeField]
		private Transform _sensor;
		[SerializeField]
		private float _sensorSize = 0.7f;
		[SerializeField]
		private float _sensorRange = 0.4f;

		[SerializeField]
		private float _moveSpeed = 2f;
		[SerializeField]
		private float _speedBoostPower = 0.5f;

		[SerializeField]
		private LayerMask _stoneLayer;
		[SerializeField]
		private LayerMask _bombLayer;
		[SerializeField]
		private LayerMask _brickLayer;
		[SerializeField]
		private LayerMask _fireLayer;

		[SerializeField]
		private GameObject _bombPrefab;
		[SerializeField]
		private GameObject _deathEffect;
		[SerializeField]
		private TMP_Text _nicknameText;

		[SerializeField]
		private GameObject _powerUpTextPrefab;

		private List<ActivePowerUp> _activePowerUps = new List<ActivePowerUp>();

		private bool _isButtonLeft;
		private bool _isButtonRight;
		private bool _isButtonUp;
		private bool _isButtonDown;
		private bool _isButtonBomb;
		private bool _isButtonDetonate;

		private int _bombsAllowed;
		private int _fireLength;
		private bool _hasNoclipWalls;
		private bool _hasNoclipBombs;
		private bool _hasNoclipFire;
		private bool _hasDetonator;

		private bool _canMove;
		private bool _isMoving;
		private bool _isInsideBomb;
		private bool _isInsideWall;
		private bool _isInsideFire;
		private int _direction;

		private SpriteRenderer _spriteRenderer;
		private Animator _animator;
		private Collider2D _collider;

		private List<Bomb> _activeBombs = new List<Bomb>();
		
		private Vector3 _spawnPosition;
		private float _baseSpeed;
		private bool _isDead = false;
		public bool IsDead => _isDead;

		private NetworkVariable<FixedString64Bytes> _nickname = new NetworkVariable<FixedString64Bytes>(
			"",
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Owner);

		private NetworkVariable<int> _networkDirection = new NetworkVariable<int>(
			DIRECTION_IDLE,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Owner);

		private NetworkVariable<bool> _networkIsMoving = new NetworkVariable<bool>(
			false,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Owner);

		private NetworkVariable<bool> _networkIsFlippedX = new NetworkVariable<bool>(
			false,
			NetworkVariableReadPermission.Everyone,
			NetworkVariableWritePermission.Owner);

		private const int DIRECTION_DOWN = 2;
		private const int DIRECTION_LEFT = 4;
		private const int DIRECTION_IDLE = 5;
		private const int DIRECTION_RIGHT = 6;
		private const int DIRECTION_UP = 8;

		private const string ANIM_DIRECTION = "Direction";
		private const string ANIM_MOVING = "Moving";

		public static event Action BombPlacedEvent;

		private void Awake()
		{
			_spriteRenderer = GetComponent<SpriteRenderer>();
			_animator = GetComponent<Animator>();
			_collider = GetComponent<Collider2D>();
			_baseSpeed = _moveSpeed;
		}

		private void OnEnable()
		{
			SubscribeEvents();
		}

		private void Update()
		{
			if (_isDead)
			{
				return;
			}

			if (IsOwner)
			{
				UpdatePowerUpTimers();
				
				if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
				{
					return;
				}

				GetInput();
				GetDirection();
				HandleSensor();
				HandleBombs();
				Move();

				if (_networkDirection.Value != _direction)
				{
					_networkDirection.Value = _direction;
				}
				
				if (_networkIsMoving.Value != _isMoving)
				{
					_networkIsMoving.Value = _isMoving;
				}
				
				if (_networkIsFlippedX.Value != _spriteRenderer.flipX)
				{
					_networkIsFlippedX.Value = _spriteRenderer.flipX;
				}
			}

			Animate();
		}

		private void OnTriggerEnter2D(Collider2D other)
		{
			if (!IsOwner)
			{
				return;
			}

			if (!other.gameObject.CompareTag("PowerUp"))
			{
				return;
			}

			RequestPowerUpServerRpc(other.gameObject);
		}

		private void OnDisable()
		{
			UnsubscribeEvents();
		}

		public override void OnNetworkSpawn()
		{
			if (!AllPlayers.Contains(this))
			{
				AllPlayers.Add(this);
			}

			_bombsAllowed = 1;
			_fireLength = 1;
			
			_spawnPosition = transform.position;

			if (IsOwner)
			{
				CameraController cameraController = CameraController.Instance;
				if (cameraController != null)
				{
					cameraController.SetTarget(transform);
				}

				_nickname.Value = LobbyManager.PlayerName;
			}

			UpdateNicknameDisplay(_nickname.Value.ToString());
		}

		public override void OnNetworkDespawn()
		{
			AllPlayers.Remove(this);

			if (IsOwner)
			{
				for (int i = 0; i < _activePowerUps.Count; i++)
				{
					if (_activePowerUps[i].UIText != null)
					{
						Destroy(_activePowerUps[i].UIText.gameObject);
					}
				}
				_activePowerUps.Clear();
			}
		}

		public void Damage(int source)
		{
			if (!IsOwner)
			{
				return;
			}

			if (source == Bomberman.Damage.ENEMY_DAMAGE)
			{
				Die();
			}
			else if (source == Bomberman.Damage.FIRE_DAMAGE && !_hasNoclipFire)
			{
				Die();
			}
		}

		public bool CheckDetonator()
		{
			return _hasDetonator;
		}

		public void OnBombExploded(Bomb bomb)
		{
			_activeBombs.Remove(bomb);
		}

		private void SubscribeEvents()
		{
			_nickname.OnValueChanged += OnNicknameChanged;
			_networkIsFlippedX.OnValueChanged += OnIsFlippedXChanged;
		}

		private void UnsubscribeEvents()
		{
			_nickname.OnValueChanged -= OnNicknameChanged;
			_networkIsFlippedX.OnValueChanged -= OnIsFlippedXChanged;
		}

		private void OnNicknameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
		{
			UpdateNicknameDisplay(newValue.ToString());
		}

		private void OnIsFlippedXChanged(bool oldValue, bool newValue)
		{
			_spriteRenderer.flipX = newValue;
		}

		private void UpdateNicknameDisplay(string nickname)
		{
			if (_nicknameText != null)
			{
				_nicknameText.text = nickname;
				_nicknameText.color = Color.black;
			}
		}

		[ServerRpc]
		private void RequestPowerUpServerRpc(Unity.Netcode.NetworkObjectReference powerUpRef, ServerRpcParams rpcParams = default)
		{
			if (powerUpRef.TryGet(out NetworkObject powerUpNetObj) && powerUpNetObj.IsSpawned)
			{
				PowerUp powerUp = powerUpNetObj.GetComponent<PowerUp>();
				if (powerUp != null)
				{
					int type = powerUp.Type;
					powerUpNetObj.Despawn(true);

					ClientRpcParams clientRpcParams = new ClientRpcParams
					{
						Send = new ClientRpcSendParams
						{
							TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId }
						}
					};
					ApplyPowerUpClientRpc(type, clientRpcParams);
				}
			}
		}

		[ClientRpc]
		private void ApplyPowerUpClientRpc(int type, ClientRpcParams rpcParams = default)
		{
			ApplyPowerUp(type);
		}

		private void ApplyPowerUp(int type)
		{
			PowerUpType pType = (PowerUpType)type;
			switch (pType)
			{
				case PowerUpType.ExtraBomb:
					_bombsAllowed = 5;
					break;
				case PowerUpType.ExtraFire:
					_fireLength++;
					break;
				case PowerUpType.SpeedBoost:
					_moveSpeed += _speedBoostPower;
					break;
				case PowerUpType.NoclipWalls:
					_hasNoclipWalls = true;
					break;
				case PowerUpType.NoclipFire:
					_hasNoclipFire = true;
					break;
				case PowerUpType.NoclipBombs:
					_hasNoclipBombs = true;
					break;
				case PowerUpType.Detonator:
					_hasDetonator = true;
					break;
			}

			if (IsOwner)
			{
				AddPowerUpTimer(pType);
			}
		}

		private void AddPowerUpTimer(PowerUpType type)
		{
			if (_powerUpTextPrefab == null)
			{
				return;
			}

			if (GameManager.Instance == null || GameManager.Instance.PowerUpContainer == null)
			{
				Debug.LogWarning("PowerUpContainer not found in GameManager! UI timer will not be shown.");
				return;
			}

			GameObject textObj = Instantiate(_powerUpTextPrefab, GameManager.Instance.PowerUpContainer);
			TMP_Text uiText = textObj.GetComponent<TMP_Text>();

			_activePowerUps.Add(new ActivePowerUp
			{
				Type = type,
				Timer = 15f,
				UIText = uiText
			});
		}

		private void UpdatePowerUpTimers()
		{
			if (!IsOwner)
			{
				return;
			}

			for (int i = _activePowerUps.Count - 1; i >= 0; i--)
			{
				ActivePowerUp activePowerUp = _activePowerUps[i];
				activePowerUp.Timer -= Time.deltaTime;

				if (activePowerUp.Timer > 0)
				{
					activePowerUp.UIText.text = $"{activePowerUp.Type}: {Mathf.CeilToInt(activePowerUp.Timer)}s";
				}
				else
				{
					RemovePowerUpEffect(activePowerUp.Type);
					Destroy(activePowerUp.UIText.gameObject);
					_activePowerUps.RemoveAt(i);
				}
			}
		}

		private void RemovePowerUpEffect(PowerUpType type)
		{
			if (type == PowerUpType.ExtraFire) 
			{ 
				_fireLength = Mathf.Max(1, _fireLength - 1); 
				return; 
			}
			if (type == PowerUpType.SpeedBoost) 
			{ 
				_moveSpeed -= _speedBoostPower; 
				return; 
			}

			int activeCount = 0;
			foreach (var p in _activePowerUps)
			{
				if (p.Type == type)
				{
					activeCount++;
				}
			}

			if (activeCount > 1)
			{
				return;
			}

			switch (type)
			{
				case PowerUpType.ExtraBomb:
					_bombsAllowed = 1;
					break;
				case PowerUpType.NoclipWalls:
					_hasNoclipWalls = false;
					break;
				case PowerUpType.NoclipFire:
					_hasNoclipFire = false;
					break;
				case PowerUpType.NoclipBombs:
					_hasNoclipBombs = false;
					break;
				case PowerUpType.Detonator:
					_hasDetonator = false;
					foreach (var bomb in _activeBombs)
					{
						if (bomb != null)
						{
							bomb.StartTicking();
						}
					}
					break;
			}
		}

		private void Die()
		{
			if (_isDead)
			{
				return;
			}
			_isDead = true;

			for (int i = 0; i < _activePowerUps.Count; i++)
			{
				if (_activePowerUps[i].UIText != null)
				{
					Destroy(_activePowerUps[i].UIText.gameObject);
				}
			}
			_activePowerUps.Clear();

			DieServerRpc();
		}

		[ServerRpc]
		private void DieServerRpc()
		{
			if (!NetworkObject.IsSpawned)
			{
				return;
			}
			
			SpawnDeathEffectClientRpc(transform.position);

			if (Enemy.AllEnemies.Count > 0)
			{
				RespawnClientRpc();
			}
			else
			{
				if (GameManager.Instance != null)
				{
					GameManager.Instance.NotifyPlayerDied(_nickname.Value.ToString());
				}
				GetComponent<NetworkObject>().Despawn(true);
			}
		}

		[ClientRpc]
		private void RespawnClientRpc()
		{
			StartCoroutine(RespawnRoutine());
		}

		private System.Collections.IEnumerator RespawnRoutine()
		{
			_isDead = true;

			if (_spriteRenderer != null)
			{
				_spriteRenderer.enabled = false;
			}
			if (_collider != null)
			{
				_collider.enabled = false;
			}
			if (_nicknameText != null)
			{
				_nicknameText.gameObject.SetActive(false);
			}

			yield return new WaitForSeconds(1.5f);

			_isDead = false;
			_bombsAllowed = 1;
			_fireLength = 1;
			_moveSpeed = _baseSpeed;
			_hasNoclipBombs = false;
			_hasNoclipFire = false;
			_hasNoclipWalls = false;
			_hasDetonator = false;

			if (IsOwner)
			{
				transform.position = _spawnPosition;
			}

			if (_spriteRenderer != null)
			{
				_spriteRenderer.enabled = true;
			}
			if (_collider != null)
			{
				_collider.enabled = true;
			}
			if (_nicknameText != null)
			{
				_nicknameText.gameObject.SetActive(true);
			}
		}

		[ClientRpc]
		private void SpawnDeathEffectClientRpc(Vector3 position)
		{
			Instantiate(_deathEffect, position, Quaternion.identity);
		}

		private void HandleBombs()
		{
			if (_isButtonBomb
				&& _activeBombs.Count < _bombsAllowed
				&& !_isInsideBomb
				&& !_isInsideFire
				&& !_isInsideWall)
			{
				Vector2 bombPosition = new Vector2(
					Mathf.Round(transform.position.x),
					Mathf.Round(transform.position.y));

				SpawnBombLocally(bombPosition, _fireLength, _hasDetonator);
				PlaceBombServerRpc(bombPosition, _fireLength, _hasDetonator);
			}

			if (_isButtonDetonate && _hasDetonator)
			{
				DetonateBombsLocally();
				DetonateBombsServerRpc();
			}
		}

		private void SpawnBombLocally(Vector2 position, int fireLength, bool hasDetonator)
		{
			GameObject bombObject = Instantiate(_bombPrefab, position, transform.rotation);
			Bomb bomb = bombObject.GetComponent<Bomb>();
			bomb.Init(this, fireLength, hasDetonator);
			_activeBombs.Add(bomb);

			if (IsOwner)
			{
				if (BombPlacedEvent != null)
				{
					BombPlacedEvent.Invoke();
				}
			}
		}

		private void DetonateBombsLocally()
		{
			List<Bomb> bombsCopy = new List<Bomb>(_activeBombs);
			foreach (Bomb bomb in bombsCopy)
			{
				if (bomb != null)
				{
					bomb.Blow();
				}
			}
		}

		[ServerRpc]
		private void PlaceBombServerRpc(Vector2 position, int fireLength, bool hasDetonator)
		{
			PlaceBombClientRpc(position, fireLength, hasDetonator);
		}

		[ClientRpc]
		private void PlaceBombClientRpc(Vector2 position, int fireLength, bool hasDetonator)
		{
			if (IsOwner)
			{
				return;
			}

			SpawnBombLocally(position, fireLength, hasDetonator);
		}

		[ServerRpc]
		private void DetonateBombsServerRpc()
		{
			DetonateBombsClientRpc();
		}

		[ClientRpc]
		private void DetonateBombsClientRpc()
		{
			if (IsOwner)
			{
				return;
			}

			DetonateBombsLocally();
		}

		private void Move()
		{
			if (!_canMove)
			{
				_isMoving = false;
				return;
			}

			_isMoving = true;

			switch (_direction)
			{
				case DIRECTION_DOWN:
					transform.position = new Vector2(
						Mathf.Round(transform.position.x),
						transform.position.y - _moveSpeed * Time.deltaTime);
					break;
				case DIRECTION_LEFT:
					transform.position = new Vector2(
						transform.position.x - _moveSpeed * Time.deltaTime,
						Mathf.Round(transform.position.y));
					_spriteRenderer.flipX = false;
					break;
				case DIRECTION_RIGHT:
					transform.position = new Vector2(
						transform.position.x + _moveSpeed * Time.deltaTime,
						Mathf.Round(transform.position.y));
					_spriteRenderer.flipX = true;
					break;
				case DIRECTION_UP:
					transform.position = new Vector2(
						Mathf.Round(transform.position.x),
						transform.position.y + _moveSpeed * Time.deltaTime);
					break;
				case DIRECTION_IDLE:
					_isMoving = false;
					break;
			}
		}

		private void HandleSensor()
		{
			_sensor.transform.localPosition = Vector2.zero;
			Vector2 sensorBox = new Vector2(_sensorSize, _sensorSize);

			_isInsideWall = Physics2D.OverlapBox(_sensor.position, sensorBox, 0, _brickLayer);
			_isInsideBomb = Physics2D.OverlapBox(_sensor.position, sensorBox, 0, _bombLayer);
			_isInsideFire = Physics2D.OverlapBox(_sensor.position, sensorBox, 0, _fireLayer);

			switch (_direction)
			{
				case DIRECTION_DOWN:
					_sensor.transform.localPosition = new Vector2(0, -_sensorRange);
					break;
				case DIRECTION_LEFT:
					_sensor.transform.localPosition = new Vector2(-_sensorRange, 0);
					break;
				case DIRECTION_RIGHT:
					_sensor.transform.localPosition = new Vector2(_sensorRange, 0);
					break;
				case DIRECTION_UP:
					_sensor.transform.localPosition = new Vector2(0, _sensorRange);
					break;
			}

			_canMove = !Physics2D.OverlapBox(_sensor.position, sensorBox, 0, _stoneLayer);

			if (_canMove && !_hasNoclipWalls && !_isInsideWall)
			{
				_canMove = !Physics2D.OverlapBox(_sensor.position, sensorBox, 0, _brickLayer);
			}

			if (_canMove && !_isInsideBomb && !_hasNoclipBombs)
			{
				_canMove = !Physics2D.OverlapBox(_sensor.position, sensorBox, 0, _bombLayer);
			}
		}

		private void GetDirection()
		{
			_direction = DIRECTION_IDLE;

			if (_isButtonLeft)
			{
				_direction = DIRECTION_LEFT;
			}

			if (_isButtonRight)
			{
				_direction = DIRECTION_RIGHT;
			}

			if (_isButtonUp)
			{
				_direction = DIRECTION_UP;
			}

			if (_isButtonDown)
			{
				_direction = DIRECTION_DOWN;
			}
		}

		private void GetInput()
		{
			_isButtonLeft = Input.GetKey(KeyCode.LeftArrow)
				&& !Input.GetKey(KeyCode.RightArrow)
				&& !Input.GetKey(KeyCode.UpArrow)
				&& !Input.GetKey(KeyCode.DownArrow);

			_isButtonRight = !Input.GetKey(KeyCode.LeftArrow)
				&& Input.GetKey(KeyCode.RightArrow)
				&& !Input.GetKey(KeyCode.UpArrow)
				&& !Input.GetKey(KeyCode.DownArrow);

			_isButtonUp = !Input.GetKey(KeyCode.LeftArrow)
				&& !Input.GetKey(KeyCode.RightArrow)
				&& Input.GetKey(KeyCode.UpArrow)
				&& !Input.GetKey(KeyCode.DownArrow);

			_isButtonDown = !Input.GetKey(KeyCode.LeftArrow)
				&& !Input.GetKey(KeyCode.RightArrow)
				&& !Input.GetKey(KeyCode.UpArrow)
				&& Input.GetKey(KeyCode.DownArrow);

			_isButtonBomb = Input.GetKeyDown(KeyCode.Z);
			_isButtonDetonate = Input.GetKeyDown(KeyCode.X);
		}

		private void Animate()
		{
			if (IsOwner)
			{
				_animator.SetInteger(ANIM_DIRECTION, _direction);
				_animator.SetBool(ANIM_MOVING, _isMoving);
			}
			else
			{
				_animator.SetInteger(ANIM_DIRECTION, _networkDirection.Value);
				_animator.SetBool(ANIM_MOVING, _networkIsMoving.Value);
			}
		}

		private class ActivePowerUp
		{
			public PowerUpType Type;
			public float Timer;
			public TMP_Text UIText;
		}
	}
}
