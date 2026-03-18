using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Client.MasterData;
using Game.Library.Shared.Enums;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Weapon;
using Game.Library.Shared;
using Game.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;

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
        private int _pendingLevelUpCount;
        private readonly Queue<SurvivorSignals.Player.LeveledUp> _pendingLevelUps = new();

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

            protected void Transition(StageEvent evt) => StateMachine.Transition(evt);

            protected bool TryGetLocalPlayer(out SurvivorFusionPlayer player)
            {
                return Context._runnerService.TryGetLocalPlayerComponent(out player);
            }
        }

        #endregion

        #region ReadyState

        private class ReadyState : StageStateBase
        {
            private bool _countdownComplete;

            public override void Enter()
            {
                Debug.Log("[ReadyState] Enter");
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
                if (TryGetLocalPlayer(out var localPlayer))
                {
                    localPlayer.RpcClientSceneReady();
                }
                Debug.Log("[ReadyState] Scene ready notification sent to server");

                _countdownComplete = true;
            }

            public override void Update()
            {
                if (_countdownComplete)
                {
                    Transition(StageEvent.StartGame);
                }
            }

            public override void Exit() => Debug.Log("[ReadyState] Exit");

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
                    Context.ScopedResolver.Resolve<IMasterDataService>(),
                    Context._addressableService);

                var itemViewGo = new GameObject("[SurvivorItemView]");
                itemViewGo.transform.SetParent(View.transform);
                var itemView = itemViewGo.AddComponent<SurvivorItemView>();
                await itemView.InitializeAsync(
                    Context._itemSpawnedSub, Context._itemDespawnedSub,
                    Context.ScopedResolver.Resolve<IMasterDataService>(),
                    Context._addressableService);

                // アイテムプロキシ収集時にサーバーへ RPC 送信
                itemView.OnProxyItemCollected += itemId =>
                {
                    if (TryGetLocalPlayer(out var localPlayer))
                    {
                        localPlayer.RpcClientItemCollected(itemId);
                    }
                };
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
                Debug.Log($"[PlayingState] Enter ({Context._runnerService.GetDebugStatus()})");
                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();

                _disconnected = false;

                Context._runnerService.OnClientDisconnected += OnDisconnected;

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

                if (Context._pendingLevelUpCount > 0)
                {
                    Context._pendingLevelUpCount--;
                    Transition(StageEvent.LevelUp);
                    return;
                }

                // サーバー権威の勝敗結果
                if (StageModel.HasNetworkResult)
                {
                    Transition(StageModel.NetworkResult.IsVictory
                        ? StageEvent.Victory : StageEvent.GameOver);
                    return;
                }

                StageModel.GameTime.Value += Time.deltaTime;
                View.UpdateTime(StageModel.GameTime.Value);

                // 安全ネット: HP=0 で GameOver（サーバー Game.Ended が遅延した場合）
                if (StageModel.IsDead)
                {
                    Transition(StageEvent.GameOver);
                    return;
                }
            }

            public override void Exit()
            {
                Debug.Log("[PlayingState] Exit");
                Context._runnerService.OnClientDisconnected -= OnDisconnected;
            }

            private void OnDisconnected()
            {
                Debug.LogWarning("[PlayingState] Server disconnected");
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

                if (TryGetLocalPlayer(out var localPlayer))
                {
                    localPlayer.RpcClientRequestPause();
                }

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

                if (TryGetLocalPlayer(out var localPlayer))
                {
                    localPlayer.RpcClientRequestResume();
                }

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
                Context._inputService.DisablePlayer();
                ApplicationEvents.ShowCursor();

                if (TryGetLocalPlayer(out var localPlayer))
                {
                    localPlayer.RpcClientRequestPause();
                }

                ShowLevelUpDialogAsync().Forget();
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
                                rp.RpcClientWeaponReplace(removeWeaponId.Value, result.WeaponId);
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
                            cp.RpcClientWeaponChoice(result.WeaponId, result.IsNewWeapon);
                        }
                        break;
                    }
                }

                View.WeaponManager.UpdateDamageMultiplier(StageModel.GetDamageMultiplier());

                if (TryGetLocalPlayer(out var resumePlayer))
                {
                    resumePlayer.RpcClientRequestResume();
                }

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
                Context._inputService.EnablePlayer();
            }

            /// <summary>
            /// サーバーから受信した最小構造体をマスターデータで補完し、UI 用オプションに変換する。
            /// </summary>
            private List<SurvivorWeaponUpgradeOption> ConvertNetworkOptions(
                SurvivorNetworkWeaponUpgradeOption[] networkOptions)
            {
                var result = new List<SurvivorWeaponUpgradeOption>(networkOptions.Length);
                var memDb = Context.ScopedResolver.Resolve<IMasterDataService>().MemoryDatabase;

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

                // ゲーム状態をフリーズ（スコア稼ぎ防止）
                ApplicationEvents.PauseTime();
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
                var kills = Context.GetCappedKills();
                var clearTime = StageModel.GameTime.Value;
                var isTimeUp = StageModel.IsTimeUp;
                var hpRatio = Context.GetHpRatio();

                Debug.Log($"[VictoryState] Saving result: score={score}, kills={kills}, clearTime={clearTime:F2}s, isTimeUp={isTimeUp}, hpRatio={hpRatio:P0}");

                Context._saveService.CompleteCurrentStage(score, kills, clearTime, true, isTimeUp, hpRatio);
                await Context._saveService.SaveAsync();
                Context._isResultSaved = true;

                Debug.Log("[VictoryState] Result saved successfully");

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
                var kills = Context.GetCappedKills();
                var clearTime = StageModel.GameTime.Value;
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
    }
}
