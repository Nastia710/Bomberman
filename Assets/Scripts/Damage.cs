using UnityEngine;

namespace Bomberman
{
	public class Damage : MonoBehaviour
	{
		[SerializeField]
		private int _source;

		public const int FIRE_DAMAGE = 1;
		public const int ENEMY_DAMAGE = 2;

		private void OnTriggerEnter2D(Collider2D other)
		{
			if (other.gameObject.CompareTag("Player") && other.TryGetComponent<BomberMan>(out BomberMan player))
			{
				player.Damage(_source);
			}

			if (other.gameObject.CompareTag("Enemy") && other.TryGetComponent<Enemy>(out Enemy enemy))
			{
				enemy.Damage(_source);
			}
		}
	}
}
