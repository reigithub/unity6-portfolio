using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Core.Services
{
    /// <summary>
    /// MVC用マスターデータサービス
    /// GameServiceManager経由で使用
    /// </summary>
    public class MasterDataService : MasterDataServiceBase, IGameService
    {
        private IAddressableAssetService _assetService;

        public MasterDataService()
        {
        }

        public MasterDataService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public void Startup()
        {
        }

        public void Shutdown()
        {
        }

        protected override async UniTask<TextAsset> LoadMasterDataBinaryAsync()
        {
            _assetService ??= GameServiceManager.Get<AddressableAssetService>();

            if (_assetService == null)
            {
                throw new DependencyInjectionException(
                    typeof(IAddressableAssetService),
                    DIErrorType.ServiceNotRegistered,
                    "IAddressableAssetService not available in MasterDataService");
            }

            return await _assetService.LoadAssetAsync<TextAsset>("MasterDataBinary");
        }
    }
}