using Cysharp.Threading.Tasks;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// ゲームモードランチャーのインターフェース
    /// MVC/MVPで共通
    /// </summary>
    public interface IGameLauncher
    {
        UniTask StartupAsync();
        UniTask ShutdownAsync();
    }
}