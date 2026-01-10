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
        private GameServiceReference<AddressableAssetService> _assetService;
        private AddressableAssetService AssetService => _assetService.Reference;

        public MemoryDatabase MemoryDatabase { get; private set; }

        public void Startup()
        {
            var formatterResolvers = MasterDataHelper.GetMessagePackFormatterResolvers();
            StaticCompositeResolver.Instance.Register(formatterResolvers);
            var options = MessagePackSerializerOptions.Standard.WithResolver(StaticCompositeResolver.Instance);
            MessagePackSerializer.DefaultOptions = options;
        }

        public async Task LoadMasterDataAsync()
        {
            var asset = await AssetService.LoadAssetAsync<TextAsset>("MasterDataBinary");
            var binary = asset.bytes;
            MemoryDatabase = new MemoryDatabase(binary, maxDegreeOfParallelism: Environment.ProcessorCount);
        }
    }
}