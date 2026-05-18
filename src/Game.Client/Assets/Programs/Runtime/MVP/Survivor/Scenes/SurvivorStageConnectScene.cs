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
using Game.Shared.Realtime.Client;
#if UNITY_EDITOR
using Game.Shared.Multiplayer;
#endif

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
        [Inject] private readonly IGameSessionConfig _sessionConfig;
        [Inject] private readonly IAuthSessionRefresher _authSessionRefresher;
        [Inject] private readonly ILobbyClient _lobbyClient;

        // Hub から OnLobbyClosed を受け取った場合や Cancel 経由で複数の遷移を発火しないためのガード。
        private bool _isExitingScene;

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

            // P2P Host 起動失敗 / タイムアウトで Hub から OnLobbyClosed が来た場合に Title へ戻すフォールバック。
            _lobbyClient.OnLobbyClosed += HandleLobbyClosed;
        }

        public override async UniTask Terminate()
        {
            _lobbyClient.OnLobbyClosed -= HandleLobbyClosed;
            await base.Terminate();
        }

        private void HandleLobbyClosed(string reason)
        {
            if (_isExitingScene) return;
            Debug.LogWarning($"[SurvivorStageConnectScene] Lobby closed during connect: {reason}");
            OnLobbyClosedFallbackAsync(reason).Forget();
        }

        private async UniTaskVoid OnLobbyClosedFallbackAsync(string reason)
        {
            if (_isExitingScene) return;
            _isExitingScene = true;

            SceneComponent.SetInteractables(false);
            SceneComponent.ShowError($"Connection aborted: {reason}");

            // 進行中の Fusion 接続をキャンセルしてセッション設定を破棄。
            try
            {
                _networkConnector.Disconnect();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorStageConnectScene] Disconnect during fallback failed: {ex.Message}");
            }
            _sessionConfig.Clear();

            await _sceneService.TransitionAsync<SurvivorTitleScene>();
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
                    // 本番 P2P Host (Lobby 経由で Configure 済) は MPPM tag より優先判定。
                    // P2PHost が Lobby 経由で確定しているケースでは MPPM tag を見ない。
                    if (_sessionConfig.ConnectionSource == GameConnectionSource.P2PHost)
                    {
                        Debug.Log("[SurvivorStageConnectScene] P2P Host mode");
                        SceneComponent.SetStatus("Starting P2P host...");
                        await _networkConnector.StartHostAsync(stageId);
                    }
                    else
                    {
#if UNITY_EDITOR
                        var role = MppmHelper.ResolveTag();
                        if (role == MppmTag.Host)
                        {
                            Debug.Log("[SurvivorStageConnectScene] MPPM Host mode");
                            SceneComponent.SetStatus("Starting host...");
                            await _networkConnector.StartHostAsync(stageId);
                        }
                        else if (role == MppmTag.Server)
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
                }

                // Phase 2: サーバー接続 + 全員 Ready 待機
                Debug.Log($"[SurvivorStageConnectScene] Phase 2: HasMatchResult={_sessionConfig.IsClientConfigured}, {_runnerService.GetDebugStatus()}");
                if (_sessionConfig.IsClientConfigured)
                {
                    SceneComponent.SetStatus("Connecting to server...");
                    await ConnectToServerAsync(stageId);
                    await NotifySessionInfoToServer(stageId, playerId);
                    SceneComponent.SetStatus("Waiting for players...");
                    await WaitForAllPlayersReadyAsync();
                }
                else if (_runnerService.IsHostMode)
                {
                    // Host モード (Editor MPPM の Host tag、または本番 P2PHost)
                    // 本番 P2PHost では Phase 1 で StartHostAsync 完了後にここに到達し、
                    // 自身も Client として扱うため NotifySession + WaitForReady を実行。
                    await NotifySessionInfoToServer(stageId, playerId);

                    // P2P Host モードのみ Lobby Hub に「ホスト準備完了」を通知。
                    // Lobby Hub はこの通知を受けて他クライアントへ OnGameStarting を broadcast し、
                    // Photon セッション作成競合 (GameNotFound) を防ぐ。
                    if (_sessionConfig.ConnectionSource == GameConnectionSource.P2PHost)
                    {
                        Debug.Log("[SurvivorStageConnectScene] Notifying lobby hub: host is ready");
                        await _lobbyClient.NotifyHostReadyAsync();
                    }

                    SceneComponent.SetStatus("Waiting for players...");
                    await WaitForAllPlayersReadyAsync();
                }
                else if (_runnerService.IsServer)
                {
                    // Editor Server-only mode: 全 Client 接続待ち
                    SceneComponent.SetStatus("Waiting for clients...");
                    await WaitForAllPlayersReadyAsync();
                }

                // Phase 3: StageScene へ遷移 (ConnectionSource で分岐)
                // P2P Host/Client → SurvivorGameStageScene (PR3.5 で導入した統合シーン)
                // DS 経路 (Local/Remote/Matchmaking) → 既存 SurvivorClientStageScene 継続
                var source = _sessionConfig.ConnectionSource;
                Debug.Log($"[DIAG-Phase3-Pre] starting transition, source={source}");
                if (source is GameConnectionSource.P2PHost or GameConnectionSource.P2PClient)
                {
                    Debug.Log($"[SurvivorStageConnectScene] Connection established (source={source}), transitioning to SurvivorGameStageScene");
                    await _sceneService.TransitionAsync<SurvivorGameStageScene>();
                }
                else
                {
                    Debug.Log($"[SurvivorStageConnectScene] Connection established (source={source}), transitioning to SurvivorClientStageScene");
                    await _sceneService.TransitionAsync<SurvivorClientStageScene>();
                }
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
            if (_sessionConfig.IsClientConfigured)
                return; // マッチメイキング経由 → Phase 2 で接続

            // IssueTokenAsync (signed POST) 前に refresh を保証。
            // 以降 IssueTokenAsync が 3 経路で呼ばれるが、refresher 内部で dedup されるため 1 回で済む。
            await _authSessionRefresher.EnsureFreshAsync();

            if (GameEnvironmentHelper.CurrentConfig?.UseLocalServerOrchestrator == true)
            {
                Debug.Log("[SurvivorStageConnectScene] Starting local server orchestrator...");
                SceneComponent.SetStatus("Starting local server...");
                await _localServerOrchestrator.StartAsync(SceneComponent.destroyCancellationToken);

                var localTokenResult = await IssueTokenAsync(stageId);
                _sessionConfig.Configure(GameConnectionSource.Local,
                    port: _localServerOrchestrator.HeadlessServerPort,
                    sessionName: localTokenResult.SessionName,
                    sessionToken: localTokenResult.Token);
                return;
            }

            // DS アドレスをトークンレスポンスから動的取得し、接続先を決定する。
            // 優先順位: 1) レスポンスの ServerAddress → Remote 接続
            //           2) envConfig.UnityServerAddress → Remote フォールバック
            //           3) それ以外 → Local 接続（127.0.0.1）
            var tokenResult = await IssueTokenAsync(stageId);
            var envConfig = GameEnvironmentHelper.CurrentConfig;

            if (!string.IsNullOrEmpty(tokenResult?.ServerAddress) && tokenResult.ServerPort > 0)
            {
                // DS 割り当て済み: レスポンスに含まれる DS アドレスへ直接接続
                _sessionConfig.Configure(GameConnectionSource.Remote,
                    address: tokenResult.ServerAddress,
                    port: (ushort)tokenResult.ServerPort,
                    sessionName: tokenResult.SessionName,
                    sessionToken: tokenResult.Token);
                Debug.Log($"[SurvivorStageConnectScene] DS アドレスをトークンレスポンスから取得: {_sessionConfig.ServerAddress}:{_sessionConfig.ServerPort} ({_sessionConfig.SessionName})");
            }
            else if (envConfig != null && !_sessionConfig.IsLocalAddress(envConfig.UnityServerAddress))
            {
                // envConfig にリモートアドレスが設定されている場合のフォールバック（ローカル開発用）
                _sessionConfig.Configure(GameConnectionSource.Remote,
                    address: envConfig.UnityServerAddress,
                    port: envConfig.UnityServerPort,
                    sessionName: tokenResult?.SessionName,
                    sessionToken: tokenResult?.Token);
                Debug.Log($"[SurvivorStageConnectScene] envConfig フォールバック: {_sessionConfig.ServerAddress}:{_sessionConfig.ServerPort} ({_sessionConfig.SessionName})");
            }
            else
            {
                // ローカル接続（127.0.0.1）
                _sessionConfig.Configure(GameConnectionSource.Local, sessionName: tokenResult?.SessionName, sessionToken: tokenResult?.Token);
                Debug.Log($"[SurvivorStageConnectScene] ローカルサーバーへ接続 ({_sessionConfig.SessionName})...");
            }
        }

        /// <summary>
        /// SP 接続用セッショントークンとセッション名を Game.Server から取得する。
        /// 取得失敗時は null を返す。
        /// </summary>
        /// <returns>取得成功時はトークンレスポンス、失敗時は null。</returns>
        private async UniTask<UnityServerAuthResponse> IssueTokenAsync(int stageId = 0, int playerCount = 1)
        {
            var response = await _unityServerApiService.IssueTokenAsync(stageId, playerCount);
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
            var source = _sessionConfig.ConnectionSource;
            Debug.Log($"[SurvivorStageConnectScene] Connecting via Fusion (source={source}, session={_sessionConfig.SessionName}, stageId={stageId})");
            await _networkConnector.ConnectAsync(stageId);
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
            _sessionConfig.Clear();
            await _sceneService.TransitionAsync<SurvivorTitleScene>();
        }
    }
}
