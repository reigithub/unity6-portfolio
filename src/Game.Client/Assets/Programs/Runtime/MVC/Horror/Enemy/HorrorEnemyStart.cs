using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// 敵生成地点。スポーンエントリマスター（HorrorEnemySpawnMaster）から敵種を解決し、
    /// 配置された地点で敵 prefab を Addressables から生成して <see cref="HorrorEnemyController.Initialize"/> する。
    /// 撃破済みエントリは生成しない（セーブデータからの自己復元）。
    /// HorrorPlayerStart と同じ「シーンに配置 → シーン側が走査して起動」する作法に倣う。
    /// </summary>
    public class HorrorEnemyStart : MonoBehaviour
    {
        [Tooltip("スポーンエントリの ID（HorrorEnemySpawnMasterTable の PrimaryKey）。0 は未設定")]
        [SerializeField] private int _spawnId;

        private GameObject _enemy;

        /// <summary>スポーンエントリの ID（シーン起動時の未設定・重複検証用）</summary>
        public int SpawnId => _spawnId;

        /// <summary>
        /// マスターデータを引いて敵を生成・初期化する。撃破済みの場合は生成しない。
        /// </summary>
        /// <param name="player">追跡対象のプレイヤー GameObject</param>
        public async UniTask LoadEnemyAsync(GameObject player)
        {
            var enemyService = GameServiceManager.Resolve<IHorrorEnemyService>();
            if (enemyService.IsDefeated(_spawnId)) return;

            var dbService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            if (!dbService.Database.HorrorEnemySpawnMasterTable.TryFindById(_spawnId, out var spawn))
            {
                Debug.LogError($"[HorrorEnemyStart] HorrorEnemySpawnMaster (Id={_spawnId}) が見つかりません。");
                return;
            }

            if (!dbService.Database.HorrorEnemyMasterTable.TryFindById(spawn.EnemyMasterId, out var master))
            {
                Debug.LogError($"[HorrorEnemyStart] HorrorEnemyMaster (Id={spawn.EnemyMasterId}) が見つかりません。");
                return;
            }

            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            _enemy = await assetService.InstantiateAsync(master.ModelAssetName, transform);

            if (_enemy.TryGetComponent<HorrorEnemyController>(out var controller))
            {
                controller.Initialize(player, master, _spawnId);
                return;
            }

            throw new MissingComponentException($"Cannot find {nameof(HorrorEnemyController)}");
        }

        public void UnloadEnemy()
        {
            if (_enemy == null) return;
            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            assetService.ReleaseInstance(_enemy);
            _enemy = null;
        }
    }
}
