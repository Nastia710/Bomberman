using UnityEngine;

namespace Bomberman
{
    public class DestroyOverTime : MonoBehaviour
    {
        [SerializeField]
        private float _delay;

        private float _counter;

        private void Start()
        {
            _counter = _delay;
        }

        private void Update()
        {
            if (_counter > 0)
            {
                _counter -= Time.deltaTime;
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
