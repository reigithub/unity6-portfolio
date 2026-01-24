using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.Library.Shared.Enums;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Weapon;
using Game.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Services;
using UnityEngine;

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
                GameRootController.SetFadeImmediate(1f);

                // StageModel, WaveManagerはSurvivorStageScene.Startup()で初期化済み
                View.InitializePlayer(StageModel.CurrentLevelMaster, GameRootController.MainCamera);

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
                _countdownComplete = true;
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
        }

        #endregion

        #region PlayingState

        private class PlayingState : StageStateBase
        {
            private bool _isFirstEntry = true;

            public override void Enter()
            {
                Debug.Log("[PlayingState] Enter");
                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();

                // 初回（ReadyStateからの遷移）のみWaveを開始
                // LevelUpStateやPausedStateからの復帰時はWaveを開始しない
                if (_isFirstEntry)
                {
                    _isFirstEntry = false;
                    WaveManager.StartWave();

                    // HUDをフェードイン表示（カウントダウン後、初めてPlayingStateに入った時）
                    View.SetHudVisible(true);
                }

                Context._inputService.EnablePlayer();
                Context._pauseRequested = false;
                Context._levelUpRequested = false;
            }

            public override void Update()
            {
                StageModel.GameTime.Value += Time.deltaTime;
                View.UpdateTime(StageModel.GameTime.Value);

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
            }

            public override void Exit() => Debug.Log("[PlayingState] Exit");
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
                            break; // 成功したらループを抜ける
                        }

                        // キャンセル時はループ継続（武器選択に戻る）
                        continue;
                    }
                    else
                    {
                        // 通常の武器追加/アップグレード
                        await View.WeaponManager.ApplyUpgradeOptionAsync(result);
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
                // 同じステージシーンに再遷移（Terminate→Startupで完全リセット）
                await SceneService.TransitionAsync<SurvivorStageScene>();
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