using Cysharp.Threading.Tasks;
using Game.Client.MasterData;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Core.Services
{
    /// <summary>
    /// MVC用オーディオ再生サービス
    /// GameServiceManager経由で使用
    /// </summary>
    public class AudioService : AudioServiceBase, IGameService
    {
        private readonly IAddressableAssetService _assetService;
        private readonly IMasterDataService _masterDataService;

        protected override MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        public AudioService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

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
