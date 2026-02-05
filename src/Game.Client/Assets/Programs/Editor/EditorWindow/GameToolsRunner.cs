using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor
{
    /// <summary>
    /// Game.Tools CLIコマンドをUnity Editorから実行するユーティリティ
    /// </summary>
    public static class GameToolsRunner
    {
        private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        private static readonly string GameToolsProject = Path.Combine(ProjectRoot, "src", "Game.Tools");

        /// <summary>
        /// Game.Toolsコマンドを同期実行
        /// </summary>
        public static GameToolsResult Run(string command, string args, int timeoutMs = 120000)
        {
            var startInfo = CreateStartInfo(command, args);

            try
            {
                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null) outputBuilder.AppendLine(e.Data);
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null) errorBuilder.AppendLine(e.Data);
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                bool completed = process.WaitForExit(timeoutMs);

                if (!completed)
                {
                    process.Kill();
                    return new GameToolsResult
                    {
                        Success = false,
                        Output = outputBuilder.ToString(),
                        Error = "Process timed out",
                        ExitCode = -1
                    };
                }

                // WaitForExit(timeout) の後に WaitForExit() を呼ぶと非同期出力が確実に完了する
                process.WaitForExit();

                return new GameToolsResult
                {
                    Success = process.ExitCode == 0,
                    Output = outputBuilder.ToString(),
                    Error = errorBuilder.ToString(),
                    ExitCode = process.ExitCode
                };
            }
            catch (Exception ex)
            {
                return new GameToolsResult
                {
                    Success = false,
                    Output = string.Empty,
                    Error = ex.Message,
                    ExitCode = -1
                };
            }
        }

        /// <summary>
        /// Game.Toolsコマンドを非同期実行（UIブロッキングなし）
        /// </summary>
        public static async Task<GameToolsResult> RunAsync(string command, string args, Action<string> onOutput = null)
        {
            var startInfo = CreateStartInfo(command, args);

            try
            {
                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();

                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        outputBuilder.AppendLine(e.Data);
                        onOutput?.Invoke(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data != null)
                    {
                        errorBuilder.AppendLine(e.Data);
                        onOutput?.Invoke($"[ERROR] {e.Data}");
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                return new GameToolsResult
                {
                    Success = process.ExitCode == 0,
                    Output = outputBuilder.ToString(),
                    Error = errorBuilder.ToString(),
                    ExitCode = process.ExitCode
                };
            }
            catch (Exception ex)
            {
                return new GameToolsResult
                {
                    Success = false,
                    Output = string.Empty,
                    Error = ex.Message,
                    ExitCode = -1
                };
            }
        }

        private static ProcessStartInfo CreateStartInfo(string command, string args)
        {
            return new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{GameToolsProject}\" -- {command} {args}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = ProjectRoot,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
        }

        #region Convenience Methods

        /// <summary>
        /// クライアント用バイナリをビルド
        /// </summary>
        public static GameToolsResult BuildClient()
        {
            return Run("masterdata", "build " +
                "--tsv-dir masterdata/raw/ " +
                "--proto-dir masterdata/proto/ " +
                "--out-client src/Game.Client/Assets/MasterData/MasterDataBinary.bytes");
        }

        /// <summary>
        /// サーバー用バイナリをビルド
        /// </summary>
        public static GameToolsResult BuildServer()
        {
            return Run("masterdata", "build " +
                "--tsv-dir masterdata/raw/ " +
                "--proto-dir masterdata/proto/ " +
                "--out-server src/Game.Server/MasterData/masterdata.bytes");
        }

        /// <summary>
        /// クライアント＋サーバー両方のバイナリをビルド
        /// </summary>
        public static GameToolsResult BuildAll()
        {
            return Run("masterdata", "build " +
                "--tsv-dir masterdata/raw/ " +
                "--proto-dir masterdata/proto/ " +
                "--out-client src/Game.Client/Assets/MasterData/MasterDataBinary.bytes " +
                "--out-server src/Game.Server/MasterData/masterdata.bytes");
        }

        /// <summary>
        /// ProtoスキーマからC#クラスを生成
        /// </summary>
        public static GameToolsResult Codegen()
        {
            return Run("masterdata", "codegen " +
                "--proto-dir masterdata/proto/ " +
                "--out-client src/Game.Client/Assets/Programs/Runtime/Shared/MasterData/ " +
                "--out-server src/Game.Server/MasterData/");
        }

        /// <summary>
        /// TSVファイルを検証
        /// </summary>
        public static GameToolsResult Validate()
        {
            return Run("masterdata", "validate " +
                "--tsv-dir masterdata/raw/ " +
                "--proto-dir masterdata/proto/");
        }

        #endregion

        #region Migrate Methods

        /// <summary>
        /// 保留中のマイグレーションを適用
        /// </summary>
        public static GameToolsResult MigrateUp(string schema = "")
        {
            var args = "up";
            if (!string.IsNullOrEmpty(schema))
            {
                args += $" --schema {schema}";
            }
            return Run("migrate", args);
        }

        /// <summary>
        /// マイグレーションをロールバック
        /// </summary>
        public static GameToolsResult MigrateDown(int steps = 1, string schema = "")
        {
            var args = $"down --steps {steps}";
            if (!string.IsNullOrEmpty(schema))
            {
                args += $" --schema {schema}";
            }
            return Run("migrate", args);
        }

        /// <summary>
        /// マイグレーション状態を表示
        /// </summary>
        public static GameToolsResult MigrateStatus(string schema = "")
        {
            var args = "status";
            if (!string.IsNullOrEmpty(schema))
            {
                args += $" --schema {schema}";
            }
            return Run("migrate", args);
        }

        /// <summary>
        /// データベースをリセット（削除＋再作成）
        /// </summary>
        public static GameToolsResult MigrateReset(bool seed = false, string schema = "")
        {
            var args = "reset --force --version 9999999999";
            if (seed)
            {
                args += " --seed";
            }
            if (!string.IsNullOrEmpty(schema))
            {
                args += $" --schema {schema}";
            }
            return Run("migrate", args);
        }

        #endregion

        #region SeedData Methods

        /// <summary>
        /// TSVファイルからデータベースにシード
        /// </summary>
        public static GameToolsResult SeedData(string tsvDir = "masterdata/raw/", string schema = "master")
        {
            return Run("seeddata", $"seed --tsv-dir {tsvDir} --schema {schema}");
        }

        /// <summary>
        /// データベースからTSVファイルにダンプ
        /// </summary>
        public static GameToolsResult DumpData(string outDir = "masterdata/dump/", string schema = "master")
        {
            return Run("seeddata", $"dump --out-dir {outDir} --schema {schema}");
        }

        /// <summary>
        /// 2つのTSVディレクトリを比較
        /// </summary>
        public static GameToolsResult DiffData(string sourceDir, string targetDir)
        {
            return Run("seeddata", $"diff --source-dir {sourceDir} --target-dir {targetDir}");
        }

        #endregion
    }

    /// <summary>
    /// Game.Tools実行結果
    /// </summary>
    public class GameToolsResult
    {
        public bool Success { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
        public int ExitCode { get; set; }

        public string GetCombinedOutput()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Output))
            {
                sb.AppendLine(Output);
            }
            if (!string.IsNullOrEmpty(Error))
            {
                sb.AppendLine(Error);
            }
            return sb.ToString();
        }
    }
}
