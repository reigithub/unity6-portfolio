using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Extensions;
using Game.Shared.Services;

namespace Game.Horror.Services
{
    public class HorrorGameRootService : IHorrorGameRootService, IGameService
    {
        private readonly IAddressableAssetService _assetService;
        private readonly IInputSystemService _inputSystemService;
        private readonly IMessagePipeService _messagePipeService;
        private readonly IHorrorOptionSaveRepository _optionRepository;

        private HorrorGameRootContainer _container;

        public HorrorGameRootService(
            IAddressableAssetService assetService,
            IInputSystemService inputSystemService,
            IMessagePipeService messagePipeService,
            IHorrorOptionSaveRepository optionRepository
            )
        {
            _assetService = assetService;
            _inputSystemService = inputSystemService;
            _messagePipeService = messagePipeService;
            _optionRepository = optionRepository;
        }

        public async UniTask LoadAsync()
        {
            var go = await _assetService.InstantiateAsync("HorrorGameRootContainer");
            if (go == null) return;

            if (go.TryGetComponent<HorrorGameRootContainer>(out var component))
            {
                _container = component;
                _container.Initialize(_inputSystemService, _messagePipeService, _optionRepository);
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

        public UniTask GlobalFadeInAsync(CancellationToken token = default)
            => _container.GlobalFadeInAsync(token);

        public UniTask GlobalFadeOutAsync(CancellationToken token = default)
            => _container.GlobalFadeOutAsync(token);
    }
}
