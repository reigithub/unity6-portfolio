using Cysharp.Threading.Tasks;

namespace Game.ScoreTimeAttack.Base
{
    /// <summary>
    /// Presenterのライフサイクルインターフェース
    /// </summary>
    public interface IPresenterLifecycle
    {
        UniTask InitializeAsync();
        UniTask StartAsync();
        UniTask DisposeAsync();
    }
}