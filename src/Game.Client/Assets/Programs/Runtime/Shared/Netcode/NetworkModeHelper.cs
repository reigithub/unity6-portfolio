using Unity.Netcode;

namespace Game.Shared.Netcode
{
    /// <summary>
    /// ネットワークモード判定ヘルパー。
    /// SP（NetworkManager なし or リスニングなし）→ 従来通り動作。
    /// Client-only → ゲームロジック除外、ビジュアル実行。
    /// </summary>
    public static class NetworkModeHelper
    {
        private static NetworkManager Nm => NetworkManager.Singleton;

        /// <summary>
        /// ネットワークサーバーとして動作中か（Host / DedicatedServer）
        /// #if UNITY_SERVERディレクティブと適切に使い分けること
        /// </summary>
        public static bool IsNetworkServer =>
            Nm != null && Nm.IsServer && Nm.IsListening;

        /// <summary>ネットワーククライアントとして動作中か（Host含まず）</summary>
        public static bool IsNetworkClientOnly =>
            Nm != null && Nm.IsClient && !Nm.IsServer && Nm.IsListening;
    }
}
