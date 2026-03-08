using Game.Shared.Input;
using Game.Shared.Services;

namespace Game.MVP.Survivor.Server
{
    /// <summary>
    /// サーバー用入力サービス
    /// 内部でProjectDefaultInputSystemを生成するが、Enableしない。
    /// アクションは存在するがイベントが一切発火しないため、
    /// WasPressedThisFrame()は常にfalseを返す。
    /// </summary>
    public class NullInputService : IInputService
    {
        private ProjectDefaultInputSystem _inputSystem = new();

        public ProjectDefaultInputSystem.PlayerActions Player => _inputSystem.Player;
        public ProjectDefaultInputSystem.UIActions UI => _inputSystem.UI;

        public void Startup()
        {
            // コンストラクタで生成済み。Enable しない — アクションは存在するが発火しない
        }

        public void Shutdown()
        {
            _inputSystem?.Dispose();
            _inputSystem = null;
        }

        public void EnablePlayer() { }
        public void DisablePlayer() { }
        public void EnableUI() { }
        public void DisableUI() { }
    }
}
