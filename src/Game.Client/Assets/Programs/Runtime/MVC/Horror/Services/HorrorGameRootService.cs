using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Horror.Interaction;
using Game.Horror.Services.Interfaces;
using Game.Shared.Extensions;
using Game.Shared.Services;

namespace Game.Horror.Services
{
    public class HorrorGameRootService : IHorrorGameRootService
    {
        private readonly IAddressableAssetService _assetService;

        private HorrorGameRootContainer _container;

        public HorrorGameRootService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public async UniTask LoadAsync()
        {
            var go = await _assetService.InstantiateAsync("HorrorGameRootContainer");
            if (go == null) return;

            if (go.TryGetComponent<HorrorGameRootContainer>(out var component))
            {
                _container = component;
                _container.Initialize();
            }

            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        public void Unload()
        {
            _assetService.ReleaseInstance(_container.gameObject);
            _container.gameObject.SafeDestroy();
            _container = null;
        }

        public UnityEngine.Camera Camera => _container.Camera;

        public IInteractionPromptPool PromptPool => _container.PromptPool;

        public UniTask GlobalFadeInAsync(CancellationToken token = default)
            => _container.GlobalFadeInAsync(token);

        public UniTask GlobalFadeOutAsync(CancellationToken token = default)
            => _container.GlobalFadeOutAsync(token);
    }
}
