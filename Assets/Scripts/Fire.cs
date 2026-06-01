using UnityEngine;

namespace Bomberman
{
	public class Fire : MonoBehaviour
	{
		[SerializeField]
		private GameObject _brickDeathEffect;

		private void OnCollisionEnter2D(Collision2D other)
		{
			if (other.gameObject.CompareTag("Brick"))
			{
				Destroy(other.gameObject);
				Instantiate(_brickDeathEffect, transform.position, transform.rotation);
				
				if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
				{
					if (GameManager.Instance != null)
					{
						GameManager.Instance.OnBrickDestroyed(transform.position);
					}
				}
			}
		}

		public void DestroySelf()
		{
			Destroy(gameObject);
		}
	}
}
