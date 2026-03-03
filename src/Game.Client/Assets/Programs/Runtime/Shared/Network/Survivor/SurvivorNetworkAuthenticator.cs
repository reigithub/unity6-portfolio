using System;
using Mirror;
using UnityEngine;

namespace Game.Shared.Network.Survivor
{
    public struct AuthRequestMessage : NetworkMessage
    {
        public byte[] Payload;
    }

    public struct AuthResponseMessage : NetworkMessage
    {
        public bool Approved;
    }

    /// <summary>
    /// Mirror 接続認証。クライアントから MemoryPack ペイロードを送信し、
    /// サーバーで検証する。ConnectionApprovalCallback の Mirror 版。
    /// </summary>
    public class SurvivorNetworkAuthenticator : NetworkAuthenticator
    {
        /// <summary>クライアント側: 接続前にペイロードを設定する。</summary>
        public static byte[] PendingPayload { get; set; }

        /// <summary>サーバー側: 認証成功時に発火。SurvivorServerSession が購読する。</summary>
        public static event Action<NetworkConnectionToClient, int, string> OnPlayerAuthenticated;

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<AuthRequestMessage>(OnAuthRequest, false);
        }

        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<AuthRequestMessage>();
        }

        public override void OnStartClient()
        {
            NetworkClient.RegisterHandler<AuthResponseMessage>(OnAuthResponse, false);
        }

        public override void OnStopClient()
        {
            NetworkClient.UnregisterHandler<AuthResponseMessage>();
        }

        public override void OnServerAuthenticate(NetworkConnectionToClient conn)
        {
            // AuthRequestMessage を待機（何もしない）
        }

        public override void OnClientAuthenticate()
        {
            var msg = new AuthRequestMessage
            {
                Payload = PendingPayload ?? Array.Empty<byte>()
            };
            NetworkClient.Send(msg);
        }

        private void OnAuthRequest(NetworkConnectionToClient conn, AuthRequestMessage msg)
        {
            var (stageId, token) = SurvivorNetworkConnectionPayload.Decode(msg.Payload);

            Debug.Log($"[SurvivorAuthenticator] Auth request: conn={conn.connectionId}, " +
                      $"stageId={stageId}, payload={msg.Payload?.Length ?? 0} bytes");

            // Phase 2: 常に承認（Phase 3 で token 検証追加予定）
            conn.Send(new AuthResponseMessage { Approved = true });
            ServerAccept(conn);
            OnPlayerAuthenticated?.Invoke(conn, stageId, token);
        }

        private void OnAuthResponse(AuthResponseMessage msg)
        {
            if (msg.Approved)
            {
                ClientAccept();
            }
            else
            {
                ClientReject();
            }
        }

        private void OnDestroy()
        {
            OnPlayerAuthenticated = null;
        }
    }
}
