using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Item;
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
    /// </summary>
    public class SurvivorItemView : MonoBehaviour
    {
        private const float FloatAmplitude = 0.2f;
        private const float FloatSpeed = 2f;

        private readonly Dictionary<int, ItemProxyData> _proxies = new();
        private readonly Dictionary<int, GameObject> _prefabs = new();
        private readonly Dictionary<int, float> _scales = new();
        private IDisposable _spawnSub;
        private IDisposable _despawnSub;
        private IAddressableAssetService _assetService;

        /// <summary>クライアント側でアイテムプロキシが収集された時に発火（itemId）</summary>
        public event Action<int> OnProxyItemCollected;

        private class ItemProxyData
        {
            public GameObject GameObject;
            public ItemProxyCollectible Collectible;
            public Vector3 InitialPosition;
            public float FloatTimer;
            public float Scale;
        }

        private SurvivorFusionGameState _gameState;

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

            _spawnSub = spawnSub.Subscribe(s => OnSpawned(s.ItemId, s.PosX, s.PosY, s.PosZ));
            _despawnSub = despawnSub.Subscribe(s => OnDespawned(s.ItemId));
            Debug.Log($"[SurvivorItemView] Initialized: prefabs={_prefabs.Count}");
        }

        private void OnSpawned(int itemId, float posX, float posY, float posZ)
        {
            var position = new Vector3(posX, posY, posZ);

            if (_proxies.TryGetValue(itemId, out var existing))
            {
                existing.GameObject.transform.position = position;
                existing.InitialPosition = position;
                existing.Collectible.Reset();
                return;
            }

            float scale = 1f;
            GameObject instance;

            if (_prefabs.TryGetValue(itemId, out var prefab) && prefab != null)
            {
                instance = Instantiate(prefab, transform);

                // サーバー専用 SurvivorItem を除去（ICollectible プロキシで置換する）
                var itemComponent = instance.GetComponent<SurvivorItem>();
                if (itemComponent != null) Destroy(itemComponent);

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
                var col = instance.GetComponent<Collider>();
                if (col != null) col.isTrigger = true;
                scale = 0.5f;
                Debug.LogWarning($"[SurvivorItemView] Prefab not found for item {itemId}, using fallback");
            }

            instance.name = $"ItemProxy_{itemId}";
            instance.transform.position = position;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.SetParent(transform);

            // Item レイヤー設定（PlayerController の OverlapSphere 検出用）
            SetLayerRecursively(instance, LayerConstants.Item);

            // ICollectible プロキシ追加（PlayerController の吸引・収集ロジックで動作）
            var collectible = instance.AddComponent<ItemProxyCollectible>();
            collectible.Initialize(scale, itemId, _gameState);
            collectible.OnCollected += OnProxyItemCollectedHandler;

            _proxies[itemId] = new ItemProxyData
            {
                GameObject = instance,
                Collectible = collectible,
                InitialPosition = position,
                FloatTimer = 0f,
                Scale = scale
            };
        }

        private void Update()
        {
            if (_gameState != null && _gameState.IsPaused) return;

            float dt = Time.deltaTime;

            foreach (var kvp in _proxies)
            {
                var data = kvp.Value;
                if (data.GameObject == null || data.Collectible.IsAttracting) continue;

                // 浮遊アニメーション（SurvivorItem.UpdateFloatAnimation と同等）
                data.FloatTimer += dt * FloatSpeed;
                float yOffset = Mathf.Sin(data.FloatTimer) * FloatAmplitude * data.Scale;
                data.GameObject.transform.position = data.InitialPosition + Vector3.up * yOffset;
            }
        }

        private void OnDespawned(int itemId)
        {
            if (_proxies.TryGetValue(itemId, out var data))
            {
                if (data.GameObject != null) Destroy(data.GameObject);
                _proxies.Remove(itemId);
            }
        }

        private void OnProxyItemCollectedHandler(int itemId)
        {
            OnProxyItemCollected?.Invoke(itemId);

            // クライアント側で即座にプロキシを削除（サーバーの Despawn RPC を待たない）
            if (_proxies.TryGetValue(itemId, out var data))
            {
                if (data.GameObject != null) Destroy(data.GameObject);
                _proxies.Remove(itemId);
            }
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
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

    /// <summary>
    /// クライアントプロキシ用 ICollectible 実装。
    /// PlayerController の既存吸引ロジック（OverlapSphere → StartAttraction）で動作する。
    /// Collect は no-op（実際の回収はサーバーが管理、Despawn ClientRpc で削除）。
    /// </summary>
    public class ItemProxyCollectible : MonoBehaviour, ICollectible
    {
        private Transform _attractTarget;
        private float _attractSpeed;
        private float _floatAmplitude;
        private Vector3 _initialPosition;

        private SurvivorFusionGameState _gameState;

        public int ItemId { get; private set; }
        public bool IsCollected { get; private set; }
        public bool IsAttracting => _attractTarget != null;

        /// <summary>収集時コールバック（SurvivorItemView が RPC 送信用に設定）</summary>
        public event System.Action<int> OnCollected;

        public void Initialize(float scale, int itemId, SurvivorFusionGameState gameState)
        {
            _floatAmplitude = 0.2f * scale;
            ItemId = itemId;
            _gameState = gameState;
        }

        public void StartAttraction(Transform target, float speed)
        {
            if (_attractTarget != null) return;
            _attractTarget = target;
            _attractSpeed = speed;
            _initialPosition = transform.position;
        }

        public void Collect()
        {
            if (IsCollected) return;
            IsCollected = true;
            OnCollected?.Invoke(ItemId);
        }

        public void Reset()
        {
            _attractTarget = null;
            _attractSpeed = 0f;
            IsCollected = false;
        }

        private void Update()
        {
            if (_attractTarget == null) return;
            if (_gameState != null && _gameState.IsPaused) return;

            // 吸引移動のみ（収集判定は SurvivorPlayerController が担当）
            var diff = _attractTarget.position - transform.position;
            transform.position += diff.normalized * _attractSpeed * Time.deltaTime;
        }
    }
}
