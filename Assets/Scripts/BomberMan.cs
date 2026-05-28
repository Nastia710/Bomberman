using System;
using System.Collections.Generic;
using UnityEngine;

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

    public class BomberMan : MonoBehaviour
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

        public static event Action BombPlacedEvent;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
        }

        private void Start()
        {
            _bombsAllowed = 1;
            _fireLength = 1;
        }

        private void Update()
        {
            GetInput();
            GetDirection();
            HandleSensor();
            HandleBombs();
            Move();
            Animate();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.gameObject.CompareTag("PowerUp"))
            {
                return;
            }

            PowerUp powerUp = other.GetComponent<PowerUp>();
            ApplyPowerUp(powerUp.Type);
            Destroy(other.gameObject);
        }

        public void Damage(int source)
        {
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
            switch ((PowerUpType)type)
            {
                case PowerUpType.ExtraBomb:
                    _bombsAllowed++;
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
        }

        private void Die()
        {
            Instantiate(_deathEffect, transform.position, transform.rotation);
            Destroy(gameObject);
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

                GameObject bombObject = Instantiate(_bombPrefab, bombPosition, transform.rotation);
                Bomb bomb = bombObject.GetComponent<Bomb>();
                bomb.Init(this);
                _activeBombs.Add(bomb);

                BombPlacedEvent?.Invoke();
            }

            if (_isButtonDetonate)
            {
                List<Bomb> bombsCopy = new List<Bomb>(_activeBombs);
                foreach (Bomb bomb in bombsCopy)
                {
                    bomb.Blow();
                }
            }
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
            _animator.SetInteger("Direction", _direction);
            _animator.SetBool("Moving", _isMoving);
        }
    }
}
