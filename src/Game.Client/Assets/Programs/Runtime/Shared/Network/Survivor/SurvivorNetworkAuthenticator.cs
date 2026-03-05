using System;
using System.Text;
using Game.Library.Shared.RequestSigning;
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
    /// サーバーで HMAC トークン検証を行う。
    /// SP モード: SharedSecret 未設定 + token 空 → 無条件承認。
    /// MP モード: HMAC 署名検証 → 承認/拒否。
    /// </summary>
    public class SurvivorNetworkAuthenticator : NetworkAuthenticator
    {
        /// <summary>クライアント側: 接続前にペイロードを設定する。</summary>
        public static byte[] PendingPayload { get; set; }

        /// <summary>サーバー側: 認証成功時に発火。SurvivorServerSession が購読する。</summary>
        public static event Action<NetworkConnectionToClient, int, string> OnPlayerAuthenticated;

        /// <summary>Dedicated Server 起動時に Bootstrap が設定する共有シークレット。</summary>
        public static byte[] SharedSecret { get; set; }

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

            // SP モード: token が空 & SharedSecret 未設定 → 無条件承認
            if (string.IsNullOrEmpty(token) && SharedSecret == null)
            {
                Debug.Log($"[SurvivorAuthenticator] SP mode: auto-approve conn={conn.connectionId}");
                conn.Send(new AuthResponseMessage { Approved = true });
                ServerAccept(conn);
                OnPlayerAuthenticated?.Invoke(conn, stageId, token);
                return;
            }

            // MP モード: SharedSecret が未設定だがトークンが送られてきた
            if (SharedSecret == null)
            {
                Debug.LogError($"[SurvivorAuthenticator] SharedSecret not set but token provided. Rejecting conn={conn.connectionId}");
                conn.Send(new AuthResponseMessage { Approved = false });
                ServerReject(conn);
                return;
            }

            // HMAC トークン検証
            var parsed = SessionTokenHelper.ParseAndVerify(token, SharedSecret);
            if (parsed == null)
            {
                Debug.LogWarning($"[SurvivorAuthenticator] Token verification failed for conn={conn.connectionId}");
                conn.Send(new AuthResponseMessage { Approved = false });
                ServerReject(conn);
                return;
            }

            Debug.Log($"[SurvivorAuthenticator] Verified: user={parsed.UserId}, match={parsed.MatchId}");
            conn.Send(new AuthResponseMessage { Approved = true });
            ServerAccept(conn);
            OnPlayerAuthenticated?.Invoke(conn, stageId, parsed.UserId);
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
