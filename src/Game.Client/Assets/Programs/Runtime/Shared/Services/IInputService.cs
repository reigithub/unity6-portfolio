using Game.Shared.Input;

namespace Game.Shared.Services
{
    /// <summary>
    /// 入力サービスインターフェース
    /// ゲーム全体で共有されるInputSystemのUI入力を提供
    /// </summary>
    public interface IInputService
    {
        void Startup();
        void Shutdown();

        ProjectDefaultInputSystem.PlayerActions Player { get; }
        ProjectDefaultInputSystem.UIActions UI { get; }

        void EnablePlayer();
        void DisablePlayer();

        void EnableUI();
        void DisableUI();
    }
}