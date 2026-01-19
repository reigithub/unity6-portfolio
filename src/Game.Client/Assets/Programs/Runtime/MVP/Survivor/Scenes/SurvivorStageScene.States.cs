using Cysharp.Threading.Tasks;
using DG.Tweening;
using Game.MVP.Core.DI;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.UI;
using Game.MVP.Survivor.Weapon;
using Game.Shared;
using Game.Shared.Bootstrap;
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
            _stateMachine.AddTransition<PausedState, ReadyState>(StageEvent.Retry);
            _stateMachine.AddTransition<PausedState, QuitToTitleState>(StageEvent.QuitToTitle);
            _stateMachine.AddTransition<LevelUpState, PlayingState>(StageEvent.LevelUpComplete);

            _stateMachine.SetInitState<ReadyState>();
        }

        #endregion

        #region StageStateBase

        private abstract class StageStateBase : State<SurvivorStageScene, StageEvent>
        {
            protected IGameSceneService SceneService => Context._sceneService;
            protected IGameRootController GameRootController => Context._gameRunner.GameRootController;
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
                View.InitializePlayer(StageModel.PlayerMaster, GameRootController.MainCamera);

                InitializeAndCountdownAsync().Forget();
            }

            private async UniTaskVoid InitializeAndCountdownAsync()
            {
                // ゲームコンポーネントの初期化
                await View.InitializeWeaponManagerAsync(
                    StageModel.GetStartingWeaponId(),
                    StageModel.GetDamageMultiplier()
                );
                await View.InitializeEnemySpawnerAsync(WaveManager);
                await View.InitializeExperienceOrbSpawnerAsync();

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

                Debug.Log("[ReadyState] Countdown complete");
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
            public override void Enter()
            {
                Debug.Log("[PlayingState] Enter");
                ApplicationEvents.ResumeTime();
                ApplicationEvents.HideCursor();
                WaveManager.StartWave();

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
                var result = await SceneService.TransitionDialogAsync<SurvivorPauseDialog, SurvivorPauseDialogComponent, SurvivorPauseResult>();

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

                var result = await SceneService.TransitionDialogAsync<
                    SurvivorPlayerLevelUpDialog,
                    SurvivorPlayerLevelUpDialogComponent,
                    SurvivorPlayerLevelUpDialogArg,
                    SurvivorWeaponUpgradeOption
                >(new(options, StageModel.Level.Value));

                if (result != null)
                {
                    await View.WeaponManager.ApplyUpgradeOptionAsync(result);
                    View.WeaponManager.UpdateDamageMultiplier(StageModel.GetDamageMultiplier());
                }

                Transition(StageEvent.LevelUpComplete);
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
                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();
                View.ShowVictory();
                TransitionToResultAsync().Forget();
            }

            private async UniTaskVoid TransitionToResultAsync()
            {
                await UniTask.Delay(ResultDisplayDuration);
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
                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();
                View.ShowGameOver();
                TransitionToResultAsync().Forget();
            }

            private async UniTaskVoid TransitionToResultAsync()
            {
                await UniTask.Delay(ResultDisplayDuration);
                await SceneService.TransitionAsync<SurvivorTotalResultScene>();
            }

            public override void Exit() => Debug.Log("[GameOverState] Exit");
        }

        #endregion

        #region QuitToTitleState

        private class QuitToTitleState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[QuitToTitleState] Enter");
                ApplicationEvents.ResumeTime();
                ApplicationEvents.ShowCursor();
                TransitionToResultAsync().Forget();
            }

            private async UniTaskVoid TransitionToResultAsync()
            {
                // リザルト画面へ遷移（途中終了の結果も表示）
                await SceneService.TransitionAsync<SurvivorTotalResultScene>();
            }

            public override void Exit() => Debug.Log("[QuitToTitleState] Exit");
        }

        #endregion
    }
}