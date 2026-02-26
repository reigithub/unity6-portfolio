using Unity.Netcode;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// ネットワークモード判定ヘルパー。
    /// SP（NetworkManager なし or リスニングなし）→ 従来通り動作。
    /// Server → ゲームロジック実行、ビジュアル除外。
    /// Client-only → ゲームロジック除外、ビジュアル実行。
    /// </summary>
    public static class NetworkModeHelper
    {
        private static NetworkManager Nm => NetworkManager.Singleton;

        /// <summary>ネットワークサーバーとして動作中か</summary>
        public static bool IsNetworkServer =>
            Nm != null && Nm.IsServer && Nm.IsListening;

        /// <summary>ネットワーククライアントとして動作中か（Host含まず）</summary>
        public static bool IsNetworkClientOnly =>
            Nm != null && Nm.IsClient && !Nm.IsServer && Nm.IsListening;

        /// <summary>ゲームロジック実行すべきか（SP or Server）</summary>
        public static bool ShouldRunGameLogic =>
            !IsNetworkClientOnly;

        /// <summary>ビジュアル描画すべきか（SP or Client）</summary>
        public static bool ShouldRunVisuals =>
            !IsNetworkServer;
    }
}
