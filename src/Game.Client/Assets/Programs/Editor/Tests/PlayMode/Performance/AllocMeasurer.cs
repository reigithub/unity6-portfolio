using System;

namespace Game.Tests.PlayMode.Performance
{
    /// <summary>
    /// GC アロケーション計測ヘルパー。
    /// `GC.GetTotalMemory` の前後差分で managed alloc 量を測る。
    /// Layer 1 (EditMode 4 項目) の alloc 検証パターンを PlayMode でも再現する。
    /// </summary>
    public static class AllocMeasurer
    {
        /// <summary>
        /// action 実行前後の alloc 差分（bytes）を返す。事前に GC.Collect で安定化させる。
        /// GC.GetTotalAllocatedBytes は .NET Standard 2.1 には含まれないため、
        /// Unity 互換の GC.GetTotalMemory を使用する（Layer 1 と同一パターン）。
        /// </summary>
        public static long Measure(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            GC.Collect();
            GC.WaitForPendingFinalizers();

            long before = GC.GetTotalMemory(forceFullCollection: true);
            action();
            long after = GC.GetTotalMemory(forceFullCollection: false);
            return after - before;
        }

        public static bool IsZeroAlloc(long bytes) => bytes <= 0;
    }
}
