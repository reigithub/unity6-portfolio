using Game.Shared.Network;
using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// クライアントモード時、SyncVar の変更を監視し、
    /// プレイヤーの Transform + Animator を補間更新する。
    /// ローカルプレイヤー: クライアント予測移動 + サーバー補正（閾値超過時のみ）
    /// リモートプレイヤー: サーバー位置への補間表示
    /// </summary>
    public class SurvivorPlayerView : MonoBehaviour
    {
        private SurvivorPlayerController _controller;
        private SurvivorNetworkPlayerState _networkState;
        private Animator _animator;

        private Vector3 _targetPosition;
        private float _targetRotationY;
        private bool _isActive;
        private bool _isLocalPlayer;

        // リモートプレイヤー用補間速度
        private const float RemoteInterpSpeed = 15f;

        // ローカルプレイヤー用サーバー補正閾値
        private const float CorrectionThreshold = 1.5f;
        private const float CorrectionSpeed = 10f;

        private static readonly int AnimatorHashSpeed = Animator.StringToHash("Speed");

        public void Initialize(SurvivorPlayerController controller, SurvivorNetworkPlayerState networkState)
        {
            _controller = controller;
            _networkState = networkState;
            _animator = controller.GetComponentInChildren<Animator>();
            _targetPosition = transform.position;
            _targetRotationY = transform.eulerAngles.y;
            _networkState.OnStateUpdated += OnStateChanged;
            _isLocalPlayer = networkState.isOwned && !NetworkModeHelper.IsNetworkServer;
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

            // HP はサーバー権威: 常に同期
            _controller.SetCurrentHp(current.CurrentHp);
        }

        private void Update()
        {
            if (!_isActive) return;

            if (_isLocalPlayer)
            {
                // ローカルプレイヤー: クライアント予測で移動済み
                // サーバー位置との乖離が閾値を超えた場合のみ補正
                var error = Vector3.Distance(transform.position, _targetPosition);
                if (error > CorrectionThreshold)
                {
                    transform.position = Vector3.Lerp(
                        transform.position, _targetPosition,
                        Time.deltaTime * CorrectionSpeed);
                }
            }
            else
            {
                // リモートプレイヤー: サーバー位置への補間表示
                transform.position = Vector3.Lerp(
                    transform.position, _targetPosition,
                    Time.deltaTime * RemoteInterpSpeed);

                var rot = transform.eulerAngles;
                rot.y = Mathf.LerpAngle(rot.y, _targetRotationY, Time.deltaTime * RemoteInterpSpeed);
                transform.eulerAngles = rot;
            }
        }

        private void OnDestroy()
        {
            if (_networkState != null)
                _networkState.OnStateUpdated -= OnStateChanged;
        }
    }
}
