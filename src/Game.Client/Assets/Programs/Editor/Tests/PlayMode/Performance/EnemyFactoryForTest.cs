using System;
using System.Collections.Generic;
using Game.MVP.Survivor.Enemy;
using UnityEngine;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// テスト用の敵 GameObject 生成ヘルパー。
    /// 実 <see cref="EnemyProxyTarget"/> を使用し、<see cref="IEnemyDeathQuery"/> は
    /// <see cref="MockEnemyDeathQuery"/>（本番 SurvivorEnemyView の Dictionary lookup を模倣）を注入することで、
    /// L2-3 測定値が本番実装より軽く見えるバイアスを排除する。
    /// </summary>
    public static class EnemyFactoryForTest
    {
        public const string EnemyLayerName = "Enemy";

        public struct SpawnResult
        {
            public GameObject[] GameObjects;
            public EnemyProxyTarget[] Targets;
            public MockEnemyDeathQuery DeathQuery;
        }

        public static SpawnResult CreateEnemies(
            LocalPhysicsTestScene scene,
            int count,
            int seed,
            float halfExtent = 50f,
            float colliderRadius = 1f)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            var rng = new System.Random(seed);
            var gos = new GameObject[count];
            var targets = new EnemyProxyTarget[count];
            var deathQuery = new MockEnemyDeathQuery(count);

            int layer = LayerMask.NameToLayer(EnemyLayerName);

            for (int i = 0; i < count; i++)
            {
                var pos = new Vector3(
                    (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent,
                    0f,
                    (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtent);

                var go = scene.CreateGameObject($"Enemy_{i}", pos);
                if (layer >= 0) go.layer = layer;

                var col = go.AddComponent<SphereCollider>();
                col.radius = colliderRadius;
                col.isTrigger = false;

                var proxy = go.AddComponent<EnemyProxyTarget>();
                proxy.NetworkId = i;
                proxy.DeathQuery = deathQuery;
                deathQuery.Register(i, isDead: false);

                gos[i] = go;
                targets[i] = proxy;
            }

            return new SpawnResult
            {
                GameObjects = gos,
                Targets = targets,
                DeathQuery = deathQuery
            };
        }
    }

    /// <summary>
    /// SurvivorEnemyView の Dictionary lookup を再現する IEnemyDeathQuery テスト実装。
    /// 本番 IsProxyDead は `_proxies.TryGetValue(networkId, out var data) || data.IsDead`
    /// の Dictionary lookup + null check を毎回行うため、Mock 側も同等 cost を模倣する。
    /// </summary>
    public class MockEnemyDeathQuery : IEnemyDeathQuery
    {
        private readonly Dictionary<int, bool> _isDead;

        public MockEnemyDeathQuery(int initialCapacity)
        {
            _isDead = new Dictionary<int, bool>(initialCapacity);
        }

        public void Register(int networkId, bool isDead)
        {
            _isDead[networkId] = isDead;
        }

        public void SetDead(int networkId, bool isDead)
        {
            _isDead[networkId] = isDead;
        }

        public bool IsProxyDead(int networkId)
        {
            // 本番と同じく「未登録 or dead」を dead とみなす
            return !_isDead.TryGetValue(networkId, out bool dead) || dead;
        }
    }
}
