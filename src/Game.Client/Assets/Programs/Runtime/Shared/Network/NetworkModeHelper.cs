using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Game.Shared.Network
{
    /// <summary>
    /// ネットワークモード判定ヘルパー（Fusion 2 専用）。
    /// </summary>
    public static class NetworkModeHelper
    {
        private static NetworkRunner _fusionRunner;
        private static GameMode _fusionGameMode;

        /// <summary>Fusion Runner を登録する。セッション開始時に呼び出す。</summary>
        public static void SetFusionRunner(NetworkRunner runner, GameMode gameMode)
        {
            _fusionRunner = runner;
            _fusionGameMode = gameMode;
        }

        /// <summary>Fusion Runner をクリアする。セッション終了時に呼び出す。</summary>
        public static void ClearFusionRunner()
        {
            _fusionRunner = null;
            _fusionGameMode = default;
        }

        private static bool IsActive => _fusionRunner != null;

        // =====================================================================
        //  モード判定
        // =====================================================================

        /// <summary>ネットワークサーバーとして動作中か（Host / DedicatedServer）</summary>
        public static bool IsNetworkServer => IsActive && _fusionRunner.IsServer;

        /// <summary>ネットワーククライアントとして動作中か（Host含まず）</summary>
        public static bool IsNetworkClient => IsActive && _fusionGameMode == GameMode.Client;

        /// <summary>描画不要なサーバーか（DedicatedServer / MPPM Server）</summary>
        public static bool IsHeadlessServer => IsActive && _fusionGameMode == GameMode.Server;

        /// <summary>Host モードか（Server + Client 両方アクティブ）</summary>
        public static bool IsNetworkHost => IsActive && _fusionGameMode == GameMode.Host;

        /// <summary>クライアントがサーバーに接続済みか</summary>
        public static bool IsNetworkClientConnected => IsActive;

        // =====================================================================
        //  プレイヤーアクセス
        // =====================================================================

        /// <summary>ローカルプレイヤーのコンポーネントを取得</summary>
        public static bool TryGetLocalPlayerComponent<T>(out T component)
        {
            component = default;
            if (!IsActive) return false;

            var playerObject = _fusionRunner.GetPlayerObject(_fusionRunner.LocalPlayer);
            if (playerObject == null) return false;
            return playerObject.TryGetComponent(out component);
        }

        /// <summary>サーバー接続中の全プレイヤーからコンポーネントを取得</summary>
        public static IEnumerable<T> GetNetworkPlayerComponents<T>()
        {
            if (!IsActive) yield break;

            foreach (var player in _fusionRunner.ActivePlayers)
            {
                var playerObject = _fusionRunner.GetPlayerObject(player);
                if (playerObject != null && playerObject.TryGetComponent(out T comp))
                    yield return comp;
            }
        }

        // =====================================================================
        //  切断イベント
        // =====================================================================

        private static event Action _onClientDisconnected;

        /// <summary>クライアント切断イベント</summary>
        public static event Action OnClientDisconnected
        {
            add => _onClientDisconnected += value;
            remove => _onClientDisconnected -= value;
        }

        /// <summary>Fusion 側の切断通知。SurvivorFusionRunner から呼び出す。</summary>
        internal static void RaiseClientDisconnected() => _onClientDisconnected?.Invoke();

        // =====================================================================
        //  デバッグ
        // =====================================================================

        /// <summary>デバッグ用ネットワーク状態文字列</summary>
        public static string GetDebugStatus()
        {
            if (IsActive)
            {
                return $"[Fusion] isServer={_fusionRunner.IsServer}, gameMode={_fusionGameMode}";
            }
            return "[Offline]";
        }
    }
}
