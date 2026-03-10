using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Game.Shared.Network
{
    /// <summary>
    /// ネットワークモード判定ヘルパー。
    /// SP（NetworkManager なし or リスニングなし）→ 従来通り動作。
    /// Client-only → ゲームロジック除外、ビジュアル実行。
    /// Mirror API をラップし、ゲームロジック層から Mirror 直接参照を排除する。
    /// </summary>
    public static class NetworkModeHelper
    {
        /// <summary>
        /// ネットワークサーバーとして動作中か（Host / DedicatedServer）
        /// </summary>
        public static bool IsNetworkServer => NetworkServer.active;

        /// <summary>ネットワーククライアントとして動作中か（Host含まず）</summary>
        public static bool IsNetworkClient =>
            NetworkClient.active && !NetworkServer.active;

        /// <summary>描画不要なサーバーか（DedicatedServer / MPPM Server）</summary>
        public static bool IsHeadlessServer =>
            NetworkServer.active && !NetworkClient.active;

        /// <summary>Host モードか（Server + Client 両方アクティブ）</summary>
        public static bool IsNetworkHost =>
            NetworkServer.active && NetworkClient.active;

        /// <summary>クライアントがサーバーに接続済みか</summary>
        public static bool IsNetworkClientConnected => NetworkClient.isConnected;

        /// <summary>ローカルプレイヤーのコンポーネントを取得</summary>
        public static bool TryGetLocalPlayerComponent<T>(out T component)
        {
            component = default;

            if (NetworkClient.localPlayer == null)
                return false;

            return NetworkClient.localPlayer.TryGetComponent(out component);
        }

        /// <summary>サーバー接続中の全プレイヤーからコンポーネントを取得</summary>
        public static IEnumerable<T> GetNetworkPlayerComponents<T>()
        {
            foreach (var conn in NetworkServer.connections.Values)
            {
                if (conn?.identity != null)
                {
                    if (conn.identity.TryGetComponent(out T component))
                        yield return component;
                }
            }
        }

        /// <summary>クライアント切断イベント（Mirror.NetworkClient.OnDisconnectedEvent のパススルー）</summary>
        public static event Action OnClientDisconnected
        {
            add => NetworkClient.OnDisconnectedEvent += value;
            remove => NetworkClient.OnDisconnectedEvent -= value;
        }

        /// <summary>デバッグ用ネットワーク状態文字列</summary>
        public static string GetDebugStatus()
        {
            return $"isServer={IsNetworkServer}, NetworkServer.active={NetworkServer.active}, NetworkClient.isConnected={NetworkClient.isConnected}";
        }
    }
}
