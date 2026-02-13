using Game.Shared.Services.Network.Models;
using Game.Shared.Services;

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
                NetworkErrorType.CircuitBreakerOpen => error.Message ?? "サーバーが一時的に利用できません。しばらく待ってから再試行してください。",
                NetworkErrorType.ValidationError => "入力内容に誤りがあります。",
                _ => error.Message ?? "不明なエラーが発生しました"
            };
        }

        /// <summary>
        /// ApiErrorResponseをローカライズされたメッセージに変換
        /// </summary>
        /// <param name="error">APIエラーレスポンス</param>
        /// <returns>日本語のエラーメッセージ</returns>
        public static string GetLocalizedMessage(ApiErrorResponse error)
        {
            if (error == null)
            {
                return "不明なエラーが発生しました";
            }

            // エラーコードに基づくメッセージを取得
            if (!string.IsNullOrEmpty(error.error))
            {
                var errorCodeMessage = GetErrorCodeMessage(error.error);
                if (errorCodeMessage != null)
                {
                    return errorCodeMessage;
                }
            }

            // メッセージがあればそれを返す
            if (!string.IsNullOrEmpty(error.message))
            {
                return error.message;
            }

            return "不明なエラーが発生しました";
        }

        /// <summary>
        /// クライアントエラーの詳細メッセージを取得
        /// </summary>
        private static string GetClientErrorMessage(NetworkError error)
        {
            // まずErrorCodeをチェック
            if (!string.IsNullOrEmpty(error.ErrorCode))
            {
                var errorCodeMessage = GetErrorCodeMessage(error.ErrorCode);
                if (errorCodeMessage != null)
                {
                    return errorCodeMessage;
                }
            }

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
        /// サーバーからのErrorCodeに基づくメッセージを取得
        /// </summary>
        /// <param name="errorCode">サーバーからのエラーコード</param>
        /// <returns>ローカライズされたメッセージ（未定義の場合はnull）</returns>
        private static string GetErrorCodeMessage(string errorCode)
        {
            return errorCode switch
            {
                // 認証関連
                "INVALID_CREDENTIALS" => "メールアドレスまたはパスワードが正しくありません。",
                "TOKEN_EXPIRED" => "セッションの有効期限が切れました。再度ログインしてください。",
                "TOKEN_INVALID" => "認証に失敗しました。再度ログインしてください。",
                "ACCOUNT_LOCKED" => "アカウントがロックされています。しばらく待ってから再試行してください。",
                "ACCOUNT_NOT_FOUND" => "アカウントが見つかりません。",
                "ACCOUNT_ALREADY_EXISTS" => "このアカウントは既に登録されています。",
                "EMAIL_ALREADY_EXISTS" => "このメールアドレスは既に使用されています。",

                // ゲーム関連
                "SCORE_ALREADY_SUBMITTED" => "このスコアは既に送信されています。",
                "STAGE_NOT_UNLOCKED" => "このステージはまだ解放されていません。",
                "INVALID_STAGE_ID" => "無効なステージです。",
                "PLAYER_NOT_FOUND" => "プレイヤーデータが見つかりません。",
                "SAVE_DATA_CORRUPTED" => "セーブデータが破損しています。",

                // 入力バリデーション
                "VALIDATION_ERROR" => "入力内容に誤りがあります。",
                "INVALID_FORMAT" => "入力形式が正しくありません。",
                "REQUIRED_FIELD_MISSING" => "必須項目が入力されていません。",
                "VALUE_OUT_OF_RANGE" => "入力値が範囲外です。",

                // サーバー関連
                "MAINTENANCE" => "現在メンテナンス中です。しばらくお待ちください。",
                "SERVICE_UNAVAILABLE" => "サービスが一時的に利用できません。",

                // 未定義のエラーコード
                _ => null
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
