using Game.Shared.Enums;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// ゲームモード別のランチャーインターフェース
    /// </summary>
    public interface IGameModeLauncher : IGameLauncher
    {
        GameMode Mode { get; }
    }
}
