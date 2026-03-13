using Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// Fusion 2 敵バッチ同期 NetworkBehaviour シングルトン。
    /// 500+ 体の敵状態を NetworkArray で一括同期し、
    /// ChangeDetector 経由で MessagePipe に配信する。
    /// </summary>
    public class SurvivorFusionEnemyBatchSync : NetworkBehaviour
    {
        public static SurvivorFusionEnemyBatchSync Instance { get; private set; }

        [Inject] private IPublisher<SurvivorSignals.Enemy.BatchUpdated> _enemyBatchPub;

        [Networked] public int ActiveCount { get; set; }
        [Networked, Capacity(512)]
        public NetworkArray<EnemyStateData> EnemyStates => default;

        private ChangeDetector _changeDetector;

        private bool _hasLoggedFirstWrite;
        private bool _hasLoggedFirstPublish;

        public override void Spawned()
        {
            // StateAuthority インスタンスを優先（SP モードで Client レプリカに上書きされるのを防ぐ）
            if (HasStateAuthority || Instance == null)
            {
                Instance = this;
            }
            DontDestroyOnLoad(gameObject);

            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            Debug.Log($"[SurvivorFusionEnemyBatchSync] Spawned (StateAuth={HasStateAuthority}, Injected={_enemyBatchPub != null}, IsInstance={Instance == this})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Server 側: スナップショット配列を NetworkArray に書き込む</summary>
        public void WriteEnemyStates(SurvivorNetworkEnemyStateSnapshot[] snapshots)
        {
            if (!HasStateAuthority) return;

            ActiveCount = Mathf.Min(snapshots.Length, 512);
            if (!_hasLoggedFirstWrite)
            {
                _hasLoggedFirstWrite = true;
                Debug.Log($"[SurvivorFusionEnemyBatchSync] First WriteEnemyStates: count={ActiveCount}");
            }
            for (int i = 0; i < ActiveCount; i++)
            {
                var s = snapshots[i];
                EnemyStates.Set(i, new EnemyStateData
                {
                    NetworkId = s.NetworkId,
                    EnemyMasterId = s.EnemyMasterId,
                    PosX = s.PositionX,
                    PosY = s.PositionY,
                    PosZ = s.PositionZ,
                    VelX = s.VelocityX,
                    VelY = s.VelocityY,
                    VelZ = s.VelocityZ,
                    CurrentHp = s.CurrentHp,
                    SyncTypeByte = s.SyncTypeByte,
                });
            }
        }

        public override void Render()
        {
            if (_changeDetector == null) return;

            foreach (var change in _changeDetector.DetectChanges(this))
            {
                if (change == nameof(EnemyStates) || change == nameof(ActiveCount))
                {
                    PublishBatch();
                    break;
                }
            }
        }

        private void PublishBatch()
        {
            var count = ActiveCount;
            var snapshots = new SurvivorNetworkEnemyStateSnapshot[count];
            for (int i = 0; i < count; i++)
            {
                var e = EnemyStates[i];
                snapshots[i] = new SurvivorNetworkEnemyStateSnapshot
                {
                    NetworkId = e.NetworkId,
                    EnemyMasterId = e.EnemyMasterId,
                    PositionX = e.PosX,
                    PositionY = e.PosY,
                    PositionZ = e.PosZ,
                    VelocityX = e.VelX,
                    VelocityY = e.VelY,
                    VelocityZ = e.VelZ,
                    CurrentHp = e.CurrentHp,
                    SyncTypeByte = e.SyncTypeByte,
                };
            }
            if (!_hasLoggedFirstPublish)
            {
                _hasLoggedFirstPublish = true;
                Debug.Log($"[SurvivorFusionEnemyBatchSync] First ChangeDetector publish: count={count}");
            }
            _enemyBatchPub?.Publish(new SurvivorSignals.Enemy.BatchUpdated(snapshots));
        }
    }
}
