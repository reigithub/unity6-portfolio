using Game.Shared.Signals.Survivor;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// プレイヤービジュアル駆動 — Controller の R3 Observable と MessagePipe シグナルを購読し Animator を制御。
    /// Visual 子 GameObject に配置され、SetActive(false) で一括停止する。
    /// Animator は Addressable モデルロード後に SetAnimator() で設定する。
    /// </summary>
    public class SurvivorPlayerPresenter : MonoBehaviour
    {
        [Inject] private ISubscriber<SurvivorSignals.Player.Died> _playerDiedSub;

        [SerializeField] private SurvivorPlayerController _controller;

        private static readonly int AnimatorHashSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimatorHashDeath = Animator.StringToHash("Death");

        private Animator _animator;
        private R3.DisposableBag _subscriptions;

        /// <summary>
        /// Addressable モデルロード完了後に Animator を設定する。
        /// </summary>
        public void SetAnimator(Animator animator)
        {
            _animator = animator;
        }

        private void OnEnable()
        {
            if (_controller == null) return;

            // スピードが変わった時にアニメーターを更新
            _controller.Speed
                .DistinctUntilChanged()
                .Subscribe(speed =>
                {
                    if (_animator != null)
                    {
                        _animator.SetFloat(AnimatorHashSpeed, speed);
                    }
                })
                .AddTo(ref _subscriptions);

            // 死亡シグナル → Death アニメーション
            if (_playerDiedSub != null)
            {
                _playerDiedSub
                    .Subscribe(_ =>
                    {
                        if (_animator != null)
                        {
                            _animator.SetTrigger(AnimatorHashDeath);
                        }
                    })
                    .AddTo(ref _subscriptions);
            }
        }

        private void OnDisable()
        {
            _subscriptions.Dispose();
            _subscriptions = new R3.DisposableBag();
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();
        }
    }
}
