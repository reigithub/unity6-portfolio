using Cysharp.Threading.Tasks;
using Game.Shared.Exceptions;
using Game.Shared.Services;
using UnityEngine;
using VContainer;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// MVP用マスターデータサービス
    /// VContainerの[Inject]コンストラクタ、またはAppServiceProviderからの手動生成で使用
    /// </summary>
    public class MasterDataService : MasterDataServiceBase
    {
        private readonly IAddressableAssetService _assetService;

        [Inject]
        public MasterDataService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        protected override async UniTask<TextAsset> LoadMasterDataBinaryAsync()
        {
            if (_assetService == null)
            {
                throw new DependencyInjectionException(
                    typeof(IAddressableAssetService),
                    DIErrorType.ServiceNotRegistered,
                    "IAddressableAssetService not injected into MasterDataService");
            }

            return await _assetService.LoadAssetAsync<TextAsset>("MasterDataBinary");
        }
    }
}