using UnityEngine;
using Unity.Netcode;

namespace Bomberman
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField]
        private Transform _bottomLeftBound;
        [SerializeField]
        private Transform _topRightBound;

        private Camera _camera;
        private Transform _target;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
                {
                    BomberMan[] players = FindObjectsByType<BomberMan>(FindObjectsSortMode.None);
                    foreach (BomberMan p in players)
                    {
                        if (p.IsOwner)
                        {
                            _target = p.transform;
                            break;
                        }
                    }
                }

                if (_target == null)
                {
                    return;
                }
            }

            float cameraHalfHeight = _camera.orthographicSize;
            float cameraHalfWidth = cameraHalfHeight * ((float)Screen.width / Screen.height);

            float minX = _bottomLeftBound.position.x - 0.5f;
            float minY = _bottomLeftBound.position.y - 0.5f;
            float maxX = _topRightBound.position.x + 0.5f;
            float maxY = _topRightBound.position.y + 0.5f;

            Vector3 playerPosition = _target.position;

            float clampMinX = minX + cameraHalfWidth;
            float clampMaxX = maxX - cameraHalfWidth;
            float x = clampMinX > clampMaxX ? (minX + maxX) / 2f : Mathf.Clamp(playerPosition.x, clampMinX, clampMaxX);

            float clampMinY = minY + cameraHalfHeight;
            float clampMaxY = maxY - cameraHalfHeight;
            float y = clampMinY > clampMaxY ? (minY + maxY) / 2f : Mathf.Clamp(playerPosition.y, clampMinY, clampMaxY);

            transform.position = new Vector3(x, y, transform.position.z);
        }

        private void OnDrawGizmos()
        {
            if (_bottomLeftBound == null || _topRightBound == null)
            {
                return;
            }

            float minX = _bottomLeftBound.position.x - 0.5f;
            float minY = _bottomLeftBound.position.y - 0.5f;
            float maxX = _topRightBound.position.x + 0.5f;
            float maxY = _topRightBound.position.y + 0.5f;

            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector2(minX, minY), new Vector2(maxX, minY));
            Gizmos.DrawLine(new Vector2(minX, maxY), new Vector2(maxX, maxY));
            Gizmos.DrawLine(new Vector2(minX, minY), new Vector2(minX, maxY));
            Gizmos.DrawLine(new Vector2(maxX, minY), new Vector2(maxX, maxY));
        }
    }
}
