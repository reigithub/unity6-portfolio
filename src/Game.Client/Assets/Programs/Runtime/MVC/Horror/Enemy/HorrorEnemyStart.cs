using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// 敵生成地点。配置された地点で HorrorEnemy prefab を Addressables から生成し、
    /// マスターデータ（ScriptableDatabase）を引いて <see cref="HorrorEnemyController.Initialize"/> する。
    /// HorrorPlayerStart と同じ「シーンに配置 → シーン側が走査して起動」する作法に倣う。
    /// </summary>
    public class HorrorEnemyStart : MonoBehaviour
    {
        [Tooltip("生成する敵のマスターデータ ID（HorrorEnemyMasterTable の PrimaryKey）")]
        [SerializeField] private int _enemyMasterId = 1;

        private GameObject _enemy;

        /// <summary>
        /// マスターデータを引いて敵を生成・初期化する。
        /// </summary>
        /// <param name="player">追跡対象のプレイヤー GameObject</param>
        public async UniTask LoadEnemyAsync(GameObject player)
        {
            var dbService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            if (!dbService.Database.HorrorEnemyMasterTable.TryFindById(_enemyMasterId, out var master))
            {
                Debug.LogError($"[HorrorEnemyStart] HorrorEnemyMaster (Id={_enemyMasterId}) が見つかりません。");
                return;
            }

            var assetService = GameServiceManager.Resolve<IAddressableAssetService>();
            _enemy = await assetService.InstantiateAsync(master.ModelAssetName, transform);

            if (_enemy.TryGetComponent<HorrorEnemyController>(out var controller))
            {
                controller.Initialize(player, master);
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
