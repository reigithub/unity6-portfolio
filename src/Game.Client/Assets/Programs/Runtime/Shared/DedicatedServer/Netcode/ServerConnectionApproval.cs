#if UNITY_SERVER
using System.Text;
using Unity.Netcode;
using UnityEngine;

namespace Game.Shared.DedicatedServer.Netcode
{
    /// <summary>
    /// NGO Connection Approval コールバック。
    /// クライアント接続時にセッショントークンを検証する。
    /// Phase 2: 基本構造のみ（常に承認）。Phase 3 でトークン検証を実装。
    /// </summary>
    public static class ServerConnectionApproval
    {
        /// <summary>
        /// NetworkManager.ConnectionApprovalCallback に登録するコールバック。
        /// </summary>
        public static void ApproveConnection(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response)
        {
            // Connection Payload からトークンを取得
            string token = string.Empty;
            if (request.Payload != null && request.Payload.Length > 0)
            {
                token = Encoding.UTF8.GetString(request.Payload);
            }

            Debug.Log($"[ConnectionApproval] Client {request.ClientNetworkId} " +
                      $"requesting approval (payload={request.Payload?.Length ?? 0} bytes)");

            // --- Phase 2: 常に承認 ---
            // Phase 3 でセッショントークン検証を追加予定:
            //   - MagicOnion Realtime サーバーの MatchSessionTokenService で発行されたトークン
            //   - HTTP API or 共有 Redis でトークンを検証
            //   - 検証失敗時は response.Approved = false + response.Reason で切断
            response.Approved = true;
            response.CreatePlayerObject = false;  // Phase 3+ で NetworkObject 生成を制御
            response.Pending = false;

            Debug.Log($"[ConnectionApproval] Client {request.ClientNetworkId} approved");
        }
    }
}
#endif
