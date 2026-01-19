using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Enemy;
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
        [SerializeField] private string _itemPrefabBasePath = "Assets/StoreAssets/BTM_Assets/BTM_Items_Gems/Prefabs/";

        // DI
        [Inject] private IAddressableAssetService _assetService;
        [Inject] private IMasterDataService _masterDataService;
        private MemoryDatabase Database => _masterDataService.MemoryDatabase;

        // Pools (ItemId -> Pool)
        private readonly Dictionary<int, Queue<SurvivorItem>> _pools = new();
        private readonly Dictionary<int, List<SurvivorItem>> _activeItems = new();
        private readonly Dictionary<int, GameObject> _prefabCache = new();
        private readonly Dictionary<int, SurvivorItemMaster> _masterCache = new();

        // Events
        private readonly Subject<SurvivorItem> _onItemCollected = new();
        public Observable<SurvivorItem> OnItemCollected => _onItemCollected;

        // 経験値収集イベント（後方互換性）
        private readonly Subject<int> _onExperienceCollected = new();
        public Observable<int> OnExperienceCollected => _onExperienceCollected;

        public async UniTask InitializeAsync()
        {
            // マスタデータをキャッシュ
            var items = Database.SurvivorItemMasterTable.All;
            foreach (var item in items)
            {
                _masterCache[item.Id] = item;
            }

            Debug.Log($"[SurvivorItemSpawner] Initialized with {_masterCache.Count} item types");
        }

        /// <summary>
        /// 指定アイテムのプールを事前初期化
        /// </summary>
        public async UniTask PreloadItemAsync(int itemId)
        {
            if (_pools.ContainsKey(itemId)) return;
            if (!_masterCache.TryGetValue(itemId, out var master)) return;

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

        /// <summary>
        /// 経験値アイテム（デフォルト小）を事前読み込み
        /// </summary>
        public async UniTask PreloadExperienceOrbsAsync()
        {
            // 経験値アイテム（ItemType == 1）を全て読み込み
            foreach (var kvp in _masterCache)
            {
                if (kvp.Value.ItemType == (int)SurvivorItemType.Experience)
                {
                    await PreloadItemAsync(kvp.Key);
                }
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
                master.Scale
            );

            item.OnCollected += OnItemCollectedHandler;

            return item;
        }

        /// <summary>
        /// アイテムをスポーン
        /// </summary>
        public void SpawnItem(int itemId, Vector3 position)
        {
            if (!_masterCache.TryGetValue(itemId, out var master))
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
        /// 経験値をスポーン（後方互換性）
        /// 経験値量に応じて適切なアイテムを選択
        /// </summary>
        public void SpawnExperience(Vector3 position, int experienceValue)
        {
            // 経験値量に最も近いアイテムを選択
            int bestItemId = 1; // デフォルト
            int bestDiff = int.MaxValue;

            foreach (var kvp in _masterCache)
            {
                if (kvp.Value.ItemType == (int)SurvivorItemType.Experience)
                {
                    int diff = Mathf.Abs(kvp.Value.EffectValue - experienceValue);
                    if (diff < bestDiff)
                    {
                        bestDiff = diff;
                        bestItemId = kvp.Key;
                    }
                }
            }

            SpawnItem(bestItemId, position);
        }

        /// <summary>
        /// 敵の死亡イベントに接続
        /// </summary>
        public void ConnectToEnemySpawner(SurvivorEnemySpawner enemySpawner)
        {
            enemySpawner.OnEnemyKilled
                .Subscribe(enemy => { SpawnExperience(enemy.transform.position, enemy.ExperienceValue); })
                .AddTo(this);
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

            // 経験値の場合は後方互換イベントも発火
            if (item.ItemType == SurvivorItemType.Experience)
            {
                _onExperienceCollected.OnNext(item.EffectValue);
            }

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
            _onExperienceCollected.Dispose();
        }
    }
}
