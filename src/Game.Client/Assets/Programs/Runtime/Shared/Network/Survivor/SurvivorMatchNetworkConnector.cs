using Game.Library.Shared.Realtime.Hubs;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// MagicOnion MatchResult -> NGO 接続パラメータの変換と保持。
    /// SP 時は localhost:7777、MP 時は MatchResult から取得。
    /// </summary>
    public static class SurvivorMatchNetworkConnector
    {
        private static MatchResult _lastMatchResult;

        public static void StoreMatchResult(MatchResult result) => _lastMatchResult = result;
        public static void Clear() => _lastMatchResult = null;

        public static bool HasMatchResult => _lastMatchResult != null;

        public static string ServerAddress =>
            _lastMatchResult?.ServerAddress ?? "127.0.0.1";

        public static ushort ServerPort =>
            _lastMatchResult != null ? (ushort)_lastMatchResult.ServerPort : (ushort)7777;

        public static string MatchId => _lastMatchResult?.MatchId ?? "sp-local";
    }
}
