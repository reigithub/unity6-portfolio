using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.Environment;
using Game.Shared.Network.Survivor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Dedicated Server 起動時の初期化処理を担う IAsyncStartable。
    /// VContainer の RegisterEntryPoint 経由で自動実行される。
    /// </summary>
    /// <remarks>
    /// 起動順序:
    /// 1. <see cref="StartAsync"/> で Config を構築し <see cref="UnityServerConfigProvider"/> にセット
    /// 2. <see cref="IUnityServerHttpListener.Start"/> でヘルスチェックポートをリスン開始
    /// 3. Fusion ポートを設定
    /// 4. Game.Server へ自己登録してハートビートを開始
    /// 5. <see cref="_startupComplete"/> を complete にして他の IAsyncStartable のバリアを解除
    /// </remarks>
    public sealed class UnityServerBootstrap : IAsyncStartable, IDisposable
    {
        private readonly UnityServerConfigProvider _configProvider;
        private readonly IUnityServerHttpListener _listener;
        private readonly IUnityServerRegistryApiClient _registry;
        private readonly IUnityServerSessionConfig _sessionConfig;

        private CancellationTokenSource _heartbeatCts;
        private Task _heartbeatTask;
        private readonly UniTaskCompletionSource _startupComplete = new UniTaskCompletionSource();
        private bool _disposed;
        private bool _quitHandlerAttached;

        /// <summary>
        /// <see cref="UnityServerBootstrap"/> を初期化する。
        /// </summary>
        /// <param name="configProvider">設定プロバイダ。</param>
        /// <param name="listener">HTTP リスナー。</param>
        /// <param name="registry">Registry クライアント。</param>
        /// <param name="sessionConfig">Fusion 接続パラメータ設定用コネクタ。</param>
        [Inject]
        public UnityServerBootstrap(
            UnityServerConfigProvider configProvider,
            IUnityServerHttpListener listener,
            IUnityServerRegistryApiClient registry,
            IUnityServerSessionConfig sessionConfig)
        {
            _configProvider = configProvider;
            _listener = listener;
            _registry = registry;
            _sessionConfig = sessionConfig;
        }

        /// <summary>
        /// 他の <see cref="IAsyncStartable"/>（例: SurvivorServerGameLoop）から StartAsync 完了を待つためのバリア。
        /// Listener.Start() 完了を保証してから呼び出し元の処理が走ることを保証する。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        public UniTask WaitForStartupAsync(CancellationToken ct)
            => _startupComplete.Task.AttachExternalCancellation(ct);

        /// <summary>
        /// サーバー初期化処理を実行する。VContainer から自動呼び出し。
        /// </summary>
        /// <param name="cancellation">キャンセルトークン。</param>
        public async UniTask StartAsync(CancellationToken cancellation)
        {
            Debug.Log("[ServerBootstrap] ========================================");
            Debug.Log("[ServerBootstrap] Dedicated Server starting...");
            Debug.Log($"[ServerBootstrap] BatchMode: {Application.isBatchMode}");
            Debug.Log($"[ServerBootstrap] Platform: {Application.platform}");
            Debug.Log($"[ServerBootstrap] Unity Version: {Application.unityVersion}");
            Debug.Log($"[ServerBootstrap] Product Version: {Application.version}");
            Debug.Log("[ServerBootstrap] ========================================");

            // 1. Config 構築（GCE metadata 含む async。非 GCE では 2 秒 timeout で null）
            var config = await UnityServerConfigFactory.BuildAsync(cancellation);

            Debug.Log($"[ServerBootstrap] DsId={config.DsId}, GamePort={config.GamePort}, HealthPort={config.HealthPort}");
            Debug.Log($"[ServerBootstrap] GameServerUrl={config.GameServerUrl ?? "(none)"}");
            Debug.Log($"[ServerBootstrap] PublicAddress={config.PublicAddress ?? "(none)"}");
            Debug.Log($"[ServerBootstrap] InternalAddress={config.InternalAddress ?? "(none)"}");
            Debug.Log($"[ServerBootstrap] AuthSecretKey={(config.AuthSecretKey.IsEmpty ? "未設定" : "設定済み（HMAC 認証有効）")}");

            // PUBLIC_ADDRESS を環境変数に書き戻す（SurvivorNetworkStageConnector が参照）
            EnvVarHelper.Set(EnvVarKeys.PublicAddress, config.PublicAddress);

            // 2. ConfigProvider に Config をセット（これ以降 _configProvider.Current が使用可能）
            _configProvider.Set(config);

            // サーバー向けフレームレート設定
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // 3. HTTP リスナー起動
            _listener.Start();

            // 4. Fusion ポート設定（SessionName / MaxPlayerCount はセッションリクエスト受信時に設定）
            _sessionConfig.Configure(ConnectionSource.DedicatedServer, port: config.GamePort);

            // 5. Application.quitting ハンドラー登録
            Application.quitting += OnApplicationQuitting;
            _quitHandlerAttached = true;

            // 6. Game.Server への自己登録・ハートビート開始
            if (!string.IsNullOrEmpty(config.GameServerUrl))
            {
                var dsAddress = config.PublicAddress ?? NetworkAddressHelper.GetLocalIPv4Address();
                await _registry.RegisterAsync(dsAddress, config.InternalAddress, cancellation);

                _heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                _heartbeatTask = UnityServerHeartbeatLoop.RunAsync(_registry, TimeSpan.FromSeconds(30), _heartbeatCts.Token);

                Debug.Log("[ServerBootstrap] ハートビート開始（30秒間隔）");
            }
            else
            {
                Debug.LogWarning("[ServerBootstrap] GAME_SERVER_URL が未設定のため自己登録をスキップします");
            }

            // 7. 全ての IAsyncStartable がこのバリアを await できるように complete にする
            _startupComplete.TrySetResult();

            Debug.Log("[ServerBootstrap] 初期化完了");
        }

        /// <summary>
        /// ハートビート停止、Listener 解放、登録解除を行う。
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            Debug.Log("[ServerBootstrap] Dispose 開始");

            // ハートビート停止
            _heartbeatCts?.Cancel();
            try
            {
                _heartbeatTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // 停止待ちのエラーは無視
            }

            // ServerHttpListener 停止
            try
            {
                _listener?.Dispose();
            }
            catch
            {
                // 停止時の例外は無視
            }

            // Game.Server へ登録解除通知
            try
            {
                _registry?.DeregisterAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
            }
            catch
            {
                // 登録解除失敗は無視（シャットダウン中のため）
            }

            if (_quitHandlerAttached)
            {
                Application.quitting -= OnApplicationQuitting;
                _quitHandlerAttached = false;
            }

            Debug.Log("[ServerBootstrap] Dispose 完了");
        }

        private void OnApplicationQuitting() => Dispose();
    }
}
