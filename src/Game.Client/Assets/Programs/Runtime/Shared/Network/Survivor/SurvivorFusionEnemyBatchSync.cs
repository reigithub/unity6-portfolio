using Fusion;
using Game.Shared.Network.Fusion;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Fusion 2 敵バッチ同期 NetworkBehaviour シングルトン。
    /// 500+ 体の敵状態を NetworkArray で一括同期し、
    /// ChangeDetector 経由で MessagePipe に配信する。
    /// </summary>
    public class SurvivorFusionEnemyBatchSync : NetworkBehaviour
    {
        [Inject] private IFusionRunnerService _runnerService;
        [Inject] private IPublisher<SurvivorSignals.Enemy.BatchUpdated> _enemyBatchPub;

        private const int MaxEnemies = 512;

        [Networked] public int ActiveCount { get; set; }
        [Networked, Capacity(MaxEnemies)]
        public NetworkArray<SurvivorEnemyStateData> EnemyStates => default;

        private ChangeDetector _changeDetector;
        private SurvivorNetworkEnemyStateSnapshot[] _snapshotBuffer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private bool _hasLoggedFirstWrite;
        private bool _hasLoggedFirstPublish;
#endif

        public override void Spawned()
        {
            DontDestroyOnLoad(gameObject);

            _runnerService?.Register(this);
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            _snapshotBuffer = new SurvivorNetworkEnemyStateSnapshot[MaxEnemies];
            Debug.Log($"[SurvivorFusionEnemyBatchSync] Spawned (StateAuth={HasStateAuthority}, Injected={_enemyBatchPub != null})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _runnerService?.Unregister(this);
        }

        /// <summary>Server 側: スナップショット配列を NetworkArray に書き込む</summary>
        public void WriteEnemyStates(SurvivorNetworkEnemyStateSnapshot[] snapshots)
        {
            if (!HasStateAuthority) return;

            ActiveCount = Mathf.Min(snapshots.Length, MaxEnemies);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_hasLoggedFirstWrite)
            {
                _hasLoggedFirstWrite = true;
                Debug.Log($"[SurvivorFusionEnemyBatchSync] First WriteEnemyStates: count={ActiveCount}");
            }
#endif
            for (int i = 0; i < ActiveCount; i++)
            {
                var s = snapshots[i];
                EnemyStates.Set(i, new SurvivorEnemyStateData
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
            for (int i = 0; i < count; i++)
            {
                var e = EnemyStates[i];
                _snapshotBuffer[i] = new SurvivorNetworkEnemyStateSnapshot
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!_hasLoggedFirstPublish)
            {
                _hasLoggedFirstPublish = true;
                Debug.Log($"[SurvivorFusionEnemyBatchSync] First ChangeDetector publish: count={count}");
            }
#endif
            _enemyBatchPub?.Publish(new SurvivorSignals.Enemy.BatchUpdated(_snapshotBuffer, count));
        }
    }
}
