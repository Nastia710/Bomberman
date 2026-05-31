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
            Vector3 desiredPosition = new Vector3(_target.position.x, _target.position.y, transform.position.z);

            float clampedX = Mathf.Clamp(desiredPosition.x, _bottomLeftBound.position.x - 0.5f + cameraHalfWidth, _topRightBound.position.x + 0.5f - cameraHalfWidth);
            float clampedY = Mathf.Clamp(desiredPosition.y, _bottomLeftBound.position.y - 0.5f + cameraHalfHeight, _topRightBound.position.y + 0.5f - cameraHalfHeight);

            transform.position = new Vector3(clampedX, clampedY, desiredPosition.z);
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
