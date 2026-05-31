using UnityEngine;

namespace Bomberman
{
    public class PowerUp : MonoBehaviour
    {
        [SerializeField]
        private int _type;
        [SerializeField]
        private float _invincibilityTime;

        public int Type => _type;

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
                if (Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    GetComponent<Unity.Netcode.NetworkObject>().Despawn(true);
                }
            }
        }
    }
}
