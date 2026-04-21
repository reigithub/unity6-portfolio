using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;

namespace Game.Shared.Services
{
    /// <summary>
    /// Unity Dedicated Server 接続トークン取得 API サービスのインターフェース。
    /// SP クライアントがゲームサーバーに接続する前にトークンを取得するために使用する。
    /// </summary>
    public interface IUnityServerApiService
    {
        /// <summary>
        /// Game.Server から Unity Dedicated Server 接続用トークンを取得する。
        /// 認証済みユーザーに対して HMAC 署名付きセッショントークンを発行する。
        /// </summary>
        /// <returns>成功時はトークンとセッション名を含むレスポンス、失敗時はエラー情報。</returns>
        UniTask<ApiResponse<UnityServerAuthResponse>> IssueTokenAsync(int stageId = 0, int playerCount = 1);
    }
}
