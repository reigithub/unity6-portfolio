using Game.Shared.Services;
using VContainer;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// MVP用オーディオ再生サービス
    /// VContainerの[Inject]コンストラクタ、またはAppServiceProviderからの手動生成で使用
    /// </summary>
    public class AudioService : AudioServiceBase
    {
        private readonly IAddressableAssetService _assetService;
        private readonly IMasterDataService _masterDataService;

        protected override IAddressableAssetService AssetService => _assetService;
        protected override IMasterDataService MasterDataService => _masterDataService;

        [Inject]
        public AudioService(IAddressableAssetService assetService, IMasterDataService masterDataService)
        {
            _assetService = assetService;
            _masterDataService = masterDataService;
        }
    }
}
