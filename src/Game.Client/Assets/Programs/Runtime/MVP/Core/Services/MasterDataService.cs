using Cysharp.Threading.Tasks;
using Game.Shared.Services;
using UnityEngine;
using VContainer;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// MVP用マスターデータサービス
    /// VContainerでDIされる、またはコンストラクタインジェクションで使用
    /// </summary>
    public class MasterDataService : MasterDataServiceBase
    {
        [Inject] private IAddressableAssetService _assetService;

        /// <summary>
        /// VContainer用のデフォルトコンストラクタ
        /// </summary>
        public MasterDataService()
        {
        }

        /// <summary>
        /// 手動インジェクション用コンストラクタ（AppServiceProvider等で使用）
        /// </summary>
        public MasterDataService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        protected override async UniTask<TextAsset> LoadMasterDataBinaryAsync()
        {
            return await _assetService.LoadAssetAsync<TextAsset>("MasterDataBinary");
        }
    }
}