using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// クライアントモード時、ClientRpc からアイテムオブジェクトを管理。
    /// サーバーからの ItemId で Addressable プレハブをロードし、正式モデルで表示する。
    /// プロキシは ICollectible を実装し、PlayerController の既存吸引ロジックで動作する。
    /// 浮遊アニメーションは ItemProxyCollectible が自己管理する。
    /// </summary>
    public class SurvivorItemView : MonoBehaviour
    {
        private readonly Dictionary<int, ItemProxyData> _proxies = new();
        private readonly Dictionary<int, GameObject> _prefabs = new();
        private readonly Dictionary<int, float> _scales = new();
        private IDisposable _spawnSub;
        private IDisposable _despawnSub;
        private IAddressableAssetService _assetService;
        private SurvivorFusionGameState _gameState;

        /// <summary>クライアント側でアイテムプロキシが収集された時に発火（itemId）</summary>
        public event Action<int> OnProxyItemCollected;

        /// <summary>
        /// 非同期初期化。全アイテムプレハブをプリロードし、スポーン・デスポーンシグナルを購読する。
        /// </summary>
        public async UniTask InitializeAsync(
            ISubscriber<SurvivorSignals.Item.Spawned> spawnSub,
            ISubscriber<SurvivorSignals.Item.Despawned> despawnSub,
            IMasterDataService masterDataService,
            IAddressableAssetService assetService,
            SurvivorFusionGameState gameState)
        {
            _assetService = assetService;
            _gameState = gameState;

            // 全アイテムプレハブをプリロード
            var allItems = masterDataService.MemoryDatabase.SurvivorItemMasterTable.All;
            foreach (var item in allItems)
            {
                if (!_prefabs.ContainsKey(item.Id))
                {
                    try
                    {
                        var prefab = await assetService.LoadAssetAsync<GameObject>(item.AssetName);
                        _prefabs[item.Id] = prefab;
                        _scales[item.Id] = item.Scale.ToScale();
                    }
                    catch
                    {
                        Debug.LogWarning($"[SurvivorItemView] Failed to load prefab: {item.AssetName}");
                    }
                }
            }

            _spawnSub = spawnSub.Subscribe(s => OnSpawned(s.NetworkId, s.ItemId, s.PosX, s.PosY, s.PosZ));
            _despawnSub = despawnSub.Subscribe(s => OnDespawned(s.NetworkId));
            Debug.Log($"[SurvivorItemView] Initialized: prefabs={_prefabs.Count}");
        }

        private void OnSpawned(int networkId, int itemId, float posX, float posY, float posZ)
        {
            var position = new Vector3(posX, posY, posZ);

            // 既存プロキシがある場合は破棄（networkId 再利用時の安全策）
            if (_proxies.TryGetValue(networkId, out var existing))
            {
                if (existing.GameObject != null) Destroy(existing.GameObject);
                _proxies.Remove(networkId);
            }

            float scale = 1f;
            GameObject instance;

            if (_prefabs.TryGetValue(itemId, out var prefab) && prefab != null)
            {
                instance = Instantiate(prefab, transform);

                // サーバー専用 SurvivorItem を除去（ICollectible プロキシで置換する）
                if (instance.TryGetComponent<SurvivorItem>(out var itemComponent))
                    itemComponent.StripForProxy();

                // Collider をトリガーに変更（PlayerController の OverlapSphere/OnTriggerEnter で検出）
                foreach (var col in instance.GetComponentsInChildren<Collider>())
                {
                    col.isTrigger = true;
                }

                if (_scales.TryGetValue(itemId, out var s))
                    scale = s;
            }
            else
            {
                // フォールバック: プレハブ未ロード時
                instance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                if (instance.TryGetComponent<Collider>(out var col))
                    col.isTrigger = true;
                scale = 0.5f;
                Debug.LogWarning($"[SurvivorItemView] Prefab not found for item {itemId}, using fallback");
            }

            instance.name = $"ItemProxy_{networkId}";
            instance.transform.position = position;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetParent(transform);

            // Item レイヤー設定（PlayerController の OverlapSphere 検出用）
            instance.SetLayerRecursively(LayerConstants.Item);

            // ICollectible プロキシ追加（PlayerController の吸引・収集ロジックで動作）
            var collectible = instance.AddComponent<ItemProxyCollectible>();
            collectible.Initialize(scale, networkId, _gameState);
            collectible.OnCollected += OnProxyItemCollectedHandler;

            _proxies[networkId] = new ItemProxyData
            {
                GameObject = instance,
                Collectible = collectible,
                Scale = scale
            };
        }

        private void OnDespawned(int networkId)
        {
            if (_proxies.TryGetValue(networkId, out var data))
            {
                if (data.GameObject != null) Destroy(data.GameObject);
                _proxies.Remove(networkId);
            }
        }

        private void OnProxyItemCollectedHandler(int networkId)
        {
            OnProxyItemCollected?.Invoke(networkId);

            // クライアント側で即座にプロキシを削除（サーバーの Despawn RPC を待たない）
            if (_proxies.TryGetValue(networkId, out var data))
            {
                if (data.GameObject != null) Destroy(data.GameObject);
                _proxies.Remove(networkId);
            }
        }

        // 診断: 5 秒毎にプロキシ数サマリー
        private const float DiagSummaryInterval = 5f;
        private float _diagLastSummaryTime;

        private void Update()
        {
            var now = Time.unscaledTime;
            if (now - _diagLastSummaryTime >= DiagSummaryInterval)
            {
                _diagLastSummaryTime = now;
                Debug.Log($"[SurvivorItemView DIAG] proxies={_proxies.Count}, prefabsLoaded={_prefabs.Count}");
            }
        }

        private void OnDestroy()
        {
            _spawnSub?.Dispose();
            _despawnSub?.Dispose();

            foreach (var data in _proxies.Values)
            {
                if (data.GameObject != null) Destroy(data.GameObject);
            }
            _proxies.Clear();

            // プレハブリリース
            foreach (var prefab in _prefabs.Values)
            {
                _assetService?.ReleaseAsset(prefab);
            }
            _prefabs.Clear();
        }
    }
}
