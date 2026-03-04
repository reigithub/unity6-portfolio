using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Scenes.Models;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Services;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Network;
using Game.Shared.Network.Survivor;
using Game.Shared.Unity.Server;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using R3;
using R3.Triggers;
using Unity.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorメインステージシーン（Presenter）
    /// StateMachineでゲームループを管理
    /// </summary>
    public partial class SurvivorStageScene : GamePrefabScene<SurvivorStageScene, SurvivorStageSceneComponent>, IGameSceneScope
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IAddressableAssetService _addressableService;
        [Inject] private readonly IAudioService _audioService;
        [Inject] private readonly IInputService _inputService;
        [Inject] private readonly ILockOnService _lockOnService;
        [Inject] private readonly ISurvivorNetworkStageConnector _networkConnector;
        [Inject] private readonly ILocalServerOrchestrator _localServerOrchestrator;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.DamageReceived> _damageReceivedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.Died> _playerDiedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Wave.Started> _waveStartedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Wave.Completed> _waveCompletedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Game.Ended> _gameEndedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Enemy.Killed> _enemyKilledSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Enemy.BatchUpdated> _enemyBatchSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Item.Spawned> _itemSpawnedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Item.Despawned> _itemDespawnedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadySub;

        private SurvivorStageModel _stageModel;
        private SurvivorNetworkPlayerState _localPlayerState;
        private SurvivorStageWaveManager _waveManager;
        private SceneInstance? _stageSceneInstance;
        private ISurvivorStageSceneView _stageSceneView;
        private bool _isClientOnly;

        protected override string AssetPathOrAddress => "SurvivorStageScene";

        #region IGameSceneScope

        public IObjectResolver ScopedResolver { get; set; }

        public void ConfigureScope(IContainerBuilder builder)
        {
            // ゲームシーンと共に寿命が終わる者たちを登録する
            builder.Register<SurvivorStageModel>(Lifetime.Scoped);
            builder.Register<SurvivorStageWaveManager>(Lifetime.Scoped);
        }

        #endregion

        public override async UniTask Startup()
        {
            await base.Startup();

            // ネットワーククライアントモードを起動時に1回だけキャッシュ（SP: false, MP Client: true）
            _isClientOnly = NetworkModeHelper.IsNetworkClientOnly;

            // サーバーではNullStageViewでHUD呼び出しをno-op化
#if UNITY_SERVER
            _stageSceneView = new NullSurvivorStageSceneView();
#else
            _stageSceneView = SceneComponent;
#endif

            // セッションからステージ情報を取得
            var session = _saveService.CurrentSession;
            if (session == null)
            {
                Debug.LogError("[SurvivorStageScene] No active session found!");
                return;
            }

            // IGameSceneScopeのスコープから取得して初期化
            _stageModel = ScopedResolver.Resolve<SurvivorStageModel>();
            _stageModel.Initialize(session.PlayerId, session.StageId);

            _waveManager = ScopedResolver.Resolve<SurvivorStageWaveManager>();
            _waveManager.Initialize(session.StageId);

            // インゲームフィールドをロード
            await LoadUnitySceneAsync();

            // プレイヤーを動的生成
            await SpawnPlayerAsync();

            BuildStateMachine();
            SubscribeEvents();
            SubscribeSignals();
            SetupServerNetworkingIfActive();
            BindModelToView();

            _stageSceneView.Initialize(_stageModel, _waveManager.TotalWaves);

            // ReadyState開始前に暗転状態にしておく（ステージ裏側が見えないように）
            GameRootController?.SetFadeImmediate(1f);

            _lockOnService.Initialize(GameRootController?.MainCamera, LayerConstants.Enemy);
            _lockOnService.SetAutoTarget(SceneComponent.PlayerController?.transform);
            await _lockOnService.PreloadAsync();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            // ステージ環境シーンをAdditiveでロード
            var stageAssetName = _stageModel.StageMaster?.AssetName;
            if (!string.IsNullOrEmpty(stageAssetName))
            {
                _stageSceneInstance = await _addressableService.LoadSceneAsync(stageAssetName);
                SceneManager.SetActiveScene(_stageSceneInstance.Value.Scene);
                // LightProbes.TetrahedralizeAsync();
                Debug.Log($"[SurvivorStageScene] Loaded stage environment: {stageAssetName}");

                // ステージシーンに固有のスカイボックスがあれば適用
                var skybox = SurvivorStageSceneHelper.GetSkybox(_stageSceneInstance.Value.Scene);
                if (skybox != null && skybox.material != null)
                {
                    GameRootController?.SetSkyboxMaterial(skybox.material);
                    Debug.Log($"[SurvivorStageScene] Applied stage skybox: {skybox.material.name}");
                }
            }
        }

        private async UniTask SpawnPlayerAsync()
        {
            if (!_stageSceneInstance.HasValue)
            {
                Debug.LogWarning("[SurvivorStageScene] Stage scene not loaded, skipping player spawn");
                return;
            }

            // ステージシーン内のPlayerStartを検索
            var playerStart = SurvivorStageSceneHelper.GetPlayerStart(Resolver, _stageSceneInstance.Value.Scene);
            if (playerStart == null)
            {
                Debug.LogWarning("[SurvivorStageScene] PlayerStart not found in stage scene, player spawn skipped");
                return;
            }

            // プレイヤー生成
            var playerMaster = _stageModel.PlayerMaster;
            var levelMaster = _stageModel.CurrentLevelMaster;
            if (playerMaster == null || levelMaster == null)
            {
                Debug.LogError("[SurvivorStageScene] PlayerMaster or LevelMaster is null!");
                return;
            }

            var playerController = await playerStart.LoadPlayerAsync(Resolver, playerMaster, levelMaster);
            if (playerController != null)
            {
                // SceneComponentにプレイヤーを設定
                SceneComponent.SetPlayerController(playerController);
                Debug.Log($"[SurvivorStageScene] Player spawned and assigned to SceneComponent");

                // プレイヤー入力を一時的に無効化
                _inputService.DisablePlayer();
            }
        }

        private void SubscribeEvents()
        {
            SceneComponent.OnPauseClicked
                .Subscribe(_ => _pauseRequested = true)
                .AddTo(Disposables);

            // キルカウントはWaveManagerのOnKillCountedを使用（目標数を超える加算を防ぐ）
            _waveManager.OnKillCounted
                .Subscribe(_ => _stageModel.AddKill())
                .AddTo(Disposables);

            if (SceneComponent.SurvivorItemSpawner != null)
            {
                SceneComponent.SurvivorItemSpawner.OnItemCollected
                    .Subscribe(item => _stageModel.CollectItem(item))
                    .AddTo(Disposables);
            }

            _stageModel.Level
                .Skip(1)
                .Subscribe(_ => _levelUpRequested = true)
                .AddTo(Disposables);

            SceneComponent.UpdateAsObservable()
                .Subscribe(_ => _stateMachine?.Update())
                .AddTo(Disposables);

            // InputService
            Observable.EveryUpdate(UnityFrameProvider.Update)
                .Where(_ => Application.isPlaying)
                .Subscribe(_ =>
                {
                    if (_inputService.UI.Escape.WasPressedThisFrame())
                        _pauseRequested = true;

                    if (_pauseRequested) return;

                    if (_inputService.UI.Click.WasPressedThisFrame())
                    {
                        var point = _inputService.UI.Point.ReadValue<Vector2>();
                        _lockOnService.SetTarget(point);
                    }

                    if (_inputService.UI.ScrollWheel.WasPressedThisFrame())
                    {
                        var scrollWheel = _inputService.UI.ScrollWheel.ReadValue<Vector2>();
                        GameRootController.SetCameraRadius(scrollWheel);
                    }
                })
                .AddTo(Disposables);

            // 自動保存のセットアップ
            SetupAutoSave();
        }

        /// <summary>
        /// SurvivorSignals 購読（統一）。
        /// SP/Server: ゲームロジックが直接 Publish。
        /// MP Client: ClientRpc → NetworkSurvivorGameManager が Publish。
        /// </summary>
        private void SubscribeSignals()
        {
            _damageReceivedSub.Subscribe(s => _stageModel.TakeDamage(s.Damage)).AddTo(Disposables);

            _playerDiedSub.Subscribe(_ => _stageModel.ForceSetHp(0)).AddTo(Disposables);

            _waveStartedSub.Subscribe(s =>
            {
                _stageModel.CurrentWave.Value = s.WaveNumber;
                _stageSceneView.UpdateWave(s.WaveNumber, _waveManager.TotalWaves);
                if (s.WaveNumber > 0)
                {
                    _stageSceneView.ShowWaveBanner(s.WaveNumber, _waveManager.TotalWaves, s.TargetKillCount);
                }
            }).AddTo(Disposables);

            _waveCompletedSub.Subscribe(s =>
            {
                if (_isClientOnly)
                {
                    // MP Client: サーバーが計算済みスコアをそのまま加算
                    _stageModel.AddScore(s.WaveClearScore);
                }
                else
                {
                    // SP/Server: ローカルで計算
                    var remainingTime = _stageModel.TimeLimit - _stageModel.GameTime.Value;
                    var spawnInfo = _waveManager.GetSpawnInfo();
                    _stageModel.AddWaveClearScore(
                        s.WaveNumber, remainingTime, spawnInfo.ScoreMultiplier,
                        _stageModel.CurrentHp.Value, _stageModel.MaxHp.Value);
                }
            }).AddTo(Disposables);

            _gameEndedSub.Subscribe(s => _stageModel.SetNetworkResult(s.Result)).AddTo(Disposables);

            _enemyKilledSub.Subscribe(s =>
            {
                _stageModel.AddScore(s.ScoreGained);
                _stageModel.AddKill();
                _stageSceneView.UpdateKills(s.TotalKills);
            }).AddTo(Disposables);
        }

        /// <summary>
        /// サーバーネットワーキングのセットアップ（ランタイム判定）。
        /// Dedicated Server: Startup 時に NetworkServer.active == true → 実行。
        /// Host mode: Startup 時は false → スキップ。ReadyState の StartHostAsync 後に再呼び出し。
        /// </summary>
        internal void SetupServerNetworkingIfActive()
        {
            if (!NetworkModeHelper.IsNetworkServer) return;

            SubscribeNetworkSignals();
            var networkBridge = new SurvivorNetworkBridge();
            SceneComponent.EnemySpawner?.SetNetworkBridge(networkBridge);
            SceneComponent.SurvivorItemSpawner?.SetNetworkBridge(networkBridge);
        }

        /// <summary>
        /// Server 用: MessagePipe Signal → ClientRpc 転送。
        /// </summary>
        private void SubscribeNetworkSignals()
        {
            var gm = SurvivorNetworkGameManager.Instance;
            if (gm == null) return;

            // ダメージ・死亡はサーバーの SurvivorPlayerController.States が
            // 直接 GameManager.NotifyPlayerDamagedClientRpc / NotifyPlayerDiedClientRpc を呼ぶ。
            // userId 付きで送信するため、ブリッジ不要。

            _waveStartedSub.Subscribe(s =>
            {
                gm.NotifyWaveStartedClientRpc(s.WaveNumber, s.TargetKillCount, s.EnemyCount);
            }).AddTo(Disposables);

            _waveCompletedSub.Subscribe(s =>
            {
                var spawnInfo = _waveManager.GetSpawnInfo();
                gm.NotifyWaveClearedClientRpc(s.WaveNumber, _waveManager.CurrentWave.CurrentValue, spawnInfo.ScoreMultiplier);
            }).AddTo(Disposables);

            _waveManager.IsAllWavesCleared
                .Where(cleared => cleared)
                .Subscribe(_ => gm.NotifyAllWavesClearedClientRpc())
                .AddTo(Disposables);
        }

        private void SetupAutoSave()
        {
            // 30秒ごとにセッションを保存（中断データ）
            Observable.Interval(TimeSpan.FromSeconds(30))
                .Subscribe(_ => SaveCurrentSession())
                .AddTo(Disposables);

            // アプリ中断時（バックグラウンド移行時）に保存
            SceneComponent.OnApplicationPauseObservable
                .Where(paused => paused)
                .Subscribe(_ => SaveCurrentSession())
                .AddTo(Disposables);

            // OnApplicationQuit は削除（クリア記録はVictoryState/GameOverStateで保存済み）
        }

        private void SaveCurrentSession()
        {
            if (_saveService.CurrentSession == null) return;

            // ゲームオーバーや勝利後は保存しない
            if (_stageModel.IsDead || _saveService.CurrentSession.IsCompleted) return;

            _saveService.UpdateSession(
                currentWave: _waveManager.CurrentWave.CurrentValue,
                elapsedTime: _stageModel.GameTime.Value,
                currentHp: _stageModel.CurrentHp.Value,
                experience: _stageModel.Experience.Value,
                level: _stageModel.Level.Value,
                score: _stageModel.Score.Value,
                totalKills: _stageModel.TotalKills.Value
            );

            SaveCurrentSessionAsync().Forget();
        }

        private async UniTask SaveCurrentSessionAsync()
        {
            try
            {
                await _saveService.SaveAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorStageScene] Auto-save failed: {ex.Message}");
            }
        }

        private void BindModelToView()
        {
            // HP（View更新）
            _stageModel.CurrentHp
                .CombineLatest(_stageModel.MaxHp, (current, max) => (current, max))
                .Subscribe(hp => _stageSceneView.UpdateHp(hp.current, hp.max))
                .AddTo(Disposables);

            if (SceneComponent.PlayerController != null)
            {
                _stageModel.CurrentHp
                    .Subscribe(hp => SceneComponent.PlayerController.SetCurrentHp(hp))
                    .AddTo(Disposables);

                SceneComponent.PlayerController.CurrentStamina
                    .Subscribe(stamina =>
                    {
                        _stageSceneView.UpdateStamina(stamina, SceneComponent.PlayerController.MaxStamina);

                        if (_inputService.Player.enabled)
                        {
                            if (stamina > 0)
                                _inputService.Player.LeftShift.Enable();
                            else
                                _inputService.Player.LeftShift.Disable();
                        }
                    })
                    .AddTo(Disposables);
            }

            // 経験値
            _stageModel.Experience
                .CombineLatest(_stageModel.ExperienceToNextLevel, (current, max) => (current, max))
                .Subscribe(exp => _stageSceneView.UpdateExperience(exp.current, exp.max))
                .AddTo(Disposables);

            // レベル
            _stageModel.Level
                .Subscribe(level => _stageSceneView.UpdateLevel(level))
                .AddTo(Disposables);

            // キル数
            _stageModel.TotalKills
                .Subscribe(kills => _stageSceneView.UpdateKills(kills))
                .AddTo(Disposables);

            // 敵の撃破数（目標数に対する進捗を表示）
            _waveManager.EnemiesKilled
                .CombineLatest(_waveManager.TargetKillsThisWave, (killed, target) => (killed, target))
                .Subscribe(enemies => _stageSceneView.UpdateEnemies(enemies.killed, enemies.target))
                .AddTo(Disposables);
        }

        public override async UniTask Ready()
        {
            // グローバルフェードインはスキップ（ReadyStateでカメラ追従後にフェードイン）
            // await base.Ready();

            // ステートマシン開始（ReadyStateへ）
            _stateMachine.Update();

            await UniTask.CompletedTask;
        }

        public override async UniTask Terminate()
        {
            _networkConnector?.Disconnect();
            ApplicationEvents.ResumeTime();

            Debug.Log($"[SurvivorStageScene.Terminate] _retryOrQuit={_retryOrQuit}, _isResultSaved={_isResultSaved}");

            // クリア記録保存済み or Retry/Quit時はスキップ
            if (_isResultSaved)
            {
                Debug.Log("[SurvivorStageScene.Terminate] Skipping save - result already saved in VictoryState/GameOverState");

                // プレイ時間だけ加算
                _saveService.AddPlayTime(_stageModel.GameTime.Value);
            }
            else if (!_retryOrQuit)
            {
                // 中断データのみ保存（VictoryState/GameOverStateに到達していない場合）
                Debug.Log("[SurvivorStageScene.Terminate] Saving interrupted session data");
                SaveCurrentSession();
                await _saveService.SaveAsync();
            }
            else
            {
                Debug.Log("[SurvivorStageScene.Terminate] Skipping save due to _retryOrQuit=true");
            }

            // スカイボックスをデフォルトに戻す
            GameRootController?.ResetSkyboxMaterial();

            // ステージ環境シーンをアンロード
            if (_stageSceneInstance.HasValue)
            {
                await _addressableService.UnloadSceneAsync(_stageSceneInstance.Value);
                _stageSceneInstance = null;
                Debug.Log("[SurvivorStageScene] Unloaded stage environment");
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
