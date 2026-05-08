using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Scenes.Models;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.Services;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Weapon;
using Game.Shared.Bootstrap;
using Game.Shared.Constants;
using Game.Shared.Extensions;
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
using VContainer.Unity;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivor メインステージシーンのクライアント Presenter。
    /// ゲームロジックはサーバー (<see cref="SurvivorNetworkStageScene"/>) 側で実行され、
    /// 本クラスはサーバーから受信した状態を表示する View 側の責務を担う。
    /// SP/MP の違いはロビー経由の有無とプレイヤー人数のみで、接続先は SP 時はローカル別プロセスの
    /// Headless Server、MP 時はリモートサーバーであり、いずれも GameMode.Client で接続する。
    /// </summary>
    public partial class SurvivorStageScene : GamePrefabScene<SurvivorStageScene, SurvivorStageSceneComponent>, IGameSceneScope
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IMasterDataService _masterDataService;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IAddressableAssetService _addressableService;
        [Inject] private readonly IAudioService _audioService;
        [Inject] private readonly IInputService _inputService;
        [Inject] private readonly ILockOnService _lockOnService;
        [Inject] private readonly ISurvivorNetworkStageConnector _networkConnector;
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly IUnityServerSessionConfig _sessionConfig;
        [Inject] private readonly IAuthSessionService _authSessionService;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.DamageReceived> _damageReceivedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.Died> _playerDiedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Wave.Started> _waveStartedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Wave.Completed> _waveCompletedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Game.Ended> _gameEndedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Enemy.Killed> _enemyKilledSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Enemy.BatchUpdated> _enemyBatchSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Item.Spawned> _itemSpawnedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Item.Despawned> _itemDespawnedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.LeveledUp> _leveledUpSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.ItemCollected> _itemCollectedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Player.Revived> _revivedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Game.Paused> _gamePausedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Game.Resumed> _gameResumedSub;
        [Inject] private readonly ISubscriber<SurvivorSignals.Game.CountdownStarted> _countdownStartedSub;

        private SurvivorStageModel _stageModel;
        private SurvivorNetworkStageModel _networkStageModel;

        /// <summary>自分の UserId（シグナル受信時のフィルタに使用）</summary>
        private string MyUserId => _authSessionService?.UserId ?? string.Empty;
        private SurvivorStageWaveManager _waveManager;
        private SceneInstance? _stageSceneInstance;

        protected override string AssetPathOrAddress => "SurvivorStageScene";

        #region IGameSceneScope

        public IObjectResolver ScopedResolver { get; set; }

        public void ConfigureScope(IContainerBuilder builder)
        {
            // per-player モデル（クライアントは自分 1 人分のみ Resolve するため動作等価）
            builder.Register<SurvivorStageModel>(Lifetime.Transient);
            // セッション共有モデル
            builder.Register<SurvivorNetworkStageModel>(Lifetime.Scoped);
            builder.Register<SurvivorStageWaveManager>(Lifetime.Scoped);
        }

        #endregion

        public override async UniTask Startup()
        {
            await base.Startup();

            Debug.Log($"[SurvivorStageScene] Startup: {_runnerService.GetDebugStatus()}");

            // セッションからステージ情報を取得
            var session = _saveService.CurrentSession;
            if (session == null)
            {
                Debug.LogError("[SurvivorStageScene] No active session found!");
                return;
            }

            // IGameSceneScopeのスコープから取得して初期化
            _networkStageModel = ScopedResolver.Resolve<SurvivorNetworkStageModel>();
            _networkStageModel.Initialize(session.StageId);

            _stageModel = ScopedResolver.Resolve<SurvivorStageModel>();
            _stageModel.Initialize(session.PlayerId);

            _waveManager = ScopedResolver.Resolve<SurvivorStageWaveManager>();
            _waveManager.Initialize(session.StageId);

            // スポーン完了後にアクティブシーンを復元するため事前に保存
            var rootScene = SceneManager.GetActiveScene();

            // インゲームフィールドをロード（SetActiveScene で物理シーンがアクティブになる）
            await LoadUnitySceneAsync();

            // サーバーにフィールドシーンロード完了を通知
            // サーバーはこの通知を受けてからプレイヤーをスポーンする（アクティブシーン = 物理シーン保証）
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                // 自分の UserId をサーバーに登録 (per-player シグナル識別に必要)
                var myUserId = _authSessionService?.UserId ?? string.Empty;
                if (!string.IsNullOrEmpty(myUserId))
                {
                    gs.RpcRegisterPlayerUserId(myUserId);
                }
                gs.RpcNotifyFieldSceneLoaded();
            }

            // プレイヤーを動的生成
            await SpawnPlayerAsync();

            // アクティブシーンを GameRootScene に復元（ダイアログ等のシーン遷移はアクティブシーンで行われるため）
            if (rootScene.IsValid())
            {
                SceneManager.SetActiveScene(rootScene);
            }

            BuildStateMachine();
            SubscribeEvents();
            SubscribeSignals();
            BindModelToView();

            SceneComponent.Initialize(_stageModel, _networkStageModel, _waveManager.TotalWaves);

            // ReadyState開始前に暗転状態にしておく（ステージ裏側が見えないように）
            GameRootController?.SetFadeImmediate(1f);

            _lockOnService.Initialize(GameRootController?.MainCamera, LayerConstants.Enemy);
            _lockOnService.SetAutoTarget(SceneComponent.PlayerController?.transform);
            await _lockOnService.PreloadAsync();
        }

        private async UniTask LoadUnitySceneAsync()
        {
            // ステージ環境シーンをAdditiveでロード
            var stageAssetName = _networkStageModel.StageMaster?.AssetName;
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

            var playerController = await playerStart.LoadPlayerAsync(Resolver, playerMaster, levelMaster, SceneComponent.transform);
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
                        GameRootController?.SetCameraRadius(scrollWheel);
                    }
                })
                .AddTo(Disposables);

            // ヒットコールバック設定: 武器サブクラスから Collider + WeaponId を受け取り、
            // NetworkId を取得してサーバーに RPC 送信する（ローカルダメージは適用しない = サーバー権威）。
            // Collider から NetworkId を引く経路は 2 系統:
            //   - SurvivorEnemyController: サーバー側実敵コンポーネント（MPPM 等で同一プロセスに混在する場合）
            //   - EnemyProxyTarget: クライアント側敵プロキシ（通常のクライアント経路）
            SceneComponent.WeaponManager.SetHitCallback((other, weaponId) =>
            {
                if (!_runnerService.TryGetLocalPlayerComponent<SurvivorFusionPlayer>(out var localPlayer)) return;

                int networkId = -1;
                var enemy = other.GetComponentInParent<SurvivorEnemyController>();
                if (enemy != null && !enemy.IsDead)
                {
                    networkId = enemy.NetworkId;
                }
                else
                {
                    var proxy = other.GetComponentInParent<EnemyProxyTarget>();
                    if (proxy != null)
                    {
                        networkId = proxy.NetworkId;
                    }
                }

                if (networkId >= 0)
                {
                    localPlayer.RpcClientHitReported(networkId, weaponId);
                }
            });

            // 自動保存のセットアップ
            SetupAutoSave();
        }

        /// <summary>
        /// SurvivorSignals 購読。
        /// サーバー側のゲームロジックが RPC でブロードキャスト → SurvivorFusionGameState が
        /// MessagePipe Publish → 本 Presenter で受信。SP/MP ともにクライアント経路は同一。
        /// </summary>
        private void SubscribeSignals()
        {
            // サーバー権威の残HPで同期（自分宛てのみ、他プレイヤーの HP 変動は HUD に影響させない）
            _damageReceivedSub.Subscribe(s =>
            {
                if (s.UserId != MyUserId) return;
                _stageModel.ForceSetHp(s.RemainingHp);
            }).AddTo(Disposables);

            _playerDiedSub.Subscribe(s =>
            {
                if (s.UserId != MyUserId) return;
                _stageModel.ForceSetHp(0);
            }).AddTo(Disposables);

            _waveStartedSub.Subscribe(s =>
            {
                _networkStageModel.CurrentWave.Value = s.WaveNumber;
                SceneComponent.UpdateWave(s.WaveNumber, _waveManager.TotalWaves);
                _waveManager.UpdateClientWaveDisplay(s.TargetKillCount, s.EnemyCount);

                if (s.WaveNumber > 0)
                {
                    SceneComponent.ShowWaveBanner(s.WaveNumber, _waveManager.TotalWaves, s.TargetKillCount);
                }
            }).AddTo(Disposables);

            _waveCompletedSub.Subscribe(s =>
            {
                _stageModel.AddScore(s.WaveClearScore);
                _waveManager.SetWaveFromServer(s.WaveNumber, s.WaveNumber + 1);
            }).AddTo(Disposables);

            _gameEndedSub.Subscribe(s =>
            {
                // サーバーの確定キル数でクライアントのカウントを上書き（バッチ同期遅延による不整合を防止）
                if (s.Result.TotalKills > 0)
                {
                    _stageModel.TotalKills.Value = s.Result.TotalKills;
                }
                Debug.Log($"[SurvivorStageScene] GameEnded received: result={s.Result.IsVictory}, kills={_stageModel.TotalKills.Value} (server={s.Result.TotalKills})");
                _networkStageModel.SetNetworkResult(s.Result);
            }).AddTo(Disposables);

            _enemyKilledSub.Subscribe(s =>
            {
                // Score と Kill は「自分が倒したキル」のみ個別加算
                if (s.KillerUserId == MyUserId)
                {
                    _stageModel.AddScore(s.ScoreGained);
                    _stageModel.AddKill();
                }
                // TotalKills はセッション集計（全員合計）として HUD 表示のみ更新
                SceneComponent.UpdateKills(s.TotalKills);
            }).AddTo(Disposables);

            _enemyBatchSub.Subscribe(signal =>
            {
                int deathCount = 0;
                for (int i = 0; i < signal.Count; i++)
                {
                    if (signal.Enemies[i].SyncType == EnemySyncType.Death)
                    {
                        _waveManager.IncrementClientKillCount();
                        deathCount++;
                    }
                }
                if (deathCount > 0)
                {
                    Debug.Log($"[SurvivorStageScene] BatchUpdated: deaths={deathCount}, clientKills={_stageModel.TotalKills.Value}");
                }
            }).AddTo(Disposables);

            _leveledUpSub.Subscribe(s =>
            {
                if (s.UserId != MyUserId) return; // 自分のレベルアップのみ処理
                _stageModel.SetLevelFromServer(s.Level, s.Experience, s.ExperienceToNextLevel);
                _pendingLevelUps.Enqueue(s);
                _pendingLevelUpCount++;
            }).AddTo(Disposables);

            _itemCollectedSub.Subscribe(s =>
            {
                if (s.UserId != MyUserId) return; // 自分の収集のみ処理
                _stageModel.SetExperienceFromServer(s.CurrentExperience, s.ExperienceToNextLevel);
                if (s.ItemType == (int)SurvivorItemType.Recovery)
                {
                    _stageModel.Heal(s.EffectValue);
                }
            }).AddTo(Disposables);

            // 復活シグナル受信 (PR4 時点ではサーバーからの発火経路なし、将来 PR で接続)
            _revivedSub.Subscribe(s =>
            {
                if (s.UserId != MyUserId) return;
                _stateMachine?.Transition(StageEvent.Revived);
            }).AddTo(Disposables);

            // サーバー権威の Pause を全クライアントで受け取り、自プレイヤーの入力を完全停止する。
            // VS Co-op 仕様: 武器選択中は全プレイヤーの入力が一時停止される。
            // 自分が LevelUp 中でなくても、他プレイヤーが LevelUp 中ならここで入力が止まる。
            _gamePausedSub.Subscribe(_ =>
            {
                _inputService.DisablePlayer();
            }).AddTo(Disposables);

            _gameResumedSub.Subscribe(_ =>
            {
                _inputService.EnablePlayer();
            }).AddTo(Disposables);
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
        }

        private void SaveCurrentSession()
        {
            if (_saveService.CurrentSession == null) return;

            // ゲームオーバーや勝利後は保存しない
            if (_stageModel.IsDead || _saveService.CurrentSession.IsCompleted) return;

            _saveService.UpdateSession(
                currentWave: _waveManager.CurrentWave.CurrentValue,
                elapsedTime: _networkStageModel.GameTime.Value,
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
                .Subscribe(hp => SceneComponent.UpdateHp(hp.current, hp.max))
                .AddTo(Disposables);

            if (SceneComponent.PlayerController != null)
            {
                _stageModel.CurrentHp
                    .Subscribe(hp => SceneComponent.PlayerController.SetCurrentHp(hp))
                    .AddTo(Disposables);

                SceneComponent.PlayerController.CurrentStamina
                    .Subscribe(stamina =>
                    {
                        SceneComponent.UpdateStamina(stamina, SceneComponent.PlayerController.MaxStamina);

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
                .Subscribe(exp => SceneComponent.UpdateExperience(exp.current, exp.max))
                .AddTo(Disposables);

            // レベル
            _stageModel.Level
                .Subscribe(level => SceneComponent.UpdateLevel(level))
                .AddTo(Disposables);

            // キル数
            _stageModel.TotalKills
                .Subscribe(kills => SceneComponent.UpdateKills(kills))
                .AddTo(Disposables);

            // 敵の撃破数（目標数に対する進捗を表示）
            _waveManager.EnemiesKilled
                .CombineLatest(_waveManager.TargetKillsThisWave, (killed, target) => (killed, target))
                .Subscribe(enemies => SceneComponent.UpdateEnemies(enemies.killed, enemies.target))
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
            // イベント解除は Disposables で自動処理
            ApplicationEvents.ResumeTime();

            Debug.Log($"[SurvivorStageScene.Terminate] _retryOrQuit={_retryOrQuit}, _isResultSaved={_isResultSaved}");

            // クリア記録保存済み or Retry/Quit時はスキップ
            if (_isResultSaved)
            {
                Debug.Log("[SurvivorStageScene.Terminate] Skipping save - result already saved in VictoryState/GameOverState");

                // プレイ時間だけ加算
                _saveService.AddPlayTime(_networkStageModel.GameTime.Value);
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

            // ステージ環境シーンをアンロード（Fusion 切断前に実行 — Shutdown がシーンをクリーンアップするため）
            if (_stageSceneInstance.HasValue)
            {
                await _addressableService.UnloadSceneAsync(_stageSceneInstance.Value);
                _stageSceneInstance = null;
                Debug.Log("[SurvivorStageScene] Unloaded stage environment");
            }

            await base.Terminate();

            // Fusion 切断（Addressables シーンアンロード後に実行）
            _networkConnector?.Disconnect();
            _sessionConfig.Clear();
            await UniTask.Yield();
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
