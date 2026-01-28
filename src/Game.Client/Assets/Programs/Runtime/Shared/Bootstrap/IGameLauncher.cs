using Cysharp.Threading.Tasks;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// ゲームモードランチャーのインターフェース
    /// MVC/MVPで共通
    /// </summary>
    public interface IGameLauncher
    {
        /// <summary>
        /// ゲームモードを起動する
        /// サービスの初期化、マスターデータのロード、初期シーンへの遷移等を行う
        /// </summary>
        /// <returns>起動完了を待機するタスク</returns>
        UniTask StartupAsync();

        /// <summary>
        /// ゲームモードを終了する
        /// サービスのクリーンアップ、リソースの解放等を行う
        /// </summary>
        /// <returns>終了完了を待機するタスク</returns>
        UniTask ShutdownAsync();
    }
}