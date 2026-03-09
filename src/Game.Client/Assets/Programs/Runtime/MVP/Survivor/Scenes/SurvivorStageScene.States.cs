using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Library.Shared.Enums;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Weapon;
using Game.Library.Shared;
using Game.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Network;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Services;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.MVP.Survivor.Scenes
{
    public partial class SurvivorStageScene
    {
        #region StateMachine

        private enum StageEvent
        {
            StartGame,
            Pause,
            Resume,
            LevelUp,
            LevelUpComplete,
            Victory,
            GameOver,
            Retry,
            QuitToTitle
        }

        private StateMachine<SurvivorStageScene, StageEvent> _stateMachine;
        private bool _isResultSaved;
        private bool _retryOrQuit;
        private bool _pauseRequested;
        private bool _levelUpRequested;

        private void BuildStateMachine()
        {
            _stateMachine = new StateMachine<SurvivorStageScene, StageEvent>(this);

            _stateMachine.AddTransition<ReadyState, PlayingState>(StageEvent.StartGame);
            _stateMachine.AddTransition<PlayingState, PausedState>(StageEvent.Pause);
            _stateMachine.AddTransition<PlayingState, LevelUpState>(StageEvent.LevelUp);
            _stateMachine.AddTransition<PlayingState, VictoryState>(StageEvent.Victory);
            _stateMachine.AddTransition<PlayingState, GameOverState>(StageEvent.GameOver);
            _stateMachine.AddTransition<PausedState, PlayingState>(StageEvent.Resume);
            _stateMachine.AddTransition<PausedState, RetryState>(StageEvent.Retry);
            _stateMachine.AddTransition<PausedState, QuitToTitleState>(StageEvent.QuitToTitle);
            _stateMachine.AddTransition<LevelUpState, PlayingState>(StageEvent.LevelUpComplete);

            _stateMachine.SetInitState<ReadyState>();
        }

        #endregion

        #region StageStateBase

        private abstract class StageStateBase : State<SurvivorStageScene, StageEvent>
        {
            protected IGameSceneService SceneService => Context._sceneService;
            protected IAudioService AudioService => Context._audioService;
            protected IGameRootController GameRootController => Context.GameRootController;
            protected Services.SurvivorStageWaveManager WaveManager => Context._waveManager;
            protected Models.SurvivorStageModel StageModel => Context._stageModel;
            protected SurvivorStageSceneComponent View => Context.SceneComponent;
            protected ISurvivorStageSceneView StageSceneView => Context._stageSceneView;

            protected void Transition(StageEvent evt) => StateMachine.Transition(evt);
        }

        #endregion

        #region ReadyState

        private class ReadyState : StageStateBase
        {
            private bool _countdownComplete;

            public override void Enter()
            {
                Debug.Log("[ReadyState] Enter");

                // 時間は動かしておく（Cinemachineカメラ追従のため）
                // カウントダウン開始時に停止する
                _countdownComplete = false;

                // 暗転状態を維持（ステージ裏側が見えないように）
                GameRootController?.SetFadeImmediate(1f);

                // StageModel, WaveManagerはSurvivorStageScene.Startup()で初期化済み
                View.InitializePlayer(StageModel.CurrentLevelMaster, GameRootController?.MainCamera);

                InitializeAndCountdownAsync().Forget();
            }

            private async UniTaskVoid InitializeAndCountdownAsync()
            {
                var readyAudioTask = AudioService.PlayRandomOneAsync(AudioPlayTag.StageReady);

                // ゲームコンポーネントの初期化
                await View.InitializeWeaponManagerAsync(
                    StageModel.GetStartingWeaponId(),
                    StageModel.GetDamageMultiplier()
                );
                StageSceneView.InitializeWeaponDisplay();
                await View.InitializeEnemySpawnerAsync(WaveManager);
                await View.InitializeItemSpawnerAsync();

                // ネットワーク初期化
                // SurvivorStageConnectScene で接続確立済みのため、NetworkClient.localPlayer は利用可能
                if (NetworkModeHelper.IsNetworkClientConnected)
                {
                    InitializeClientViews();
                }
                else if (NetworkModeHelper.IsNetworkServer)
                {
                    // Server-only: VContainer Inject でネットワークオブジェクトの IPublisher を解決
                    InitializeServerViews();
                }

                Debug.Log("[ReadyState] Initialization complete, waiting for camera follow");

                if (UnityPlaymodeHelper.IsServer())
                {
                    // サーバー: 全クライアントのシーン準備完了を待機してからゲーム開始
                    Debug.Log("[ReadyState] Server: waiting for all clients scene ready...");
                    await WaitForAllClientsSceneReadyAsync();
                    Debug.Log("[ReadyState] Server: all clients ready, starting game");
                    _countdownComplete = true;
                }
                else
                {
                    await UniTask.Yield();

                    Debug.Log("[ReadyState] Camera ready, fading in");

                    // フェードイン
                    var fadeTweener = GameRootController.FadeIn(0.5f);
                    if (fadeTweener != null)
                    {
                        await fadeTweener.ToUniTask();
                    }

                    Debug.Log("[ReadyState] Showing countdown");

                    // カウントダウン中は時間を停止（敵スポーンやゲーム進行を防ぐ）
                    ApplicationEvents.PauseTime();

                    // カウントダウンダイアログを表示（3, 2, 1, GO!）
                    await SceneService.TransitionDialogAsync<
                        SurvivorCountdownDialog,
                        SurvivorCountdownDialogComponent,
                        SurvivorCountdownResult>();

                    await readyAudioTask;
                    Debug.Log("[ReadyState] Countdown complete");
                    AudioService.PlayRandomOneAsync(AudioPlayTag.StageStart).Forget();

                    // サーバーに準備完了を通知（サーバーはこれを受けてゲーム開始）
                    Context._localPlayerState?.NotifySceneReadyServerRpc();
                    Debug.Log("[ReadyState] Scene ready notification sent to server");

                    _countdownComplete = true;
                }
            }

            public override void Update()
            {
                // カウントダウン完了後にゲーム開始
                if (_countdownComplete)
                {
                    Transition(StageEvent.StartGame);
                }
            }

            public override void Exit() => Debug.Log("[ReadyState] Exit");

            /// <summary>
            /// サーバー: 全クライアントが NotifySceneReadyServerRpc を送信するまで待機。
            /// タイムアウト付き（30秒）で、クライアント切断に対応。
            /// </summary>
            private async UniTask WaitForAllClientsSceneReadyAsync()
            {
                var gm = SurvivorNetworkGameManager.Instance;
                if (gm == null)
                {
                    Debug.LogWarning("[ReadyState] GameManager not found, skipping wait");
                    return;
                }

                gm.ResetSceneReadyTracking();

                var tcs = new UniTaskCompletionSource();
                void OnReady() => tcs.TrySetResult();
                gm.OnAllClientsSceneReady += OnReady;

                try
                {
                    var winIndex = await UniTask.WhenAny(
                        tcs.Task,
                        UniTask.Delay(TimeSpan.FromSeconds(30), DelayType.Realtime)
                    );
                    if (winIndex == 1)
                    {
                        Debug.LogWarning("[ReadyState] Timeout waiting for clients scene ready, proceeding anyway");
                    }
                }
                finally
                {
                    gm.OnAllClientsSceneReady -= OnReady;
                }
            }

            private void InitializeClientViews()
            {
                // MP Client: NetworkBehaviour への DI 注入は不要
                // （NetworkMessage + RegisterHandler パターンにより VContainer/MessagePipe 依存を排除済み）

                // ローカルプレイヤーの NetworkSurvivorPlayerState を取得
                if (NetworkModeHelper.TryGetLocalPlayerComponent<SurvivorNetworkPlayerState>(out var localPlayerState))
                {
                    Context._localPlayerState = localPlayerState;
                    Debug.Log("[ReadyState] Local NetworkSurvivorPlayerState bound");

                    // PlayerController に NetworkPlayerState をバインド
                    // → ClientInputProvider 生成 → SendMoveInputServerRpc でサーバーに入力送信
                    var playerController = View.PlayerController;
                    if (playerController != null)
                    {
                        playerController.BindNetworkPlayerState(Context._localPlayerState);
                        Debug.Log("[ReadyState] Client: PlayerController bound to NetworkPlayerState");
                    }
                }

                // View に ISubscriber を注入（AddComponent なので Initialize 経由）
                var enemyViewGo = new GameObject("[SurvivorEnemyView]");
                enemyViewGo.transform.SetParent(View.transform);
                enemyViewGo.AddComponent<SurvivorEnemyView>().Initialize(Context._enemyBatchSub);

                var itemViewGo = new GameObject("[SurvivorItemView]");
                itemViewGo.transform.SetParent(View.transform);
                itemViewGo.AddComponent<SurvivorItemView>().Initialize(
                    Context._itemSpawnedSub, Context._itemDespawnedSub);
            }

            private void InitializeServerViews()
            {
                // サーバー側プレイヤーコントローラーに NetworkPlayerState をバインド
                // → ServerInputProvider（ServerRpc 受信入力）+ StateSynchronizer（SyncVar 送信）が有効化
                var playerController = View.PlayerController;
                if (playerController != null)
                {
                    foreach (var nps in NetworkModeHelper.GetNetworkPlayerComponents<SurvivorNetworkPlayerState>())
                    {
                        if (nps != null)
                        {
                            playerController.BindNetworkPlayerState(nps);

                            // エネミースポーナーにプレイヤー Transform を登録
                            // サーバーのプレイヤーコントローラーが物理演算を行うため、その Transform を使用
                            View.EnemySpawner?.AddPlayer(playerController.transform);
                            break; // 現在は1プレイヤー対応
                        }
                    }
                }

                Debug.Log("[ReadyState] Server-only: network objects injected, player bound to NetworkPlayerState");
            }

        }

        #endregion

        #region PlayingState

        private class PlayingState : StageStateBase
        {
            private bool _isFirstEntry = true;
            private bool _disconnected;

            public override void Enter()
            {
                Debug.Log($"[PlayingState] Enter (isClient={Context._isClient}, isServer={UnityPlaymodeHelper.IsServer()}, {NetworkModeHelper.GetDebugStatus()})");
                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();

                _disconnected = false;

                // Mirror 切断検知（MP モード）
                if (Context._isClient)
                {
                    NetworkModeHelper.OnClientDisconnected += OnDisconnected;
                }

                // 初回（ReadyStateからの遷移）のみWaveを開始
                // LevelUpStateやPausedStateからの復帰時はWaveを開始しない
                if (_isFirstEntry)
                {
                    _isFirstEntry = false;

                    // SP / Server: ローカルで Wave 開始
                    // MP Client: サーバーが Wave 開始 → ClientRpc で通知
                    if (!Context._isClient)
                    {
                        Debug.Log("[PlayingState] Starting first wave (server/SP)");
                        WaveManager.StartWave();
                    }
                    else
                    {
                        Debug.Log("[PlayingState] MP Client: wave start driven by server");
                    }

                    // HUDをフェードイン表示（カウントダウン後、初めてPlayingStateに入った時）
                    StageSceneView.SetHudVisible(true);
                }

                Context._inputService.EnablePlayer();
                Context._pauseRequested = false;
                Context._levelUpRequested = false;
            }

            public override void Update()
            {
                // 切断検知 → タイトルに戻る
                if (_disconnected)
                {
                    _disconnected = false;
                    Transition(StageEvent.QuitToTitle);
                    return;
                }

                // ポーズ・レベルアップはクライアントでもローカル処理
                if (Context._pauseRequested)
                {
                    Context._pauseRequested = false;
                    Transition(StageEvent.Pause);
                    return;
                }

                if (Context._levelUpRequested)
                {
                    Context._levelUpRequested = false;
                    Transition(StageEvent.LevelUp);
                    return;
                }

                // クライアントモード: サーバー権威
                if (Context._isClient)
                {
                    // サーバーからの勝敗結果を確認
                    if (StageModel.HasNetworkResult)
                    {
                        Transition(StageModel.NetworkResult.IsVictory
                            ? StageEvent.Victory : StageEvent.GameOver);
                        return;
                    }

                    // HUDタイマー表示はローカル累積（サーバーと概ね同期）
                    StageModel.GameTime.Value += Time.deltaTime;
                    StageSceneView.UpdateTime(StageModel.GameTime.Value);

                    // 勝敗判定はサーバーが Game.Ended ClientRpc で通知
                    return;
                }

                // SP / Server: ローカルシミュレーション
                StageModel.GameTime.Value += Time.deltaTime;
                StageSceneView.UpdateTime(StageModel.GameTime.Value);

                // 勝利条件: 時間制限到達 or 全ウェーブクリア
                if (StageModel.IsTimeUp || WaveManager.IsAllWavesCleared.CurrentValue)
                {
                    Transition(StageEvent.Victory);
                    return;
                }

                // 敗北条件: HP0
                if (StageModel.IsDead)
                {
                    Transition(StageEvent.GameOver);
                    return;
                }
            }

            public override void Exit()
            {
                Debug.Log("[PlayingState] Exit");
                NetworkModeHelper.OnClientDisconnected -= OnDisconnected;
            }

            private void OnDisconnected()
            {
                Debug.LogWarning("[PlayingState] Mirror server disconnected");
                _disconnected = true;
            }
        }

        #endregion

        #region PausedState

        private class PausedState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[PausedState] Enter");
                ApplicationEvents.PauseTime();
                ApplicationEvents.ShowCursor();
                ShowPauseDialogAsync().Forget();
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
                        Transition(StageEvent.QuitToTitle);
                        break;
                }
            }

            public override void Exit()
            {
                Debug.Log("[PausedState] Exit");
                ApplicationEvents.ResumeTime();
            }
        }

        #endregion

        #region LevelUpState

        private class LevelUpState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log($"[LevelUpState] Enter - Level {StageModel.Level.Value}");
                ApplicationEvents.PauseTime();
                ApplicationEvents.ShowCursor();
                ShowLevelUpDialogAsync().Forget();
            }

            private async UniTaskVoid ShowLevelUpDialogAsync()
            {
                // プレイヤーのステータスを更新（移動速度、ピックアップ範囲など）
                UpdatePlayerStats();

                // サーバー: ステータス更新のみ、武器選択はクライアントの ServerRpc で受信
                if (UnityPlaymodeHelper.IsServer())
                {
                    Transition(StageEvent.LevelUpComplete);
                    return;
                }

                if (View.WeaponManager == null)
                {
                    Transition(StageEvent.LevelUpComplete);
                    return;
                }

                var options = View.WeaponManager.GetUpgradeOptions(StageModel.WeaponChoiceCount.Value);

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
                            // 入れ替え実行
                            await View.WeaponManager.ReplaceWeaponAsync(
                                removeWeaponId.Value,
                                result.WeaponId);
                            Context._localPlayerState?.SendWeaponReplaceServerRpc(
                                removeWeaponId.Value, result.WeaponId);
                            break; // 成功したらループを抜ける
                        }

                        // キャンセル時はループ継続（武器選択に戻る）
                        continue;
                    }
                    else
                    {
                        // 通常の武器追加/アップグレード
                        await View.WeaponManager.ApplyUpgradeOptionAsync(result);
                        Context._localPlayerState?.SendWeaponChoiceServerRpc(
                            result.WeaponId, result.IsNewWeapon);
                        break; // 成功したらループを抜ける
                    }
                }

                View.WeaponManager.UpdateDamageMultiplier(StageModel.GetDamageMultiplier());
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
                ApplicationEvents.ResumeTime();
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

                // ゲーム状態をフリーズ（スコア稼ぎ防止）
                ApplicationEvents.PauseTime();
                Context._inputService.DisablePlayer();

                // HUDを非表示
                StageSceneView.SetHudVisible(false);

                ApplicationEvents.ShowCursor();
                StageSceneView.ShowVictory();

                // 保存完了を待機してからリザルト画面へ遷移
                SaveAndTransitionToResultAsync().Forget();
            }

            private async UniTaskVoid SaveAndTransitionToResultAsync()
            {
                // クリア記録を保存
                var score = StageModel.Score.Value;
                var kills = Context.GetCappedKills();
                var clearTime = StageModel.GameTime.Value;
                var isTimeUp = StageModel.IsTimeUp;
                var hpRatio = Context.GetHpRatio();

                Debug.Log($"[VictoryState] Saving result: score={score}, kills={kills}, clearTime={clearTime:F2}s, isTimeUp={isTimeUp}, hpRatio={hpRatio:P0}");

                Context._saveService.CompleteCurrentStage(score, kills, clearTime, true, isTimeUp, hpRatio);
                await Context._saveService.SaveAsync();
                Context._isResultSaved = true;

                // サーバー/ホスト: クライアントに勝利を通知
                if (!Context._isClient && Context._localPlayerState != null)
                {
                    var result = new SurvivorNetworkGameResult
                    {
                        IsVictory = true,
                        ClearTime = clearTime
                    };
                    Context._localPlayerState.ReportGameEndServerRpc(result);
                }

                Debug.Log("[VictoryState] Result saved successfully");

                // サーバー: リザルト画面は不要
                if (UnityPlaymodeHelper.IsServer())
                {
                    ApplicationEvents.ResumeTime();
                    return;
                }

                // Victory表示の待機（保存処理と並行して最低2秒は表示）
                await UniTask.Delay(ResultDisplayDuration, DelayType.Realtime);

                // 遷移前に時間を再開
                ApplicationEvents.ResumeTime();
                await SceneService.TransitionAsync<SurvivorTotalResultScene>();
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

                // ゲーム状態をフリーズ
                ApplicationEvents.PauseTime();
                Context._inputService.DisablePlayer();

                // HUDを非表示
                StageSceneView.SetHudVisible(false);

                ApplicationEvents.ShowCursor();
                StageSceneView.ShowGameOver();

                // 保存完了を待機してからリザルト画面へ遷移
                SaveAndTransitionToResultAsync().Forget();
            }

            private async UniTaskVoid SaveAndTransitionToResultAsync()
            {
                // ゲームオーバー記録を保存
                var score = StageModel.Score.Value;
                var kills = Context.GetCappedKills();
                var clearTime = StageModel.GameTime.Value;
                var hpRatio = 0f; // ゲームオーバーなのでHP=0

                Debug.Log($"[GameOverState] Saving result: score={score}, kills={kills}, clearTime={clearTime:F2}s, hpRatio={hpRatio:P0}");

                Context._saveService.CompleteCurrentStage(score, kills, clearTime, false, false, hpRatio);
                await Context._saveService.SaveAsync();

                Context._isResultSaved = true;

                // サーバー/ホスト: クライアントに敗北を通知
                if (!Context._isClient && Context._localPlayerState != null)
                {
                    var result = new SurvivorNetworkGameResult
                    {
                        IsVictory = false,
                        ClearTime = clearTime
                    };
                    Context._localPlayerState.ReportGameEndServerRpc(result);
                }

                Debug.Log("[GameOverState] Result saved successfully");

                // サーバー: リザルト画面は不要
                if (UnityPlaymodeHelper.IsServer())
                {
                    ApplicationEvents.ResumeTime();
                    return;
                }

                // GameOver表示の待機（保存処理と並行して最低2秒は表示）
                await UniTask.Delay(ResultDisplayDuration, DelayType.Realtime);

                // 遷移前に時間を再開
                ApplicationEvents.ResumeTime();
                await SceneService.TransitionAsync<SurvivorTotalResultScene>();
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
    }
}
