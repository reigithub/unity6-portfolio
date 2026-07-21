using Game.Shared.Services.Interfaces;

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

        /// <summary>残 HP（0 = 未記録・未ロード。復元側で最大 HP へ正規化する）。</summary>
        int CurrentHealth { get; }

        /// <summary>残 HP を記録する。未ロード・同値の場合は何もしない。</summary>
        void SetCurrentHealth(int health);
    }
}
