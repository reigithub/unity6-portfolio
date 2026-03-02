using Unity.Cinemachine;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// プレイヤー追従カメラ制御
    /// Prefab にアタッチ済みのため、クラス定義と SerializeField は常にコンパイル
    /// SurvivorGameRootController から呼び出されるため、メソッドシグネチャも維持
    /// </summary>
    public class SurvivorPlayerFollowCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _playerFollowCamera;

        [SerializeField] private float _changeRadius = 0.5f;
        [SerializeField] private float _minRadius = 5f;
        [SerializeField] private float _maxRadius = 10f;

#if !UNITY_SERVER
        private CinemachineOrbitalFollow _orbitalFollow;
        private CinemachineInputAxisController _inputAxisController;
#endif

        public void Initialize()
        {
#if !UNITY_SERVER
            TryGetComponent(out _orbitalFollow);
            TryGetComponent(out _inputAxisController);
#endif
        }

        /// <summary>
        /// カメラのフォロー対象を設定
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
#if !UNITY_SERVER
            if (_playerFollowCamera != null && target != null)
            {
                _playerFollowCamera.Follow = target;
                _playerFollowCamera.LookAt = target;
            }
#endif
        }

        /// <summary>
        /// フォロー対象をクリア
        /// </summary>
        public void ClearFollowTarget()
        {
#if !UNITY_SERVER
            if (_playerFollowCamera != null)
            {
                _playerFollowCamera.Follow = null;
                _playerFollowCamera.LookAt = null;
            }
#endif
        }

        public void SetCameraRadius(Vector2 scrollWheel)
        {
#if !UNITY_SERVER
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
#endif
        }

        public void SetInputAxisEnable(bool enable)
        {
#if !UNITY_SERVER
            _inputAxisController.enabled = enable;
#endif
        }
    }
}
