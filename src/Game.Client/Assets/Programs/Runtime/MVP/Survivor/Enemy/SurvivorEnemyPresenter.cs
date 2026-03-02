#if !UNITY_SERVER
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// 敵ビジュアル駆動 — Controller の R3 Observable を購読し Animator/VFX を制御。
    /// Game.MVP.Survivor アセンブリ内（Server ビルドから除外済み）。
    /// </summary>
    public class SurvivorEnemyPresenter : MonoBehaviour
    {
        // Animator hashes（Controller から移動）
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private Animator _animator;
        private EnemyVisualEffectController _visualEffectController;
        private SurvivorEnemyController _controller;
        private DisposableBag _subscriptions;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            TryGetComponent(out _visualEffectController);
        }

        public void Initialize(SurvivorEnemyController controller)
        {
            _controller = controller;
            _subscriptions.Dispose();
            _subscriptions = new DisposableBag();

            controller.OnHitReceived
                .Subscribe(_ =>
                {
                    if (_animator != null)
                    {
                        _animator.SetTrigger(HitHash);
                    }

                    if (_visualEffectController != null)
                    {
                        _visualEffectController.PlayHitFlash();
                    }
                })
                .AddTo(ref _subscriptions);

            controller.OnAnimationStateChanged
                .Subscribe(state =>
                {
                    switch (state)
                    {
                        case EnemyAnimationState.Idle:
                        case EnemyAnimationState.HitStun:
                            if (_animator != null) _animator.SetFloat(SpeedHash, 0f);
                            break;
                        case EnemyAnimationState.Attack:
                            if (_animator != null)
                            {
                                _animator.SetFloat(SpeedHash, 0f);
                                _animator.SetTrigger(AttackHash);
                            }
                            break;
                        case EnemyAnimationState.Death:
                            if (_animator != null) _animator.SetTrigger(DeathHash);
                            if (_visualEffectController != null)
                            {
                                _visualEffectController.PlayDeathDissolveAsync(destroyCancellationToken).Forget();
                            }
                            break;
                    }
                })
                .AddTo(ref _subscriptions);
        }

        private void Update()
        {
            if (_controller == null || _controller.CurrentAnimationState != EnemyAnimationState.Chase) return;

            if (_animator != null)
            {
                _animator.SetFloat(SpeedHash, _controller.NormalizedSpeed);
            }
        }

        public void ResetForPool()
        {
            _subscriptions.Dispose();
            _subscriptions = new DisposableBag();
            _controller = null;

            if (_visualEffectController != null)
            {
                _visualEffectController.ResetEffects();
            }
        }
    }
}
#endif
