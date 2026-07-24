using Game.Shared.Services;

namespace Game.Core.Services
{
    /// <summary>
    /// MVC用オーディオ再生サービス
    /// GameServiceManager経由で使用
    /// </summary>
    public class AudioService : AudioServiceBase
    {
        private readonly IAddressableAssetService _assetService;
        private readonly IMasterDataService _masterDataService;

        protected override IAddressableAssetService AssetService => _assetService;
        protected override IMasterDataService MasterDataService => _masterDataService;

        public AudioService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public AudioService(IAddressableAssetService assetService, IMasterDataService masterDataService)
        {
            _assetService = assetService;
            _masterDataService = masterDataService;
        }
    }
}
