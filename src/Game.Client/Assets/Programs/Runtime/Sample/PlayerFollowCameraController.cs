using Unity.Cinemachine;
using UnityEngine;

namespace Sample
{
    public class PlayerFollowCameraController : MonoBehaviour
    {
        [SerializeField] private GameObject _player;

        private bool _initialized;

        private void Initialize()
        {
            if (_initialized) return;

            if (_player)
            {
                if (gameObject.TryGetComponent<CinemachineCamera>(out var cinemachineCamera))
                {
                    cinemachineCamera.Target.TrackingTarget = _player.transform;
                    _initialized = true;
                }
            }
        }

        public void SetPlayer(GameObject player)
        {
            _player = player;
            Initialize();
        }
    }
}