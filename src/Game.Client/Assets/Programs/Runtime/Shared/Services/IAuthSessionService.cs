using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;

namespace Game.Shared.Services
{
    /// <summary>
    /// セッション管理サービスインターフェース
    /// トークンの保存/復元/クリアを担当
    /// </summary>
    public interface IAuthSessionService
    {
        bool IsAuthenticated { get; }
        string AuthToken { get; }
        string RefreshToken { get; }
        string UserId { get; }
        string UserName { get; }
        string AuthType { get; }
        string SigningKey { get; }

        /// <summary>
        /// 最後に session が refresh された UTC 時刻。
        /// null は未 refresh (初期 state or ClearSessionAsync 後)。
        /// </summary>
        DateTime? LastRefreshedAt { get; }

        /// <summary>
        /// 最後の refresh から default threshold (<see cref="AuthSessionService._defaultFreshnessThreshold"/>)
        /// 以内であれば true を返す。
        /// TitleScene / AccountLinkDialog 等で冗長な refresh 呼び出しを skip する判定に使用する。
        /// </summary>
        /// <returns>LastRefreshedAt が null、threshold 超過、または時計巻き戻しがあった場合は false。</returns>
        bool IsRecentlyRefreshed();

        /// <summary>
        /// 最後の refresh から指定時間内であれば true を返す。
        /// カスタム threshold を指定したい特殊ケース (動的 threshold) 用の overload。
        /// </summary>
        /// <param name="threshold">skip 判定の閾値 (例: <c>TimeSpan.FromSeconds(30)</c>)。</param>
        /// <returns>LastRefreshedAt が null、threshold 超過、または時計巻き戻しがあった場合は false。</returns>
        bool IsRecentlyRefreshed(TimeSpan threshold);

        /// <summary>
        /// <see cref="AuthApiService"/> が refresh/login 成功時に呼ぶ内部 API。
        /// <see cref="LastRefreshedAt"/> を <see cref="DateTime.UtcNow"/> で更新する。
        /// <para>
        /// <b>UI / Scene 層からは呼ばない</b>。成功時の自動更新のみで使用する。
        /// misuse すると <see cref="IsRecentlyRefreshed"/> の判定を欺けるため、レビューで gate する。
        /// </para>
        /// </summary>
        void MarkRefreshed();

        UniTask SaveSessionAsync(LoginResponse response, string authType = "guest");
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
