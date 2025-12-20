using UnityEngine;

namespace Sample
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private GameObject player;

        private Vector3 _offset;

        private void Start()
        {
            _offset = this.transform.position - player.transform.position;
        }

        private void LateUpdate()
        {
            this.transform.position = player.transform.position + _offset;
        }
    }
}