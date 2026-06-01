using UnityEngine;

namespace Bomberman
{
	public class PowerUp : MonoBehaviour
	{
		public int Type => _type;

		[SerializeField]
		private int _type;
		[SerializeField]
		private float _invincibilityTime;

		private Unity.Netcode.NetworkObject _networkObject;

		private void Awake()
		{
			_networkObject = GetComponent<Unity.Netcode.NetworkObject>();
		}

		private void Update()
		{
			if (_invincibilityTime > 0)
			{
				_invincibilityTime -= Time.deltaTime;
			}
		}

		private void OnCollisionEnter2D(Collision2D other)
		{
			if (other.gameObject.CompareTag("Fire") && _invincibilityTime <= 0)
			{
				if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
				{
					if (_networkObject != null && _networkObject.IsSpawned)
					{
						_networkObject.Despawn(true);
					}
					else
					{
						Destroy(gameObject);
					}
				}
			}
		}
	}
}
