using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.MasterData;
using Game.Core.Services;
using UnityEngine;

namespace Game.Contents.Item
{
    /// <summary>
    /// ステージアイテム生成地点
    /// </summary>
    public class StageItemStart : MonoBehaviour
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        private AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<MasterDataService> _masterDataService;
        private MemoryDatabase MemoryDatabase => _masterDataService.Reference.MemoryDatabase;

        public async UniTask LoadStageItemAsync(int stageId)
        {
            // Memo: 本当は配置した生成地点で指定したものが良いが、今はランダムにしておく（マスタ側の設定値にバラつきがなければあまり偏らないため）
            var groupIds = MemoryDatabase.StageItemSpawnMasterTable.FindByStageId(stageId).Select(x => x.GroupId).ToArray();
            var randomGroupId = Random.Range(groupIds.Min(), groupIds.Max());

            var spawnMasters = MemoryDatabase.StageItemSpawnMasterTable.FindByStageId(stageId)
                .Where(x => x.GroupId == randomGroupId);

            transform.localScale = Vector3.one;

            foreach (var spawnMaster in spawnMasters)
            {
                var itemMaster = MemoryDatabase.StageItemMasterTable.FindById(spawnMaster.StageItemId);
                var itemAsset = await AssetService.LoadAssetAsync<GameObject>(itemMaster.AssetName);

                var spawnCount = Random.Range(spawnMaster.MinSpawnCount, spawnMaster.MaxSpawnCount);

                for (int i = 0; i < spawnCount; i++)
                {
                    var randomX = Random.Range(-spawnMaster.X, spawnMaster.X);
                    var randomY = Random.Range(1f, 1f);
                    var randomZ = Random.Range(-spawnMaster.Z, spawnMaster.Z);
                    var randomOffset = new Vector3(randomX, randomY, randomZ);

                    var instance = Instantiate(itemAsset, transform.position + randomOffset, Quaternion.identity, transform);
                    instance.transform.localScale = Vector3.one;
                }
            }
        }
    }
}