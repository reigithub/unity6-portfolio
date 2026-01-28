namespace Game.Core.Services
{
    /// <summary>
    /// ゲームサービスの基本インターフェース
    /// サービスのライフサイクル管理（起動・終了）を提供
    /// </summary>
    public interface IGameService
    {
        /// <summary>
        /// サービスを起動する
        /// GameServiceManagerから呼び出され、サービスの初期化処理を行う
        /// </summary>
        public void Startup()
        {
        }

        /// <summary>
        /// サービスを終了する
        /// GameServiceManagerから呼び出され、サービスのクリーンアップ処理を行う
        /// </summary>
        public void Shutdown()
        {
        }
    }
}