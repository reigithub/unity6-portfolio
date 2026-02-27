using System;
using System.Collections.Generic;
using Game.Shared.Survivor;
using MessagePipe;
using UnityEngine;

namespace Game.MVP.Survivor.Item
{
    /// <summary>
    /// クライアントモード時、ClientRpc からプロキシアイテムオブジェクトを管理。
    /// Phase 5 は Sphere プロキシ。Phase 7 で正式モデルに置換。
    /// </summary>
    public class SurvivorItemView : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> _proxies = new();
        private IDisposable _spawnSub;
        private IDisposable _despawnSub;

        public void Initialize(
            ISubscriber<SurvivorSignals.Item.Spawned> spawnSub,
            ISubscriber<SurvivorSignals.Item.Despawned> despawnSub)
        {
            _spawnSub = spawnSub.Subscribe(s => OnSpawned(s.ItemId, s.PosX, s.PosZ));
            _despawnSub = despawnSub.Subscribe(s => OnDespawned(s.ItemId));
        }

        private void OnSpawned(int itemId, float posX, float posZ)
        {
            if (_proxies.TryGetValue(itemId, out var existing))
            {
                existing.transform.position = new Vector3(posX, 0.5f, posZ);
                return;
            }
            var proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            proxy.name = $"ItemProxy_{itemId}";
            proxy.transform.position = new Vector3(posX, 0.5f, posZ);
            proxy.transform.localScale = Vector3.one * 0.5f;
            proxy.transform.SetParent(transform);
            var col = proxy.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _proxies[itemId] = proxy;
        }

        private void OnDespawned(int itemId)
        {
            if (_proxies.TryGetValue(itemId, out var p))
            {
                Destroy(p);
                _proxies.Remove(itemId);
            }
        }

        private void OnDestroy()
        {
            _spawnSub?.Dispose();
            _despawnSub?.Dispose();
            foreach (var p in _proxies.Values)
            {
                if (p != null) Destroy(p);
            }
            _proxies.Clear();
        }
    }
}
