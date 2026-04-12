using Game.Library.Shared.Dto;
using Game.Library.Shared.RequestSigning;

namespace Game.Server.Services.Interfaces;

/// <summary>
/// Unity Dedicated Server 接続用セッション認証トークン発行・検証サービスのインターフェース。
/// SP/MP 両モードのクライアントに対して統一的にトークンを発行する。
/// </summary>
public interface IUnityServerAuthService
{
    /// <summary>
    /// 指定ユーザーに対してセッショントークンを発行する。
    /// トークンは HMAC 署名付きで Valkey に保存される（TTL 5分）。
    /// stageId が 0 より大きい場合は DS へのセッション割り当ても実行する。
    /// </summary>
    /// <param name="userId">トークン発行対象のユーザーID。</param>
    /// <param name="matchId">マッチID。null の場合は自動生成（SP 用）。</param>
    /// <param name="stageId">ステージID。0 の場合は DS 割り当てをスキップ。</param>
    /// <param name="expectedPlayers">期待プレイヤー数。DS 割り当て時に渡す。</param>
    /// <returns>発行されたトークンとセッション名を含むレスポンス。</returns>
    Task<UnityServerAuthResponse> IssueTokenAsync(
        string userId, string matchId = null, int stageId = 0, int expectedPlayers = 1);

    /// <summary>
    /// セッショントークンを検証し、ペイロードを返す。
    /// HMAC 署名検証 + Valkey 失効チェックを行う。
    /// </summary>
    /// <param name="token">検証するトークン文字列。</param>
    /// <returns>検証成功時はパース結果、失敗または失効済みの場合は null。</returns>
    Task<SessionTokenParseResult?> ValidateTokenAsync(string token);
}
