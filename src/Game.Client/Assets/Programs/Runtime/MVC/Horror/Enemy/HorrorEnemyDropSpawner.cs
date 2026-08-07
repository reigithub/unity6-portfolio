using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Interaction;
using Game.Horror.Signals;
using Game.Shared.Extensions;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Horror.Enemy
{
    /// <summary>
    /// エネミー撃破時のドロップ品スポナー。<see cref="HorrorSignals.Enemy.Died"/> を購読し、
    /// 敵種の抽選グループ（HorrorEnemyMaster.DropGroupId → HorrorEnemyDropMaster）で累積抽選して
    /// 死亡位置へドロップ品（<see cref="HorrorDropItemInteractable"/>）を生成する。
    /// 個体はアイテム種を問わない共通プレハブ（<see cref="DropItemAddress"/>）から作り、
    /// 見た目と当たり判定はアイテムの ModelAssetName が指すモデルアセットを装着して供給する。
    /// アイテム種（ItemId）単位のオブジェクトプールで個体を使い回し、拾得通知でプールへ回収する。
    /// 未回収のドロップ品は永続化しない（シーン破棄とともに消える）。
    /// </summary>
    public class HorrorEnemyDropSpawner
    {
        // アイテム種を問わないドロップ品の共通プレハブ。モデルは ModelHolder 配下へ実行時に装着する
        private const string DropItemAddress = "HorrorDropItem";

        // 抽選の分母（万分率。10000 = 100%）
        private const int RateDenominator = 10000;

        // 同時に存在するドロップ品は少数想定の事前確保数（不足時は ObjectPool が自動成長する）
        private const int PrewarmCount = 2;

        // 死亡位置が地面に接している場合のめり込み防止オフセット
        private const float SpawnHeightOffset = 0.05f;

        private readonly IAddressableAssetService _assetService;
        private readonly IScriptableDatabaseService _dbService;
        private readonly IMessagePipeService _messagePipeService;

        // ItemId → プール / ロード済みモデル prefab / 解決済みアイテムマスタ
        private readonly Dictionary<int, ObjectPool<HorrorDropItemInteractable>> _pools = new();
        private readonly Dictionary<int, GameObject> _modelPrefabs = new();
        private readonly Dictionary<int, HorrorItemMaster> _items = new();

        // 貸出中の個体 → ItemId（返却先プールの逆引き）
        private readonly Dictionary<HorrorDropItemInteractable, int> _activeDrops = new();

        // 全アイテム種が共有する構造プレハブ（ロード済みハンドル。Dispose で Release する）
        private GameObject _dropItemPrefab;

        private Transform _poolParent;
        private IDisposable _subscription;

        public HorrorEnemyDropSpawner(
            IAddressableAssetService assetService,
            IScriptableDatabaseService dbService,
            IMessagePipeService messagePipeService)
        {
            _assetService = assetService;
            _dbService = dbService;
            _messagePipeService = messagePipeService;
        }

        /// <summary>
        /// 共通プレハブとドロップテーブルが参照する全アイテムのモデルを事前ロードしてプールを構築し、
        /// 撃破シグナルの購読を開始する。シーン起動時に1回呼ぶ。
        /// </summary>
        public async UniTask InitializeAsync()
        {
            _poolParent = new GameObject("HorrorDropItemPool").transform;

            _dropItemPrefab = await _assetService.LoadAssetAsync<GameObject>(DropItemAddress);
            if (_dropItemPrefab == null)
            {
                // 共通プレハブが無いと全アイテム種のドロップが成立しないため、以降を組み立てない
                Debug.LogError($"[{nameof(HorrorEnemyDropSpawner)}] 共通プレハブのロードに失敗しました (Address={DropItemAddress})");
                return;
            }

            var database = _dbService.Database;
            foreach (var row in database.HorrorEnemyDropMasterTable.All)
            {
                if (_pools.ContainsKey(row.ItemId)) continue;

                // 参照先アイテムの存在と ModelAssetName 非空はマスターデータ検証（編集時/CI）が担保する
                if (!database.HorrorItemMasterTable.TryFindById(row.ItemId, out var item))
                {
                    Debug.LogError($"[{nameof(HorrorEnemyDropSpawner)}] HorrorItemMaster (Id={row.ItemId}) が見つかりません");
                    continue;
                }

                var modelPrefab = await _assetService.LoadAssetAsync<GameObject>(item.ModelAssetName);
                if (modelPrefab == null)
                {
                    Debug.LogError($"[{nameof(HorrorEnemyDropSpawner)}] モデルのロードに失敗しました (ModelAssetName={item.ModelAssetName})");
                    continue;
                }

                _modelPrefabs[row.ItemId] = modelPrefab;
                _items[row.ItemId] = item;

                var pool = new ObjectPool<HorrorDropItemInteractable>(
                    createFunc: () => CreateDrop(modelPrefab),
                    actionOnRelease: drop => drop.gameObject.SetActive(false),
                    actionOnDestroy: drop => drop.gameObject.SafeDestroy(),
                    collectionCheck: true,
                    defaultCapacity: PrewarmCount);
                _pools[row.ItemId] = pool;

                Prewarm(pool, PrewarmCount);
            }

            _subscription = _messagePipeService.Subscribe<HorrorSignals.Enemy.Died>(OnEnemyDied);
        }

        /// <summary>
        /// 貸出中の個体をプールへ返却する（拾得完了通知の実体）。
        /// 貸出中でない返却は無音で握りつぶさず LogError で顕在化する。
        /// </summary>
        internal void ReturnToPool(HorrorDropItemInteractable drop)
        {
            if (!_activeDrops.Remove(drop, out var itemId))
            {
                Debug.LogError($"[{nameof(HorrorEnemyDropSpawner)}] 貸出中でないドロップ品が返却されました（二重返却の疑い）", drop);
                return;
            }

            _pools[itemId].Release(drop);
        }

        /// <summary>
        /// 購読解除 → 貸出中の全返却 → プール・ロード済み prefab・プール親を破棄する。
        /// ステージシーンのアンロード前（HorrorStageScene.Terminate）に呼ぶ。
        /// </summary>
        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;

            // 貸出中を全返却してからプールを破棄する（Clear の actionOnDestroy が待機個体を破棄する）
            foreach (var (drop, itemId) in _activeDrops)
                _pools[itemId].Release(drop);
            _activeDrops.Clear();

            foreach (var pool in _pools.Values)
                pool.Clear();
            _pools.Clear();

            foreach (var modelPrefab in _modelPrefabs.Values)
                _assetService.Release(modelPrefab);
            _modelPrefabs.Clear();
            _items.Clear();

            if (_dropItemPrefab != null)
            {
                _assetService.Release(_dropItemPrefab);
                _dropItemPrefab = null;
            }

            if (_poolParent != null)
            {
                _poolParent.gameObject.SafeDestroy();
                _poolParent = null;
            }
        }

        /// <summary>
        /// 累積抽選の純関数。rows の DropRate（万分率）を先頭から累積し、roll が最初に収まった行の index を返す。
        /// 合計が 10000 未満のときの余り区間は「ドロップなし」（-1）。
        /// </summary>
        /// <param name="rows">同一 DropGroupId の抽選行</param>
        /// <param name="roll">抽選値（0 〜 9999）</param>
        internal static int RollDropIndex(IReadOnlyList<HorrorEnemyDropMaster> rows, int roll)
        {
            var cumulative = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                cumulative += rows[i].DropRate;
                if (roll < cumulative) return i;
            }

            return -1;
        }

        private void OnEnemyDied(HorrorSignals.Enemy.Died evt)
        {
            var database = _dbService.Database;

            // 不正 SpawnId は撃破記録側（HorrorEnemyService）が報告するため、ここでは二重報告しない
            if (!database.HorrorEnemySpawnMasterTable.TryFindById(evt.SpawnId, out var spawn)) return;
            if (!database.HorrorEnemyMasterTable.TryFindById(spawn.EnemyMasterId, out var master)) return;

            if (master.DropGroupId == 0) return; // ドロップなしの敵種

            var rows = database.HorrorEnemyDropMasterTable.FindByDropGroupId(master.DropGroupId);
            if (rows.IsEmpty)
            {
                // 編集時検証（HorrorEnemyMasterDropGroupValidator）をすり抜けたデータ齟齬の顕在化
                Debug.LogError($"[{nameof(HorrorEnemyDropSpawner)}] DropGroupId={master.DropGroupId} の {nameof(HorrorEnemyDropMaster)} 行がありません");
                return;
            }

            var index = RollDropIndex(rows, UnityEngine.Random.Range(0, RateDenominator));
            if (index < 0) return; // ドロップなし（正当な抽選結果）

            Spawn(rows[index], evt.Position);
        }

        private void Spawn(HorrorEnemyDropMaster row, Vector3 position)
        {
            if (!_pools.TryGetValue(row.ItemId, out var pool))
            {
                Debug.LogError($"[{nameof(HorrorEnemyDropSpawner)}] ItemId={row.ItemId} のプールがありません（prefab ロード失敗の可能性）");
                return;
            }

            var drop = pool.Get();
            drop.transform.position = position + Vector3.up * SpawnHeightOffset;

            // SetActive(true) の後に Setup を呼ぶ（コンポーネントの OnEnable を先行させる。HorrorEnemySpawner と同じ順序）
            drop.gameObject.SetActive(true);
            drop.Setup(_items[row.ItemId], row.Count, onCollected: ReturnToPool);

            _activeDrops.Add(drop, row.ItemId);
        }

        private HorrorDropItemInteractable CreateDrop(GameObject modelPrefab)
        {
            // 共通プレハブを一時的に非アクティブ化して Instantiate することで、
            // Start()（GameServiceManager からのサービス解決）を初回貸出時まで遅延させる（HorrorEnemySpawner と同じ手法）
            _dropItemPrefab.SetActive(false);
            var instance = UnityEngine.Object.Instantiate(_dropItemPrefab, _poolParent);
            _dropItemPrefab.SetActive(true);

            if (!instance.TryGetComponent<HorrorDropItemInteractable>(out var drop))
            {
                instance.SafeDestroy();
                throw new MissingComponentException($"Cannot find {nameof(HorrorDropItemInteractable)}");
            }

            // Awake 前（個体が非アクティブなうち）に装着し、コライダー・Renderer の Awake 時収集に含める
            drop.AttachModel(modelPrefab);

            return drop;
        }

        /// <summary>ObjectPool に事前確保 API がないため、Get/Release ループで指定数を生成しておく。</summary>
        private static void Prewarm(ObjectPool<HorrorDropItemInteractable> pool, int count)
        {
            var items = new HorrorDropItemInteractable[count];
            for (var i = 0; i < count; i++)
                items[i] = pool.Get();
            for (var i = 0; i < count; i++)
                pool.Release(items[i]);
        }
    }
}
