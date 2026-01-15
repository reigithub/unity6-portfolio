using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.Shared.Services;
using UnityEngine;
using VContainer;

namespace Game.MVP.Core.Services
{
    /// <summary>
    /// MVP用オーディオ再生サービス
    /// VContainerでDIされる、またはコンストラクタインジェクションで使用
    /// </summary>
    public class AudioService : AudioServiceBase
    {
        [Inject] private IAddressableAssetService _assetService;
        [Inject] private IMasterDataService _masterDataService;

        protected override MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        /// <summary>
        /// VContainer用のデフォルトコンストラクタ
        /// </summary>
        public AudioService()
        {
        }

        /// <summary>
        /// 手動インジェクション用コンストラクタ（AppServiceProvider等で使用）
        /// </summary>
        public AudioService(IAddressableAssetService assetService, IMasterDataService masterDataService)
        {
            _assetService = assetService;
            _masterDataService = masterDataService;
        }

        protected override async UniTask<AudioClip> LoadAudioClipAsync(string assetName)
        {
            return await _assetService.LoadAssetAsync<AudioClip>(assetName);
        }
    }
}
