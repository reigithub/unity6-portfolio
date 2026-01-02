using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Core.MasterData;
using Game.Core.Services;
using UnityEngine;

namespace Game.Contents.Enemy
{
    /// <summary>
    /// エネミー生成地点
    /// </summary>
    public class EnemyStart : MonoBehaviour
    {
        private GameServiceReference<AddressableAssetService> _assetService;
        private AddressableAssetService AssetService => _assetService.Reference;

        private GameServiceReference<MasterDataService> _masterDataService;
        private MemoryDatabase MemoryDatabase => _masterDataService.Reference.MemoryDatabase;

        public async UniTask LoadEnemyAsync(GameObject player, int stageId, int spawnGroupId = 1)
        {
            var spawnMasters = MemoryDatabase.EnemySpawnMasterTable.FindByStageId(stageId)
                .Where(x => x.GroupId == spawnGroupId);

            foreach (var spawnMaster in spawnMasters)
            {
                var enemyMaster = MemoryDatabase.EnemyMasterTable.FindById(spawnMaster.EnemyId);
                var enemyAsset = await AssetService.LoadAssetAsync<GameObject>(enemyMaster.AssetName);

                var spawnCount = Random.Range(spawnMaster.MinSpawnCount, spawnMaster.MaxSpawnCount);

                for (int i = 0; i < spawnCount; i++)
                {
                    // WARN: 一体ずつ配置位置を決めるのが面倒なので生成地点を中心としたランダムな位置に生成する
                    var randomX = Random.Range(-spawnMaster.X, spawnMaster.X);
                    var randomY = Random.Range(1f, spawnMaster.Y);
                    var randomZ = Random.Range(-spawnMaster.Z, spawnMaster.Z);
                    var randomOffset = new Vector3(randomX, randomY, randomZ);

                    var enemy = Instantiate(enemyAsset, transform.position + randomOffset, Quaternion.identity, transform);
                    if (enemy.TryGetComponent<EnemyController>(out var enemyController))
                    {
                        enemyController.Initialize(player, enemyMaster);
                    }
                }
            }
        }
    }
}