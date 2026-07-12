using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Services;
using UnityEngine;

namespace Game.ScoreTimeAttack.Enemy
{
    /// <summary>
    /// エネミー生成地点
    /// </summary>
    public class ScoreTimeAttackEnemyStart : MonoBehaviour
    {
        public async UniTask LoadEnemyAsync(GameObject player, int stageId)
        {
            var memoryDatabase = GameServiceManager.Resolve<IMasterDataService>().MemoryDatabase;
            var spawnMasters = memoryDatabase.ScoreTimeAttackEnemySpawnMasterTable.FindByStageId(stageId);
            // .Where(x => x.GroupId == ???);

            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            foreach (var spawnMaster in spawnMasters)
            {
                var enemyMaster = memoryDatabase.ScoreTimeAttackEnemyMasterTable.FindById(spawnMaster.EnemyId);
                var enemyAsset = await assetService.LoadAssetAsync<GameObject>(enemyMaster.AssetName);

                var spawnCount = Random.Range(spawnMaster.MinSpawnCount, spawnMaster.MaxSpawnCount);

                for (int i = 0; i < spawnCount; i++)
                {
                    // WARN: 一体ずつ配置位置を決めるのが面倒なので生成地点を中心としたランダムな位置に生成する
                    var randomX = Random.Range(-spawnMaster.X, spawnMaster.X);
                    var randomY = spawnMaster.Y;
                    var randomZ = Random.Range(-spawnMaster.Z, spawnMaster.Z);
                    var randomOffset = new Vector3(randomX, randomY, randomZ);

                    var enemy = Instantiate(enemyAsset, transform.position + randomOffset, Quaternion.identity, transform);
                    if (enemy.TryGetComponent<ScoreTimeAttackEnemyController>(out var enemyController))
                    {
                        enemyController.Initialize(player, enemyMaster);
                    }
                }
            }
        }
    }
}
