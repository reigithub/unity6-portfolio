using Game.Shared.Enums;

namespace Game.Shared.Bootstrap
{
    /// <summary>
    /// ゲームモード別のランチャーインターフェース
    /// </summary>
    public interface IGameModeLauncher : IGameLauncher
    {
        /// <summary>
        /// このランチャーが担当するゲームモード（MVC/MVP）
        /// </summary>
        GameMode Mode { get; }
    }
}
