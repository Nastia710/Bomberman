using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

namespace Bomberman
{
    public enum PowerUpType
    {
        ExtraBomb = 0,
        ExtraFire = 1,
        SpeedBoost = 2,
        NoclipWalls = 3,
        NoclipFire = 4,
        NoclipBombs = 5,
        Detonator = 6
    }

    public class BomberMan : NetworkBehaviour
    {
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

        private class ActivePowerUp
        {
            public PowerUpType Type;
            public float Timer;
            public TMP_Text UIText;
        }

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

        private List<Bomb> _activeBombs = new List<Bomb>();

        private NetworkVariable<FixedString64Bytes> _nickname = new NetworkVariable<FixedString64Bytes>(
            "",
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private NetworkVariable<int> _networkDirection = new NetworkVariable<int>(
            5,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> _networkIsMoving = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> _networkFlipX = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner);

        public static event Action BombPlacedEvent;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
        }

        public override void OnNetworkSpawn()
        {
            _nickname.OnValueChanged += OnNicknameChanged;
            _networkFlipX.OnValueChanged += OnFlipXChanged;

            _bombsAllowed = 1;
            _fireLength = 1;

            if (IsOwner)
            {
                CameraController cameraController = FindFirstObjectByType<CameraController>();
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
            _nickname.OnValueChanged -= OnNicknameChanged;
            _networkFlipX.OnValueChanged -= OnFlipXChanged;
        }

        private void OnNicknameChanged(FixedString64Bytes oldValue, FixedString64Bytes newValue)
        {
            UpdateNicknameDisplay(newValue.ToString());
        }

        private void OnFlipXChanged(bool oldValue, bool newValue)
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

        private void Update()
        {
            if (IsOwner)
            {
                UpdatePowerUpTimers();
                
                if (GameManager.Instance != null && !GameManager.Instance.IsPlaying()) return;

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
                
                if (_networkFlipX.Value != _spriteRenderer.flipX)
                {
                    _networkFlipX.Value = _spriteRenderer.flipX;
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

            PowerUp powerUp = other.GetComponent<PowerUp>();
            ApplyPowerUp(powerUp.Type);
            
            if (IsServer)
            {
                other.GetComponent<NetworkObject>()?.Despawn(true);
            }
            else
            {
                DespawnPowerUpServerRpc(other.gameObject);
            }
        }

        [ServerRpc]
        private void DespawnPowerUpServerRpc(Unity.Netcode.NetworkObjectReference powerUpRef)
        {
            if (powerUpRef.TryGet(out NetworkObject powerUpNetObj))
            {
                powerUpNetObj.Despawn(true);
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

        public int GetFireLength()
        {
            return _fireLength;
        }

        public void OnBombExploded(Bomb bomb)
        {
            _activeBombs.Remove(bomb);
        }

        private void ApplyPowerUp(int type)
        {
            PowerUpType pType = (PowerUpType)type;
            switch (pType)
            {
                case PowerUpType.ExtraBomb:
                    if (_bombsAllowed < 5) _bombsAllowed++;
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
            if (_powerUpTextPrefab == null) return;

            GameObject containerObj = GameObject.Find("PowerUpContainer");
            if (containerObj == null)
            {
                Debug.LogWarning("PowerUpContainer not found in the scene! UI timer will not be shown.");
                return;
            }

            GameObject textObj = Instantiate(_powerUpTextPrefab, containerObj.transform);
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
            if (!IsOwner) return;

            for (int i = _activePowerUps.Count - 1; i >= 0; i--)
            {
                ActivePowerUp p = _activePowerUps[i];
                p.Timer -= Time.deltaTime;

                if (p.Timer > 0)
                {
                    p.UIText.text = $"{p.Type}: {Mathf.CeilToInt(p.Timer)}s";
                }
                else
                {
                    RemovePowerUpEffect(p.Type);
                    Destroy(p.UIText.gameObject);
                    _activePowerUps.RemoveAt(i);
                }
            }
        }

        private void RemovePowerUpEffect(PowerUpType type)
        {
            switch (type)
            {
                case PowerUpType.ExtraBomb:
                    _bombsAllowed = Mathf.Max(1, _bombsAllowed - 1);
                    break;
                case PowerUpType.ExtraFire:
                    _fireLength = Mathf.Max(1, _fireLength - 1);
                    break;
                case PowerUpType.SpeedBoost:
                    _moveSpeed -= _speedBoostPower;
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
                        if (bomb != null) bomb.StartTicking();
                    }
                    break;
            }
        }

        private bool _isDead;

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;

            for (int i = 0; i < _activePowerUps.Count; i++)
            {
                if (_activePowerUps[i].UIText != null)
                {
                    Destroy(_activePowerUps[i].UIText.gameObject);
                }
            }
            _activePowerUps.Clear();

            if (IsServer)
            {
                SpawnDeathEffectClientRpc(transform.position);
                GetComponent<NetworkObject>().Despawn(true);
            }
            else
            {
                DieServerRpc();
            }
        }

        [ServerRpc]
        private void DieServerRpc()
        {
            if (!NetworkObject.IsSpawned) return;
            SpawnDeathEffectClientRpc(transform.position);
            GetComponent<NetworkObject>().Despawn(true);
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
                BombPlacedEvent?.Invoke();
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
                case 2:
                    transform.position = new Vector2(
                        Mathf.Round(transform.position.x),
                        transform.position.y - _moveSpeed * Time.deltaTime);
                    break;
                case 4:
                    transform.position = new Vector2(
                        transform.position.x - _moveSpeed * Time.deltaTime,
                        Mathf.Round(transform.position.y));
                    _spriteRenderer.flipX = false;
                    break;
                case 6:
                    transform.position = new Vector2(
                        transform.position.x + _moveSpeed * Time.deltaTime,
                        Mathf.Round(transform.position.y));
                    _spriteRenderer.flipX = true;
                    break;
                case 8:
                    transform.position = new Vector2(
                        Mathf.Round(transform.position.x),
                        transform.position.y + _moveSpeed * Time.deltaTime);
                    break;
                case 5:
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
                case 2:
                    _sensor.transform.localPosition = new Vector2(0, -_sensorRange);
                    break;
                case 4:
                    _sensor.transform.localPosition = new Vector2(-_sensorRange, 0);
                    break;
                case 6:
                    _sensor.transform.localPosition = new Vector2(_sensorRange, 0);
                    break;
                case 8:
                    _sensor.transform.localPosition = new Vector2(0, _sensorRange);
                    break;
            }

            _canMove = !Physics2D.OverlapBox(_sensor.position, sensorBox, 0, _stoneLayer);

            if (_canMove && !_hasNoclipWalls)
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
            _direction = 5;

            if (_isButtonLeft)
            {
                _direction = 4;
            }

            if (_isButtonRight)
            {
                _direction = 6;
            }

            if (_isButtonUp)
            {
                _direction = 8;
            }

            if (_isButtonDown)
            {
                _direction = 2;
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
                _animator.SetInteger("Direction", _direction);
                _animator.SetBool("Moving", _isMoving);
            }
            else
            {
                _animator.SetInteger("Direction", _networkDirection.Value);
                _animator.SetBool("Moving", _networkIsMoving.Value);
            }
        }
    }
}
