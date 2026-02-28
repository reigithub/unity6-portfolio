using Unity.Netcode;

namespace Game.Shared.Netcode.Server
{
    /// <summary>
    /// ゲームモード固有のサーバーロジック。
    /// ServerNetworkManager が NGO ライフサイクルイベントを委譲する。
    /// ペイロード形式・セッション管理・スポーン対象はゲームモードが決定する。
    /// </summary>
    public interface IServerGameMode
    {
        /// <summary>サーバー起動時の初期化。</summary>
        void Initialize();

        /// <summary>
        /// ConnectionApproval 処理。ペイロードのデコードと承認判定を行う。
        /// response.Approved / response.Pending を設定すること。
        /// </summary>
        void OnConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request,
            NetworkManager.ConnectionApprovalResponse response);

        /// <summary>クライアント接続完了時。</summary>
        void OnClientConnected(ulong clientId);

        /// <summary>クライアント切断時。</summary>
        void OnClientDisconnected(ulong clientId);

        /// <summary>セッション終了・クリーンアップ。</summary>
        void Cleanup();
    }
}