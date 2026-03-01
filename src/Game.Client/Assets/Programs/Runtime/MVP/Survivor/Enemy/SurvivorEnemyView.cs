using System;
using System.Collections.Generic;
using Game.Library.Shared.Dto;
using Game.Shared.Network.Survivor;
using Game.Shared.Survivor;
using MessagePipe;
using UnityEngine;

namespace Game.MVP.Survivor.Enemy
{
    /// <summary>
    /// クライアントモード時、バッチ ClientRpc からプロキシ敵オブジェクトを管理。
    /// Phase 5 は Capsule プロキシ。Phase 7 で正式モデルに置換。
    /// </summary>
    public class SurvivorEnemyView : MonoBehaviour
    {
        private readonly Dictionary<int, GameObject> _proxies = new();
        private IDisposable _subscription;

        public void Initialize(ISubscriber<SurvivorSignals.Enemy.BatchUpdated> subscriber)
        {
            _subscription = subscriber.Subscribe(signal => OnReceived(signal.Enemies));
        }

        private void OnReceived(SurvivorNetworkEnemyStateSnapshot[] enemies)
        {
            foreach (var e in enemies)
            {
                switch (e.SyncType)
                {
                    case EnemySyncType.Spawn:
                        SpawnProxy(e);
                        break;
                    case EnemySyncType.PositionUpdate:
                        UpdateProxy(e);
                        break;
                    case EnemySyncType.Death:
                        DespawnProxy(e.NetworkId);
                        break;
                }
            }
        }

        private void SpawnProxy(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (_proxies.ContainsKey(e.NetworkId)) return;
            var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            proxy.name = $"EnemyProxy_{e.NetworkId}";
            proxy.transform.position = new Vector3(e.PositionX, 0, e.PositionZ);
            proxy.transform.SetParent(transform);
            var col = proxy.GetComponent<Collider>();
            if (col != null) Destroy(col);
            _proxies[e.NetworkId] = proxy;
        }

        private void UpdateProxy(SurvivorNetworkEnemyStateSnapshot e)
        {
            if (_proxies.TryGetValue(e.NetworkId, out var p))
                p.transform.position = new Vector3(e.PositionX, 0, e.PositionZ);
        }

        private void DespawnProxy(int id)
        {
            if (_proxies.TryGetValue(id, out var p))
            {
                Destroy(p);
                _proxies.Remove(id);
            }
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
            foreach (var p in _proxies.Values)
            {
                if (p != null) Destroy(p);
            }
            _proxies.Clear();
        }
    }
}
