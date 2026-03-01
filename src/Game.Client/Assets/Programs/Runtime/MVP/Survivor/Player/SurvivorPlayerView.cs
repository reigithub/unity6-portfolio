using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// クライアントモード時、NetworkVariable の変更を監視し、
    /// プレイヤーの Transform + Animator を補間更新する。
    /// </summary>
    public class SurvivorPlayerView : MonoBehaviour
    {
        private SurvivorPlayerController _controller;
        private SurvivorNetworkPlayerState _networkState;
        private Animator _animator;

        private Vector3 _targetPosition;
        private float _targetRotationY;
        private float _interpolationSpeed = 15f;
        private bool _isActive;

        private static readonly int AnimatorHashSpeed = Animator.StringToHash("Speed");

        public void Initialize(SurvivorPlayerController controller, SurvivorNetworkPlayerState networkState)
        {
            _controller = controller;
            _networkState = networkState;
            _animator = controller.GetComponentInChildren<Animator>();
            _targetPosition = transform.position;
            _targetRotationY = transform.eulerAngles.y;
            _networkState.State.OnValueChanged += OnStateChanged;
            _isActive = true;
        }

        private void OnStateChanged(
            SurvivorNetworkPlayerStateSnapshot prev,
            SurvivorNetworkPlayerStateSnapshot current)
        {
            _targetPosition = new Vector3(current.PositionX, current.PositionY, current.PositionZ);
            _targetRotationY = current.RotationY;

            if (_animator != null)
                _animator.SetFloat(AnimatorHashSpeed, current.Speed);

            _controller.SetCurrentHp(current.CurrentHp);
        }

        private void Update()
        {
            if (!_isActive) return;

            transform.position = Vector3.Lerp(
                transform.position, _targetPosition,
                Time.deltaTime * _interpolationSpeed);

            var rot = transform.eulerAngles;
            rot.y = Mathf.LerpAngle(rot.y, _targetRotationY, Time.deltaTime * _interpolationSpeed);
            transform.eulerAngles = rot;
        }

        private void OnDestroy()
        {
            if (_networkState != null)
                _networkState.State.OnValueChanged -= OnStateChanged;
        }
    }
}
