using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.Shared.Exceptions;
using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace Game.Shared.Services
{
    /// <summary>
    /// マスターデータサービスの共通基底クラス
    /// SaveServiceBaseと同様に、MVC/MVP両方で継承して使用
    /// </summary>
    public abstract class MasterDataServiceBase : IMasterDataService
    {
        private static bool _isMessagePackInitialized;

        public MemoryDatabase MemoryDatabase { get; private set; }

        protected MasterDataServiceBase()
        {
            InitializeMessagePack();
        }

        private static void InitializeMessagePack()
        {
            if (_isMessagePackInitialized) return;

            var formatterResolvers = new[]
            {
                MasterMemoryResolver.Instance,
                StandardResolver.Instance
            };
            var compositeResolver = CompositeResolver.Create(formatterResolvers);
            var options = MessagePackSerializerOptions.Standard.WithResolver(compositeResolver);
            MessagePackSerializer.DefaultOptions = options;

            _isMessagePackInitialized = true;
        }

        /// <summary>
        /// マスターデータバイナリを読み込む（派生クラスで実装）
        /// </summary>
        protected abstract UniTask<TextAsset> LoadMasterDataBinaryAsync();

        public async UniTask LoadMasterDataAsync()
        {
            try
            {
                var asset = await LoadMasterDataBinaryAsync();
                if (asset == null)
                {
                    throw new MasterDataLoadException(
                        "MasterDataBinary",
                        "Master data binary asset returned null");
                }

                if (asset.bytes == null || asset.bytes.Length == 0)
                {
                    throw new MasterDataLoadException(
                        "MasterDataBinary",
                        "Master data binary is empty or corrupted");
                }

                MemoryDatabase = new MemoryDatabase(asset.bytes, maxDegreeOfParallelism: Environment.ProcessorCount);
                Debug.Log($"[{GetType().Name}] Master data loaded successfully.");
            }
            catch (MasterDataLoadException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new MasterDataLoadException(
                    "MasterDataBinary",
                    $"Failed to load or parse master data: {ex.Message}",
                    ex);
            }
        }
    }
}
