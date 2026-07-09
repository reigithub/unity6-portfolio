using Game.Core.Services;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror プレイヤー状態（復帰地点等）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorPlayerService : IGameService
    {
        /// <summary>最後に使ったセーブポイントの InteractionId（0 = 未記録・未ロード）。</summary>
        int LastSavepointId { get; }

        /// <summary>最後に使ったセーブポイントを記録する。未ロード・Id 0・同値の場合は何もしない。</summary>
        void SetLastSavepoint(int interactionId);
    }
}
