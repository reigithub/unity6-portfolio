using Game.Shared.Input;

namespace Game.Shared.Services
{
    /// <summary>
    /// 入力サービスインターフェース
    /// ゲーム全体で共有されるInputSystemのUI入力を提供
    /// </summary>
    public interface IInputService
    {
        /// <summary>
        /// 入力サービスを起動し、InputSystemを有効化する
        /// </summary>
        void Startup();

        /// <summary>
        /// 入力サービスを終了し、InputSystemを無効化・破棄する
        /// </summary>
        void Shutdown();

        /// <summary>
        /// プレイヤー入力アクション（移動、ジャンプ、攻撃等）
        /// </summary>
        ProjectDefaultInputSystem.PlayerActions Player { get; }

        /// <summary>
        /// UI入力アクション（メニュー操作、決定、キャンセル等）
        /// </summary>
        ProjectDefaultInputSystem.UIActions UI { get; }

        /// <summary>
        /// プレイヤー入力を有効化する
        /// </summary>
        void EnablePlayer();

        /// <summary>
        /// プレイヤー入力を無効化する（メニュー表示中等）
        /// </summary>
        void DisablePlayer();

        /// <summary>
        /// UI入力を有効化する
        /// </summary>
        void EnableUI();

        /// <summary>
        /// UI入力を無効化する
        /// </summary>
        void DisableUI();
    }
}