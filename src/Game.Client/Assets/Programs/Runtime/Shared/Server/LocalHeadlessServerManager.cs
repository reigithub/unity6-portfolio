#if !UNITY_SERVER
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Debug = UnityEngine.Debug;

namespace Game.Shared.Server
{
    /// <summary>
    /// Unity Headless Server プロセスのライフサイクル管理。
    /// {ProductName}_Server.exe --port 7777 -batchmode -nographics で起動。
    /// </summary>
    public class LocalHeadlessServerManager
    {
        private readonly string _exePath;
        private readonly ushort _port;

        private Process _process;
        private bool _isRunning;

        public int ProcessId => _process?.Id ?? 0;

        public LocalHeadlessServerManager(string exePath, ushort port)
        {
            _exePath = exePath;
            _port = port;
        }

        public async UniTask StartAsync(CancellationToken ct)
        {
            if (_isRunning) return;

            ValidateBinary();

            Debug.Log($"[LocalHeadless] Starting headless server: {Path.GetFileName(_exePath)} --port {_port}...");

            var psi = new ProcessStartInfo
            {
                FileName = _exePath,
                Arguments = $"--port {_port} -batchmode -nographics",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            _process = Process.Start(psi);
            if (_process == null)
            {
                throw new InvalidOperationException("[LocalHeadless] Failed to start headless server");
            }

            // 5秒待機 + プロセス生存確認
            await WaitForReadyAsync(ct);

            _isRunning = true;
            Debug.Log("[LocalHeadless] Headless server is ready");
        }

        public void Stop()
        {
            if (!_isRunning) return;

            try
            {
                Debug.Log("[LocalHeadless] Stopping headless server...");
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                    _process.WaitForExit(5000);
                }
                _isRunning = false;
                _process = null;
                Debug.Log("[LocalHeadless] Headless server stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[LocalHeadless] Failed to stop: {ex.Message}");
            }
        }

        private void ValidateBinary()
        {
            if (string.IsNullOrEmpty(_exePath) || !File.Exists(_exePath))
            {
                throw new FileNotFoundException(
                    $"[LocalHeadless] Headless server binary not found: {_exePath}\n" +
                    "Unity Editor の Build > Server > Windows Dedicated Server Development でビルドしてください。");
            }
        }

        private async UniTask WaitForReadyAsync(CancellationToken ct)
        {
            // Headless Server は HTTP エンドポイントがないため、プロセス生存確認のみ
            await UniTask.Delay(TimeSpan.FromSeconds(5), cancellationToken: ct);

            if (_process == null || _process.HasExited)
            {
                throw new InvalidOperationException("[LocalHeadless] Headless server process exited during startup");
            }
        }
    }
}
#endif
