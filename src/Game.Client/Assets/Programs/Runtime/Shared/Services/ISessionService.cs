using Cysharp.Threading.Tasks;

namespace Game.Shared.Services
{
    /// <summary>
    /// セッション管理サービスインターフェース
    /// トークンの保存/復元/クリアを担当
    /// </summary>
    public interface ISessionService
    {
        bool IsAuthenticated { get; }
        string AuthToken { get; }
        string UserId { get; }
        string UserName { get; }
        string AuthType { get; }

        UniTask SaveSessionAsync(Dto.Auth.LoginResponse response, string authType = "guest");
        UniTask<bool> RestoreSessionAsync();
        UniTask ClearSessionAsync();
        UniTask<string> GetOrCreateDeviceFingerprintAsync();

        /// <summary>
        /// UserId を "0000 0000 0000" 形式にフォーマットして返す
        /// </summary>
        string FormatUserId();

        /// <summary>
        /// 引き継ぎパスワードをローカルに保存
        /// </summary>
        UniTask SaveTransferPasswordAsync(string password);

        /// <summary>
        /// ローカルに保存された引き継ぎパスワードを取得
        /// </summary>
        string GetTransferPassword();

        /// <summary>
        /// ローカルに保存された引き継ぎパスワードをクリア
        /// </summary>
        UniTask ClearTransferPasswordAsync();
    }
}
