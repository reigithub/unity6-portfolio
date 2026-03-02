#if !UNITY_SERVER
using System;
using System.Diagnostics;
using System.IO;
using Debug = UnityEngine.Debug;

namespace Game.Shared.Server
{
    /// <summary>
    /// 孤児プロセス管理。
    /// クラッシュ後の次回起動時に前回のプロセスを検出・終了。
    /// </summary>
    public static class OrphanProcessGuard
    {
        /// <summary>
        /// PID ファイルから孤児プロセスを検出・終了
        /// </summary>
        public static void CleanupOrphans(string pidFilePath)
        {
            if (string.IsNullOrEmpty(pidFilePath) || !File.Exists(pidFilePath))
            {
                return;
            }

            try
            {
                var lines = File.ReadAllLines(pidFilePath);
                foreach (var line in lines)
                {
                    if (int.TryParse(line.Trim(), out var pid) && pid > 0)
                    {
                        KillProcess(pid);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OrphanProcessGuard] Failed to read PID file: {ex.Message}");
            }
            finally
            {
                ClearPids(pidFilePath);
            }
        }

        /// <summary>
        /// 4つの PID をファイルに保存
        /// </summary>
        public static void SavePids(string pidFilePath, int pgPid, int valkeyPid, int gameServerPid, int headlessPid)
        {
            try
            {
                var dir = Path.GetDirectoryName(pidFilePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                File.WriteAllText(pidFilePath, $"{pgPid}\n{valkeyPid}\n{gameServerPid}\n{headlessPid}\n");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OrphanProcessGuard] Failed to save PIDs: {ex.Message}");
            }
        }

        /// <summary>
        /// PID ファイルを削除
        /// </summary>
        public static void ClearPids(string pidFilePath)
        {
            try
            {
                if (File.Exists(pidFilePath))
                {
                    File.Delete(pidFilePath);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OrphanProcessGuard] Failed to clear PID file: {ex.Message}");
            }
        }

        private static void KillProcess(int pid)
        {
            try
            {
                var process = Process.GetProcessById(pid);
                if (!process.HasExited)
                {
                    Debug.Log($"[OrphanProcessGuard] Killing orphan process: PID={pid} ({process.ProcessName})");
                    process.Kill();
                    process.WaitForExit(5000);
                }
            }
            catch (ArgumentException)
            {
                // プロセスが既に終了済み
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OrphanProcessGuard] Failed to kill PID={pid}: {ex.Message}");
            }
        }
    }
}
#endif
