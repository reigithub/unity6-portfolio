using System;
using System.Threading.Tasks;
using Game.Core.MasterData;
using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace Game.Core.Services
{
    public class MasterDataService : IMasterDataService
    {
        private IAddressableAssetService _assetService;

        public MemoryDatabase MemoryDatabase { get; private set; }

        public MasterDataService()
        {
        }

        public MasterDataService(IAddressableAssetService assetService)
        {
            _assetService = assetService;
        }

        public void Startup()
        {
            // DIコンテナ未導入版への後方互換性のため
            _assetService ??= GameServiceManager.Instance.GetService<AddressableAssetService>();

            var formatterResolvers = MasterDataHelper.GetMessagePackFormatterResolvers();
            StaticCompositeResolver.Instance.Register(formatterResolvers);
            var options = MessagePackSerializerOptions.Standard.WithResolver(StaticCompositeResolver.Instance);
            MessagePackSerializer.DefaultOptions = options;
        }

        public async Task LoadMasterDataAsync()
        {
            var asset = await _assetService.LoadAssetAsync<TextAsset>("MasterDataBinary");
            var binary = asset.bytes;
            MemoryDatabase = new MemoryDatabase(binary, maxDegreeOfParallelism: Environment.ProcessorCount);
        }
    }
}