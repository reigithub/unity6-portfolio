using System;
using Cysharp.Threading.Tasks;
using Game.Shared;
using Game.Shared.Services;
using UnityEngine;

namespace Game.App.Services
{
    /// <summary>
    /// アプリケーションレベルのサービスプロバイダー実装
    /// DependencyResolverModeに基づいてMVC/MVPサービスを切り替え
    /// タイトル画面終了時に破棄（各ゲームモードで再構築）
    /// </summary>
    public class AppServiceProvider : IAppServiceProvider
    {
        private readonly DependencyResolverMode _mode;

        public IMasterDataService MasterDataService { get; private set; }
        public IAddressableAssetService AddressableAssetService { get; private set; }
        public IAudioService AudioService { get; private set; }

        private bool _isInitialized;
        private bool _isDisposed;

        public AppServiceProvider()
        {
            _mode = GameEnvironmentHelper.CurrentConfig.DependencyResolverMode;
            Debug.Log($"[AppServiceProvider] Mode: {_mode}");
        }

        public AppServiceProvider(DependencyResolverMode mode)
        {
            _mode = mode;
            Debug.Log($"[AppServiceProvider] Mode: {_mode}");
        }

        public async UniTask InitializeAsync()
        {
            if (_isInitialized) return;
            if (_isDisposed)
            {
                Debug.LogError("[AppServiceProvider] Already disposed.");
                return;
            }

            Debug.Log("[AppServiceProvider] Initializing services...");

            CreateServices();
            await LoadMasterDataAsync();
            InitializeAudioService();

            _isInitialized = true;
            Debug.Log("[AppServiceProvider] Services initialized.");
        }

        private void CreateServices()
        {
            switch (_mode)
            {
                case DependencyResolverMode.ServiceLocator:
                    CreateMvcServices();
                    break;
                case DependencyResolverMode.DiContainer:
                    CreateMvpServices();
                    break;
                default:
                    // Debug.LogWarning($"[AppServiceProvider] Unknown mode: {_mode}, defaulting to ServiceLocator");
                    throw new InvalidOperationException("[AppServiceProvider] Unknown mode: {_mode}");
                // break;
            }
        }

        private void CreateMvcServices()
        {
            var addressableService = new Game.Core.Services.AddressableAssetService();
            var masterDataService = new Game.Core.Services.MasterDataService(addressableService);
            var audioService = new Game.Core.Services.AudioService(masterDataService);

            AddressableAssetService = addressableService;
            MasterDataService = masterDataService;
            AudioService = audioService;
        }

        private void CreateMvpServices()
        {
            var addressableService = new Game.MVP.Core.Services.AddressableAssetService();
            var masterDataService = new Game.MVP.Core.Services.MasterDataService(addressableService);
            var audioService = new Game.MVP.Core.Services.AudioService(addressableService, masterDataService);

            AddressableAssetService = addressableService;
            MasterDataService = masterDataService;
            AudioService = audioService;
        }

        private async UniTask LoadMasterDataAsync()
        {
            if (MasterDataService == null)
            {
                Debug.LogError("[AppServiceProvider] MasterDataService is null.");
                return;
            }

            await MasterDataService.LoadMasterDataAsync();
            Debug.Log("[AppServiceProvider] MasterData loaded.");
        }

        private void InitializeAudioService()
        {
            AudioService?.Startup();
            Debug.Log("[AppServiceProvider] AudioService started.");
        }

        public void Dispose()
        {
            if (_isDisposed) return;

            Debug.Log("[AppServiceProvider] Disposing services...");

            AudioService?.Shutdown();

            AddressableAssetService = null;
            MasterDataService = null;
            AudioService = null;

            _isInitialized = false;
            _isDisposed = true;

            Debug.Log("[AppServiceProvider] Services disposed.");
        }
    }
}