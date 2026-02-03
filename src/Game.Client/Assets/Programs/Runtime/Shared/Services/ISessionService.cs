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
        string DisplayName { get; }
        string AuthType { get; }

        void SaveSession(Dto.Auth.LoginResponse response, string authType = "guest");
        bool TryRestoreSession();
        void ClearSession();
        string GetOrCreateDeviceFingerprint();
    }
}
