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

        /// <summary>最後に使ったセーブポイントを記録する。未ロード時は LogError の上で何もしない。Id 0・同値の場合も何もしない。</summary>
        void SetLastSavepoint(int interactionId);

        /// <summary>残 HP（0 = 未記録・未ロード。復元側で最大 HP へ正規化する）。</summary>
        int CurrentHealth { get; }

        /// <summary>残 HP を記録する。未ロード時は LogError の上で何もしない。同値の場合は何もしない。</summary>
        void SetCurrentHealth(int health);

        /// <summary>最大 HP（0 = 未設定）。マスタ由来のランタイム値でセーブデータには含まれない。</summary>
        int MaxHealth { get; }

        /// <summary>最大 HP を記録する。プレイヤー初期化時にマスタ適用後の実効値を共有する。</summary>
        void SetMaxHealth(int maxHealth);

        /// <summary>HP が満タンで回復アイテムを使用できないか。MaxHealth 未設定（0 以下）は満タン扱いにしない。</summary>
        bool IsHealthFull { get; }
    }
}
