using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Client.MasterData;
using Game.Shared.Services;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Game.Core.Services
{
    /// <summary>
    /// MVC用オーディオ再生サービス
    /// GameServiceManager経由で使用
    /// </summary>
    public class AudioService : AudioServiceBase, IGameService
    {
        private IMasterDataService _masterDataService;

        protected override MemoryDatabase MemoryDatabase
            => (_masterDataService ??= GameServiceManager.Get<MasterDataService>()).MemoryDatabase;

        public AudioService()
        {
        }

        public AudioService(IMasterDataService masterDataService)
        {
            _masterDataService = masterDataService;
        }

        protected override async UniTask<AudioClip> LoadAudioClipAsync(string assetName)
        {
            return await Addressables.LoadAssetAsync<AudioClip>(assetName);
        }
    }
}
