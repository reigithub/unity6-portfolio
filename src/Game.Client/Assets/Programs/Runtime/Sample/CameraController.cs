using UnityEngine;

namespace Sample
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private GameObject player;

        private Vector3 _offset;
        private bool _initialized;

        private void Start()
        {
            Initialize();
        }

        private void LateUpdate()
        {
            Initialize();
            if (player) this.transform.position = player.transform.position + _offset;
        }

        private void Initialize()
        {
            if (_initialized) return;

            if (player)
            {
                _offset = this.transform.position - player.transform.position;
                _initialized = true;
            }
        }

        public void SetPlayer(GameObject p)
        {
            player = p;
        }
    }
}