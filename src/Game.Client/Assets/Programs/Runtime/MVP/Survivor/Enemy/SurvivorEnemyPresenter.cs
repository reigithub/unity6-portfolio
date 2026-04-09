using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// 敵ビジュアル駆動 — Controller の R3 Observable を購読し Animator/VFX を制御。
    /// Visual 子 GameObject に配置され、SetActive(false) で一括停止する。
    /// OnEnable で Controller.OnInitialized を購読し、初期化完了後に自動的に購読を開始する。
    /// </summary>
    public class SurvivorEnemyPresenter : MonoBehaviour
    {
        // Animator hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int DeathHash = Animator.StringToHash("Death");
        private static readonly int HitHash = Animator.StringToHash("Hit");
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        [SerializeField] private SurvivorEnemyController _controller;
        [SerializeField] private Animator _animator;
        [SerializeField] private EnemyVisualEffectController _visualEffectController;

        private DisposableBag _subscriptions;

        private void OnEnable()
        {
            if (_controller == null) return;

            _controller.OnInitialized
                .Subscribe(SubscribeToController)
                .AddTo(ref _subscriptions);
        }

        private void OnDisable()
        {
            _subscriptions.Dispose();
            _subscriptions = new DisposableBag();

            if (_visualEffectController != null)
            {
                _visualEffectController.ResetEffects();
            }
        }

        private void SubscribeToController(SurvivorEnemyController controller)
        {
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
    }
}
