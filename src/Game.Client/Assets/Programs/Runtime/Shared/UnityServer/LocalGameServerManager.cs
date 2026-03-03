#if !UNITY_SERVER
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.Environment;
using Debug = UnityEngine.Debug;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Game.Server (ASP.NET Core) プロセスのライフサイクル管理。
    /// Editor: dotnet run、Distribution: Game.Server.exe 直接起動。
    /// </summary>
    public class LocalGameServerManager
    {
        private readonly LocalServerConfig _config;

        /// <summary>
        /// .env の Shell-style キー → ASP.NET Core 設定キーのマッピング。
        /// </summary>
        private static readonly (string EnvKey, string ConfigKey)[] _keyMappings =
        {
            ("JWT_SECRET", "Jwt__Secret"),
            (EnvVarKeys.UnityServerAuthSecretKey, "UnityServerAuth__SecretKey"),
        };

        private Process _process;
        private bool _isRunning;

        public int ProcessId => _process?.Id ?? 0;

        public LocalGameServerManager(LocalServerConfig config)
        {
            _config = config;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_isRunning) return;

            ProcessStartInfo psi;

            if (_config.IsEditorMode)
            {
                Debug.Log("[LocalGameServer] Starting Game.Server via dotnet run...");
                psi = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"run --project \"{_config.GameServerProjectPath}\" --no-launch-profile",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
            }
            else
            {
                Debug.Log("[LocalGameServer] Starting Game.Server.exe...");
                psi = new ProcessStartInfo
                {
                    FileName = _config.GameServerExePath,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
            }

            // .env から読み取り（パスが null or ファイル不在なら空 Dictionary）
            var envVars = EnvVarParser.Parse(_config.DotEnvFilePath);

            // 環境変数設定
            psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            psi.Environment["ASPNETCORE_URLS"] = $"http://localhost:{_config.GameServerPort}";

            // DB接続文字列（.env の POSTGRES_* を使用、SP は PgPort が異なる）
            var pgDb = EnvVarParser.GetValueOrDefault(envVars, "POSTGRES_DB", "gameserver");
            var pgUser = EnvVarParser.GetValueOrDefault(envVars, "POSTGRES_USER", "gameuser");
            var pgPass = EnvVarParser.GetValueOrDefault(envVars, "POSTGRES_PASSWORD", "localdev");
            psi.Environment["ConnectionStrings__Default"] =
                $"Host=localhost;Port={_config.PgPort};Database={pgDb};Username={pgUser};Password={pgPass}";

            // Valkey（SP は EmbeddedValkey 不使用 → HealthCheck 無効化）
            psi.Environment["ConnectionStrings__Valkey"] =
                "localhost:16379,abortConnect=false,connectTimeout=1000";
            psi.Environment["RequestSigning__Enabled"] = "false";
            psi.Environment["HealthChecks__Valkey__Enabled"] = "false";

            // .env → ASP.NET Core キーマッピング（JWT_SECRET → Jwt__Secret 等）
            foreach (var (envKey, configKey) in _keyMappings)
            {
                if (envVars.TryGetValue(envKey, out var val))
                {
                    psi.Environment[configKey] = val;
                }
            }

            _process = Process.Start(psi);
            if (_process == null)
            {
                throw new InvalidOperationException("[LocalGameServer] Failed to start Game.Server");
            }

            // /health ポーリング
            await WaitForHealthyAsync(ct);

            _isRunning = true;
            Debug.Log("[LocalGameServer] Game.Server is ready");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                Debug.Log("[LocalGameServer] Stopping Game.Server...");
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(5000);
                }
                _isRunning = false;
                _process = null;
                Debug.Log("[LocalGameServer] Game.Server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalGameServer] Failed to stop: {ex.Message}");
            }
        }

        private async UniTask WaitForHealthyAsync(CancellationToken ct)
        {
            var healthUrl = $"http://localhost:{_config.GameServerPort}/health";
            var timeout = TimeSpan.FromSeconds(60);
            var startTime = DateTime.UtcNow;

            using (var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) })
            {
                while (DateTime.UtcNow - startTime < timeout)
                {
                    ct.ThrowIfCancellationRequested();

                    // プロセスが終了していたら即失敗
                    if (_process == null || _process.HasExited)
                    {
                        throw new InvalidOperationException(
                            "[LocalGameServer] Game.Server process exited unexpectedly");
                    }

                    try
                    {
                        var response = await httpClient.GetAsync(healthUrl, ct);
                        if ((int)response.StatusCode == 200)
                        {
                            return;
                        }
                    }
                    catch (HttpRequestException)
                    {
                        // まだ起動していない
                    }
                    catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                    {
                        // HTTP タイムアウト
                    }

                    await UniTask.Delay(1000, cancellationToken: ct);
                }
            }

            throw new TimeoutException("[LocalGameServer] Game.Server did not become healthy within 60 seconds");
        }
    }
}
#endif
