using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Library.Shared;
using Game.MVP.Survivor.Weapon;
using Game.Shared.Bootstrap;
using Game.Shared.Network.Survivor;
using MessagePipe;
using UnityEngine;

namespace Game.MVP.Survivor.Scenes
{
    public partial class SurvivorNetworkStageScene
    {
        #region StateMachine

        private enum StageEvent
        {
            StartGame,
            LevelUp,
            LevelUpComplete,
            Victory,
            GameOver,
        }

        private StateMachine<SurvivorNetworkStageScene, StageEvent> _stateMachine;

        private void BuildStateMachine()
        {
            _stateMachine = new StateMachine<SurvivorNetworkStageScene, StageEvent>(this);

            _stateMachine.AddTransition<ReadyState, PlayingState>(StageEvent.StartGame);
            _stateMachine.AddTransition<PlayingState, LevelUpState>(StageEvent.LevelUp);
            _stateMachine.AddTransition<PlayingState, VictoryState>(StageEvent.Victory);
            _stateMachine.AddTransition<PlayingState, GameOverState>(StageEvent.GameOver);
            _stateMachine.AddTransition<LevelUpState, PlayingState>(StageEvent.LevelUpComplete);

            _stateMachine.SetInitState<ReadyState>();
        }

        #endregion

        #region StageStateBase

        private abstract class StageStateBase : State<SurvivorNetworkStageScene, StageEvent>
        {
            protected Services.SurvivorStageWaveManager WaveManager => Context._waveManager;
            protected Models.SurvivorNetworkStageModel NetworkStageModel => Context._networkStageModel;
            protected SurvivorNetworkStageSceneComponent View => Context.SceneComponent;

            protected void Transition(StageEvent evt) => StateMachine.Transition(evt);
        }

        #endregion

        #region ReadyState

        private class ReadyState : StageStateBase
        {
            private bool _startComplete;

            public override void Enter()
            {
                Debug.Log("[SurvivorNetworkStageScene.ReadyState] Enter");
                _startComplete = false;

                // プレイヤー初期化 (全 Context に対して実施、サーバーではカメラなし)
                foreach (var ctx in Context._players.Values)
                {
                    View.InitializePlayer(ctx.StageModel.CurrentLevelMaster, null);
                }

                InitializeAndStartAsync().Forget();
            }

            private async UniTaskVoid InitializeAndStartAsync()
            {
                // ゲームコンポーネントの初期化（サーバーは SurvivorNetworkWeaponManager を使用、
                // SceneComponent.WeaponManager の初期化は不要）
                await View.InitializeEnemySpawnerAsync(WaveManager);
                await View.InitializeItemSpawnerAsync();

                // サーバー側プレイヤーコントローラーにNetworkPlayerStateをバインド
                InitializeServerViews();

                Debug.Log("[SurvivorNetworkStageScene.ReadyState] Initialization complete, waiting for all clients scene ready...");

                // 全クライアントのシーン準備完了を待機
                await WaitForAllClientsSceneReadyAsync();

                Debug.Log("[SurvivorNetworkStageScene.ReadyState] All clients ready, starting game");
                _startComplete = true;
            }

            public override void Update()
            {
                if (_startComplete)
                {
                    Transition(StageEvent.StartGame);
                }
            }

            public override void Exit() => Debug.Log("[SurvivorNetworkStageScene.ReadyState] Exit");

            /// <summary>
            /// サーバー側: NetworkPlayerStateをPlayerControllerにバインドし、
            /// EnemySpawnerにプレイヤーTransformを登録する。
            /// </summary>
            private void InitializeServerViews()
            {
                var playerController = View.PlayerController;
                if (playerController != null)
                {
                    View.EnemySpawner?.AddPlayer(playerController.transform);
                }

                Debug.Log("[SurvivorNetworkStageScene.ReadyState] Server: initialized");
            }

            /// <summary>
            /// サーバー: 全クライアントが NotifySceneReadyServerRpc を送信するまで待機。
            /// タイムアウト付き（30秒）で、クライアント切断に対応。
            /// </summary>
            private async UniTask WaitForAllClientsSceneReadyAsync()
            {
                if (!Context._runnerService.TryGet<SurvivorFusionGameState>(out var fusionGs))
                {
                    Debug.LogWarning("[SurvivorNetworkStageScene.ReadyState] FusionGameState not found, skipping wait");
                    return;
                }

                fusionGs.ResetSceneReadyTracking();

                var tcs = new UniTaskCompletionSource();
                var subscription = Context._allClientsSceneReadySub.Subscribe(_ => tcs.TrySetResult());

                try
                {
                    var winIndex = await UniTask.WhenAny(
                        tcs.Task,
                        UniTask.Delay(TimeSpan.FromSeconds(30), DelayType.Realtime));
                    if (winIndex == 1)
                    {
                        Debug.LogWarning("[SurvivorNetworkStageScene.ReadyState] Timeout waiting for clients, proceeding");
                    }
                }
                finally
                {
                    subscription.Dispose();
                }
            }
        }

        #endregion

        #region PlayingState

        private class PlayingState : StageStateBase
        {
            private bool _isFirstEntry = true;

            public override void Enter()
            {
                Debug.Log("[SurvivorNetworkStageScene.PlayingState] Enter");

                // 初回のみWave開始（LevelUpからの復帰時は不要）
                if (_isFirstEntry)
                {
                    _isFirstEntry = false;
                    Debug.Log("[SurvivorNetworkStageScene.PlayingState] Starting first wave");
                    WaveManager.StartWave();
                }
            }

            public override void Update()
            {
                // レベルアップ処理 (全 Context を走査、PendingLevelUpCount > 0 のプレイヤーを順番に処理)
                foreach (var ctx in Context._players.Values)
                {
                    if (ctx.PendingLevelUpCount > 0)
                    {
                        ctx.PendingLevelUpCount--;
                        Context._currentLevelingContext = ctx;
                        Transition(StageEvent.LevelUp);
                        return;
                    }
                }

                // ゲームタイマー更新（ポーズ中はスキップ）
                bool isPaused = Context._gameState != null && Context._gameState.IsEffectivelyPaused;
                if (!isPaused)
                {
                    var dt = Context._runnerService.IsActive && Context._runnerService.Runner != null
                        ? Context._runnerService.Runner.DeltaTime
                        : Time.deltaTime;
                    NetworkStageModel.GameTime.Value += dt;
                }

                // 勝利条件: 時間制限到達 or 全ウェーブクリア
                if (NetworkStageModel.IsTimeUp || WaveManager.IsAllWavesCleared.CurrentValue)
                {
                    Transition(StageEvent.Victory);
                    return;
                }

                // 敗北条件: 全プレイヤーが死亡
                bool allDead = Context._players.Count > 0;
                foreach (var ctx in Context._players.Values)
                {
                    if (!ctx.StageModel.IsDead) { allDead = false; break; }
                }
                if (allDead)
                {
                    Transition(StageEvent.GameOver);
                    return;
                }
            }

            public override void Exit() => Debug.Log("[SurvivorNetworkStageScene.PlayingState] Exit");
        }

        #endregion

        #region LevelUpState

        private class LevelUpState : StageStateBase
        {
            public override void Enter()
            {
                var ctx = Context._currentLevelingContext;
                if (ctx == null)
                {
                    Debug.LogWarning("[SurvivorNetworkStageScene.LevelUpState] _currentLevelingContext is null, skipping");
                    Transition(StageEvent.LevelUpComplete);
                    return;
                }

                Debug.Log($"[SurvivorNetworkStageScene.LevelUpState] Enter - player={ctx.Player}, level={ctx.StageModel.Level.Value}");

                // サーバー権威の Pause を即時開始 (RPC 往復遅延ゼロ)
                Context._gameState?.BeginLevelUpPause(ctx.Player);

                // プレイヤーステータス更新 (該当 Context の Controller)
                if (ctx.Controller != null && ctx.StageModel.CurrentLevelMaster != null)
                {
                    ctx.Controller.UpdateLevelStats(ctx.StageModel.CurrentLevelMaster);
                }

                // ダメージ倍率更新
                ctx.WeaponManager.UpdateDamageMultiplier(ctx.StageModel.GetDamageMultiplier());

                // 武器選択肢を生成して該当クライアントに送信 (InputAuthority ターゲット RPC)
                {
                    var serverOptions = ctx.WeaponManager.GetUpgradeOptions(ctx.StageModel.WeaponChoiceCount.Value);
                    if (serverOptions.Count > 0 && ctx.FusionPlayer != null)
                    {
                        var networkOptions = ConvertToNetworkOptions(serverOptions);
                        ctx.FusionPlayer.NotifyPlayerLevelUp(
                            ctx.StageModel.Level.Value,
                            ctx.StageModel.Experience.Value,
                            ctx.StageModel.ExperienceToNextLevel.Value,
                            networkOptions);
                        Debug.Log($"[SurvivorNetworkStageScene.LevelUpState] Sent LevelUp to {ctx.UserId} with {networkOptions.Length} options");
                    }
                }

                // サーバーは即座に完了（クライアントがUI選択を処理する）
                Transition(StageEvent.LevelUpComplete);
            }

            public override void Exit()
            {
                // IsPaused 解除はサーバー側 OnClientWeaponChoice 受信時の EndLevelUpPause で行う。
                // サーバー LevelUpState は即時 Transition で抜けるが、IsPaused は HashSet 参照カウントで維持される。
                Debug.Log("[SurvivorNetworkStageScene.LevelUpState] Exit");
            }

            private static SurvivorNetworkWeaponUpgradeOption[] ConvertToNetworkOptions(
                List<SurvivorWeaponUpgradeOption> options)
            {
                var result = new SurvivorNetworkWeaponUpgradeOption[options.Count];
                for (int i = 0; i < options.Count; i++)
                {
                    var opt = options[i];
                    result[i] = new SurvivorNetworkWeaponUpgradeOption
                    {
                        WeaponId = opt.WeaponId,
                        IsNewWeapon = opt.IsNewWeapon,
                        CurrentLevel = opt.CurrentLevel,
                    };
                }
                return result;
            }
        }

        #endregion

        #region VictoryState

        private class VictoryState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[SurvivorNetworkStageScene.VictoryState] Enter");
                ApplicationEvents.PauseTime();

                // 未送信のDeath/Position をクライアントに即座に同期（GameEnded RPC より先に届ける）
                View.EnemySpawner?.FlushPendingSync();

                // 残存敵を全クリア＆スポーン停止
                View.EnemySpawner?.ClearAllEnemies();

                SaveAndNotifyAsync().Forget();
            }

            private async UniTaskVoid SaveAndNotifyAsync()
            {
                var score = Context.GetTotalScore();
                var kills = Context.GetCappedKills();
                var clearTime = NetworkStageModel.GameTime.Value;
                var isTimeUp = NetworkStageModel.IsTimeUp;
                var hpRatio = Context.GetHpRatio();

                Debug.Log($"[SurvivorNetworkStageScene.VictoryState] Saving: score={score}, kills={kills}, time={clearTime:F2}s");

                Context._saveService.CompleteCurrentStage(score, kills, clearTime, true, isTimeUp, hpRatio);
                await Context._saveService.SaveAsync();

                // クライアントに勝利を通知（確定キル数を含め、バッチ同期遅延による不整合を防止）
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                    gs.NotifyGameEnded(true, clearTime, kills);

                Debug.Log("[SurvivorNetworkStageScene.VictoryState] Result saved, clients notified");
                ApplicationEvents.ResumeTime();
            }

            public override void Exit() => Debug.Log("[SurvivorNetworkStageScene.VictoryState] Exit");
        }

        #endregion

        #region GameOverState

        private class GameOverState : StageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[SurvivorNetworkStageScene.GameOverState] Enter");
                ApplicationEvents.PauseTime();

                // 未送信のDeath/Position をクライアントに即座に同期（GameEnded RPC より先に届ける）
                View.EnemySpawner?.FlushPendingSync();

                // 残存敵を全クリア＆スポーン停止
                View.EnemySpawner?.ClearAllEnemies();

                SaveAndNotifyAsync().Forget();
            }

            private async UniTaskVoid SaveAndNotifyAsync()
            {
                var score = Context.GetTotalScore();
                var kills = Context.GetCappedKills();
                var clearTime = NetworkStageModel.GameTime.Value;

                Debug.Log($"[SurvivorNetworkStageScene.GameOverState] Saving: score={score}, kills={kills}, time={clearTime:F2}s");

                Context._saveService.CompleteCurrentStage(score, kills, clearTime, false, false, 0f);
                await Context._saveService.SaveAsync();

                // クライアントに敗北を通知（確定キル数を含め、バッチ同期遅延による不整合を防止）
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                    gs.NotifyGameEnded(false, clearTime, kills);

                Debug.Log("[SurvivorNetworkStageScene.GameOverState] Result saved, clients notified");
                ApplicationEvents.ResumeTime();
            }

            public override void Exit() => Debug.Log("[SurvivorNetworkStageScene.GameOverState] Exit");
        }

        #endregion
    }
}
