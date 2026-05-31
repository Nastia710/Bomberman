using System.Collections.Generic;
using UnityEngine;

namespace Bomberman
{
    public class Bomb : MonoBehaviour
    {
        [SerializeField]
        private GameObject _fireMid;
        [SerializeField]
        private GameObject _fireHorizontal;
        [SerializeField]
        private GameObject _fireLeft;
        [SerializeField]
        private GameObject _fireRight;
        [SerializeField]
        private GameObject _fireVertical;
        [SerializeField]
        private GameObject _fireTop;
        [SerializeField]
        private GameObject _fireBottom;

        [SerializeField]
        private float _delay;

        [SerializeField]
        private LayerMask _stoneLayer;
        [SerializeField]
        private LayerMask _blowableLayer;

        private float _counter;
        private bool _isCalculated;
        private bool _canTick;
        private int _fireLength;

        private BomberMan _bomberMan;

        private List<Vector2> _cellsToBlowRight = new List<Vector2>();
        private List<Vector2> _cellsToBlowLeft = new List<Vector2>();
        private List<Vector2> _cellsToBlowUp = new List<Vector2>();
        private List<Vector2> _cellsToBlowDown = new List<Vector2>();

        public void Init(BomberMan bomberMan, int fireLength, bool hasDetonator)
        {
            _bomberMan = bomberMan;
            _fireLength = fireLength;
            _canTick = !hasDetonator;
            _isCalculated = false;
            _counter = _delay;
        }

        private void Update()
        {
            if (_counter > 0)
            {
                if (_canTick)
                {
                    _counter -= Time.deltaTime;
                }
            }
            else
            {
                Blow();
            }
        }

        public void StartTicking()
        {
            _canTick = true;
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Fire"))
            {
                Blow();
            }
        }

        public void Blow()
        {
            CalculateFireDirections();
            Instantiate(_fireMid, transform.position, transform.rotation);

            SpawnDirectionalFire(_cellsToBlowLeft, _fireLeft, _fireHorizontal);
            SpawnDirectionalFire(_cellsToBlowRight, _fireRight, _fireHorizontal);
            SpawnDirectionalFire(_cellsToBlowUp, _fireTop, _fireVertical);
            SpawnDirectionalFire(_cellsToBlowDown, _fireBottom, _fireVertical);

            if (_bomberMan != null)
            {
                _bomberMan.OnBombExploded(this);
            }

            Destroy(gameObject);
        }

        private void SpawnDirectionalFire(List<Vector2> cells, GameObject endPrefab, GameObject midPrefab)
        {
            if (cells.Count <= 0)
            {
                return;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                if (i == cells.Count - 1)
                {
                    Instantiate(endPrefab, cells[i], transform.rotation);
                }
                else
                {
                    Instantiate(midPrefab, cells[i], transform.rotation);
                }
            }
        }

        private void CalculateFireDirections()
        {
            if (_isCalculated)
            {
                return;
            }

            CalculateDirectionCells(_cellsToBlowLeft, Vector2.left);
            CalculateDirectionCells(_cellsToBlowRight, Vector2.right);
            CalculateDirectionCells(_cellsToBlowUp, Vector2.up);
            CalculateDirectionCells(_cellsToBlowDown, Vector2.down);

            _isCalculated = true;
        }

        private void CalculateDirectionCells(List<Vector2> cells, Vector2 direction)
        {
            for (int i = 1; i <= _fireLength; i++)
            {
                Vector2 checkPosition = new Vector2(
                    transform.position.x + direction.x * i,
                    transform.position.y + direction.y * i);

                if (Physics2D.OverlapCircle(checkPosition, 0.1f, _stoneLayer))
                {
                    break;
                }

                cells.Add(checkPosition);

                if (Physics2D.OverlapCircle(checkPosition, 0.1f, _blowableLayer))
                {
                    break;
                }
            }
        }

        private void OnDrawGizmos()
        {
            DrawGizmosForCells(_cellsToBlowLeft, Color.yellow);
            DrawGizmosForCells(_cellsToBlowRight, Color.green);
            DrawGizmosForCells(_cellsToBlowUp, Color.blue);
            DrawGizmosForCells(_cellsToBlowDown, Color.gray);
        }

        private void DrawGizmosForCells(List<Vector2> cells, Color color)
        {
            Gizmos.color = color;
            foreach (Vector2 cell in cells)
            {
                Gizmos.DrawSphere(cell, 0.2f);
            }
        }
    }
}
