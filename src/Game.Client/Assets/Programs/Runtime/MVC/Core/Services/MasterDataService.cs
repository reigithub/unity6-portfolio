using System;
using System.Threading.Tasks;
using Game.Library.Shared.MasterData;
using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace Game.Core.Services
{
    public class MasterDataService : IMasterDataService
    {
        private IAddressableAssetService _assetService;
        private IAddressableAssetService AssetService => _assetService ??= GameServiceManager.Get<AddressableAssetService>();

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
            var formatterResolvers = new[]
            {
                MasterMemoryResolver.Instance, // 自動生成されたResolver
                StandardResolver.Instance      // MessagePackの標準Resolver
            };
            // StaticCompositeResolver.Instance.Register(formatterResolvers); // 複数回の実行でエラーになるので廃止
            var compositeResolver = CompositeResolver.Create(formatterResolvers);
            var options = MessagePackSerializerOptions.Standard.WithResolver(compositeResolver);
            MessagePackSerializer.DefaultOptions = options;
        }

        public void Shutdown()
        {
        }

        public async Task LoadMasterDataAsync()
        {
            var asset = await AssetService.LoadAssetAsync<TextAsset>("MasterDataBinary");
            var binary = asset.bytes;
            MemoryDatabase = new MemoryDatabase(binary, maxDegreeOfParallelism: Environment.ProcessorCount);
        }
    }
}