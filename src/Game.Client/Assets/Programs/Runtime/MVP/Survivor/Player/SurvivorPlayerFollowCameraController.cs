using Unity.Cinemachine;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    public class SurvivorPlayerFollowCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _playerFollowCamera;

        [SerializeField] private float _changeRadius = 0.5f;
        [SerializeField] private float _minRadius = 5f;
        [SerializeField] private float _maxRadius = 10f;

        private CinemachineOrbitalFollow _orbitalFollow;
        private CinemachineInputAxisController _inputAxisController;

        public void Initialize()
        {
            TryGetComponent(out _orbitalFollow);
            TryGetComponent(out _inputAxisController);
        }

        /// <summary>
        /// カメラのフォロー対象を設定
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            if (_playerFollowCamera != null && target != null)
            {
                _playerFollowCamera.Follow = target;
                _playerFollowCamera.LookAt = target;
            }
        }

        /// <summary>
        /// フォロー対象をクリア
        /// </summary>
        public void ClearFollowTarget()
        {
            if (_playerFollowCamera != null)
            {
                _playerFollowCamera.Follow = null;
                _playerFollowCamera.LookAt = null;
            }
        }

        public void SetCameraRadius(Vector2 scrollWheel)
        {
            switch (_orbitalFollow.OrbitStyle)
            {
                case CinemachineOrbitalFollow.OrbitStyles.ThreeRing:
                {
                    var radius = _orbitalFollow.Orbits.Center.Radius;
                    var pitch = scrollWheel.y < 0f ? _changeRadius : -_changeRadius;
                    var clamped = Mathf.Clamp(radius + pitch, _minRadius, _maxRadius);
                    _orbitalFollow.Orbits.Center.Radius = clamped;
                    break;
                }
                case CinemachineOrbitalFollow.OrbitStyles.Sphere:
                {
                    var radius = _orbitalFollow.Radius;
                    var pitch = scrollWheel.y < 0f ? _changeRadius : -_changeRadius;
                    var clamped = Mathf.Clamp(radius + pitch, _minRadius, _maxRadius);
                    _orbitalFollow.Radius = clamped;
                    break;
                }
            }
        }

        public void SetInputAxisEnable(bool enable)
        {
            _inputAxisController.enabled = enable;
        }
    }
}