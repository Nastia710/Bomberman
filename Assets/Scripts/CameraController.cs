using UnityEngine;
using Unity.Netcode;

namespace Bomberman
{
	public class CameraController : MonoBehaviour
	{
		public static CameraController Instance { get; private set; }

		[SerializeField]
		private Transform _bottomLeftBound;
		[SerializeField]
		private Transform _topRightBound;

		private Camera _camera;
		private Transform _target;
		private bool _isPanning;

		private void Awake()
		{
			Instance = this;
			_camera = GetComponent<Camera>();
		}

		private void LateUpdate()
		{
			if (_bottomLeftBound == null || _topRightBound == null)
			{
				return;
			}

			if (_target == null)
			{
				if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
				{
					foreach (BomberMan player in BomberMan.AllPlayers)
					{
						if (player != null && player.IsOwner)
						{
							_target = player.transform;
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

			Vector3 desiredPosition = new Vector3(_target.position.x, _target.position.y, transform.position.z);

			float clampedX = Mathf.Clamp(desiredPosition.x, _bottomLeftBound.position.x - 0.5f + cameraHalfWidth, _topRightBound.position.x + 0.5f - cameraHalfWidth);
			float clampedY = Mathf.Clamp(desiredPosition.y, _bottomLeftBound.position.y - 0.5f + cameraHalfHeight, _topRightBound.position.y + 0.5f - cameraHalfHeight);

			Vector3 finalPosition = new Vector3(clampedX, clampedY, desiredPosition.z);

			if (Vector3.Distance(transform.position, finalPosition) > 5f)
			{
				_isPanning = true;
			}

			if (_isPanning)
			{
				transform.position = Vector3.Lerp(transform.position, finalPosition, 15f * Time.deltaTime);
				if (Vector3.Distance(transform.position, finalPosition) < 0.1f)
				{
					_isPanning = false;
				}
			}
			else
			{
				transform.position = finalPosition;
			}
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

		public void SetTarget(Transform target)
		{
			_target = target;
		}
	}
}
