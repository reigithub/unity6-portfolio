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
    /// <summary>
    /// SurvivorGameStageScene の Server State Machine 部分 (P2P Host または将来の DS で動作)。
    /// Client State Machine (.States.cs) と並列駆動。Host モードでは両方動く。
    /// 元実装: SurvivorNetworkStageScene.States.cs。命名衝突回避のため Server プレフィックスを付与。
    /// </summary>
    public partial class SurvivorGameStageScene
    {
        #region Server StateMachine

        private enum ServerStageEvent
        {
            StartGame,
            LevelUp,
            LevelUpComplete,
            Victory,
            GameOver,
        }

        private StateMachine<SurvivorGameStageScene, ServerStageEvent> _serverStateMachine;

        private void BuildServerStateMachine()
        {
            _serverStateMachine = new StateMachine<SurvivorGameStageScene, ServerStageEvent>(this);

            _serverStateMachine.AddTransition<ServerReadyState, ServerPlayingState>(ServerStageEvent.StartGame);
            _serverStateMachine.AddTransition<ServerPlayingState, ServerLevelUpState>(ServerStageEvent.LevelUp);
            _serverStateMachine.AddTransition<ServerPlayingState, ServerVictoryState>(ServerStageEvent.Victory);
            _serverStateMachine.AddTransition<ServerPlayingState, ServerGameOverState>(ServerStageEvent.GameOver);
            _serverStateMachine.AddTransition<ServerLevelUpState, ServerPlayingState>(ServerStageEvent.LevelUpComplete);

            _serverStateMachine.SetInitState<ServerReadyState>();
        }

        #endregion

        #region ServerStageStateBase

        private abstract class ServerStageStateBase : State<SurvivorGameStageScene, ServerStageEvent>
        {
            protected Services.SurvivorStageWaveManager WaveManager => Context._waveManager;
            protected Models.SurvivorNetworkStageModel NetworkStageModel => Context._networkStageModel;
            protected SurvivorGameStageSceneComponent View => Context.SceneComponent;

            protected void Transition(ServerStageEvent evt) => StateMachine.Transition(evt);
        }

        #endregion

        #region ServerReadyState

        private class ServerReadyState : ServerStageStateBase
        {
            private bool _startComplete;

            public override void Enter()
            {
                Debug.Log("[SurvivorGameStageScene.ServerReadyState] Enter");
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

                Debug.Log("[SurvivorGameStageScene.ServerReadyState] Initialization complete, waiting for all clients scene ready...");

                // 全クライアントのシーン準備完了を待機
                await WaitForAllClientsSceneReadyAsync();

                Debug.Log("[SurvivorGameStageScene.ServerReadyState] All clients ready, starting game");
                _startComplete = true;
            }

            public override void Update()
            {
                if (_startComplete)
                {
                    Transition(ServerStageEvent.StartGame);
                }
            }

            public override void Exit() => Debug.Log("[SurvivorGameStageScene.ServerReadyState] Exit");

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

                Debug.Log("[SurvivorGameStageScene.ServerReadyState] Server: initialized");
            }

            /// <summary>
            /// サーバー: 全クライアントが NotifySceneReadyServerRpc を送信するまで待機。
            /// タイムアウト付き（30秒）で、クライアント切断に対応。
            /// </summary>
            private async UniTask WaitForAllClientsSceneReadyAsync()
            {
                if (!Context._runnerService.TryGet<SurvivorFusionGameState>(out var fusionGs))
                {
                    Debug.LogWarning("[SurvivorGameStageScene.ServerReadyState] FusionGameState not found, skipping wait");
                    return;
                }

                fusionGs.ResetSceneReadyTracking();

                var tcs = new UniTaskCompletionSource();
                var subscription = Context._allClientsSceneReadySub.Subscribe(_ => tcs.TrySetResult());

                // 症状 1 診断 (観察期間限定): 完了経路 (TCS / TIMEOUT) と所要時間を可視化。
                // 症状 1 真因確定後の次 PR で削除すること。
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var winIndex = await UniTask.WhenAny(
                        tcs.Task,
                        UniTask.Delay(TimeSpan.FromSeconds(30), DelayType.Realtime));
                    sw.Stop();
                    if (winIndex == 0)
                    {
                        Debug.Log($"[DIAG-AllClientsReady] completed via TCS (all clients ready), elapsed={sw.ElapsedMilliseconds}ms");
                    }
                    else
                    {
                        Debug.LogWarning($"[DIAG-AllClientsReady] completed via TIMEOUT (30s), elapsed={sw.ElapsedMilliseconds}ms");
                    }
                }
                finally
                {
                    subscription.Dispose();
                }
            }
        }

        #endregion

        #region ServerPlayingState

        private class ServerPlayingState : ServerStageStateBase
        {
            private bool _isFirstEntry = true;

            public override void Enter()
            {
                Debug.Log("[SurvivorGameStageScene.ServerPlayingState] Enter");

                // 初回のみWave開始（LevelUpからの復帰時は不要）
                if (_isFirstEntry)
                {
                    _isFirstEntry = false;
                    Context._gameState.NotifyGameStarted();
                    Debug.Log("[SurvivorGameStageScene.ServerPlayingState] Starting first wave");
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
                        Transition(ServerStageEvent.LevelUp);
                        return;
                    }
                }

                // 勝利条件: 時間制限到達 or 全ウェーブクリア
                if (NetworkStageModel.IsTimeUp || WaveManager.IsAllWavesCleared.CurrentValue)
                {
                    Transition(ServerStageEvent.Victory);
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
                    Transition(ServerStageEvent.GameOver);
                    return;
                }
            }

            public override void Exit() => Debug.Log("[SurvivorGameStageScene.ServerPlayingState] Exit");
        }

        #endregion

        #region ServerLevelUpState

        private class ServerLevelUpState : ServerStageStateBase
        {
            public override void Enter()
            {
                var ctx = Context._currentLevelingContext;
                if (ctx == null)
                {
                    Debug.LogWarning("[SurvivorGameStageScene.ServerLevelUpState] _currentLevelingContext is null, skipping");
                    Transition(ServerStageEvent.LevelUpComplete);
                    return;
                }

                Debug.Log($"[SurvivorGameStageScene.ServerLevelUpState] Enter - player={ctx.Player}, level={ctx.StageModel.Level.Value}");

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
                        Debug.Log($"[SurvivorGameStageScene.ServerLevelUpState] Sent LevelUp to {ctx.UserId} with {networkOptions.Length} options");
                    }
                }

                // サーバーは即座に完了（クライアントがUI選択を処理する）
                Transition(ServerStageEvent.LevelUpComplete);
            }

            public override void Exit()
            {
                // IsPaused 解除はサーバー側 OnClientWeaponChoice 受信時の EndLevelUpPause で行う。
                // サーバー LevelUpState は即時 Transition で抜けるが、IsPaused は HashSet 参照カウントで維持される。
                Debug.Log("[SurvivorGameStageScene.ServerLevelUpState] Exit");
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

        #region ServerVictoryState

        private class ServerVictoryState : ServerStageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[SurvivorGameStageScene.ServerVictoryState] Enter");
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
                var kills = Context.GetCappedKillsServer();
                var clearTime = NetworkStageModel.GameTime.Value;
                var isTimeUp = NetworkStageModel.IsTimeUp;
                var hpRatio = Context.GetHpRatioServer();

                Debug.Log($"[SurvivorGameStageScene.ServerVictoryState] Saving: score={score}, kills={kills}, time={clearTime:F2}s");

                // DS 専用 save (Host では Client SM の VictoryState が同じ save を行うため二重保存になる)。
                if (Context._runnerService.IsDedicatedServer)
                {
                    Context._saveService.CompleteCurrentStage(score, kills, clearTime, true, isTimeUp, hpRatio);
                    await Context._saveService.SaveAsync();
                }

                // クライアントに勝利を通知（確定キル数を含め、バッチ同期遅延による不整合を防止）
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                    gs.NotifyGameEnded(true, clearTime, kills);

                Debug.Log("[SurvivorGameStageScene.ServerVictoryState] Result saved, clients notified");
                ApplicationEvents.ResumeTime();
            }

            public override void Exit() => Debug.Log("[SurvivorGameStageScene.ServerVictoryState] Exit");
        }

        #endregion

        #region ServerGameOverState

        private class ServerGameOverState : ServerStageStateBase
        {
            public override void Enter()
            {
                Debug.Log("[SurvivorGameStageScene.ServerGameOverState] Enter");
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
                var kills = Context.GetCappedKillsServer();
                var clearTime = NetworkStageModel.GameTime.Value;

                Debug.Log($"[SurvivorGameStageScene.ServerGameOverState] Saving: score={score}, kills={kills}, time={clearTime:F2}s");

                // DS 専用 save (Host では Client SM の GameOverState が同じ save を行うため二重保存になる)。
                if (Context._runnerService.IsDedicatedServer)
                {
                    Context._saveService.CompleteCurrentStage(score, kills, clearTime, false, false, 0f);
                    await Context._saveService.SaveAsync();
                }

                // クライアントに敗北を通知（確定キル数を含め、バッチ同期遅延による不整合を防止）
                if (Context._runnerService.TryGet<SurvivorFusionGameState>(out var gs))
                    gs.NotifyGameEnded(false, clearTime, kills);

                Debug.Log("[SurvivorGameStageScene.ServerGameOverState] Result saved, clients notified");
                ApplicationEvents.ResumeTime();
            }

            public override void Exit() => Debug.Log("[SurvivorGameStageScene.ServerGameOverState] Exit");
        }

        #endregion
    }
}
