using UnityEngine;

namespace Bomberman
{
    public class Fire : MonoBehaviour
    {
        [SerializeField]
        private GameObject _brickDeathEffect;

        public void DestroySelf()
        {
            Destroy(gameObject);
        }

        private void OnCollisionEnter2D(Collision2D other)
        {
            if (other.gameObject.CompareTag("Brick"))
            {
                Destroy(other.gameObject);
                Instantiate(_brickDeathEffect, transform.position, transform.rotation);
                
                if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsServer)
                {
                    GameManager.Instance.OnBrickDestroyed(transform.position);
                }
            }
        }
    }
}
