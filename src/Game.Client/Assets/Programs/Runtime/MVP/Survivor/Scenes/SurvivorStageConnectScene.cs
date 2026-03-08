using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.Shared.Network;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using Game.Shared.Unity.Server;
using MessagePipe;
using Mirror;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// ネットワーク接続確立シーン（Presenter）。
    /// StageSelectScene と StageScene の間に挿入し、
    /// ネットワーク接続を完了してからインゲームシーンへ遷移する。
    /// </summary>
    public class SurvivorStageConnectScene : GamePrefabScene<SurvivorStageConnectScene, SurvivorStageConnectSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISurvivorNetworkStageConnector _networkConnector;
        [Inject] private readonly ILocalServerOrchestrator _localServerOrchestrator;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly ISubscriber<SurvivorSignals.Session.AllPlayersReady> _allPlayersReadySub;

        protected override string AssetPathOrAddress => "SurvivorStageConnectScene";

        public override async UniTask Startup()
        {
            await base.Startup();

            SceneComponent.OnRetryClicked
                .Subscribe(_ => ConnectAndTransitionAsync().Forget())
                .AddTo(Disposables);

            SceneComponent.OnCancelClicked
                .Subscribe(_ => OnCancelAsync().Forget())
                .AddTo(Disposables);
        }

        public override async UniTask Ready()
        {
            await base.Ready();
            ConnectAndTransitionAsync().Forget();
        }

        private async UniTaskVoid ConnectAndTransitionAsync()
        {
            try
            {
                SceneComponent.SetInteractables(false);
                SceneComponent.SetStatus("Connecting...");

                var session = _saveService.CurrentSession;
                if (session == null)
                {
                    SceneComponent.ShowError("No active session found.");
                    SceneComponent.SetInteractables(true);
                    return;
                }

                var stageId = session.StageId;

                // Phase 1: ネットワーク初期化（モード別）
                if (!UnityPlaymodeHelper.IsServer())
                {
#if UNITY_EDITOR
                    if (Game.Shared.Multiplayer.MppmHelper.IsActive())
                    {
                        // MPPM: タグに従いローカルロールで起動（MatchResult は無視）
                        await StartEditorNetworkAsync(stageId);
                    }
                    else if (!SurvivorNetworkMatchConnector.HasMatchResult)
                    {
                        // Non-MPPM SP: Host モードで起動
                        await StartEditorNetworkAsync(stageId);
                    }
                    // else: Non-MPPM + MatchResult → 外部サーバー接続（Phase 2）
#else
                    if (!SurvivorNetworkMatchConnector.HasMatchResult)
                    {
                        // 配布ビルド SP: Orchestrator で全サービス起動
                        SceneComponent.SetStatus("Starting local server...");
                        await _localServerOrchestrator.StartAsync(SceneComponent.destroyCancellationToken);
                        SurvivorNetworkMatchConnector.SetLocalServer(_localServerOrchestrator.HeadlessServerPort);
                    }
#endif
                }

                // Phase 2: サーバー接続 + 全員 Ready 待機
                Debug.Log($"[SurvivorStageConnectScene] Phase 2: HasMatchResult={SurvivorNetworkMatchConnector.HasMatchResult}, ServerActive={NetworkServer.active}, ClientConnected={NetworkClient.isConnected}");
                if (SurvivorNetworkMatchConnector.HasMatchResult)
                {
                    SceneComponent.SetStatus("Connecting to server...");
                    await ConnectToServerAsync(stageId);
                    SceneComponent.SetStatus("Waiting for players...");
                    await WaitForAllPlayersReadyAsync();
                }
                else if (NetworkServer.active && NetworkClient.isConnected)
                {
                    // Editor Host mode: Server + ローカルClient
                    SceneComponent.SetStatus("Waiting for players...");
                    await WaitForAllPlayersReadyAsync();
                }
                else if (NetworkServer.active)
                {
                    // Editor Server-only mode: 全 Client 接続待ち
                    SceneComponent.SetStatus("Waiting for clients...");
                    await WaitForAllPlayersReadyAsync();
                }

                // Phase 3: StageScene へ遷移
                Debug.Log("[SurvivorStageConnectScene] Connection established, transitioning to StageScene");
                await _sceneService.TransitionAsync<SurvivorStageScene>();
            }
            catch (OperationCanceledException)
            {
                // シーン破棄によるキャンセル — 何もしない
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SurvivorStageConnectScene] Connection failed: {ex.Message}");
                SceneComponent.ShowError(ex.Message);
                SceneComponent.SetInteractables(true);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor ネットワーク初期化。MPPM タグでロール判定し分岐する。
        /// 非MPPM 時はタグ空 → Host (SP デフォルト)。
        /// </summary>
        private async UniTask StartEditorNetworkAsync(int stageId)
        {
            var role = Game.Shared.Multiplayer.MppmHelper.ResolveTag();

            switch (role)
            {
                case Game.Shared.Multiplayer.MppmHelper.MppmTag.Host:
                    Debug.Log("[SurvivorStageConnectScene] Editor Host mode: starting host...");
                    SceneComponent.SetStatus("Starting host...");
                    await _networkConnector.StartHostAsync(stageId);
                    break;

                case Game.Shared.Multiplayer.MppmHelper.MppmTag.Client:
                    Debug.Log("[SurvivorStageConnectScene] Editor Client mode: will connect to localhost:7777...");
                    SurvivorNetworkMatchConnector.SetLocalServer(7777);
                    break;

                case Game.Shared.Multiplayer.MppmHelper.MppmTag.Server:
                    Debug.Log("[SurvivorStageConnectScene] Editor Server-only mode: starting server...");
                    SceneComponent.SetStatus("Starting server...");
                    await _networkConnector.StartServerAsync(stageId);
                    break;
            }
        }
#endif

        private async UniTask ConnectToServerAsync(int stageId)
        {
            var address = SurvivorNetworkMatchConnector.ServerAddress;
            var port = SurvivorNetworkMatchConnector.ServerPort;
            var sessionToken = SurvivorNetworkMatchConnector.SessionToken;
            Debug.Log($"[SurvivorStageConnectScene] Connecting to Mirror server: {address}:{port} (stageId={stageId})");
            await _networkConnector.ConnectAsync(address, port, stageId, sessionToken);
        }

        private async UniTask WaitForAllPlayersReadyAsync()
        {
            Debug.Log("[SurvivorStageConnectScene] WaitForAllPlayersReady: subscribing...");
            var tcs = new UniTaskCompletionSource();

            // MessagePipe 経由（ClientRpc → IPublisher、またはサーバーローカル直接 Publish）
            var subscription = _allPlayersReadySub.Subscribe(_ =>
            {
                Debug.Log("[SurvivorStageConnectScene] AllPlayersReady received");
                tcs.TrySetResult();
            });

            try
            {
                // Realtime で待機（Time.timeScale に依存しない）
                var winIndex = await UniTask.WhenAny(
                    tcs.Task,
                    UniTask.Delay(TimeSpan.FromSeconds(10), DelayType.Realtime)
                );
                Debug.Log($"[SurvivorStageConnectScene] WaitForAllPlayersReady completed (index={winIndex})");
            }
            finally
            {
                subscription.Dispose();
            }
        }

        private async UniTaskVoid OnCancelAsync()
        {
            SceneComponent.SetInteractables(false);
            _networkConnector.Disconnect();
            SurvivorNetworkMatchConnector.Clear();
            await _sceneService.TransitionAsync<SurvivorTitleScene>();
        }
    }
}
