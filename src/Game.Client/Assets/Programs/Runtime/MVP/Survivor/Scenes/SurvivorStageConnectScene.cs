using System;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.Shared;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using Game.Shared.Unity.Server;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer;
using Game.Library.Shared.Dto;

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
        [Inject] private readonly IFusionRunnerService _runnerService;
        [Inject] private readonly IUnityServerApiService _unityServerApiService;

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
                var playerId = session.PlayerId;

                // Phase 1: ネットワーク初期化（モード別）
                if (!UnityPlaymodeHelper.IsServer())
                {
#if UNITY_EDITOR
                    var role = Game.Shared.Multiplayer.MppmHelper.ResolveTag();
                    if (role == Game.Shared.Multiplayer.MppmHelper.MppmTag.Host)
                    {
                        Debug.Log("[SurvivorStageConnectScene] MPPM Host mode");
                        SceneComponent.SetStatus("Starting host...");
                        await _networkConnector.StartHostAsync(stageId);
                    }
                    else if (role == Game.Shared.Multiplayer.MppmHelper.MppmTag.Server)
                    {
                        Debug.Log("[SurvivorStageConnectScene] MPPM Server mode");
                        SceneComponent.SetStatus("Starting server...");
                        await _networkConnector.StartServerAsync(stageId);
                    }
                    else
                    {
                        // Client / None → 起動済みサーバーに接続
                        await PrepareClientConnectionAsync(stageId);
                    }
#else
                    await PrepareClientConnectionAsync(stageId);
#endif
                }

                // Phase 2: サーバー接続 + 全員 Ready 待機
                Debug.Log($"[SurvivorStageConnectScene] Phase 2: HasMatchResult={SurvivorNetworkMatchConnector.HasMatchResult}, {_runnerService.GetDebugStatus()}");
                if (SurvivorNetworkMatchConnector.HasMatchResult)
                {
                    SceneComponent.SetStatus("Connecting to server...");
                    await ConnectToServerAsync(stageId);
                    await NotifySessionInfoToServer(stageId, playerId);
                    SceneComponent.SetStatus("Waiting for players...");
                    await WaitForAllPlayersReadyAsync();
                }
                else if (_runnerService.IsHostMode)
                {
                    // Editor Host mode: Server + ローカルClient
                    await NotifySessionInfoToServer(stageId, playerId);
                    SceneComponent.SetStatus("Waiting for players...");
                    await WaitForAllPlayersReadyAsync();
                }
                else if (_runnerService.IsServer)
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

        /// <summary>
        /// クライアント接続準備（エディタ/配布ビルド共通）。
        /// MatchResult 未設定時、環境設定に応じて接続先を決定する。
        /// 優先順位: 1) マッチメイキング済み → スキップ、2) ローカルオーケストレーター、
        /// 3) FusionServerAddress が設定されていればリモートサーバー、4) ローカル(127.0.0.1)
        /// </summary>
        private async UniTask PrepareClientConnectionAsync(int stageId)
        {
            if (SurvivorNetworkMatchConnector.HasMatchResult)
                return; // マッチメイキング経由 → Phase 2 で接続

            if (GameEnvironmentHelper.CurrentConfig?.UseLocalServerOrchestrator == true)
            {
                Debug.Log("[SurvivorStageConnectScene] Starting local server orchestrator...");
                SceneComponent.SetStatus("Starting local server...");
                await _localServerOrchestrator.StartAsync(SceneComponent.destroyCancellationToken);

                var localTokenResult = await IssueTokenAsync(stageId);
                SurvivorNetworkMatchConnector.ConfigureForLocalServer(
                    _localServerOrchestrator.HeadlessServerPort,
                    sessionToken: localTokenResult?.Token ?? "",
                    sessionName: localTokenResult?.SessionName ?? SurvivorNetworkMatchConnector.DefaultLocalSessionName);
                return;
            }

            // FusionServerAddress が設定されていればクラウドサーバーに接続
            var envConfig = GameEnvironmentHelper.CurrentConfig;
            var address = envConfig?.UnityServerAddress;
            if (!string.IsNullOrEmpty(address) &&
                address != "localhost" &&
                address != SurvivorNetworkMatchConnector.DefaultLocalAddress)
            {
                var port = envConfig.UnityServerPort > 0
                    ? envConfig.UnityServerPort
                    : SurvivorNetworkMatchConnector.DefaultPort;

                var tokenResult = await IssueTokenAsync(stageId);
                var sessionName = tokenResult != null
                    ? tokenResult.SessionName
                    : (!string.IsNullOrEmpty(envConfig.UnityServerSessionName)
                        ? envConfig.UnityServerSessionName
                        : SurvivorNetworkMatchConnector.DefaultRemoteSessionName);
                var sessionToken = tokenResult != null ? tokenResult.Token : string.Empty;

                Debug.Log($"[SurvivorStageConnectScene] Connecting to remote server: {address}:{port} ({sessionName})");
                SurvivorNetworkMatchConnector.ConfigureForRemoteServer(address, port, sessionName, sessionToken);
            }
            else
            {
                var defaultTokenResult = await IssueTokenAsync(stageId);
                Debug.Log($"[SurvivorStageConnectScene] Connecting to local server ({defaultTokenResult?.SessionName ?? SurvivorNetworkMatchConnector.DefaultLocalSessionName})...");
                SurvivorNetworkMatchConnector.ConfigureForLocalServer(
                    SurvivorNetworkMatchConnector.DefaultPort,
                    sessionToken: defaultTokenResult?.Token ?? "",
                    sessionName: defaultTokenResult?.SessionName ?? SurvivorNetworkMatchConnector.DefaultLocalSessionName);
            }
        }

        /// <summary>
        /// SP 接続用セッショントークンとセッション名を Game.Server から取得する。
        /// 取得失敗時は null を返す。
        /// </summary>
        /// <returns>取得成功時はトークンレスポンス、失敗時は null。</returns>
        private async UniTask<UnityServerAuthResponse> IssueTokenAsync(int stageId = 0, int expectedPlayers = 1)
        {
            var response = await _unityServerApiService.IssueTokenAsync(stageId, expectedPlayers);
            if (response.IsSuccess && response.Data != null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[SurvivorStageConnectScene] Session token acquired, session={response.Data.SessionName}");
#endif
                return response.Data;
            }

            Debug.LogWarning($"[SurvivorStageConnectScene] Failed to fetch session token: {response.Error?.Message ?? "Unknown error"}. Proceeding without token.");
            return null;
        }

        private async UniTask ConnectToServerAsync(int stageId)
        {
            var address = SurvivorNetworkMatchConnector.ServerAddress;
            var port = SurvivorNetworkMatchConnector.ServerPort;
            var sessionToken = SurvivorNetworkMatchConnector.SessionToken;
            Debug.Log($"[SurvivorStageConnectScene] Connecting to Fusion server: {address}:{port} (stageId={stageId})");
            await _networkConnector.ConnectAsync(address, port, stageId, sessionToken);
        }

        /// <summary>
        /// Fusion 接続後にステージ ID をサーバーに RPC 通知。
        /// SurvivorFusionGameState が Spawn されるまで待機。
        /// </summary>
        private async UniTask NotifySessionInfoToServer(int stageId, int playerId)
        {
            await UniTask.WaitUntil(
                () => _runnerService.TryGet<SurvivorFusionGameState>(out _),
                cancellationToken: SceneComponent.destroyCancellationToken);

            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.RpcSetSessionInfo(stageId, playerId);
                Debug.Log($"[SurvivorStageConnectScene] Sent session info: stageId={stageId}, playerId={playerId}");
            }
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
