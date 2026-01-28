using System;

namespace Game.Shared.Exceptions
{
    /// <summary>
    /// ゲーム固有の例外基底クラス
    /// エラーコードとエラーレベルを持ち、ログ出力やエラーハンドリングで利用
    /// </summary>
    public abstract class GameException : Exception
    {
        /// <summary>エラーコード（ログ検索用）</summary>
        public string ErrorCode { get; protected set; }

        /// <summary>エラーレベル（0=Info, 1=Warning, 2=Error, 3=Critical）</summary>
        public int ErrorLevel { get; protected set; }

        protected GameException(string message, string errorCode = "UNKNOWN", int errorLevel = 2)
            : base(message)
        {
            ErrorCode = errorCode;
            ErrorLevel = errorLevel;
        }

        protected GameException(string message, Exception innerException, string errorCode = "UNKNOWN", int errorLevel = 2)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            ErrorLevel = errorLevel;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] {Message}";
        }
    }

    /// <summary>
    /// アセット読み込み失敗時の例外
    /// Addressablesでのアセット読み込みエラーを表現
    /// </summary>
    public class GameAssetLoadException : GameException
    {
        /// <summary>読み込みを試みたアセットアドレス</summary>
        public string AssetAddress { get; }

        /// <summary>要求された型</summary>
        public Type RequestedType { get; }

        /// <summary>リトライ回数</summary>
        public int RetryCount { get; }

        public GameAssetLoadException(
            string address,
            Type requestedType,
            string message,
            int retryCount = 0)
            : base(message, "ASSET_LOAD_FAILED", 2)
        {
            AssetAddress = address;
            RequestedType = requestedType;
            RetryCount = retryCount;
        }

        public GameAssetLoadException(
            string address,
            Type requestedType,
            string message,
            Exception innerException,
            int retryCount = 0)
            : base(message, innerException, "ASSET_LOAD_FAILED", 2)
        {
            AssetAddress = address;
            RequestedType = requestedType;
            RetryCount = retryCount;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] Asset: {AssetAddress}, Type: {RequestedType?.Name ?? "Unknown"}, Retries: {RetryCount} - {Message}";
        }
    }

    /// <summary>
    /// シーン遷移失敗時の例外
    /// GameSceneServiceでのシーン遷移エラーを表現
    /// </summary>
    public class GameSceneTransitionException : GameException
    {
        /// <summary>遷移元シーン名</summary>
        public string FromScene { get; }

        /// <summary>遷移先シーン名</summary>
        public string ToScene { get; }

        public GameSceneTransitionException(
            string fromScene,
            string toScene,
            string message)
            : base(message, "SCENE_TRANSITION_FAILED", 2)
        {
            FromScene = fromScene;
            ToScene = toScene;
        }

        public GameSceneTransitionException(
            string fromScene,
            string toScene,
            string message,
            Exception innerException)
            : base(message, innerException, "SCENE_TRANSITION_FAILED", 2)
        {
            FromScene = fromScene;
            ToScene = toScene;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] From: {FromScene ?? "None"} -> To: {ToScene ?? "Unknown"} - {Message}";
        }
    }

    /// <summary>
    /// マスターデータ読み込み失敗時の例外
    /// バージョン不整合やデータ破損を表現
    /// </summary>
    public class MasterDataLoadException : GameException
    {
        /// <summary>データキー</summary>
        public string DataKey { get; }

        /// <summary>現在のバージョン</summary>
        public int CurrentVersion { get; }

        /// <summary>必要なバージョン</summary>
        public int RequiredVersion { get; }

        public MasterDataLoadException(
            string dataKey,
            string message,
            int currentVersion = 0,
            int requiredVersion = 0)
            : base(message, "MASTER_DATA_LOAD_FAILED", 3)
        {
            DataKey = dataKey;
            CurrentVersion = currentVersion;
            RequiredVersion = requiredVersion;
        }

        public MasterDataLoadException(
            string dataKey,
            string message,
            Exception innerException,
            int currentVersion = 0,
            int requiredVersion = 0)
            : base(message, innerException, "MASTER_DATA_LOAD_FAILED", 3)
        {
            DataKey = dataKey;
            CurrentVersion = currentVersion;
            RequiredVersion = requiredVersion;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] Key: {DataKey}, Version: {CurrentVersion}/{RequiredVersion} - {Message}";
        }
    }

    /// <summary>
    /// セーブデータ破損時の例外
    /// データ修復やリセットの判定に使用
    /// </summary>
    public class SaveDataCorruptedException : GameException
    {
        /// <summary>セーブデータキー</summary>
        public string SaveKey { get; }

        /// <summary>復旧可能かどうか</summary>
        public bool IsRecoverable { get; }

        public SaveDataCorruptedException(
            string saveKey,
            string message,
            bool isRecoverable = false)
            : base(message, "SAVE_DATA_CORRUPTED", 3)
        {
            SaveKey = saveKey;
            IsRecoverable = isRecoverable;
        }

        public SaveDataCorruptedException(
            string saveKey,
            string message,
            Exception innerException,
            bool isRecoverable = false)
            : base(message, innerException, "SAVE_DATA_CORRUPTED", 3)
        {
            SaveKey = saveKey;
            IsRecoverable = isRecoverable;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] Key: {SaveKey}, Recoverable: {IsRecoverable} - {Message}";
        }
    }

    /// <summary>
    /// セッションエラータイプ
    /// </summary>
    public enum SessionErrorType
    {
        /// <summary>セッションが見つからない</summary>
        NotFound,
        /// <summary>セッション期限切れ</summary>
        Expired,
        /// <summary>セッションデータ破損</summary>
        Corrupted,
        /// <summary>競合するセッションが存在</summary>
        ConflictingSession,
        /// <summary>無効な状態</summary>
        InvalidState
    }

    /// <summary>
    /// ゲームセッション管理エラー時の例外
    /// セッション喪失時の処理分岐に使用
    /// </summary>
    public class GameSessionException : GameException
    {
        /// <summary>セッションID</summary>
        public string SessionId { get; }

        /// <summary>エラータイプ</summary>
        public SessionErrorType ErrorType { get; }

        public GameSessionException(
            string sessionId,
            SessionErrorType errorType,
            string message)
            : base(message, "SESSION_ERROR", 2)
        {
            SessionId = sessionId;
            ErrorType = errorType;
        }

        public GameSessionException(
            string sessionId,
            SessionErrorType errorType,
            string message,
            Exception innerException)
            : base(message, innerException, "SESSION_ERROR", 2)
        {
            SessionId = sessionId;
            ErrorType = errorType;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] Session: {SessionId ?? "Unknown"}, Type: {ErrorType} - {Message}";
        }
    }

    /// <summary>
    /// DIエラータイプ
    /// </summary>
    public enum DIErrorType
    {
        /// <summary>サービス未登録</summary>
        ServiceNotRegistered,
        /// <summary>循環依存</summary>
        CircularDependency,
        /// <summary>型の不一致</summary>
        TypeMismatch,
        /// <summary>インスタンス生成失敗</summary>
        InstantiationFailed,
        /// <summary>注入失敗</summary>
        InjectionFailed
    }

    /// <summary>
    /// 依存性注入エラー時の例外
    /// DIコンテナでの注入失敗を表現
    /// </summary>
    public class DependencyInjectionException : GameException
    {
        /// <summary>対象のサービス型</summary>
        public Type ServiceType { get; }

        /// <summary>エラータイプ</summary>
        public DIErrorType ErrorType { get; }

        public DependencyInjectionException(
            Type serviceType,
            DIErrorType errorType,
            string message)
            : base(message, "DI_ERROR", 3)
        {
            ServiceType = serviceType;
            ErrorType = errorType;
        }

        public DependencyInjectionException(
            Type serviceType,
            DIErrorType errorType,
            string message,
            Exception innerException)
            : base(message, innerException, "DI_ERROR", 3)
        {
            ServiceType = serviceType;
            ErrorType = errorType;
        }

        public override string ToString()
        {
            return $"[{ErrorCode}] Type: {ServiceType?.Name ?? "Unknown"}, DIError: {ErrorType} - {Message}";
        }
    }
}
