using Game.Shared.Services.Network.Models;

namespace Game.Shared.Services.Network
{
    /// <summary>
    /// ネットワークエラーメッセージのローカライズ
    /// APIエラーを日本語に変換
    /// </summary>
    public static class NetworkErrorLocalizer
    {
        /// <summary>
        /// エラーをローカライズされたメッセージに変換
        /// </summary>
        /// <param name="error">ネットワークエラー</param>
        /// <returns>日本語のエラーメッセージ</returns>
        public static string GetLocalizedMessage(NetworkError error)
        {
            if (error == null)
            {
                return "不明なエラーが発生しました";
            }

            return error.Type switch
            {
                NetworkErrorType.ConnectionError => "サーバーに接続できません。ネットワーク接続を確認してください。",
                NetworkErrorType.Timeout => "接続がタイムアウトしました。しばらく待ってから再試行してください。",
                NetworkErrorType.AuthenticationError => "認証に失敗しました。再度ログインしてください。",
                NetworkErrorType.RateLimited => "リクエストが多すぎます。しばらく待ってから再試行してください。",
                NetworkErrorType.ServerError => "サーバーエラーが発生しました。しばらく待ってから再試行してください。",
                NetworkErrorType.ClientError => GetClientErrorMessage(error),
                NetworkErrorType.Cancelled => "操作がキャンセルされました。",
                NetworkErrorType.RetryExhausted => "接続に失敗しました。ネットワーク接続を確認してください。",
                _ => error.Message ?? "不明なエラーが発生しました"
            };
        }

        /// <summary>
        /// クライアントエラーの詳細メッセージを取得
        /// </summary>
        private static string GetClientErrorMessage(NetworkError error)
        {
            // ステータスコードに基づく詳細メッセージ
            return error.StatusCode switch
            {
                400 => "リクエストが不正です。",
                404 => "データが見つかりません。",
                409 => "データの競合が発生しました。",
                422 => "入力データが無効です。",
                _ => error.Message ?? "リクエストエラーが発生しました。"
            };
        }

        /// <summary>
        /// オフライン状態のメッセージを取得
        /// </summary>
        public static string GetOfflineMessage()
        {
            return "オフラインです。ネットワーク接続を確認してください。";
        }

        /// <summary>
        /// キャッシュからのデータ表示通知メッセージを取得
        /// </summary>
        public static string GetCacheNoticeMessage()
        {
            return "キャッシュデータを表示中";
        }

        /// <summary>
        /// スコアキューイング通知メッセージを取得
        /// </summary>
        public static string GetScoreQueuedMessage()
        {
            return "スコアは後で送信されます";
        }

        /// <summary>
        /// スコア送信中のメッセージを取得
        /// </summary>
        public static string GetScoreSubmittingMessage()
        {
            return "スコアを送信中...";
        }
    }
}
