#if !UNITY_SERVER
using Game.Shared.Signals.Survivor;
using MessagePipe;
using R3;
using UnityEngine;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// プレイヤービジュアル駆動 — Controller の R3 Observable と MessagePipe シグナルを購読し Animator を制御。
    /// Game.MVP.Survivor アセンブリ内（Server ビルドから除外済み）。
    /// </summary>
    public class SurvivorPlayerPresenter : MonoBehaviour
    {
        private static readonly int AnimatorHashSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimatorHashDeath = Animator.StringToHash("Death");

        private Animator _animator;
        private SurvivorPlayerController _controller;
        private R3.DisposableBag _subscriptions;

        private void Awake()
        {
            TryGetComponent(out _animator);
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        public void Initialize(
            SurvivorPlayerController controller,
            ISubscriber<SurvivorSignals.Player.Died> diedSubscriber)
        {
            _controller = controller;
            _subscriptions.Dispose();
            _subscriptions = new R3.DisposableBag();

            // スピードが変わった時にアニメーターを更新
            controller.Speed
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
            diedSubscriber
                .Subscribe(_ =>
                {
                    if (_animator != null)
                    {
                        _animator.SetTrigger(AnimatorHashDeath);
                    }
                })
                .AddTo(ref _subscriptions);
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();
        }
    }
}
#endif
