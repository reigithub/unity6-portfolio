using Cysharp.Threading.Tasks;
using Game.Shared.Services;
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
    /// InitializeAsync でモデルロード・Animator 取得・購読開始をすべて自己完結する。
    /// </summary>
    public class SurvivorPlayerPresenter : MonoBehaviour
    {
        [Inject] private ISubscriber<SurvivorSignals.Player.Died> _playerDiedSub;
        [Inject] private IAddressableAssetService _addressableService;

        private static readonly int AnimatorHashSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimatorHashDeath = Animator.StringToHash("Death");

        private Animator _animator;
        private R3.DisposableBag _subscriptions;

        /// <summary>
        /// DI 注入 → モデルロード → Animator 取得 → 購読開始を自己完結する。
        /// Controller.InitializeVisualAsync から呼ばれる。
        /// </summary>
        public async UniTask InitializeAsync(string assetName, IObjectResolver resolver, SurvivorPlayerController controller)
        {
            resolver.Inject(this);

            var modelObj = await _addressableService.InstantiateAsync(assetName + "_Model", transform);
            if (modelObj != null)
            {
                modelObj.transform.localPosition = Vector3.zero;
                modelObj.transform.localRotation = Quaternion.identity;
                modelObj.TryGetComponent(out _animator);
            }

            controller.Speed
                .DistinctUntilChanged()
                .Subscribe(speed =>
                {
                    if (_animator != null)
                        _animator.SetFloat(AnimatorHashSpeed, speed);
                })
                .AddTo(ref _subscriptions);

            _playerDiedSub?
                .Subscribe(_ =>
                {
                    if (_animator != null)
                        _animator.SetTrigger(AnimatorHashDeath);
                })
                .AddTo(ref _subscriptions);
        }

        private void OnDestroy()
        {
            _subscriptions.Dispose();
        }
    }
}
