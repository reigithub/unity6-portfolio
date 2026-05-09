using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Fusion;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Player;
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
    public partial class SurvivorNetworkStageScene : GamePrefabScene<SurvivorNetworkStageScene, SurvivorNetworkStageSceneComponent>, IGameSceneScope
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IAddressableAssetService _addressableService;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly IMasterDataService _masterDataService;

        // Server signals
        [Inject] private readonly ISubscriber<SurvivorSignals.Weapon.HitReported> _hitReportedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Weapon.ApplyRequested> _weaponApplySub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Item.CollectReported> _itemCollectReportedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.DamageReceived> _damageReceivedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.Died> _playerDiedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersDisconnected> _allPlayersDisconnectedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllClientsSceneReady> _allClientsSceneReadySub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllClientsFieldSceneLoaded> _allClientsFieldSceneLoadedSub;

        private SurvivorNetworkStageModel _networkStageModel;
        private SurvivorStageWaveManager _waveManager;
        private SurvivorFusionGameState _gameState;
        private SceneInstance? _stageSceneInstance;

        /// <summary>サーバーサイドの per-player コンテキスト Dictionary</summary>
        internal readonly Dictionary<PlayerRef, SurvivorNetworkPlayerContext> _players = new();

        /// <summary>直近でヒット報告をしたプレイヤー (Kill/Item 帰属の暫定解決)</summary>
        private SurvivorNetworkPlayerContext _lastHittingContext;

        /// <summary>現在 LevelUp 処理中のプレイヤー (State Machine が参照)</summary>
        internal SurvivorNetworkPlayerContext _currentLevelingContext;

        /// <summary>UserId から Context を索引する</summary>
        private bool TryGetContextByUserId(string userId, out SurvivorNetworkPlayerContext context)
        {
            if (!string.IsNullOrEmpty(userId) && _gameState != null
                && _gameState.TryGetPlayerRef(userId, out var pref)
                && _players.TryGetValue(pref, out context))
            {
                return true;
            }
            context = null;
            return false;
        }

        protected override string AssetPathOrAddress => "SurvivorNetworkStageScene";

        #region IGameSceneScope

        public IObjectResolver ScopedResolver { get; set; }

        public void ConfigureScope(IContainerBuilder builder)
        {
            // per-player モデルは Transient で、Context 毎に独立インスタンスを保証
            builder.Register<SurvivorStageModel>(Lifetime.Transient);
            builder.Register<SurvivorNetworkWeaponManager>(Lifetime.Transient);
            // セッション共有モデルは Scoped
            builder.Register<SurvivorNetworkStageModel>(Lifetime.Scoped);
            builder.Register<SurvivorStageWaveManager>(Lifetime.Scoped);
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

            _networkStageModel = ScopedResolver.Resolve<SurvivorNetworkStageModel>();
            _networkStageModel.Initialize(session.StageId);

            _waveManager = ScopedResolver.Resolve<SurvivorStageWaveManager>();
            _waveManager.Initialize(session.StageId);

            // StageModel / WeaponManager は per-player Context で生成 (SpawnPlayerAsync 内)

            // スポーン完了後にアクティブシーンを復元するため事前に保存
            var rootScene = SceneManager.GetActiveScene();

            await LoadUnitySceneAsync();
            await UniTask.Yield();
            await SpawnPlayerAsync();

            // アクティブシーンを GameRootScene に復元（ダイアログ等のシーン遷移はアクティブシーンで行われるため）
            if (rootScene.IsValid())
            {
                SceneManager.SetActiveScene(rootScene);
            }

            _runnerService.TryGet(out _gameState);

            BuildStateMachine();
            SubscribeEvents();
            SubscribeSignals();
            SetupServerNetworking();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            var stageAssetName = _networkStageModel.StageMaster?.AssetName;
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

            // クライアントのフィールドシーンロード完了を待機
            // クライアントの SetActiveScene が完了してからスポーンすることで、
            // レプリケーション時にクライアント側でも物理シーンに配置されることを保証する
            Debug.Log("[SurvivorNetworkStageScene] Waiting for client field scene loaded...");
            var fieldSceneTcs = new UniTaskCompletionSource();
            var fieldSceneSub = _allClientsFieldSceneLoadedSub.Subscribe(_ => fieldSceneTcs.TrySetResult());
            try
            {
                await UniTask.WhenAny(
                    fieldSceneTcs.Task,
                    UniTask.Delay(System.TimeSpan.FromSeconds(10), DelayType.Realtime));
            }
            finally
            {
                fieldSceneSub.Dispose();
            }
            Debug.Log("[SurvivorNetworkStageScene] Client field scene loaded (or timeout), spawning player");

            // Fusion プレイヤーオブジェクトを PlayerStart 位置にスポーン
            Debug.Log($"[SurvivorNetworkStageScene] SpawnPlayerAsync: PlayerStart pos={playerStart.transform.position}, Runner={_runnerService.Runner != null}");
            if (_runnerService.Runner != null &&
                _runnerService.Runner.TryGetComponent<SurvivorFusionRunner>(out var fusionRunner))
            {
                fusionRunner.SpawnConnectedPlayers(playerStart.transform.position, Quaternion.identity);
                Debug.Log("[SurvivorNetworkStageScene] SpawnConnectedPlayers called");
            }
            else
            {
                Debug.LogWarning("[SurvivorNetworkStageScene] FusionRunner not found, spawn skipped!");
            }

            var session = _saveService.CurrentSession;
            if (session == null) return;
            var memoryDatabase = _masterDataService.MemoryDatabase;
            if (!memoryDatabase.SurvivorPlayerMasterTable.TryFindById(session.PlayerId, out var playerMaster)
                || !memoryDatabase.SurvivorPlayerLevelMasterTable.TryFindByPlayerIdAndLevel((session.PlayerId, 1), out var levelMaster))
            {
                Debug.LogError("[SurvivorNetworkStageScene] PlayerMaster or LevelMaster is null!");
                return;
            }

            // GameState を早期取得 (UserId 参照のため)
            _runnerService.TryGet(out _gameState);

            SurvivorPlayerController firstController = null;
            foreach (var player in _runnerService.Runner.ActivePlayers)
            {
                if (!_runnerService.Runner.TryGetPlayerObject(player, out _))
                    continue;

                var ctrl = await playerStart.LoadPlayerAsync(Resolver, playerMaster, levelMaster, targetPlayer: player);
                if (ctrl != null && firstController == null)
                {
                    firstController = ctrl;
                }

                // PR3b: per-player で StageModel / WeaponManager を新規 Resolve (Transient)
                // ※ ScopedResolver を使う (Resolver は親コンテナで SurvivorStageModel が未登録)
                if (!_players.ContainsKey(player))
                {
                    var stageModel = ScopedResolver.Resolve<SurvivorStageModel>();
                    stageModel.Initialize(session.PlayerId);
                    var weaponManager = ScopedResolver.Resolve<SurvivorNetworkWeaponManager>();
                    weaponManager.Initialize(
                        stageModel.GetStartingWeaponId(),
                        stageModel.GetDamageMultiplier());

                    string userId = (_gameState != null && _gameState.TryGetUserId(player, out var uid))
                        ? uid : string.Empty;

                    var context = new SurvivorNetworkPlayerContext(player, userId, stageModel, weaponManager);
                    context.Controller = ctrl;
                    context.FusionPlayer = ctrl != null ? ctrl.FusionPlayer : null;
                    _players[player] = context;

                    // PR4: 敵スポナーに各プレイヤー Transform を登録 (複数プレイヤーへの分散ターゲティング)
                    if (ctrl != null && SceneComponent.EnemySpawner != null)
                    {
                        SceneComponent.EnemySpawner.AddPlayer(ctrl.transform);
                    }
                }

                Debug.Log($"[SurvivorNetworkStageScene] Player initialized: {player}");
            }

            // TODO: SetPlayerController は1人分しか保持できない。
            // 複数プレイヤーの距離検証やレベルアップ通知には Dictionary<PlayerRef, SurvivorPlayerController> が必要（別タスク）。
            if (firstController != null)
            {
                SceneComponent.SetPlayerController(firstController);
                Debug.Log("[SurvivorNetworkStageScene] Player spawned");
            }
        }

        private void SubscribeEvents()
        {
            // キル帰属: 直近ヒット Context に加算 (暫定: Kill アトリビュートの正確化は将来 PR)
            _waveManager.OnKillCounted
                .Subscribe(_ => _lastHittingContext?.StageModel.AddKill())
                .AddTo(Disposables);

            // アイテム収集 (サーバーローカル吸引経路): 直近ヒット Context にフォールバック帰属
            if (SceneComponent.SurvivorItemSpawner != null)
            {
                SceneComponent.SurvivorItemSpawner.OnItemCollected
                    .Subscribe(item =>
                    {
                        var ctx = _lastHittingContext;
                        if (ctx == null) return;
                        ctx.StageModel.CollectItem(item);

                        if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                            gs.NotifyItemCollected(
                                ctx.UserId,
                                item.ItemId,
                                (int)item.ItemType,
                                item.EffectValue,
                                ctx.StageModel.Experience.Value,
                                ctx.StageModel.ExperienceToNextLevel.Value);
                    })
                    .AddTo(Disposables);
            }

            // per-player レベルアップ検知 (各 Context の StageModel.Level を個別 Subscribe)
            foreach (var context in _players.Values)
            {
                var ctxLocal = context;
                ctxLocal.StageModel.Level
                    .Skip(1)
                    .Subscribe(_ => ctxLocal.PendingLevelUpCount++)
                    .AddTo(Disposables);
            }

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
            // サーバー権威の残HPで同期 (per-player: UserId → Context 索引)
            _damageReceivedSub.Subscribe(s =>
            {
                if (TryGetContextByUserId(s.UserId, out var ctx))
                    ctx.StageModel.ForceSetHp(s.RemainingHp);
            }).AddTo(Disposables);

            _playerDiedSub.Subscribe(s =>
            {
                if (TryGetContextByUserId(s.UserId, out var ctx))
                {
                    ctx.StageModel.ForceSetHp(0);
                    ctx.IsDead = true;
                }
            }).AddTo(Disposables);

            _waveManager.OnWaveStarted
                .Subscribe(s => _networkStageModel.CurrentWave.Value = s.WaveNumber)
                .AddTo(Disposables);

            _waveManager.OnWaveCompleted
                .Subscribe(s =>
                {
                    var remainingTime = _networkStageModel.TimeLimit - _networkStageModel.GameTime.Value;
                    var spawnInfo = _waveManager.GetSpawnInfo();
                    // 全プレイヤーにクリアスコア加算 (全員に同じスコア)
                    foreach (var ctx in _players.Values)
                    {
                        ctx.StageModel.AddWaveClearScore(
                            s.WaveNumber, remainingTime, spawnInfo.ScoreMultiplier,
                            ctx.StageModel.CurrentHp.Value, ctx.StageModel.MaxHp.Value);
                    }
                }).AddTo(Disposables);
        }

        /// <summary>
        /// サーバーネットワーキング: NetworkBridge + シグナル→ClientRpcブリッジ
        /// </summary>
        private void SetupServerNetworking()
        {
            // 武器適用 (暫定: 現在 LevelUp 中の Context に適用。LevelUpState でセット済み)
            _weaponApplySub.Subscribe(s =>
            {
                var ctx = _currentLevelingContext ?? _lastHittingContext;
                if (ctx != null) OnServerWeaponApply(ctx, s.Request);
            }).AddTo(Disposables);

            // ヒット報告 (UserId → Context 索引)
            _hitReportedSub.Subscribe(s =>
            {
                if (TryGetContextByUserId(s.UserId, out var ctx))
                {
                    _lastHittingContext = ctx;
                    OnServerHitReported(ctx, s.EnemyNetworkId, s.WeaponId);
                }
            }).AddTo(Disposables);

            // アイテム収集報告 (UserId → Context 索引)
            _itemCollectReportedSub.Subscribe(s =>
            {
                if (TryGetContextByUserId(s.UserId, out var ctx))
                {
                    OnServerItemCollectReported(ctx, s.NetworkId);
                }
            }).AddTo(Disposables);

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
                var remainingTime = _networkStageModel.TimeLimit - _networkStageModel.GameTime.Value;
                var spawnInfo = _waveManager.GetSpawnInfo();
                // 通知用 WaveClearScore は代表値 (生存プレイヤーの平均 HP 比) で計算。
                // 実際の per-player スコア加算は SubscribeSignals の OnWaveCompleted で実施。
                float avgHpRatio = 1f;
                if (_players.Count > 0)
                {
                    float totalRatio = 0f;
                    foreach (var ctx in _players.Values)
                    {
                        var maxHp = ctx.StageModel.MaxHp.Value;
                        totalRatio += maxHp > 0 ? (float)ctx.StageModel.CurrentHp.Value / maxHp : 1f;
                    }
                    avgHpRatio = totalRatio / _players.Count;
                }
                var waveClearScore = remainingTime > 0
                    ? (int)(remainingTime * spawnInfo.ScoreMultiplier * avgHpRatio) : 0;

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

        /// <summary>プレイヤーと敵の最大許容距離（武器射程 + ネットワーク遅延マージン）</summary>
        private const float MaxHitValidationDistance = 30f;

        private void OnServerHitReported(SurvivorNetworkPlayerContext ctx, int enemyNetworkId, int weaponId)
        {
            if (!SceneComponent.EnemySpawner.TryGetEnemyByNetworkId(enemyNetworkId, out var enemy))
                return;
            if (enemy.IsDead) return;

            Vector3 playerPos = ctx.Controller != null
                ? ctx.Controller.transform.position
                : enemy.transform.position;

            // サーバー側距離検証: プレイヤーと敵の距離が許容範囲内か
            float distance = Vector3.Distance(playerPos, enemy.transform.position);
            if (distance > MaxHitValidationDistance)
            {
                Debug.LogWarning($"[ServerHitValidation] Rejected: enemy={enemyNetworkId}, weapon={weaponId}, distance={distance:F1} > {MaxHitValidationDistance}");
                return;
            }

            // 武器発射レート検証: バースト攻撃や不正な高頻度ヒットを排除
            if (!ctx.WeaponManager.ValidateHitRate(weaponId, Time.time))
                return;

            ctx.WeaponManager.ProcessHitAuthority(enemy, weaponId, playerPos);
        }

        /// <summary>
        /// サーバー: クライアントからのアイテム収集報告を処理。
        /// networkId で個体を取得してマスターデータからアイテム効果を取得し、モデルに適用後、結果を全クライアントに通知。
        /// </summary>
        private void OnServerItemCollectReported(SurvivorNetworkPlayerContext ctx, int networkId)
        {
            var itemSpawner = SceneComponent.SurvivorItemSpawner;
            if (itemSpawner == null) return;

            // networkId から個体を取得（すでに破棄済みの報告は無視）
            if (!itemSpawner.TryGetItemByNetworkId(networkId, out var item)) return;
            int itemId = item.ItemId;

            // マスターデータからアイテム情報を取得
            if (!itemSpawner.TryGetItemMaster(itemId, out var master)) return;

            var itemType = (SurvivorItemType)master.ItemType;
            var effectValue = master.EffectValue;

            // 該当プレイヤーのモデルにアイテム効果を適用
            switch (itemType)
            {
                case SurvivorItemType.Experience:
                    ctx.StageModel.AddExperience(effectValue);
                    break;
                case SurvivorItemType.Recovery:
                    ctx.StageModel.Heal(effectValue);
                    break;
            }

            // 全クライアントに結果を通知（NotifyItemCollected は ItemId、NotifyItemDespawned は networkId）
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.NotifyItemCollected(
                    ctx.UserId, itemId, (int)itemType, effectValue,
                    ctx.StageModel.Experience.Value,
                    ctx.StageModel.ExperienceToNextLevel.Value);
                gs.NotifyItemDespawned(networkId);
            }
        }

        private void OnServerWeaponApply(SurvivorNetworkPlayerContext ctx, SurvivorWeaponApplyRequest request)
        {
            bool success = false;
            switch (request.Type)
            {
                case SurvivorWeaponApplyType.AddOrUpgrade:
                    success = request.IsNewWeapon
                        ? ctx.WeaponManager.AddWeapon(request.WeaponId)
                        : ctx.WeaponManager.UpgradeWeapon(request.WeaponId);
                    break;

                case SurvivorWeaponApplyType.Replace:
                    success = ctx.WeaponManager.ReplaceWeapon(request.RemoveWeaponId, request.WeaponId);
                    break;
            }

            ctx.WeaponManager.UpdateDamageMultiplier(ctx.StageModel.GetDamageMultiplier());

            // 武器変更をクライアントに通知（整合性確認用）
            if (success && _gameState != null && ctx.WeaponManager.TryGetWeaponById(request.WeaponId, out var slot))
            {
                _gameState.NotifyWeaponChanged(
                    ctx.UserId,
                    request.WeaponId,
                    slot.Level,
                    request.IsNewWeapon || request.Type == SurvivorWeaponApplyType.Replace);
            }

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

            // per-player コンテキストを Dispose (内部モデルの Dispose は VContainer Scope 管理に委譲)
            foreach (var context in _players.Values)
            {
                context.Dispose();
            }
            _players.Clear();

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
        /// 全プレイヤーの平均 HP 割合（0.0 ~ 1.0）
        /// </summary>
        private float GetHpRatio()
        {
            if (_players.Count == 0) return 0f;
            float total = 0f;
            foreach (var ctx in _players.Values)
            {
                var maxHp = ctx.StageModel.MaxHp.Value;
                total += maxHp > 0 ? (float)ctx.StageModel.CurrentHp.Value / maxHp : 0f;
            }
            return total / _players.Count;
        }

        /// <summary>
        /// 全プレイヤー合計キル数をキャップして取得
        /// </summary>
        private int GetCappedKills()
        {
            int total = 0;
            foreach (var ctx in _players.Values) total += ctx.StageModel.TotalKills.Value;
            return Math.Min(total, _waveManager.TotalTargetKills);
        }

        /// <summary>
        /// 全プレイヤー合計スコア
        /// </summary>
        private int GetTotalScore()
        {
            int total = 0;
            foreach (var ctx in _players.Values) total += ctx.StageModel.Score.Value;
            return total;
        }
    }
}
