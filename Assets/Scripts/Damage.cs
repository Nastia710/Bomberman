using UnityEngine;

namespace Bomberman
{
    public class Damage : MonoBehaviour
    {
        public const int FIRE_DAMAGE = 1;
        public const int ENEMY_DAMAGE = 2;

        [SerializeField]
        private int _source;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.gameObject.CompareTag("Player"))
            {
                other.GetComponent<BomberMan>().Damage(_source);
            }

            if (other.gameObject.CompareTag("Enemy"))
            {
                other.GetComponent<Enemy>().Damage(_source);
            }
        }
    }
}
