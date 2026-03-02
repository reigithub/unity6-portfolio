#if !UNITY_SERVER
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Game.Shared.Server
{
    /// <summary>
    /// 組み込み Valkey のライフサイクル管理。
    /// valkey-server 起動 → PING 確認 → Process.Kill で停止。
    /// </summary>
    public class EmbeddedValkeyManager
    {
        private readonly string _binaryPath;
        private readonly int _port;

        private Process _process;
        private bool _isRunning;

        public int ProcessId => _process?.Id ?? 0;

        public EmbeddedValkeyManager(string binaryPath, int port)
        {
            _binaryPath = binaryPath;
            _port = port;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_isRunning) return;

            ValidateBinary();

            Debug.Log($"[EmbeddedValkey] Starting valkey-server on port {_port}...");

            var psi = new ProcessStartInfo
            {
                FileName = _binaryPath,
                Arguments = $"--port {_port} --save \"\" --appendonly no --daemonize no",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _process = Process.Start(psi);
            if (_process == null)
            {
                throw new InvalidOperationException("[EmbeddedValkey] Failed to start valkey-server");
            }

            // ヘルスチェック: PING → +PONG
            await WaitForReadyAsync(ct);

            _isRunning = true;
            Debug.Log("[EmbeddedValkey] Valkey is ready");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                Debug.Log("[EmbeddedValkey] Stopping Valkey...");
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(5000);
                }
                _isRunning = false;
                _process = null;
                Debug.Log("[EmbeddedValkey] Valkey stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EmbeddedValkey] Failed to stop: {ex.Message}");
            }
        }

        private void ValidateBinary()
        {
            if (!File.Exists(_binaryPath))
            {
                throw new FileNotFoundException(
                    $"[EmbeddedValkey] Binary not found: {_binaryPath}\n" +
                    "Valkey バイナリを tools/valkey/valkey-server.exe に配置してください。");
            }
        }

        private async UniTask WaitForReadyAsync(CancellationToken ct)
        {
            var timeout = TimeSpan.FromSeconds(10);
            var startTime = DateTime.UtcNow;
            var pingBytes = Encoding.ASCII.GetBytes("PING\r\n");
            var buffer = new byte[64];

            while (DateTime.UtcNow - startTime < timeout)
            {
                ct.ThrowIfCancellationRequested();

                // プロセスが終了していたら即失敗
                if (_process == null || _process.HasExited)
                {
                    throw new InvalidOperationException("[EmbeddedValkey] valkey-server process exited unexpectedly");
                }

                try
                {
                    using (var client = new TcpClient())
                    {
                        await client.ConnectAsync("localhost", _port);
                        var stream = client.GetStream();
                        await stream.WriteAsync(pingBytes, 0, pingBytes.Length, ct);
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, ct);
                        var response = Encoding.ASCII.GetString(buffer, 0, bytesRead);

                        if (response.Contains("+PONG"))
                        {
                            return;
                        }
                    }
                }
                catch (SocketException)
                {
                    // まだ接続できない
                }

                await UniTask.Delay(200, cancellationToken: ct);
            }

            throw new TimeoutException("[EmbeddedValkey] Valkey did not become ready within 10 seconds");
        }
    }
}
#endif
