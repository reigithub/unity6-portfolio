using Unity.Cinemachine;
using UnityEngine;

namespace Game.ScoreTimeAttack.Player
{
    public class PlayerFollowCameraController : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera _camera;

        [SerializeField] private float _changeRadius = 0.5f;
        [SerializeField] private float _minRadius = 5f;
        [SerializeField] private float _maxRadius = 10f;

        /// <summary>
        /// カメラのフォロー対象を設定
        /// </summary>
        public void SetFollowTarget(Transform target)
        {
            if (target != null)
            {
                _camera.Follow = target;
                _camera.LookAt = target;
            }
        }

        /// <summary>
        /// フォロー対象をクリア
        /// </summary>
        public void ClearFollowTarget()
        {
            _camera.Follow = null;
            _camera.LookAt = null;
        }

        public void SetCameraRadius(Vector2 scrollWheel)
        {
            if (_camera.TryGetComponent<CinemachineOrbitalFollow>(out var orbitalFollow))
            {
                switch (orbitalFollow.OrbitStyle)
                {
                    case CinemachineOrbitalFollow.OrbitStyles.ThreeRing:
                    {
                        var radius = orbitalFollow.Orbits.Center.Radius;
                        var pitch = scrollWheel.y < 0f ? _changeRadius : -_changeRadius;
                        var clamped = Mathf.Clamp(radius + pitch, _minRadius, _maxRadius);
                        orbitalFollow.Orbits.Center.Radius = clamped;
                        break;
                    }
                    case CinemachineOrbitalFollow.OrbitStyles.Sphere:
                    {
                        var radius = orbitalFollow.Radius;
                        var pitch = scrollWheel.y < 0f ? _changeRadius : -_changeRadius;
                        var clamped = Mathf.Clamp(radius + pitch, _minRadius, _maxRadius);
                        orbitalFollow.Radius = clamped;
                        break;
                    }
                }
            }
        }
    }
}
