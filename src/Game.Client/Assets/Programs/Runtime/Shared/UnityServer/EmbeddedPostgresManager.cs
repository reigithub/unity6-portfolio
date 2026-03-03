#if !UNITY_SERVER
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// 組み込み PostgreSQL のライフサイクル管理。
    /// initdb → pg_ctl start → createdb → pg_ctl stop。
    /// </summary>
    public class EmbeddedPostgresManager
    {
        private const string DatabaseName = "gameserver";
        private const string Username = "gameuser";

        private readonly string _binDir;
        private readonly string _dataDir;
        private readonly int _port;

        private bool _isRunning;

        public int ProcessId { get; private set; }

        public EmbeddedPostgresManager(string binDir, string dataDir, int port)
        {
            _binDir = binDir;
            _dataDir = dataDir;
            _port = port;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_isRunning) return;

            ValidateBinaries();

            var isFirstRun = !File.Exists(Path.Combine(_dataDir, "PG_VERSION"));

            if (isFirstRun)
            {
                Debug.Log("[EmbeddedPostgres] First run: initializing database cluster...");
                await RunProcessAsync(
                    Path.Combine(_binDir, "initdb"),
                    $"-D \"{_dataDir}\" --username={Username} --auth=trust --encoding=UTF8",
                    ct);

                // postgresql.conf にポートとリッスンアドレスを追記
                var confPath = Path.Combine(_dataDir, "postgresql.conf");
                File.AppendAllText(confPath, $"\nport = {_port}\nlisten_addresses = 'localhost'\n");
                Debug.Log($"[EmbeddedPostgres] Configured port={_port}");
            }

            // pg_ctl start
            Debug.Log("[EmbeddedPostgres] Starting PostgreSQL...");
            var logPath = Path.Combine(_dataDir, "pg.log");
            await RunProcessAsync(
                Path.Combine(_binDir, "pg_ctl"),
                $"start -D \"{_dataDir}\" -l \"{logPath}\" -w",
                ct);

            // PID を取得（postmaster.pid の1行目）
            var pidFile = Path.Combine(_dataDir, "postmaster.pid");
            if (File.Exists(pidFile))
            {
                var pidLine = File.ReadAllLines(pidFile)[0].Trim();
                if (int.TryParse(pidLine, out var pid))
                {
                    ProcessId = pid;
                }
            }

            // pg_isready でポーリング
            await WaitForReadyAsync(ct);

            if (isFirstRun)
            {
                Debug.Log("[EmbeddedPostgres] Creating database...");
                await RunProcessAsync(
                    Path.Combine(_binDir, "createdb"),
                    $"-h localhost -p {_port} -U {Username} {DatabaseName}",
                    ct);
            }

            _isRunning = true;
            Debug.Log("[EmbeddedPostgres] PostgreSQL is ready");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                Debug.Log("[EmbeddedPostgres] Stopping PostgreSQL...");
                var pgCtl = Path.Combine(_binDir, "pg_ctl");
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = pgCtl,
                    Arguments = $"stop -D \"{_dataDir}\" -m fast",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                });
                process?.WaitForExit(10000);
                _isRunning = false;
                ProcessId = 0;
                Debug.Log("[EmbeddedPostgres] PostgreSQL stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EmbeddedPostgres] Failed to stop: {ex.Message}");
            }
        }

        private void ValidateBinaries()
        {
            var requiredBinaries = new[] { "initdb", "pg_ctl", "pg_isready", "createdb", "postgres" };
            foreach (var binary in requiredBinaries)
            {
                var path = Path.Combine(_binDir, binary + ".exe");
                if (!File.Exists(path))
                {
                    throw new FileNotFoundException(
                        $"[EmbeddedPostgres] Required binary not found: {path}\n" +
                        "PostgreSQL バイナリを tools/pgsql/bin/ に配置してください。");
                }
            }
        }

        private async UniTask WaitForReadyAsync(CancellationToken ct)
        {
            var pgIsReady = Path.Combine(_binDir, "pg_isready");
            var timeout = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;

            while (DateTime.UtcNow - startTime < timeout)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = pgIsReady,
                        Arguments = $"-h localhost -p {_port}",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                    };
                    var process = Process.Start(psi);
                    process?.WaitForExit(5000);

                    if (process?.ExitCode == 0)
                    {
                        return;
                    }
                }
                catch
                {
                    // pg_isready がまだ応答しない
                }

                await UniTask.Delay(500, cancellationToken: ct);
            }

            throw new TimeoutException("[EmbeddedPostgres] PostgreSQL did not become ready within 30 seconds");
        }

        private async UniTask RunProcessAsync(string fileName, string arguments, CancellationToken ct)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            var process = Process.Start(psi);
            if (process == null)
            {
                throw new InvalidOperationException($"[EmbeddedPostgres] Failed to start: {fileName}");
            }

            var stdout = await process.StandardOutput.ReadToEndAsync();
            var stderr = await process.StandardError.ReadToEndAsync();

            while (!process.HasExited)
            {
                ct.ThrowIfCancellationRequested();
                await UniTask.Delay(100, cancellationToken: ct);
            }

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    $"[EmbeddedPostgres] {Path.GetFileName(fileName)} failed (exit={process.ExitCode})\n" +
                    $"stdout: {stdout}\nstderr: {stderr}");
            }
        }
    }
}
#endif
