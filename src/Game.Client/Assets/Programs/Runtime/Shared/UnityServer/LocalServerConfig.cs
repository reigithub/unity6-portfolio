#if !UNITY_SERVER
using System.IO;
using UnityEngine;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// SP モードのローカルサーバー構成情報。
    /// Editor / Distribution ビルドで自動パス検出。
    /// </summary>
    public class LocalServerConfig
    {
        // .env ファイルパス
        public string DotEnvFilePath { get; private set; }

        // PostgreSQL
        public string PgBinDir { get; private set; }
        public string PgDataDir { get; private set; }

        // Game.Server
        public string GameServerProjectPath { get; private set; }
        public string GameServerExePath { get; private set; }
        public bool IsEditorMode { get; private set; }

        // Headless Server
        public string HeadlessServerExePath { get; private set; }

        // Ports
        public int PgPort { get; private set; } = 15432;
        public int GameServerPort { get; private set; } = 15000;
        public ushort HeadlessServerPort { get; private set; } = 7777;

        // PID file
        public string PidFilePath { get; private set; }

        /// <summary>
        /// ランタイム環境から自動検出
        /// </summary>
        public static LocalServerConfig Detect()
        {
            var config = new LocalServerConfig();

#if UNITY_EDITOR
            // Application.dataPath = .../src/Game.Client/Assets
            // リポジトリルート = 3階層上
            var repoRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));

            config.IsEditorMode = true;
            config.DotEnvFilePath = Path.Combine(repoRoot, "docker", "game-server", ".env");
            config.PgBinDir = Path.Combine(repoRoot, "tools", "pgsql", "bin");
            config.PgDataDir = Path.Combine(repoRoot, "tools", "pgdata");
            config.GameServerProjectPath = Path.Combine(repoRoot, "src", "Game.Server");
            config.GameServerExePath = null; // Editor では dotnet run を使用
            config.HeadlessServerExePath = FindHeadlessServerExe(
                Path.Combine(repoRoot, "Builds", "Server-Windows-Dev"));
            config.PidFilePath = Path.Combine(repoRoot, "tools", "sp-server.pid");
#else
            // Distribution: 実行ファイル相対
            var appDir = Path.GetDirectoryName(Application.dataPath);

            config.IsEditorMode = false;
            config.PgBinDir = Path.Combine(appDir, "pgsql", "bin");
            config.PgDataDir = Path.Combine(appDir, "pgdata");
            config.GameServerProjectPath = null;
            config.GameServerExePath = Path.Combine(appDir, "Server", "Game.Server.exe");
            config.HeadlessServerExePath = FindHeadlessServerExe(
                Path.Combine(appDir, "DedicatedServer"));
            config.PidFilePath = Path.Combine(appDir, "sp-server.pid");
#endif

            return config;
        }

        private static string FindHeadlessServerExe(string directory)
        {
            if (!Directory.Exists(directory)) return null;

            var files = Directory.GetFiles(directory, "*_Server.exe", SearchOption.TopDirectoryOnly);
            return files.Length > 0 ? files[0] : null;
        }
    }
}
#endif
