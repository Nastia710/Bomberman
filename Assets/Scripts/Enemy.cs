using System.Collections.Generic;
using UnityEngine;

namespace Bomberman
{
    public class Enemy : MonoBehaviour
    {
        [SerializeField]
        private GameObject _bomberMan;
        [SerializeField]
        private GameObject _deathEffect;
        [SerializeField]
        private float _moveSpeed;

        private List<Vector2> _pathToBomberMan = new List<Vector2>();
        private List<Vector2> _randomPath = new List<Vector2>();
        private List<Vector2> _currentPath = new List<Vector2>();
        private PathFinder _pathFinder;
        private bool _isMoving;

        private void Awake()
        {
            _pathFinder = GetComponent<PathFinder>();
        }

        private void Start()
        {
            if (_bomberMan != null)
            {
                ReCalculatePath();
                _isMoving = true;
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

        private void SubscribeEvents()
        {
            BomberMan.BombPlacedEvent += ReCalculatePath;
        }

        private void UnsubscribeEvents()
        {
            BomberMan.BombPlacedEvent -= ReCalculatePath;
        }

        private void Update()
        {
            if (_bomberMan == null)
            {
                return;
            }

            if (GameManager.Instance != null && !GameManager.Instance.IsPlaying())
            {
                return;
            }

            if (_currentPath.Count == 0
                && Vector2.Distance(transform.position, _bomberMan.transform.position) > 0.5f)
            {
                ReCalculatePath();
                _isMoving = true;
            }

            if (_currentPath.Count == 0)
            {
                return;
            }

            if (_isMoving)
            {
                Vector2 targetCell = _currentPath[_currentPath.Count - 1];

                if (Vector2.Distance(transform.position, targetCell) > 0.1f)
                {
                    transform.position = Vector2.MoveTowards(
                        transform.position,
                        targetCell,
                        _moveSpeed * Time.deltaTime);
                }

                if (Vector2.Distance(transform.position, targetCell) <= 0.1f)
                {
                    _isMoving = false;
                }
            }
            else
            {
                ReCalculatePath();
                _isMoving = true;
            }
        }

        public void ReCalculatePath()
        {
            _pathToBomberMan = _pathFinder.GetPath(_bomberMan.transform.position);

            if (_pathToBomberMan.Count == 0)
            {
                int randomIndex = Random.Range(0, _pathFinder.FreeNodes.Count);
                _randomPath = _pathFinder.GetPath(_pathFinder.FreeNodes[randomIndex].Position);
                _currentPath = _randomPath;
            }
            else
            {
                _currentPath = _pathToBomberMan;
            }
        }

        public void Damage(int source)
        {
            if (source == Bomberman.Damage.FIRE_DAMAGE)
            {
                Instantiate(_deathEffect, transform.position, transform.rotation);
                Destroy(gameObject);
            }
        }
    }
}
