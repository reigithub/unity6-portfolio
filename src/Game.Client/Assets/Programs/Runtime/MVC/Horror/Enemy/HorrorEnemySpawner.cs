using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Horror.Services.Interfaces;
using Game.Shared.Extensions;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// Horror のエネミースポナー。シーン上のマーカー（<see cref="HorrorEnemyStart"/>）から
    /// スポーンエントリの registry を構築し、敵種（EnemyMaster）単位のオブジェクトプールで個体を使い回す。
    /// 死亡演出完了の通知（<see cref="HorrorEnemyController.Initialize"/> へ注入するコールバック）でプールへ回収する。
    /// <see cref="TrySpawn"/> はグループ連鎖スポーン（要件3）の受け口を兼ねる。
    /// </summary>
    public class HorrorEnemySpawner
    {
        private readonly IAddressableAssetService _assetService;
        private readonly IScriptableDatabaseService _dbService;
        private readonly IHorrorEnemyService _enemyService;

        // spawnId → スポーン定義（マーカー位置 + マスタ解決結果）
        private readonly Dictionary<int, SpawnEntry> _entries = new();

        // EnemyMasterId → プール / ロード済み prefab
        private readonly Dictionary<int, ObjectPool<HorrorEnemyController>> _pools = new();
        private readonly Dictionary<int, GameObject> _prefabs = new();

        // spawnId → 貸出中の個体
        private readonly Dictionary<int, HorrorEnemyController> _activeEnemies = new();

        private GameObject _player;
        private Transform _poolParent;

        private sealed class SpawnEntry
        {
            public Transform Marker;
            public HorrorEnemyMaster EnemyMaster;
        }

        public HorrorEnemySpawner(
            IAddressableAssetService assetService,
            IScriptableDatabaseService dbService,
            IHorrorEnemyService enemyService)
        {
            _assetService = assetService;
            _dbService = dbService;
            _enemyService = enemyService;
        }

        /// <summary>
        /// registry 構築 → prefab 事前ロード + プール構築 → 初期スポーンを行う。シーン起動時に1回呼ぶ。
        /// </summary>
        /// <param name="player">追跡対象のプレイヤー GameObject</param>
        /// <param name="markers">シーン上のスポーン地点マーカー</param>
        public async UniTask InitializeAsync(GameObject player, IReadOnlyList<HorrorEnemyStart> markers)
        {
            _player = player;
            _poolParent = new GameObject("HorrorEnemyPool").transform;

            BuildEntries(markers);
            await PreloadPrefabsAndBuildPoolsAsync();

            foreach (var spawnId in _entries.Keys)
                TrySpawn(spawnId);
        }

        /// <summary>
        /// 指定スポーンエントリの敵をプールから貸し出して起動する。
        /// 撃破済みエントリは生成しない（セーブデータからの自己復元）ため無音で false を返す。
        /// </summary>
        /// <param name="spawnId">スポーンエントリの ID（HorrorEnemySpawnMaster の PrimaryKey）</param>
        /// <returns>貸し出して起動できたら true</returns>
        public bool TrySpawn(int spawnId)
        {
            if (_enemyService.IsDefeated(spawnId)) return false;

            if (!_entries.TryGetValue(spawnId, out var entry))
            {
                Debug.LogError($"[{nameof(HorrorEnemySpawner)}] 未登録の SpawnId={spawnId} はスポーンできません");
                return false;
            }

            if (_activeEnemies.ContainsKey(spawnId))
            {
                Debug.LogError($"[{nameof(HorrorEnemySpawner)}] SpawnId={spawnId} は貸出中のため二重スポーンできません");
                return false;
            }

            if (!_pools.TryGetValue(entry.EnemyMaster.Id, out var pool))
            {
                Debug.LogError($"[{nameof(HorrorEnemySpawner)}] EnemyMasterId={entry.EnemyMaster.Id} のプールがありません（prefab ロード失敗の可能性）");
                return false;
            }

            var enemy = pool.Get();
            enemy.transform.SetPositionAndRotation(entry.Marker.position, entry.Marker.rotation);

            // SetActive(true) の後に Initialize を呼ぶ（コンポーネントの OnEnable を先行させる。SurvivorEnemySpawner と同じ順序）
            enemy.gameObject.SetActive(true);
            enemy.Initialize(_player, entry.EnemyMaster, spawnId, onDeathFinished: () => ReturnToPool(spawnId));

            _activeEnemies.Add(spawnId, enemy);
            return true;
        }

        /// <summary>
        /// 貸出中の個体をプールへ返却する（死亡演出完了通知の実体）。
        /// 貸出中でない返却は無音で握りつぶさず LogError で顕在化する。
        /// </summary>
        internal void ReturnToPool(int spawnId)
        {
            if (!_activeEnemies.Remove(spawnId, out var enemy))
            {
                Debug.LogError($"[{nameof(HorrorEnemySpawner)}] 貸出中でない SpawnId={spawnId} が返却されました（二重返却の疑い）");
                return;
            }

            _pools[_entries[spawnId].EnemyMaster.Id].Release(enemy);
        }

        /// <summary>
        /// 全個体・プール・ロード済み prefab・プール親を破棄する。
        /// ステージシーンのアンロード前（HorrorStageScene.Terminate）に呼ぶ。
        /// </summary>
        public void Dispose()
        {
            // 貸出中を全返却してからプールを破棄する（Clear の actionOnDestroy が待機個体を破棄する）
            foreach ((int spawnId, HorrorEnemyController controller) in _activeEnemies)
                _pools[_entries[spawnId].EnemyMaster.Id].Release(controller);
            _activeEnemies.Clear();

            foreach (var pool in _pools.Values)
                pool.Clear();
            _pools.Clear();

            foreach (var prefab in _prefabs.Values)
                _assetService.Release(prefab);
            _prefabs.Clear();

            if (_poolParent != null)
            {
                _poolParent.gameObject.SafeDestroy();
                _poolParent = null;
            }

            _entries.Clear();
        }

        /// <summary>
        /// マーカーを検証しながら spawnId → スポーン定義の registry を構築する。
        /// SpawnId の設定ミスは撃破時ではなくシーン起動時に決定的に検出する。
        /// </summary>
        private void BuildEntries(IReadOnlyList<HorrorEnemyStart> markers)
        {
            var database = _dbService.Database;
            foreach (var marker in markers)
            {
                if (marker.SpawnId == 0)
                {
                    Debug.LogError($"[{nameof(HorrorEnemySpawner)}] {marker.name} の SpawnId が未設定(0)です", marker);
                    continue;
                }

                if (_entries.ContainsKey(marker.SpawnId))
                {
                    Debug.LogError($"[{nameof(HorrorEnemySpawner)}] SpawnId={marker.SpawnId} が複数の {nameof(HorrorEnemyStart)} で重複しています", marker);
                    continue;
                }

                if (!database.HorrorEnemySpawnMasterTable.TryFindById(marker.SpawnId, out var spawn))
                {
                    Debug.LogError($"[{nameof(HorrorEnemySpawner)}] HorrorEnemySpawnMaster (Id={marker.SpawnId}) が見つかりません。");
                    continue;
                }

                if (!database.HorrorEnemyMasterTable.TryFindById(spawn.EnemyMasterId, out var master))
                {
                    Debug.LogError($"[{nameof(HorrorEnemySpawner)}] HorrorEnemyMaster (Id={spawn.EnemyMasterId}) が見つかりません。");
                    continue;
                }

                _entries.Add(marker.SpawnId, new SpawnEntry { Marker = marker.transform, EnemyMaster = master });
            }
        }

        /// <summary>
        /// registry が参照する敵種の prefab を事前ロードし、敵種単位のプールを構築・事前確保する。
        /// 事前確保数はその敵種を参照するマーカー数（同時貸出の上限）。
        /// </summary>
        private async UniTask PreloadPrefabsAndBuildPoolsAsync()
        {
            var markerCounts = new Dictionary<int, int>();
            foreach (var entry in _entries.Values)
            {
                markerCounts.TryGetValue(entry.EnemyMaster.Id, out var count);
                markerCounts[entry.EnemyMaster.Id] = count + 1;
            }

            foreach (var entry in _entries.Values)
            {
                var master = entry.EnemyMaster;
                if (_pools.ContainsKey(master.Id)) continue;

                var prefab = await _assetService.LoadAssetAsync<GameObject>(master.ModelAssetName);
                if (prefab == null)
                {
                    Debug.LogError($"[{nameof(HorrorEnemySpawner)}] prefab のロードに失敗しました (ModelAssetName={master.ModelAssetName})");
                    continue;
                }

                _prefabs[master.Id] = prefab;

                var pool = new ObjectPool<HorrorEnemyController>(
                    createFunc: () => CreateEnemy(prefab),
                    actionOnRelease: enemy => enemy.gameObject.SetActive(false),
                    actionOnDestroy: enemy => enemy.gameObject.SafeDestroy(),
                    collectionCheck: true,
                    defaultCapacity: markerCounts[master.Id]);
                _pools[master.Id] = pool;

                Prewarm(pool, markerCounts[master.Id]);
            }
        }

        /// <summary>ObjectPool に事前確保 API がないため、Get/Release ループで指定数を生成しておく。</summary>
        private static void Prewarm(ObjectPool<HorrorEnemyController> pool, int count)
        {
            var items = new HorrorEnemyController[count];
            for (var i = 0; i < count; i++)
                items[i] = pool.Get();
            for (var i = 0; i < count; i++)
                pool.Release(items[i]);
        }

        private HorrorEnemyController CreateEnemy(GameObject prefab)
        {
            // プレハブを一時的に非アクティブ化して Instantiate することで、
            // NavMeshAgent が NavMesh 外の位置（原点）で Awake するエラーを防ぐ（SurvivorEnemySpawner と同じ対策）。
            // 貸出時は TrySpawn がマーカー位置を設定してから SetActive(true) する。
            prefab.SetActive(false);
            var instance = Object.Instantiate(prefab, _poolParent);
            prefab.SetActive(true);

            if (!instance.TryGetComponent<HorrorEnemyController>(out var controller))
            {
                instance.SafeDestroy();
                throw new MissingComponentException($"Cannot find {nameof(HorrorEnemyController)}");
            }

            return controller;
        }
    }
}
