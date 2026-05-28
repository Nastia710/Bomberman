using UnityEngine;

namespace Bomberman
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField]
        private BomberMan _bomberMan;

        [SerializeField]
        private Transform _bottomLeftBound;
        [SerializeField]
        private Transform _topRightBound;

        private Camera _camera;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Update()
        {
            if (_bomberMan == null)
            {
                return;
            }

            float cameraHalfHeight = _camera.orthographicSize;
            float cameraHalfWidth = cameraHalfHeight * ((float)Screen.width / Screen.height);

            float minX = _bottomLeftBound.position.x - 0.5f;
            float minY = _bottomLeftBound.position.y - 0.5f;
            float maxX = _topRightBound.position.x + 0.5f;
            float maxY = _topRightBound.position.y + 0.5f;

            Vector3 playerPosition = _bomberMan.transform.position;
            float x = Mathf.Clamp(playerPosition.x, minX + cameraHalfWidth, maxX - cameraHalfWidth);
            float y = Mathf.Clamp(playerPosition.y, minY + cameraHalfHeight, maxY - cameraHalfHeight);

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
