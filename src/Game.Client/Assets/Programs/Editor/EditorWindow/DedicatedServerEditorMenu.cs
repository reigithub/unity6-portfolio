using System;
using System.Diagnostics;
using System.IO;
using Game.Shared.Environment;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor
{
    /// <summary>
    /// Unity Editor メニューから Windows Dedicated Server を起動・停止する。
    /// Project/Server/Start Dedicated Server / Stop Dedicated Server で操作可能。
    /// </summary>
    public static partial class ProjectEditorMenu
    {
        /// <summary>DS プロセス起動時に渡すデフォルトの Fusion ゲームポート。</summary>
        private const string DsDefaultPort = "7777";

        /// <summary>DS プロセス起動時に渡すデフォルトのヘルスチェックポート。</summary>
        private const string DsDefaultHealthPort = "7778";

        private static Process _dsProcess;

        /// <summary>ドメインリロード後にプロセスを再取得するための PID。</summary>
        private static int _dsPid
        {
            get => SessionState.GetInt("DedicatedServer_PID", 0);
            set => SessionState.SetInt("DedicatedServer_PID", value);
        }

        /// <summary>ドメインリロード後に PID からプロセスを復元する。</summary>
        [InitializeOnLoadMethod]
        private static void RestoreProcessAfterDomainReload()
        {
            var pid = _dsPid;
            if (pid <= 0) return;

            try
            {
                var process = Process.GetProcessById(pid);
                if (!process.HasExited && process.ProcessName.Contains("Unity6GameServer"))
                {
                    _dsProcess = process;
                    EditorApplication.quitting -= OnEditorQuitting;
                    EditorApplication.quitting += OnEditorQuitting;
                }
                else
                {
                    _dsPid = 0;
                }
            }
            catch
            {
                _dsPid = 0;
            }
        }

        // ---------------------------------------------------------------
        // メニューコマンド
        // ---------------------------------------------------------------

        /// <summary>
        /// Windows Dedicated Server を起動する。
        /// exe が存在しない場合はエラーを表示する。
        /// </summary>
        [MenuItem("Project/Server/Start Dedicated Server")]
        public static void StartDedicatedServer()
        {
            if (_dsProcess != null && !_dsProcess.HasExited)
            {
                Debug.LogWarning("[DedicatedServer] 既に起動中です (PID: " + _dsProcess.Id + ")");
                return;
            }

            // exe パス検出
            var exePath = ResolveDsExePath();
            if (exePath == null) return;

            // .env から環境変数を読み込む (パスは EnvVarParser が自動探索)
            var envFilePath = EnvVarHelper.FindDefaultEnvFile();
            var envVars = EnvVarHelper.Parse(envFilePath);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[DedicatedServer] .env 読み込み: {envFilePath ?? "(not found)"}");
#endif

            // .env からポートをオーバーライド (未設定時はデフォルト)
            var port = EnvVarHelper.GetValueOrDefault(envVars, EnvVarKeys.UnityServerPort, DsDefaultPort.ToString());
            var healthPort = EnvVarHelper.GetValueOrDefault(envVars, EnvVarKeys.UnityServerHealthPort, DsDefaultHealthPort.ToString());

            // ProcessStartInfo 構築
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-batchmode -nographics" +
                            $" --port {port}" +
                            $" --health-port {healthPort}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };


            // HMAC シークレット
            if (EnvVarHelper.TryGetValue(envVars, EnvVarKeys.UnityServerAuthSecretKey, out var secret))
            {
                psi.Environment[EnvVarKeys.UnityServerAuthSecretKey] = secret;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DedicatedServer] {EnvVarKeys.UnityServerAuthSecretKey} を設定しました : {secret}");
#endif
            }
            else
            {
                Debug.LogWarning($"[DedicatedServer] .env に {EnvVarKeys.UnityServerAuthSecretKey} が見つかりません。HMAC 認証なしで起動します");
            }

            // Game.Server URL を .env から取得
            if (EnvVarHelper.TryGetValue(envVars, EnvVarKeys.GameServerUrl, out var gameServerUrl))
            {
                psi.Environment[EnvVarKeys.GameServerUrl] = gameServerUrl;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[DedicatedServer] {EnvVarKeys.GameServerUrl} を設定しました : {secret}");
#endif
            }
            else
            {
                Debug.LogWarning($"[DedicatedServer] .env に {EnvVarKeys.GameServerUrl} が見つかりません。DS は Game.Server への自己登録をスキップします");
            }

            // プロセス起動
            try
            {
                _dsProcess = Process.Start(psi);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DedicatedServer] プロセス起動に失敗しました: {ex.Message}");
                _dsProcess = null;
                return;
            }

            if (_dsProcess == null)
            {
                Debug.LogError("[DedicatedServer] Process.Start が null を返しました");
                return;
            }

            _dsPid = _dsProcess.Id;
            Debug.Log($"[DedicatedServer] 起動しました (PID: {_dsProcess.Id}) port={port} health={healthPort}");

            // 標準出力・エラーを非同期で Unity Console に転送
            _dsProcess.OutputDataReceived += OnDsOutputReceived;
            _dsProcess.ErrorDataReceived += OnDsErrorReceived;
            _dsProcess.BeginOutputReadLine();
            _dsProcess.BeginErrorReadLine();

            // Editor 終了時に自動停止
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        /// <summary>
        /// 起動中の Windows Dedicated Server を停止する。
        /// </summary>
        [MenuItem("Project/Server/Stop Dedicated Server")]
        public static void StopDedicatedServer()
        {
            if (_dsProcess == null || _dsProcess.HasExited)
            {
                Debug.Log("[DedicatedServer] 起動中のサーバーがありません");
                _dsProcess = null;
                return;
            }

            try
            {
                _dsProcess.Kill();
                _dsProcess.WaitForExit(5000);
                Debug.Log("[DedicatedServer] 停止しました");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DedicatedServer] 停止中にエラーが発生しました: {ex.Message}");
            }
            finally
            {
                _dsProcess = null;
                _dsPid = 0;
                EditorApplication.quitting -= OnEditorQuitting;
            }
        }

        // ---------------------------------------------------------------
        // メニュー有効化制御
        // ---------------------------------------------------------------

        /// <summary>DS が停止中のときのみ「Start」を有効化する。</summary>
        [MenuItem("Project/Server/Start Dedicated Server", true)]
        public static bool ValidateStartDedicatedServer() =>
            _dsProcess == null || _dsProcess.HasExited;

        /// <summary>DS が起動中のときのみ「Stop」を有効化する。</summary>
        [MenuItem("Project/Server/Stop Dedicated Server", true)]
        public static bool ValidateStopDedicatedServer() =>
            _dsProcess != null && !_dsProcess.HasExited;

        // ---------------------------------------------------------------
        // 内部ヘルパー
        // ---------------------------------------------------------------

        /// <summary>
        /// リポジトリルートを Application.dataPath から算出する。
        /// </summary>
        private static string ResolveRepoRoot()
        {
            // Application.dataPath = .../src/Game.Client/Assets
            // リポジトリルート = 3階層上
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        }

        /// <summary>
        /// DS の exe パスを解決する。見つからなければエラーログを出して null を返す。
        /// </summary>
        private static string ResolveDsExePath()
        {
            var repoRoot = ResolveRepoRoot();
            var exePath = Path.Combine(repoRoot, "src", "Game.Client", "Builds", "Server", "Windows", "Unity6GameServer.exe");

            if (File.Exists(exePath)) return exePath;

            Debug.LogError(
                $"[DedicatedServer] exe が見つかりません: {exePath}\n" +
                "Unity Editor の Build > Server > Windows Dedicated Server Development でビルドしてください。");
            return null;
        }

        /// <summary>DS 標準出力を Unity Console に転送する（非メインスレッドから呼ばれる）。</summary>
        private static void OnDsOutputReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                Debug.Log($"[DS] {e.Data}");
        }

        /// <summary>DS 標準エラーを Unity Console に転送する（非メインスレッドから呼ばれる）。</summary>
        private static void OnDsErrorReceived(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
                Debug.LogWarning($"[DS] {e.Data}");
        }

        /// <summary>Editor 終了時に DS プロセスを自動停止する。</summary>
        private static void OnEditorQuitting()
        {
            if (_dsProcess != null && !_dsProcess.HasExited)
            {
                Debug.Log("[DedicatedServer] Editor 終了に伴いサーバーを停止します");
                StopDedicatedServer();
            }
        }
    }
}
