#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Shared.Environment;
using UnityEngine;

namespace Game.Shared.Server
{
    /// <summary>
    /// SP モード用ローカルサーバーオーケストレーター。
    /// PG → Game.Server → Headless を順次起動し、
    /// Dispose 時に逆順で停止する。
    /// </summary>
    public class LocalServerOrchestrator : ILocalServerOrchestrator
    {
        private readonly LocalServerConfig _config;
        private readonly EmbeddedPostgresManager _postgres;
        private readonly LocalGameServerManager _gameServer;
        private readonly LocalHeadlessServerManager _headless;

        private bool _isReady;
        private bool _isDisposed;

        public bool IsReady => _isReady;
        public ushort HeadlessServerPort => _config.HeadlessServerPort;

        public LocalServerOrchestrator()
        {
            _config = LocalServerConfig.Detect();

            // .env からセッショントークンシークレットを取得
            var envVars = EnvVarParser.Parse(_config.DotEnvFilePath);
            envVars.TryGetValue(EnvVarKeys.UnityServerAuthSecretKey, out var sessionTokenSecret);

            _postgres = new EmbeddedPostgresManager(_config.PgBinDir, _config.PgDataDir, _config.PgPort);
            _gameServer = new LocalGameServerManager(_config);
            _headless = new LocalHeadlessServerManager(
                _config.HeadlessServerExePath,
                _config.HeadlessServerPort,
                sessionTokenSecret);

            Application.quitting += OnApplicationQuitting;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_isReady) return;

            Debug.Log("[LocalServerOrchestrator] Starting SP sidecar services...");

            // 1. 孤児プロセスのクリーンアップ
            OrphanProcessGuard.CleanupOrphans(_config.PidFilePath);

            try
            {
                // 2. PostgreSQL
                await _postgres.StartAsync(ct);

                // 3. Game.Server
                await _gameServer.StartAsync(ct);

                // 4. Headless Server
                await _headless.StartAsync(ct);

                // PID 保存
                OrphanProcessGuard.SavePids(
                    _config.PidFilePath,
                    _postgres.ProcessId,
                    _gameServer.ProcessId,
                    _headless.ProcessId);

                _isReady = true;
                Debug.Log("[LocalServerOrchestrator] All SP sidecar services are ready");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalServerOrchestrator] Failed to start: {ex.Message}");
                StopAll();
                throw;
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Application.quitting -= OnApplicationQuitting;
            StopAll();
        }

        private void OnApplicationQuitting()
        {
            Dispose();
        }

        private void StopAll()
        {
            Debug.Log("[LocalServerOrchestrator] Stopping all SP sidecar services...");

            // 逆順で停止
            _headless.Stop();
            _gameServer.Stop();
            _postgres.Stop();

            OrphanProcessGuard.ClearPids(_config.PidFilePath);
            _isReady = false;

            Debug.Log("[LocalServerOrchestrator] All SP sidecar services stopped");
        }
    }
}
#endif
