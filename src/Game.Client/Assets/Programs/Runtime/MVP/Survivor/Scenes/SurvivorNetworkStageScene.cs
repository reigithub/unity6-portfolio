using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Scenes.Models;
using Game.MVP.Survivor.Services;
using Game.MVP.Survivor.Weapon;
using Game.Shared.Bootstrap;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorメインステージシーン サーバー専用Presenter。
    /// ゲームロジック（ウェーブ管理・ダメージ・勝敗判定）のみを担当し、
    /// HUD/UI/ビジュアルは一切扱わない。
    /// </summary>
    public partial class SurvivorNetworkStageScene : GamePrefabScene<SurvivorNetworkStageScene, SurvivorStageSceneComponent>, IGameSceneScope
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IAddressableAssetService _addressableService;
        [Inject] private readonly IFusionRunnerService _runnerService;

        // Server signals
        [Inject] private readonly ISubscriber<SurvivorSignals.Weapon.HitReported> _hitReportedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Weapon.ApplyRequested> _weaponApplySub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllClientsSceneReady> _allClientsSceneReadySub;

        private SurvivorStageModel _stageModel;
        private SurvivorStageWaveManager _waveManager;
        private SurvivorNetworkWeaponManager _weaponManager;
        private SceneInstance? _stageSceneInstance;

        protected override string AssetPathOrAddress => "SurvivorStageScene";

        #region IGameSceneScope

        public IObjectResolver ScopedResolver { get; set; }

        public void ConfigureScope(IContainerBuilder builder)
        {
            builder.Register<SurvivorStageModel>(Lifetime.Scoped);
            builder.Register<SurvivorStageWaveManager>(Lifetime.Scoped);
            builder.Register<SurvivorNetworkWeaponManager>(Lifetime.Scoped);
        }

        #endregion

        public override async UniTask Startup()
        {
            await base.Startup();

            Debug.Log($"[SurvivorNetworkStageScene] Startup: {_runnerService.GetDebugStatus()}");

            var session = _saveService.CurrentSession;
            if (session == null)
            {
                Debug.LogError("[SurvivorNetworkStageScene] No active session found!");
                return;
            }

            _stageModel = ScopedResolver.Resolve<SurvivorStageModel>();
            _stageModel.Initialize(session.PlayerId, session.StageId);

            _waveManager = ScopedResolver.Resolve<SurvivorStageWaveManager>();
            _waveManager.Initialize(session.StageId);

            _weaponManager = ScopedResolver.Resolve<SurvivorNetworkWeaponManager>();
            _weaponManager.Initialize(
                _stageModel.GetStartingWeaponId(),
                _stageModel.GetDamageMultiplier());

            await LoadUnitySceneAsync();
            await SpawnPlayerAsync();

            BuildStateMachine();
            SubscribeEvents();
            SubscribeSignals();
            SetupServerNetworking();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            var stageAssetName = _stageModel.StageMaster?.AssetName;
            if (!string.IsNullOrEmpty(stageAssetName))
            {
                _stageSceneInstance = await _addressableService.LoadSceneAsync(stageAssetName);
                SceneManager.SetActiveScene(_stageSceneInstance.Value.Scene);
                Debug.Log($"[SurvivorNetworkStageScene] Loaded stage environment: {stageAssetName}");
            }
        }

        private async UniTask SpawnPlayerAsync()
        {
            if (!_stageSceneInstance.HasValue)
            {
                Debug.LogWarning("[SurvivorNetworkStageScene] Stage scene not loaded, skipping player spawn");
                return;
            }

            var playerStart = SurvivorStageSceneHelper.GetPlayerStart(Resolver, _stageSceneInstance.Value.Scene);
            if (playerStart == null)
            {
                Debug.LogWarning("[SurvivorNetworkStageScene] PlayerStart not found, player spawn skipped");
                return;
            }

            var playerMaster = _stageModel.PlayerMaster;
            var levelMaster = _stageModel.CurrentLevelMaster;
            if (playerMaster == null || levelMaster == null)
            {
                Debug.LogError("[SurvivorNetworkStageScene] PlayerMaster or LevelMaster is null!");
                return;
            }

            var playerController = await playerStart.LoadPlayerAsync(Resolver, playerMaster, levelMaster);
            if (playerController != null)
            {
                SceneComponent.SetPlayerController(playerController);
                Debug.Log("[SurvivorNetworkStageScene] Player spawned");
            }
        }

        private void SubscribeEvents()
        {
            // キルカウントはWaveManagerのOnKillCountedを使用（目標数を超える加算を防ぐ）
            _waveManager.OnKillCounted
                .Subscribe(_ => _stageModel.AddKill())
                .AddTo(Disposables);

            // アイテム収集 → ClientRpc/RPC通知
            if (SceneComponent.SurvivorItemSpawner != null)
            {
                SceneComponent.SurvivorItemSpawner.OnItemCollected
                    .Subscribe(item =>
                    {
                        _stageModel.CollectItem(item);

                        if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                            gs.NotifyItemCollected(
                                "",
                                item.ItemId,
                                (int)item.ItemType,
                                item.EffectValue,
                                _stageModel.Experience.Value,
                                _stageModel.ExperienceToNextLevel.Value);
                    })
                    .AddTo(Disposables);
            }

            // ローカルレベルアップ検知
            _stageModel.Level
                .Skip(1)
                .Subscribe(_ => _pendingLevelUpCount++)
                .AddTo(Disposables);

            // StateMachine更新
            SceneComponent.UpdateAsObservable()
                .Subscribe(_ => _stateMachine?.Update())
                .AddTo(Disposables);

            // 全クライアント切断 → 敵クリア
            _allPlayersDisconnectedSub.Subscribe(_ => HandleAllPlayersDisconnected()).AddTo(Disposables);
        }

        /// <summary>
        /// サーバーシグナル購読（ローカルゲームロジック用）
        /// </summary>
        private void SubscribeSignals()
        {
            SceneComponent.PlayerController.OnDamageReceived
                .Subscribe(s => _stageModel.TakeDamage(s.Damage))
                .AddTo(Disposables);

            SceneComponent.PlayerController.OnDied
                .Subscribe(_ => _stageModel.ForceSetHp(0))
                .AddTo(Disposables);

            _waveManager.OnWaveStarted
                .Subscribe(s => _stageModel.CurrentWave.Value = s.WaveNumber)
                .AddTo(Disposables);

            _waveManager.OnWaveCompleted
                .Subscribe(s =>
                {
                    var remainingTime = _stageModel.TimeLimit - _stageModel.GameTime.Value;
                    var spawnInfo = _waveManager.GetSpawnInfo();
                    _stageModel.AddWaveClearScore(
                        s.WaveNumber, remainingTime, spawnInfo.ScoreMultiplier,
                        _stageModel.CurrentHp.Value, _stageModel.MaxHp.Value);
                }).AddTo(Disposables);
        }

        /// <summary>
        /// サーバーネットワーキング: NetworkBridge + シグナル→ClientRpcブリッジ
        /// </summary>
        private void SetupServerNetworking()
        {
            // 武器適用・ヒット報告シグナル購読
            _weaponApplySub.Subscribe(s => OnServerWeaponApply(s.Request)).AddTo(Disposables);
            _hitReportedSub.Subscribe(s => OnServerHitReported(s.EnemyNetworkId, s.WeaponId)).AddTo(Disposables);

            // シグナル→ClientRpcブリッジ
            SubscribeNetworkSignals();
        }

        /// <summary>
        /// Wave/ゲームイベントシグナル → ClientRpcブロードキャスト
        /// </summary>
        private void SubscribeNetworkSignals()
        {
            _waveManager.OnWaveStarted.Subscribe(s =>
            {
                if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                    gs.NotifyWaveStarted(s.WaveNumber, s.TargetKillCount, s.EnemyCount);
            }).AddTo(Disposables);

            _waveManager.OnWaveCompleted.Subscribe(s =>
            {
                var remainingTime = _stageModel.TimeLimit - _stageModel.GameTime.Value;
                var spawnInfo = _waveManager.GetSpawnInfo();
                var hpRatio = _stageModel.MaxHp.Value > 0
                    ? (float)_stageModel.CurrentHp.Value / _stageModel.MaxHp.Value : 1f;
                var waveClearScore = remainingTime > 0
                    ? (int)(remainingTime * spawnInfo.ScoreMultiplier * hpRatio) : 0;

                if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                    gs.NotifyWaveCompleted(s.WaveNumber, _waveManager.CurrentWave.CurrentValue, waveClearScore);
            }).AddTo(Disposables);

            _waveManager.IsAllWavesCleared
                .Where(cleared => cleared)
                .Subscribe(_ =>
                {
                    if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                        gs.NotifyAllWavesCleared();
                })
                .AddTo(Disposables);
        }

        private void HandleAllPlayersDisconnected()
        {
            Debug.Log("[SurvivorNetworkStageScene] All players disconnected, clearing enemies");
            SceneComponent.EnemySpawner?.ClearAllEnemies();
        }

        private void OnServerHitReported(int enemyNetworkId, int weaponId)
        {
            if (!SceneComponent.EnemySpawner.TryGetEnemyByNetworkId(enemyNetworkId, out var enemy))
                return;
            if (enemy.IsDead) return;

            Vector3 playerPos = SceneComponent.PlayerController != null
                ? SceneComponent.PlayerController.transform.position
                : enemy.transform.position;

            _weaponManager.ProcessHitAuthority(enemy, weaponId, playerPos);
        }

        private void OnServerWeaponApply(WeaponApplyRequest request)
        {
            switch (request.Type)
            {
                case WeaponApplyType.AddOrUpgrade:
                    if (request.IsNewWeapon)
                        _weaponManager.AddWeapon(request.WeaponId);
                    else
                        _weaponManager.UpgradeWeapon(request.WeaponId);
                    break;

                case WeaponApplyType.Replace:
                    _weaponManager.ReplaceWeapon(request.RemoveWeaponId, request.WeaponId);
                    break;
            }

            _weaponManager.UpdateDamageMultiplier(_stageModel.GetDamageMultiplier());
            Debug.Log($"[SurvivorNetworkStageScene] Server weapon applied: type={request.Type}, weaponId={request.WeaponId}");
        }

        public override async UniTask Ready()
        {
            // ステートマシン開始（ReadyStateへ）
            _stateMachine.Update();
            await UniTask.CompletedTask;
        }

        public override async UniTask Terminate()
        {
            ApplicationEvents.ResumeTime();

            // ステージ環境シーンをアンロード
            if (_stageSceneInstance.HasValue)
            {
                await _addressableService.UnloadSceneAsync(_stageSceneInstance.Value);
                _stageSceneInstance = null;
                Debug.Log("[SurvivorNetworkStageScene] Unloaded stage environment");
            }

            await base.Terminate();
        }

        /// <summary>
        /// HP割合を計算（0.0 ~ 1.0）
        /// </summary>
        private float GetHpRatio()
        {
            var maxHp = _stageModel.MaxHp.Value;
            return maxHp > 0 ? (float)_stageModel.CurrentHp.Value / maxHp : 0f;
        }

        /// <summary>
        /// キル数をキャップして取得
        /// </summary>
        private int GetCappedKills()
        {
            return Math.Min(_stageModel.TotalKills.Value, _waveManager.TotalTargetKills);
        }
    }
}
