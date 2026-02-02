using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Client.MasterData;
using Game.Shared.Services;
using UnityEngine;

namespace Game.ScoreTimeAttack.Item
{
    /// <summary>
    /// ステージアイテム生成地点
    /// </summary>
    public class ScoreTimeAttackStageItemStart : MonoBehaviour
    {
        private AddressableAssetService _assetService;
        private AddressableAssetService AssetService => _assetService ??= GameServiceManager.Get<AddressableAssetService>();

        private MasterDataService _masterDataService;
        private MemoryDatabase MemoryDatabase => (_masterDataService ??= GameServiceManager.Get<MasterDataService>()).MemoryDatabase;

        public async UniTask LoadStageItemAsync(int stageId)
        {
            // Memo: 本当は配置した生成地点で指定したものが良いが、今はランダムにしておく（マスタ側の設定値にバラつきがなければあまり偏らないため）
            var groupIds = MemoryDatabase.ScoreTimeAttackStageItemSpawnMasterTable.FindByStageId(stageId).Select(x => x.GroupId).ToArray();
            var randomGroupId = Random.Range(groupIds.Min(), groupIds.Max());

            var spawnMasters = MemoryDatabase.ScoreTimeAttackStageItemSpawnMasterTable.FindByStageId(stageId)
                .Where(x => x.GroupId == randomGroupId);

            transform.localScale = Vector3.one;

            foreach (var spawnMaster in spawnMasters)
            {
                var itemMaster = MemoryDatabase.ScoreTimeAttackStageItemMasterTable.FindById(spawnMaster.StageItemId);
                var itemAsset = await AssetService.LoadAssetAsync<GameObject>(itemMaster.AssetName);

                var spawnCount = Random.Range(spawnMaster.MinSpawnCount, spawnMaster.MaxSpawnCount);

                for (int i = 0; i < spawnCount; i++)
                {
                    var randomX = Random.Range(-spawnMaster.X, spawnMaster.X);
                    var randomY = spawnMaster.Y;
                    var randomZ = Random.Range(-spawnMaster.Z, spawnMaster.Z);
                    var randomOffset = new Vector3(randomX, randomY, randomZ);

                    var instance = Instantiate(itemAsset, transform.position + randomOffset, Quaternion.identity, transform);
                    instance.transform.localScale = Vector3.one;

                    // 地面に固定する
                    var position = instance.transform.position;
                    var ray = new Ray(position, Vector3.down);
                    if (Physics.Raycast(ray, out var raycastHit, 30f))
                    {
                        var newPosition = new Vector3(raycastHit.point.x, raycastHit.point.y + 1.5f, raycastHit.point.z);
                        instance.transform.position = newPosition;
                    }
                }
            }
        }
    }
}