using System.Threading;
using System.Threading.Tasks;

namespace Game.Shared.Unity.Server
{
    /// <summary>
    /// Game.Server の Unity Dedicated Server レジストリ API
    /// (<c>/api/unity-server/*</c>) を叩くクライアントインターフェース。
    /// Game.Server 側の <c>IUnityServerRegistryService</c> と鏡面対応する。
    /// GameServerUrl 未設定時は no-op 成功扱いで返す。
    /// </summary>
    public interface IUnityServerRegistryApiClient
    {
        /// <summary>
        /// DS を Game.Server に自己登録する。
        /// </summary>
        /// <param name="dsAddress">DS の公開アドレス（IP 文字列）。</param>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>成功した場合は true。</returns>
        Task<bool> RegisterAsync(string dsAddress, CancellationToken ct);

        /// <summary>
        /// ハートビートを Game.Server に送信する。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>成功した場合は true。</returns>
        Task<bool> HeartbeatAsync(CancellationToken ct);

        /// <summary>
        /// DS の登録解除を Game.Server に通知する。
        /// </summary>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>成功した場合は true。</returns>
        Task<bool> DeregisterAsync(CancellationToken ct);

        /// <summary>
        /// セッション終了を Game.Server に通知する。
        /// </summary>
        /// <param name="matchId">終了したセッションのマッチID。</param>
        /// <param name="ct">キャンセルトークン。</param>
        /// <returns>成功した場合は true。</returns>
        Task<bool> NotifySessionEndedAsync(string matchId, CancellationToken ct);
    }
}
