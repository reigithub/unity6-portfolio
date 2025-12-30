using Unity.Cinemachine;
using UnityEngine;

namespace Game.Contents.Player
{
    public class PlayerFollowCameraController : MonoBehaviour
    {
        [SerializeField] private GameObject _camera;

        [SerializeField] private float _changeRadius = 0.5f;
        [SerializeField] private float _minRadius = 5f;
        [SerializeField] private float _maxRadius = 10f;

        [SerializeField] private GameObject _player;

        public void SetPlayer(GameObject player)
        {
            _player = player;

            if (_player)
            {
                if (_camera.TryGetComponent<CinemachineCamera>(out var cinemaCamera))
                {
                    cinemaCamera.Target.TrackingTarget = _player.transform;
                }
            }
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