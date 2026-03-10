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
using Game.Shared.Network;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
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
using VContainer.Unity;

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

        private SurvivorStageModel _stageModel;
        private SurvivorNetworkPlayerState _localPlayerState;
        private SurvivorStageWaveManager _waveManager;
        private SceneInstance? _stageSceneInstance;
        private ISurvivorStageSceneView _stageSceneView;
        private bool _isClient;

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
            _isClient = NetworkModeHelper.IsNetworkClient;
            Debug.Log($"[SurvivorStageScene] Startup: isClient={_isClient}, IsServer={UnityPlaymodeHelper.IsServer()}, {NetworkModeHelper.GetDebugStatus()}");

            // サーバーではNullStageViewでHUD呼び出しをno-op化
            _stageSceneView = UnityPlaymodeHelper.IsServer()
                ? new NullSurvivorStageSceneView()
                : SceneComponent;

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

            // MP Client: Wave進行をサーバー権威モードに設定
            if (_isClient)
            {
                _waveManager.SetClient(true);
            }

            // インゲームフィールドをロード
            await LoadUnitySceneAsync();

            // プレイヤーを動的生成（サーバーでも生成 — 物理・武器・ダメージ処理に必要）
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
                    .Subscribe(item =>
                    {
                        _stageModel.CollectItem(item);

                        // Server / Host: アイテム収集をクライアントに通知（経験値状態含む）
                        if (NetworkModeHelper.IsNetworkServer)
                        {
                            var gm = SurvivorNetworkGameManager.Instance;
                            gm?.NotifyItemCollectedClientRpc(
                                _localPlayerState?.PlayerUserId ?? default,
                                item.ItemId,
                                (int)item.ItemType,
                                item.EffectValue,
                                _stageModel.Experience.Value,
                                _stageModel.ExperienceToNextLevel.Value);
                        }
                    })
                    .AddTo(Disposables);
            }

            // SP / Server: ローカルレベルアップ検知
            // MP Client: サーバーからの LeveledUp シグナルで _pendingLevelUpCount++ する（SubscribeSignals）
            if (!_isClient)
            {
                _stageModel.Level
                    .Skip(1)
                    .Subscribe(_ => _pendingLevelUpCount++)
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

            // Client: ヒット報告コールバック設定（プロキシ命中 → ReportHitServerRpc）
            // _localPlayerState は ReadyState.Enter() で設定されるため、ラムダキャプチャでフィールド参照
            if (_isClient)
            {
                SceneComponent.WeaponManager?.SetHitCallback((enemyId, weaponId) =>
                {
                    if (NetworkModeHelper.IsNetworkClientConnected && _localPlayerState != null)
                        _localPlayerState.ReportHitServerRpc(enemyId, weaponId);
                });
            }

            // Server: 全クライアント切断時にスポーン停止
            if (NetworkModeHelper.IsNetworkServer)
            {
                SurvivorUnityServerSession.OnAllPlayersDisconnected += HandleAllPlayersDisconnected;
            }

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
            if (_isClient)
            {
                // Client: サーバーの権威的な残HPで同期（回復アイテムによるHP差分を補正）
                _damageReceivedSub.Subscribe(s => _stageModel.ForceSetHp(s.RemainingHp)).AddTo(Disposables);
            }
            else
            {
                _damageReceivedSub.Subscribe(s => _stageModel.TakeDamage(s.Damage)).AddTo(Disposables);
            }

            _playerDiedSub.Subscribe(_ => _stageModel.ForceSetHp(0)).AddTo(Disposables);

            _waveStartedSub.Subscribe(s =>
            {
                _stageModel.CurrentWave.Value = s.WaveNumber;
                _stageSceneView.UpdateWave(s.WaveNumber, _waveManager.TotalWaves);

                // クライアント: サーバーからの敵数情報でHUD表示を更新
                if (_isClient)
                {
                    _waveManager.UpdateClientWaveDisplay(s.TargetKillCount, s.EnemyCount);
                }

                // ウェーブバナーはゲーム開始後のみ表示（カウントダウン中は非表示）
                if (s.WaveNumber > 0)
                {
                    if (!_isClient || _stageModel.GameTime.Value > 0)
                    {
                        _stageSceneView.ShowWaveBanner(s.WaveNumber, _waveManager.TotalWaves, s.TargetKillCount);
                    }
                }
            }).AddTo(Disposables);

            _waveCompletedSub.Subscribe(s =>
            {
                if (_isClient)
                {
                    // MP Client: サーバーが計算済みスコアをそのまま加算
                    _stageModel.AddScore(s.WaveClearScore);
                    _waveManager.SetWaveFromServer(s.WaveNumber, s.WaveNumber + 1);
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

            // クライアント: サーバーからの敵バッチ更新で死亡イベントをキルカウントに反映
            if (_isClient)
            {
                _enemyBatchSub.Subscribe(signal =>
                {
                    foreach (var e in signal.Enemies)
                    {
                        if (e.SyncType == EnemySyncType.Death)
                        {
                            _waveManager.IncrementClientKillCount();
                        }
                    }
                }).AddTo(Disposables);

                // MP Client: サーバーからのレベルアップ通知
                _leveledUpSub.Subscribe(s =>
                {
                    _stageModel.SetLevelFromServer(s.Level, s.Experience, s.ExperienceToNextLevel);
                    _pendingLevelUps.Enqueue(s);
                    _pendingLevelUpCount++;
                    Debug.Log($"[SurvivorStageScene] Client: LevelUp received from server: Lv.{s.Level}, options={s.Options?.Length ?? 0}");
                }).AddTo(Disposables);

                // MP Client: サーバーからのアイテム収集通知（経験値 + HP同期）
                _itemCollectedSub.Subscribe(s =>
                {
                    _stageModel.SetExperienceFromServer(s.CurrentExperience, s.ExperienceToNextLevel);

                    // 回復アイテム: クライアント側でもHP回復を反映
                    if (s.ItemType == (int)SurvivorItemType.Recovery)
                    {
                        _stageModel.Heal(s.EffectValue);
                    }
                }).AddTo(Disposables);
            }
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

            // 武器適用イベント購読
            var gm = SurvivorNetworkGameManager.Instance;
            if (gm != null)
            {
                gm.OnWeaponApplyRequested += OnServerWeaponApply;
                gm.OnHitReported += OnServerHitReported;
            }

        }

        private void HandleAllPlayersDisconnected()
        {
            Debug.Log("[SurvivorStageScene] All players disconnected, clearing enemies and stopping spawner");
            SceneComponent.EnemySpawner?.ClearAllEnemies();
        }

        // サーバー側貫通判定用の定数
        private const float PierceDetectionRadius = 0.5f;

        private void OnServerHitReported(int enemyNetworkId, int weaponId)
        {
            if (!SceneComponent.EnemySpawner.TryGetEnemyByNetworkId(enemyNetworkId, out var enemy))
                return;
            if (enemy.IsDead) return;

            if (!SceneComponent.WeaponManager.TryGetWeaponById(weaponId, out var weapon))
                return;

            // ProcRate判定（SPのRollProcRate()と同じロジック）
            var procRate = weapon.ProcRate;
            if (procRate <= 0) return;
            if (procRate < 10000 && !procRate.RollChance()) return;

            // サーバーがダメージ計算
            int damage = weapon.Damage;
            bool isCrit = weapon.CritChance > 0 && weapon.CritChance.RollChance();
            if (isCrit)
                damage = Mathf.RoundToInt(damage * weapon.CritMultiplier.ToRate());

            // プライマリターゲットにダメージ
            enemy.TakeDamage(damage);
            Debug.Log($"[ServerHit] enemy={enemyNetworkId} weapon={weaponId} dmg={damage} crit={isCrit}");

            // ノックバック
            Vector3 playerPos = SceneComponent.PlayerController != null
                ? SceneComponent.PlayerController.transform.position
                : enemy.transform.position;

            if (weapon.Knockback > 0 && SceneComponent.PlayerController != null)
            {
                var dir = (enemy.transform.position - playerPos).normalized;
                enemy.ApplyKnockback(dir * weapon.Knockback);
            }

            // サーバー権威の貫通処理（実際の敵位置で判定）
            if (weapon.Pierce > 0 && SceneComponent.PlayerController != null)
            {
                ServerProcessPierce(enemy, playerPos, weapon, damage);
            }
        }

        /// <summary>
        /// サーバー側貫通処理
        /// プレイヤー→ヒット敵の方向に沿って、実際の敵位置でSphereCastを行い追加ダメージを適用
        /// </summary>
        private void ServerProcessPierce(
            SurvivorEnemyController primaryEnemy,
            Vector3 playerPos,
            SurvivorWeaponBase weapon,
            int damage)
        {
            var direction = (primaryEnemy.transform.position - playerPos).normalized;
            // プライマリターゲットの少し先から検索開始
            var origin = primaryEnemy.transform.position + direction * 0.1f;
            float maxDistance = weapon.Range;

            var hits = Physics.SphereCastAll(
                origin, PierceDetectionRadius, direction, maxDistance,
                LayerMaskConstants.Enemy, QueryTriggerInteraction.Collide);

            // 距離順にソート
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            int pierceRemaining = weapon.Pierce;
            for (int i = 0; i < hits.Length && pierceRemaining > 0; i++)
            {
                var target = hits[i].collider.GetComponentInParent<SurvivorEnemyController>();
                if (target == null || target == primaryEnemy || target.IsDead) continue;

                target.TakeDamage(damage);
                pierceRemaining--;

                Debug.Log($"[ServerPierce] weapon={weapon.WeaponId} dmg={damage} pierce={weapon.Pierce - pierceRemaining}/{weapon.Pierce}");

                // ノックバック
                if (weapon.Knockback > 0)
                {
                    var dir = (target.transform.position - playerPos).normalized;
                    target.ApplyKnockback(dir * weapon.Knockback);
                }
            }
        }

        private void OnServerWeaponApply(WeaponApplyRequest request)
        {
            if (SceneComponent.WeaponManager == null) return;

            switch (request.Type)
            {
                case WeaponApplyType.AddOrUpgrade:
                    if (request.IsNewWeapon)
                        SceneComponent.WeaponManager.AddWeaponAsync(request.WeaponId).Forget();
                    else
                        SceneComponent.WeaponManager.UpgradeWeapon(request.WeaponId);
                    break;

                case WeaponApplyType.Replace:
                    SceneComponent.WeaponManager.ReplaceWeaponAsync(
                        request.RemoveWeaponId, request.WeaponId).Forget();
                    break;
            }

            SceneComponent.WeaponManager.UpdateDamageMultiplier(_stageModel.GetDamageMultiplier());
            Debug.Log($"[SurvivorStageScene] Server weapon applied: type={request.Type}, weaponId={request.WeaponId}");
        }

        /// <summary>
        /// Server 用: MessagePipe Signal → ClientRpc 転送。
        /// ダメージ・死亡は SurvivorPlayerController.States が直接 ClientRpc する。
        /// </summary>
        private void SubscribeNetworkSignals()
        {
            _waveStartedSub.Subscribe(s =>
            {
                var gm = SurvivorNetworkGameManager.Instance;
                gm?.NotifyWaveStartedClientRpc(s.WaveNumber, s.TargetKillCount, s.EnemyCount);
            }).AddTo(Disposables);

            _waveCompletedSub.Subscribe(s =>
            {
                // サーバー側でスコアを計算し、計算済みスコアをクライアントに送信
                var remainingTime = _stageModel.TimeLimit - _stageModel.GameTime.Value;
                var spawnInfo = _waveManager.GetSpawnInfo();
                var hpRatio = _stageModel.MaxHp.Value > 0
                    ? (float)_stageModel.CurrentHp.Value / _stageModel.MaxHp.Value : 1f;
                var waveClearScore = remainingTime > 0
                    ? (int)(remainingTime * spawnInfo.ScoreMultiplier * hpRatio) : 0;
                var gm = SurvivorNetworkGameManager.Instance;
                gm?.NotifyWaveClearedClientRpc(s.WaveNumber, _waveManager.CurrentWave.CurrentValue, waveClearScore);
            }).AddTo(Disposables);

            _waveManager.IsAllWavesCleared
                .Where(cleared => cleared)
                .Subscribe(_ => SurvivorNetworkGameManager.Instance?.NotifyAllWavesClearedClientRpc())
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
            // イベント解除
            SurvivorUnityServerSession.OnAllPlayersDisconnected -= HandleAllPlayersDisconnected;
            var gm = SurvivorNetworkGameManager.Instance;
            if (gm != null)
            {
                gm.OnWeaponApplyRequested -= OnServerWeaponApply;
                gm.OnHitReported -= OnServerHitReported;
            }

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
