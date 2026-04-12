using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// <see cref="IUnityServerRegistryApiClient.HeartbeatAsync"/> を定期的に実行するループ。
    /// <see cref="Task"/> ベース（ThreadPool 駆動）で実装し、Unity メインスレッドに依存しない。
    /// </summary>
    internal static class UnityServerHeartbeatLoop
    {
        /// <summary>
        /// ハートビートループを開始する。
        /// </summary>
        /// <param name="registry">Registry クライアント。</param>
        /// <param name="interval">ハートビート送信間隔。</param>
        /// <param name="ct">停止用キャンセルトークン。</param>
        /// <returns>ループが終了した Task。</returns>
        public static async Task RunAsync(
            IUnityServerRegistryApiClient registry,
            TimeSpan interval,
            CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                try
                {
                    await registry.HeartbeatAsync(ct);
                }
                catch (Exception ex)
                {
                    // ハートビート失敗はログのみ、ループを継続する
                    Debug.LogWarning($"[HeartbeatLoop] ハートビート失敗: {ex.Message}");
                }
            }
        }
    }
}
