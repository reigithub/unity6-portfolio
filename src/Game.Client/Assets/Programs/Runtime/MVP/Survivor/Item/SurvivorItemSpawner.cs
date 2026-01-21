using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Enemy;
using Game.Shared.Extensions;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// Survivorアイテムスポーナー
    /// 敵が倒された時やドロップ時にアイテムを生成
    /// </summary>
    public class SurvivorItemSpawner : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private int _poolSizePerItem = 50;

        // DI
        [Inject] private IAddressableAssetService _assetService;
        [Inject] private IMasterDataService _masterDataService;
        private MemoryDatabase MemoryDatabase => _masterDataService.MemoryDatabase;

        // Pools (ItemId -> Pool)
        private readonly Dictionary<int, Queue<SurvivorItem>> _pools = new();
        private readonly Dictionary<int, List<SurvivorItem>> _activeItems = new();
        private readonly Dictionary<int, GameObject> _prefabCache = new();
        private readonly Dictionary<int, SurvivorItemMaster> _masterCache = new();
        // ドロップグループキャッシュ (GroupId -> List<SurvivorItemDropMaster>)
        private readonly Dictionary<int, List<SurvivorItemDropMaster>> _dropGroupCache = new();

        // Events
        private readonly Subject<SurvivorItem> _onItemCollected = new();
        public Observable<SurvivorItem> OnItemCollected => _onItemCollected;

        public UniTask InitializeAsync()
        {
            Debug.Log("[SurvivorItemSpawner] Initialized (lazy loading enabled)");
            return UniTask.CompletedTask;
        }

        /// <summary>
        /// アイテムマスターを取得（遅延読み込み＆キャッシュ）
        /// </summary>
        private SurvivorItemMaster GetOrAddItemMaster(int itemId)
        {
            if (_masterCache.TryGetValue(itemId, out var cached))
                return cached;

            if (MemoryDatabase.SurvivorItemMasterTable.TryFindById(itemId, out var master))
            {
                _masterCache[itemId] = master;
                return master;
            }

            return null;
        }

        /// <summary>
        /// ドロップグループを取得（遅延読み込み＆キャッシュ）
        /// </summary>
        private List<SurvivorItemDropMaster> GetOrAddDropGroup(int groupId)
        {
            if (_dropGroupCache.TryGetValue(groupId, out var cached))
                return cached;

            var dropList = MemoryDatabase.SurvivorItemDropMasterTable.FindByGroupId(groupId).ToList();
            if (dropList.Count > 0)
            {
                _dropGroupCache[groupId] = dropList;
                return dropList;
            }

            return null;
        }

        /// <summary>
        /// 指定アイテムのプールを事前初期化
        /// </summary>
        public async UniTask PreloadItemAsync(int itemId)
        {
            if (_pools.ContainsKey(itemId)) return;

            var master = GetOrAddItemMaster(itemId);
            if (master == null) return;

            var prefab = await LoadPrefabAsync(master.AssetName);
            if (prefab == null) return;

            _prefabCache[itemId] = prefab;
            _pools[itemId] = new Queue<SurvivorItem>();
            _activeItems[itemId] = new List<SurvivorItem>();

            for (int i = 0; i < _poolSizePerItem; i++)
            {
                var item = CreateItem(itemId, prefab, master);
                item.gameObject.SetActive(false);
                _pools[itemId].Enqueue(item);
            }
        }

        private async UniTask<GameObject> LoadPrefabAsync(string assetName)
        {
            try
            {
                // Addressables経由で読み込み
                return await _assetService.LoadAssetAsync<GameObject>(assetName);
            }
            catch
            {
                Debug.LogWarning($"[SurvivorItemSpawner] Failed to load prefab: {assetName}");
                return null;
            }
        }

        private SurvivorItem CreateItem(int itemId, GameObject prefab, SurvivorItemMaster master)
        {
            var instance = Instantiate(prefab, transform);
            var item = instance.GetComponent<SurvivorItem>();

            if (item == null)
            {
                item = instance.AddComponent<SurvivorItem>();
            }

            // マスタデータから初期化
            item.Initialize(
                master.Id,
                (SurvivorItemType)master.ItemType,
                master.EffectValue,
                master.EffectRange,
                master.EffectDuration,
                master.Rarity,
                master.Scale.ToScale()
            );

            item.OnCollected += OnItemCollectedHandler;

            return item;
        }

        /// <summary>
        /// アイテムをスポーン
        /// </summary>
        public void SpawnItem(int itemId, Vector3 position)
        {
            var master = GetOrAddItemMaster(itemId);
            if (master == null)
            {
                Debug.LogWarning($"[SurvivorItemSpawner] Unknown item ID: {itemId}");
                return;
            }

            // プールがなければ動的に作成
            if (!_pools.ContainsKey(itemId))
            {
                PreloadItemAsync(itemId).Forget();
                return;
            }

            var item = GetFromPool(itemId);
            if (item == null)
            {
                if (_prefabCache.TryGetValue(itemId, out var prefab))
                {
                    item = CreateItem(itemId, prefab, master);
                }
                else
                {
                    return;
                }
            }

            item.Reset();
            item.SetPosition(position);
            item.gameObject.SetActive(true);

            _activeItems[itemId].Add(item);
        }

        /// <summary>
        /// 敵の死亡イベントに接続
        /// </summary>
        public void ConnectToEnemySpawner(SurvivorEnemySpawner enemySpawner)
        {
            enemySpawner.OnEnemyKilled
                .Subscribe(enemy => OnEnemyKilledHandler(enemy))
                .AddTo(this);
        }

        /// <summary>
        /// 敵死亡時のドロップ処理
        /// </summary>
        private void OnEnemyKilledHandler(SurvivorEnemyController enemy)
        {
            var position = enemy.transform.position;

            // アイテムドロップ抽選
            TrySpawnFromDropGroup(enemy.ItemDropGroupId, position);

            // 経験値ドロップ抽選
            TrySpawnFromDropGroup(enemy.ExpDropGroupId, position);
        }

        /// <summary>
        /// ドロップグループからアイテムをスポーン（抽選失敗の場合は何もしない）
        /// </summary>
        private void TrySpawnFromDropGroup(int dropGroupId, Vector3 position)
        {
            if (dropGroupId <= 0) return;

            var dropList = GetOrAddDropGroup(dropGroupId);
            if (dropList == null) return;

            var itemId = RollDropFromGroup(dropList);
            if (itemId > 0)
            {
                SpawnItem(itemId, position);
            }
        }

        /// <summary>
        /// ドロップグループからアイテムをロール
        /// 確率が10000（100%）に満たない場合は不足分がドロップ失敗となる
        /// </summary>
        private int RollDropFromGroup(List<SurvivorItemDropMaster> dropList)
        {
            if (dropList == null || dropList.Count == 0) return 0;

            // 0〜9999でロール（10000 = 100%）
            var roll = Random.Range(0, 10000);
            var cumulative = 0;

            foreach (var drop in dropList)
            {
                cumulative += drop.DropRate;
                if (roll < cumulative)
                {
                    return drop.ItemId;
                }
            }

            // 確率不足分はドロップ失敗
            return 0;
        }

        /// <summary>
        /// 範囲内の全アイテムを吸引開始（マグネット効果）
        /// </summary>
        public void AttractAllItemsInRange(Vector3 center, float range)
        {
            foreach (var kvp in _activeItems)
            {
                foreach (var item in kvp.Value)
                {
                    if (item.gameObject.activeSelf)
                    {
                        float distance = Vector3.Distance(item.transform.position, center);
                        if (distance <= range)
                        {
                            item.StartAttraction();
                        }
                    }
                }
            }
        }

        private SurvivorItem GetFromPool(int itemId)
        {
            if (!_pools.TryGetValue(itemId, out var pool)) return null;

            while (pool.Count > 0)
            {
                var item = pool.Dequeue();
                if (item != null)
                {
                    return item;
                }
            }

            return null;
        }

        private void ReturnToPool(SurvivorItem item)
        {
            int itemId = item.ItemId;
            if (_activeItems.TryGetValue(itemId, out var activeList))
            {
                activeList.Remove(item);
            }
            if (_pools.TryGetValue(itemId, out var pool))
            {
                pool.Enqueue(item);
            }
        }

        private void OnItemCollectedHandler(SurvivorItem item)
        {
            _onItemCollected.OnNext(item);
            ReturnToPool(item);
        }

        /// <summary>
        /// 全てのアイテムをクリア
        /// </summary>
        public void ClearAllItems()
        {
            foreach (var kvp in _activeItems)
            {
                foreach (var item in kvp.Value.ToArray())
                {
                    item.gameObject.SetActive(false);
                    if (_pools.TryGetValue(kvp.Key, out var pool))
                    {
                        pool.Enqueue(item);
                    }
                }
                kvp.Value.Clear();
            }
        }

        private void OnDestroy()
        {
            _onItemCollected.Dispose();
        }
    }
}
