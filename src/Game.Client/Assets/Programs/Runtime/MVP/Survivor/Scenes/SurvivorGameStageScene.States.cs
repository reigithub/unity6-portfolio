using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Weapon;
using Game.Library.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;

namespace Game.MVP.Survivor.Scenes
{
    public partial class SurvivorGameStageScene
    {
        #region StateMachine

        private enum StageEvent
        {
            StartGame,
            Pause,
            Resume,
            LevelUp,
            LevelUpComplete,
            ApparentDeath,   // 仮死状態 (HP=0 だがサーバーの GameEnded まで観戦継続)
            Revived,          // 仮死からの復活 (現在は発火経路なし、将来 PR で接続)
            Victory,
            GameOver,
            Retry,
            QuitToTitle,
            ReturnToLobby,
        }

        private StateMachine<SurvivorGameStageScene, StageEvent> _stateMachine;
        private bool _isResultSaved;
        private bool _retryOrQuit;
        private bool _pauseRequested;
        private bool _returnToTitleRequested;
        private bool _returnToLobbyRequested;
        private int _pendingLevelUpCount;
        private readonly Queue<SurvivorSignals.Player.LeveledUp> _pendingLevelUps = new();

        private void BuildStateMachine()
        {
            _stateMachine = new StateMachine<SurvivorGameStageScene, StageEvent>(this);

            _stateMachine.AddTransition<ReadyState, PlayingState>(StageEvent.StartGame);
            _stateMachine.AddTransition<PlayingState, PausedState>(StageEvent.Pause);
            _stateMachine.AddTransition<PlayingState, LevelUpState>(StageEvent.LevelUp);
            _stateMachine.AddTransition<PlayingState, ApparentDeathState>(StageEvent.ApparentDeath);
            _stateMachine.AddTransition<PlayingState, VictoryState>(StageEvent.Victory);
            _stateMachine.AddTransition<PlayingState, GameOverState>(StageEvent.GameOver);
            _stateMachine.AddTransition<PausedState, PlayingState>(StageEvent.Resume);
            _stateMachine.AddTransition<PausedState, RetryState>(StageEvent.Retry);
            _stateMachine.AddTransition<PausedState, QuitToTitleState>(StageEvent.QuitToTitle);
            _stateMachine.AddTransition<LevelUpState, PlayingState>(StageEvent.LevelUpComplete);

            // 仮死状態遷移
            _stateMachine.AddTransition<ApparentDeathState, PlayingState>(StageEvent.Revived);
            _stateMachine.AddTransition<ApparentDeathState, VictoryState>(StageEvent.Victory);
            _stateMachine.AddTransition<ApparentDeathState, GameOverState>(StageEvent.GameOver);

            // ネットワーク切断 (主に P2P Host の Quit) による強制 Title 戻り。Result/Retry/Quit 自身からは遷移不要。
            _stateMachine.AddTransition<ReadyState, QuitToTitleState>(StageEvent.QuitToTitle);
            _stateMachine.AddTransition<PlayingState, QuitToTitleState>(StageEvent.QuitToTitle);
            _stateMachine.AddTransition<LevelUpState, QuitToTitleState>(StageEvent.QuitToTitle);
            _stateMachine.AddTransition<ApparentDeathState, QuitToTitleState>(StageEvent.QuitToTitle);

            // ホスト主導の Lobby 戻り (QuitToTitle と並列)。
            _stateMachine.AddTransition<PlayingState, ReturnToLobbyState>(StageEvent.ReturnToLobby);
            _stateMachine.AddTransition<PausedState, ReturnToLobbyState>(StageEvent.ReturnToLobby);
            _stateMachine.AddTransition<LevelUpState, ReturnToLobbyState>(StageEvent.ReturnToLobby);
            _stateMachine.AddTransition<ApparentDeathState, ReturnToLobbyState>(StageEvent.ReturnToLobby);

            _stateMachine.SetInitState<ReadyState>();
        }

        #endregion

        #region StageStateBase

        private abstract class StageStateBase : State<SurvivorGameStageScene, StageEvent>
        {
            protected IGameSceneService SceneService => Context._sceneService;
            protected IAudioService AudioService => Context._audioService;
            protected IGameRootController GameRootController => Context.GameRootController;
            protected Services.SurvivorStageWaveManager WaveManager => Context._waveManager;
            protected Models.SurvivorStageModel StageModel => Context._stageModel;
            protected Models.SurvivorNetworkStageModel NetworkStageModel => Context._networkStageModel;
            protected SurvivorGameStageSceneComponent View => Context.SceneComponent;

            protected void Transition(StageEvent evt) => StateMachine.Transition(evt);

            protected bool TryGetLocalPlayer(out SurvivorFusionPlayer player)
            {
                return Context._runnerService.TryGetLocalPlayerComponent(out player);
            }

            protected bool TryHandleQuit()
            {
                if (Context._returnToLobbyRequested)
                {
                    Context._returnToLobbyRequested = false;
                    Transition(StageEvent.ReturnToLobby);
                    return true;
                }

                if (Context._returnToTitleRequested)
                {
                    Context._returnToTitleRequested = false;
                    Transition(StageEvent.QuitToTitle);
                    return true;
                }

                return false;
            }

            /// <summary>Fusion Tick が走っていない時のみローカル Time を止める。</summary>
            protected void TryPauseLocalTime()
            {
                if (!Context._runnerService.IsActive) ApplicationEvents.PauseTime();
            }

            /// <summary>Fusion Tick が走っていない時のみローカル Time を戻す (Tick 駆動下では timeScale は終始 1)。</summary>
            protected void TryResumeLocalTime()
            {
                if (!Context._runnerService.IsActive) ApplicationEvents.ResumeTime();
            }
        }

        #endregion

        #region ReadyState

        private class ReadyState : StageStateBase
        {
            private bool _countdownComplete;
            private UniTaskCompletionSource _countdownStartTcs;
            private IDisposable _countdownStartedSubscription;

            public override void Enter()
            {
                Debug.Log("[ReadyState] Enter");
                _countdownComplete = false;
                _countdownStartTcs = new UniTaskCompletionSource();

                // Server からの Countdown 開始命令 (RpcNotifyCountdownStart) を購読。
                // Initialize 完了通知 (RpcClientSceneReady) より前に登録することで、Server 側の RPC が
                // 即座に届いても取りこぼさない。
                _countdownStartedSubscription = Context._countdownStartedSub.Subscribe(_ =>
                    _countdownStartTcs.TrySetResult());

                // 暗転状態を維持（ステージ裏側が見えないように）
                GameRootController?.SetFadeImmediate(1f);

                // StageModel, WaveManagerはSurvivorGameStageScene.Startup()で初期化済み
                View.InitializePlayer(StageModel.CurrentLevelMaster, GameRootController?.MainCamera);

                InitializeAndCountdownAsync().Forget();
            }

            private async UniTaskVoid InitializeAndCountdownAsync()
            {
                var readyAudioTask = AudioService.PlayRandomOneAsync(AudioPlayTag.StageReady);

                // === Phase 1: リソースロード ===
                await View.InitializeWeaponManagerAsync(
                    StageModel.GetStartingWeaponId(),
                    StageModel.GetDamageMultiplier()
                );

                View.InitializeWeaponDisplay();
                await View.InitializeEnemySpawnerAsync(WaveManager);
                await View.InitializeItemSpawnerAsync();

                // MP Client: ネットワーク初期化
                if (Context._runnerService.IsActive)
                {
                    await InitializeClientViewsAsync();
                }

                Debug.Log("[ReadyState] Initialization complete, waiting for camera follow");

                await UniTask.Yield();

                Debug.Log("[ReadyState] Camera ready, fading in");

                // フェードイン
                var fadeTweener = GameRootController.FadeIn(0.5f);
                if (fadeTweener != null)
                {
                    await fadeTweener.ToUniTask();
                }

                // === Phase 2: Loaded 通知 (Server へ) ===
                if (TryGetLocalPlayer(out var localPlayer))
                {
                    localPlayer.RpcClientSceneReady();
                    Debug.Log("[ReadyState] Loaded notification sent to server");
                }

                // === Phase 3: Server からの Countdown 開始命令を待機 ===
                await _countdownStartTcs.Task;
                Debug.Log("[ReadyState] Countdown start signal received from server");

                // === Phase 4: Countdown 実行 (3.5s 固定、Realtime) ===
                // カウントダウン中は時間を停止（敵スポーンやゲーム進行を防ぐ）。
                // MP では Networked IsPaused 機構で Spawner 等を抑制するため timeScale は触らない
                // (Fusion の Tick を止めると同期破綻)。本 State 時点では実際には未スタートのため無害。
                TryPauseLocalTime();

                // カウントダウンダイアログを表示（3, 2, 1, GO!）
                await SceneService.TransitionDialogAsync<
                    SurvivorCountdownDialog,
                    SurvivorCountdownDialogComponent,
                    SurvivorCountdownResult>();

                await readyAudioTask;
                Debug.Log("[ReadyState] Countdown complete");
                AudioService.PlayRandomOneAsync(AudioPlayTag.StageStart).Forget();

                _countdownComplete = true;
            }

            public override void Update()
            {
                if (TryHandleQuit()) return;

                if (_countdownComplete)
                {
                    Transition(StageEvent.StartGame);
                }
            }

            public override void Exit()
            {
                _countdownStartedSubscription?.Dispose();
                Debug.Log("[ReadyState] Exit");
            }

            /// <summary>
            /// MP Client: ネットワークオブジェクトの初期化。
            /// ローカルプレイヤーの NetworkPlayerState をバインドし、
            /// EnemyView / ItemView を生成する。
            /// </summary>
            private async UniTask InitializeClientViewsAsync()
            {
                // EnemyView: Addressableプレハブプリロード → 正式モデルで表示
                var enemyViewGo = new GameObject("[SurvivorEnemyView]");
                enemyViewGo.transform.SetParent(View.transform);
                var enemyView = enemyViewGo.AddComponent<SurvivorEnemyView>();
                await enemyView.InitializeAsync(
                    Context._enemyBatchSub,
                    Context._masterDataService,
                    Context._addressableService,
                    GameRootController?.MainCamera);

                var itemViewGo = new GameObject("[SurvivorItemView]");
                itemViewGo.transform.SetParent(View.transform);
                var itemView = itemViewGo.AddComponent<SurvivorItemView>();
                Context._runnerService.TryGet<SurvivorFusionGameState>(out var itemViewGameState);
                await itemView.InitializeAsync(
                    Context._itemSpawnedSub, Context._itemDespawnedSub,
                    Context._masterDataService,
                    Context._addressableService,
                    itemViewGameState);

                // アイテムプロキシ収集時にサーバーへ通知 (Host-safe ラッパー経由)。
                itemView.OnProxyItemCollected += networkId =>
                {
                    if (TryGetLocalPlayer(out var localPlayer))
                    {
                        localPlayer.SendClientItemCollected(networkId);
                    }
                };
            }
        }

        #endregion

        #region PlayingState

        private class PlayingState : StageStateBase
        {
            private bool _isFirstEntry = true;

            public override void Enter()
            {
                Debug.Log($"[PlayingState] Enter ({Context._runnerService.GetDebugStatus()})");
                DiagDumpPlayingEnter();
                TryResumeLocalTime();
                ApplicationEvents.ShowCursor();

                if (_isFirstEntry)
                {
                    _isFirstEntry = false;
                    View.SetHudVisible(true);
                }

                Context._inputService.EnablePlayer();
                Context._pauseRequested = false;
                // _pendingLevelUpCount はデクリメント方式のためリセットしない
            }

            public override void Update()
            {
                if (TryHandleQuit()) return;

                // ポーズ・レベルアップはクライアントでもローカル処理
                if (Context._pauseRequested)
                {
                    Context._pauseRequested = false;
                    Transition(StageEvent.Pause);
                    return;
                }

                if (Context._pendingLevelUpCount > 0)
                {
                    Context._pendingLevelUpCount--;
                    Transition(StageEvent.LevelUp);
                    return;
                }

                // サーバー権威の勝敗結果
                if (NetworkStageModel.HasNetworkResult)
                {
                    Transition(NetworkStageModel.NetworkResult.IsVictory
                        ? StageEvent.Victory : StageEvent.GameOver);
                    return;
                }

                // GameTime は Server 権威 Networked プロパティ (SurvivorFusionGameState.GameTime) を Reactive Property にミラーするのみ。
                // 各 Client での自走加算は禁止 (時計ずれ防止のため)。Pause 判定は Server 側 FixedUpdateNetwork で実施済み。
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                {
                    NetworkStageModel.GameTime.Value = gs.GameTime;
                    View.UpdateTime(gs.GameTime);
                }

                // 自プレイヤーが死亡 → 仮死状態 (ApparentDeath) へ遷移。
                // 観戦状態で Wave/Time 表示を継続し、全員死亡時のみサーバーから NotifyGameEnded が届く。
                if (StageModel.IsDead)
                {
                    Transition(StageEvent.ApparentDeath);
                    return;
                }
            }

            public override void Exit()
            {
                Debug.Log("[PlayingState] Exit");
            }

            /// <summary>
            /// 症状切り分け診断 (観察期間限定): PlayingState Enter 時点で初期化されているべき
            /// プレイヤー / GameState / Wave 関連の状態を一括ダンプする。
            /// 真因確定後の次 PR で削除すること。
            /// </summary>
            private void DiagDumpPlayingEnter()
            {
                int localPid = Context._runnerService.Runner != null
                    ? Context._runnerService.Runner.LocalPlayer.PlayerId : -1;
                int frame = Time.frameCount;

                Debug.Log($"[DIAG-PlayingEnter][LocalPid={localPid}] frame={frame}");

                // 各 ActivePlayer の状態を全ダンプ
                foreach (var p in Context._runnerService.Runner.ActivePlayers)
                {
                    if (Context._runnerService.TryGetPlayerComponent<SurvivorFusionPlayer>(p, out var fp) && fp != null)
                    {
                        var pc = fp.GetComponent<SurvivorPlayerController>();
                        var pos = fp.transform.position;
                        Animator anim = pc != null ? pc.GetComponentInChildren<Animator>(true) : null;
                        bool animValid = anim != null;
                        bool animEnabled = animValid && anim.enabled;
                        bool animActive = animValid && anim.gameObject.activeInHierarchy;
                        bool animHasController = animValid && anim.runtimeAnimatorController != null;

                        Debug.Log($"[DIAG-PlayingEnter-Player][LocalPid={localPid}] target={p}, " +
                                  $"hasInputAuth={fp.HasInputAuthority}, hasStateAuth={fp.HasStateAuthority}, " +
                                  $"Health={fp.Health}/{fp.MaxHealth}, Stamina={fp.Stamina}/{fp.MaxStamina}, " +
                                  $"Speed={fp.Speed}, IsInvincible={fp.IsInvincible}, " +
                                  $"pos={pos}, " +
                                  $"animator={animValid}, animatorEnabled={animEnabled}, " +
                                  $"animatorActive={animActive}, hasController={animHasController}");
                    }
                    else
                    {
                        Debug.LogWarning($"[DIAG-PlayingEnter-Player][LocalPid={localPid}] target={p} has NO SurvivorFusionPlayer component");
                    }
                }

                // GameState の Networked 状態
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                {
                    Debug.Log($"[DIAG-PlayingEnter-GameState][LocalPid={localPid}] " +
                              $"CurrentWave={gs.CurrentWave}, IsPaused={gs.IsPaused}, " +
                              $"IsAllWavesCleared={gs.IsAllWavesCleared}, " +
                              $"WaveTargetKills={gs.WaveTargetKills}, WaveTotalEnemies={gs.WaveTotalEnemies}, " +
                              $"StageId={gs.StageId}, PlayerId={gs.PlayerId}");
                }
                else
                {
                    Debug.LogWarning($"[DIAG-PlayingEnter-GameState][LocalPid={localPid}] GameState not found");
                }

                // WaveManager / NetworkStageModel (ReadOnlyReactiveProperty は CurrentValue で読む)
                Debug.Log($"[DIAG-PlayingEnter-Wave][LocalPid={localPid}] " +
                          $"WaveManager.CurrentWave={Context._waveManager.CurrentWave.CurrentValue}, " +
                          $"NetworkStageModel.CurrentWave={Context._networkStageModel.CurrentWave.CurrentValue}, " +
                          $"GameTime={Context._networkStageModel.GameTime.Value:F2}, " +
                          $"HasNetworkResult={Context._networkStageModel.HasNetworkResult}");
            }
        }

        #endregion

        #region ApparentDeathState

        /// <summary>
        /// 仮死状態 (HP=0) のクライアントステート。
        /// 自プレイヤーは入力無効化 + 観戦状態で Wave/Time 表示を維持しつつ、
        /// サーバーの <c>NotifyGameEnded</c> (全員死亡 or 時間切れ or 全 Wave クリア) を待つ。
        /// 復活 (<see cref="StageEvent.Revived"/>) は PR4 では発火経路なし、将来 PR で接続。
        /// </summary>
        private class ApparentDeathState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[ApparentDeathState] Enter — player is in apparent death (awaiting revive or session end)");
                Context._inputService.DisablePlayer();
            }

            public override void Update()
            {
                if (TryHandleQuit()) return;

                // サーバー権威の勝敗結果 (全員死亡 / 時間切れ / 全 Wave クリア) を監視
                if (NetworkStageModel.HasNetworkResult)
                {
                    Transition(NetworkStageModel.NetworkResult.IsVictory
                        ? StageEvent.Victory : StageEvent.GameOver);
                    return;
                }

                // Wave/Time 表示更新は継続 (観戦状態)。
                // GameTime は Server 権威 Networked プロパティをミラーするのみ。
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                {
                    NetworkStageModel.GameTime.Value = gs.GameTime;
                    View.UpdateTime(gs.GameTime);
                }
            }

            public override void Exit()
            {
                Debug.Log("[ApparentDeathState] Exit");
                Context._inputService.EnablePlayer();
            }
        }

        #endregion

        #region PausedState

        private class PausedState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[PausedState] Enter");
                TryPauseLocalTime();
                ApplicationEvents.ShowCursor();

                if (TryGetLocalPlayer(out var localPlayer))
                {
                    localPlayer.SendClientRequestPause();
                }

                ShowPauseDialogAsync().Forget();
            }

            public override void Update()
            {
                // Dialog 表示中でもネットワーク切断は最優先で拾い、Title へ強制遷移する。
                TryHandleQuit();
            }

            private async UniTaskVoid ShowPauseDialogAsync()
            {
                // ポーズダイアログを表示（Optionsはダイアログ内で処理される）
                var result = await SurvivorPauseDialog.RunAsync(SceneService);

                switch (result)
                {
                    case SurvivorPauseResult.Resume:
                        Transition(StageEvent.Resume);
                        break;
                    case SurvivorPauseResult.Retry:
                        Transition(StageEvent.Retry);
                        break;
                    case SurvivorPauseResult.Quit:
                        Context.OnRequestQuit();
                        break;
                }
            }

            public override void Exit()
            {
                Debug.Log("[PausedState] Exit");

                if (TryGetLocalPlayer(out var localPlayer))
                {
                    localPlayer.SendClientRequestResume();
                }

                TryResumeLocalTime();
            }
        }

        #endregion

        #region LevelUpState

        private class LevelUpState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log($"[LevelUpState] Enter - Level {StageModel.Level.Value}");
                TryPauseLocalTime();
                Context._inputService.DisablePlayer();
                ApplicationEvents.ShowCursor();

                // サーバー権威の Pause はサーバー側 LevelUpState.Enter で BeginLevelUpPause により即時開始される。
                // クライアント側の Pause RPC は不要 (Resume はサーバーが OnClientWeaponChoice 受信で自動解除)。

                ShowLevelUpDialogAsync().Forget();
            }

            public override void Update()
            {
                // Dialog 表示中でもネットワーク切断は最優先で拾い、Title へ強制遷移する。
                TryHandleQuit();
            }

            private async UniTaskVoid ShowLevelUpDialogAsync()
            {
                // プレイヤーのステータスを更新（移動速度、ピックアップ範囲など）
                UpdatePlayerStats();

                if (View.WeaponManager == null)
                {
                    Transition(StageEvent.LevelUpComplete);
                    return;
                }

                // 武器選択肢: サーバー提供 or ローカル生成
                List<SurvivorWeaponUpgradeOption> options;
                if (Context._pendingLevelUps.Count > 0)
                {
                    var levelUpData = Context._pendingLevelUps.Dequeue();
                    options = (levelUpData.Options != null && levelUpData.Options.Length > 0)
                        ? ConvertNetworkOptions(levelUpData.Options)
                        : new List<SurvivorWeaponUpgradeOption>();
                }
                else
                {
                    options = View.WeaponManager.GetUpgradeOptions(StageModel.WeaponChoiceCount.Value);
                }

                if (options.Count == 0)
                {
                    Transition(StageEvent.LevelUpComplete);
                    return;
                }

                // 武器選択ループ（入れ替えキャンセル時に戻れるように）
                while (true)
                {
                    var result = await SceneService.TransitionDialogAsync<
                        SurvivorPlayerLevelUpDialog,
                        SurvivorPlayerLevelUpDialogComponent,
                        SurvivorPlayerLevelUpDialogArg,
                        SurvivorWeaponUpgradeOption
                    >(new(options, StageModel.Level.Value));

                    // ×ボタンでキャンセル → 武器取得なしでゲーム続行
                    if (result == null)
                    {
                        break;
                    }

                    // 新規武器 かつ スロット満杯の場合
                    if (result.IsNewWeapon && !View.WeaponManager.HasEmptySlot)
                    {
                        // 武器入れ替えダイアログを表示
                        var removeWeaponId = await SurvivorWeaponReplaceDialog.RunAsync(
                            SceneService,
                            new(result, View.WeaponManager.Weapons));

                        if (removeWeaponId.HasValue)
                        {
                            await View.WeaponManager.ReplaceWeaponAsync(
                                removeWeaponId.Value,
                                result.WeaponId);
                            if (TryGetLocalPlayer(out var rp))
                            {
                                rp.SendClientWeaponReplace(removeWeaponId.Value, result.WeaponId);
                            }
                            break;
                        }

                        // キャンセル時はループ継続（武器選択に戻る）
                        continue;
                    }
                    else
                    {
                        await View.WeaponManager.ApplyUpgradeOptionAsync(result);
                        if (TryGetLocalPlayer(out var cp))
                        {
                            cp.SendClientWeaponChoice(result.WeaponId, result.IsNewWeapon);
                        }
                        break;
                    }
                }

                View.WeaponManager.UpdateDamageMultiplier(StageModel.GetDamageMultiplier());

                // Resume はサーバー側で OnClientWeaponChoice → EndLevelUpPause により自動的に行われる。
                // クライアントは ApplyUpgradeOptionAsync 経由で RpcClientWeaponChoice を送信済みのため明示的 Resume RPC は不要。

                Transition(StageEvent.LevelUpComplete);
            }

            private void UpdatePlayerStats()
            {
                if (View.PlayerController != null && StageModel.CurrentLevelMaster != null)
                {
                    View.PlayerController.UpdateLevelStats(StageModel.CurrentLevelMaster);
                }
            }

            public override void Exit()
            {
                Debug.Log("[LevelUpState] Exit");
                TryResumeLocalTime();

                // MP で他プレイヤーがまだ LevelUp 中（IsPaused=true 維持）なら入力を有効化しない。
                // 解除は Game.Resumed シグナル経由で行う（VS Co-op 準拠：全員選択完了まで全員入力停止）。
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs) && gs.IsEffectivelyPaused)
                {
                    return;
                }
                Context._inputService.EnablePlayer();
            }

            /// <summary>
            /// サーバーから受信した最小構造体をマスターデータで補完し、UI 用オプションに変換する。
            /// </summary>
            private List<SurvivorWeaponUpgradeOption> ConvertNetworkOptions(
                SurvivorNetworkWeaponUpgradeOption[] networkOptions)
            {
                var result = new List<SurvivorWeaponUpgradeOption>(networkOptions.Length);
                var memDb = Context._masterDataService.MemoryDatabase;

                foreach (var opt in networkOptions)
                {
                    memDb.SurvivorWeaponMasterTable.TryFindById(opt.WeaponId, out var weaponMaster);

                    string upgradeEffect = null;
                    if (!opt.IsNewWeapon)
                    {
                        var nextLevel = opt.CurrentLevel + 1;
                        if (memDb.SurvivorWeaponLevelMasterTable.TryFindByWeaponIdAndLevel(
                            (opt.WeaponId, nextLevel), out var levelMaster))
                        {
                            upgradeEffect = levelMaster.Description;
                        }
                    }

                    result.Add(new SurvivorWeaponUpgradeOption
                    {
                        WeaponId = opt.WeaponId,
                        WeaponName = weaponMaster?.Name ?? "",
                        IsNewWeapon = opt.IsNewWeapon,
                        CurrentLevel = opt.CurrentLevel,
                        Description = weaponMaster?.Description ?? "",
                        UpgradeEffect = upgradeEffect,
                        IconAssetName = weaponMaster?.IconAssetName ?? ""
                    });
                }
                return result;
            }
        }

        #endregion

        #region VictoryState

        private class VictoryState : StageStateBase
        {
            private const int ResultDisplayDuration = 2000;

            public override void Enter()
            {
                Debug.Log("[VictoryState] Enter");

                // ゲーム状態をフリーズ（スコア稼ぎ防止）。MP ではサーバー側 Tick を止めないよう
                // timeScale を触らず、IsPaused / 入力無効化で代替。
                TryPauseLocalTime();
                Context._inputService.DisablePlayer();

                // 残存敵を全クリア＆スポーン停止
                View.EnemySpawner?.ClearAllEnemies();

                // HUDを非表示
                View.SetHudVisible(false);

                ApplicationEvents.ShowCursor();
                View.ShowVictory();

                // 保存完了を待機してからリザルト画面へ遷移
                SaveAndTransitionToResultAsync().Forget();
            }

            private async UniTaskVoid SaveAndTransitionToResultAsync()
            {
                // クリア記録を保存
                var score = StageModel.Score.Value;
                var totalKillsRaw = StageModel.TotalKills.Value;
                var totalTargetKills = Context._waveManager.TotalTargetKills;
                var kills = StageModel.GetCappedKills(totalTargetKills);
                // サーバー権威 ClearTime を優先 (NotifyGameEnded RPC で broadcast 済み)。
                // ローカル GameTime はサーバー側 Victory 検知から RPC 到達までのラグで進んでいるため、
                // NetworkResult.ClearTime を使うと Host / Client 間で時間値が一致する。
                var clearTime = NetworkStageModel.HasNetworkResult
                    ? NetworkStageModel.NetworkResult.ClearTime
                    : NetworkStageModel.GameTime.Value;
                var isTimeUp = NetworkStageModel.IsTimeUp;
                var hpRatio = StageModel.GetHpRatio();

                Debug.Log($"[VictoryState] Saving result: score={score}, kills={kills} (raw={totalKillsRaw}, target={totalTargetKills}), clearTime={clearTime:F2}s, isTimeUp={isTimeUp}, hpRatio={hpRatio:P0}");

                Context._saveService.CompleteCurrentStage(score, kills, clearTime, true, isTimeUp, hpRatio);
                await Context._saveService.SaveAsync();
                Context._isResultSaved = true;

                Debug.Log("[VictoryState] Result saved successfully");

                // Victory表示の待機（保存処理と並行して最低2秒は表示）
                await UniTask.Delay(ResultDisplayDuration, DelayType.Realtime);

                // 遷移前に時間を再開
                ApplicationEvents.ResumeTime();
                await SceneService.TransitionAsync<SurvivorTotalResultScene, bool>(Context._sessionConfig.IsMultiPlayer());
            }

            public override void Exit() => Debug.Log("[VictoryState] Exit");
        }

        #endregion

        #region GameOverState

        private class GameOverState : StageStateBase
        {
            private const int ResultDisplayDuration = 2000;

            public override void Enter()
            {
                Debug.Log("[GameOverState] Enter");

                // ゲーム状態をフリーズ。MP ではサーバー側 Tick を止めないよう
                // timeScale を触らず、IsPaused / 入力無効化で代替。
                TryPauseLocalTime();
                Context._inputService.DisablePlayer();

                // 残存敵を全クリア＆スポーン停止
                View.EnemySpawner?.ClearAllEnemies();

                // HUDを非表示
                View.SetHudVisible(false);

                ApplicationEvents.ShowCursor();
                View.ShowGameOver();

                // 保存完了を待機してからリザルト画面へ遷移
                SaveAndTransitionToResultAsync().Forget();
            }

            private async UniTaskVoid SaveAndTransitionToResultAsync()
            {
                // ゲームオーバー記録を保存
                var score = StageModel.Score.Value;
                var kills = StageModel.GetCappedKills(Context._waveManager.TotalTargetKills);
                // サーバー権威 ClearTime を優先 (Victory と同じ理由でラグ排除)。
                var clearTime = NetworkStageModel.HasNetworkResult
                    ? NetworkStageModel.NetworkResult.ClearTime
                    : NetworkStageModel.GameTime.Value;
                var hpRatio = 0f; // ゲームオーバーなのでHP=0

                Debug.Log($"[GameOverState] Saving result: score={score}, kills={kills}, clearTime={clearTime:F2}s, hpRatio={hpRatio:P0}");

                Context._saveService.CompleteCurrentStage(score, kills, clearTime, false, false, hpRatio);
                await Context._saveService.SaveAsync();

                Context._isResultSaved = true;

                Debug.Log("[GameOverState] Result saved successfully");

                // GameOver表示の待機（保存処理と並行して最低2秒は表示）
                await UniTask.Delay(ResultDisplayDuration, DelayType.Realtime);

                // 遷移前に時間を再開
                ApplicationEvents.ResumeTime();
                await SceneService.TransitionAsync<SurvivorTotalResultScene, bool>(Context._sessionConfig.IsMultiPlayer());
            }

            public override void Exit() => Debug.Log("[GameOverState] Exit");
        }

        #endregion

        #region RetryState

        private class RetryState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[RetryState] Enter");

                // Fusion: quit は Disconnect で処理（Terminate() 内）

                // Retryフラグを設定（Terminate()でセーブデータ更新をスキップ）
                Context._retryOrQuit = true;

                // 現在のセッション情報を取得
                var session = Context._saveService.CurrentSession;
                if (session == null)
                {
                    Debug.LogError("[RetryState] No active session found!");
                    return;
                }

                var stageId = session.StageId;
                var playerId = session.PlayerId;
                var stageGroupId = session.StageGroupId;

                // 新しいセッションで上書き（古いセッションをリセット）
                Context._saveService.StartSession(stageId, playerId, stageGroupId);

                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();
                RetryStageAsync().Forget();
            }

            private async UniTaskVoid RetryStageAsync()
            {
                // ConnectingScene 経由で再接続 → StageScene
                await SceneService.TransitionAsync<SurvivorStageConnectScene>();
            }

            public override void Exit() => Debug.Log("[RetryState] Exit");
        }

        #endregion

        #region QuitToTitleState

        private class QuitToTitleState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[QuitToTitleState] Enter");

                // Fusion: quit は Disconnect で処理（Terminate() 内）

                // Quitフラグを設定（Terminate()でセーブデータ更新をスキップ）
                Context._retryOrQuit = true;

                // セッションを終了（保存データを更新せずに破棄）
                Context._saveService.EndSession();

                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();
                TransitionToTitleAsync().Forget();
            }

            private async UniTaskVoid TransitionToTitleAsync()
            {
                // タイトル画面へ直接遷移（リザルト画面をスキップ）
                await SceneService.TransitionAsync<SurvivorTitleScene>();
            }

            public override void Exit() => Debug.Log("[QuitToTitleState] Exit");
        }

        #endregion

        #region ReturnToLobbyState

        /// <summary>
        /// ホスト主導の Lobby 戻り State。<see cref="QuitToTitleState"/> と対称的に Title ではなく
        /// Lobby (LobbyRoomScene / LobbyScene) へ遷移する。
        /// </summary>
        private class ReturnToLobbyState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[ReturnToLobbyState] Enter");

                Context._retryOrQuit = true;
                Context._saveService.EndSession();
                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();
                TransitionToLobbyAsync().Forget();
            }

            private async UniTaskVoid TransitionToLobbyAsync()
            {
                try
                {
                    await Context._saveService.SaveIfDirtyAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ReturnToLobbyState] SaveIfDirty failed: {ex.Message}");
                }

                try
                {
                    await Context._networkConnector.DisconnectAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ReturnToLobbyState] Disconnect failed: {ex.Message}");
                }

                Game.Library.Shared.Dto.LobbyInfo lobby = null;
                try
                {
                    lobby = await Context._lobbyClient.GetMyLobbyAsync();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ReturnToLobbyState] GetMyLobby failed: {ex.Message}");
                }

                if (lobby != null && !string.IsNullOrEmpty(lobby.LobbyId))
                {
                    if (string.IsNullOrEmpty(Context._lobbyClient.CurrentLobbyId))
                    {
                        try
                        {
                            var playerName = Context._authSessionService.UserName ?? "Player";
                            await Context._lobbyClient.ConnectToLobbyAsync(lobby.LobbyId, playerName);
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[ReturnToLobbyState] Hub reconnect failed: {ex.Message}. Falling back to lobby list.");
                            await SceneService.TransitionAsync<SurvivorLobbyScene>();
                            return;
                        }
                    }
                    await SceneService.TransitionAsync<SurvivorLobbyRoomScene>();
                    return;
                }

                await SceneService.TransitionAsync<SurvivorLobbyScene>();
            }

            public override void Exit() => Debug.Log("[ReturnToLobbyState] Exit");
        }

        #endregion
    }
}
