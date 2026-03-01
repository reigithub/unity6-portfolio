using System;
using Game.MVP.Survivor.Services;
using Game.Shared.Netcode.Survivor;
using Game.Shared.Survivor;
using MessagePipe;
using R3;
using Unity.Collections;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Server 用: MessagePipe Signal → ClientRpc 転送ブリッジ。
    /// SurvivorStageScene から抽出。Server でのみ生成される。
    /// </summary>
    public class SurvivorNetworkEventBridge : IDisposable
    {
        private readonly CompositeDisposable _disposables = new();

        public SurvivorNetworkEventBridge(
            ISubscriber<SurvivorSignals.Player.DamageReceived> damageReceivedSub,
            ISubscriber<SurvivorSignals.Player.Died> playerDiedSub,
            ISubscriber<SurvivorSignals.Wave.Started> waveStartedSub,
            ISubscriber<SurvivorSignals.Wave.Completed> waveCompletedSub,
            SurvivorStageWaveManager waveManager)
        {
            var gm = NetworkSurvivorGameManager.Instance;
            if (gm == null) return;

            damageReceivedSub.Subscribe(s =>
            {
                var userId = new FixedString64Bytes("local");
                gm.NotifyPlayerDamagedClientRpc(userId, s.Damage, s.RemainingHp);
            }).AddTo(_disposables);

            playerDiedSub.Subscribe(_ =>
            {
                var userId = new FixedString64Bytes("local");
                gm.NotifyPlayerDiedClientRpc(userId);
            }).AddTo(_disposables);

            waveStartedSub.Subscribe(s =>
            {
                gm.NotifyWaveStartedClientRpc(s.WaveNumber, s.TargetKillCount, s.EnemyCount);
            }).AddTo(_disposables);

            waveCompletedSub.Subscribe(s =>
            {
                var spawnInfo = waveManager.GetSpawnInfo();
                gm.NotifyWaveClearedClientRpc(s.WaveNumber, waveManager.CurrentWave.CurrentValue, spawnInfo.ScoreMultiplier);
            }).AddTo(_disposables);

            waveManager.IsAllWavesCleared
                .Where(cleared => cleared)
                .Subscribe(_ => gm.NotifyAllWavesClearedClientRpc())
                .AddTo(_disposables);
        }

        public void Dispose()
        {
            _disposables.Dispose();
        }
    }
}
