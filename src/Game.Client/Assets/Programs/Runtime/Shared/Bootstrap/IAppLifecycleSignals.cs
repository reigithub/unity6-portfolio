using R3;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// Unity の Application lifecycle event (focus / pause) を
    /// pure C# service に公開するブリッジ interface。
    /// 実装は <see cref="AppLifecycleBridge"/> (MonoBehaviour)。
    /// </summary>
    public interface IAppLifecycleSignals
    {
        /// <summary>
        /// App が foreground (true) / background (false) になったときに発火。
        /// Mobile 環境では <c>OnApplicationPause(false)</c> も foreground 復帰扱い。
        /// </summary>
        Observable<bool> OnFocusChanged { get; }
    }
}
