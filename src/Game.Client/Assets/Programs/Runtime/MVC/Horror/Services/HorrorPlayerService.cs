using Game.Horror.Services.Interfaces;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror プレイヤー状態（復帰地点等）を扱うドメインサービス。
    /// 座標ではなく InteractionId のみを永続化し、位置解決はシーン側（リスポーン Transform）に委ねる。
    /// </summary>
    public class HorrorPlayerService : IHorrorPlayerService
    {
        private readonly IHorrorSaveRepository _repository;

        public HorrorPlayerService(IHorrorSaveRepository repository)
        {
            _repository = repository;
        }

        /// <summary>最後に使ったセーブポイントの InteractionId（0 = 未記録・未ロード）。</summary>
        public int LastSavepointId => _repository.Data?.Player?.LastSavepointId ?? 0;

        /// <summary>
        /// 最後に使ったセーブポイントを記録する。未ロード・Id 0・同値の場合は何もしない（同値で Dirty にしない）。
        /// </summary>
        public void SetLastSavepoint(int interactionId)
        {
            var data = _repository.Data?.Player;
            if (data == null || interactionId == 0 || data.LastSavepointId == interactionId)
                return;

            data.LastSavepointId = interactionId;
            _repository.MarkDirty();
        }

        /// <summary>残 HP（0 = 未記録・未ロード。復元側で最大 HP へ正規化する）。</summary>
        public int CurrentHealth => _repository.Data?.Player?.CurrentHealth ?? 0;

        /// <summary>
        /// 残 HP を記録する。未ロード・同値の場合は何もしない（同値で Dirty にしない）。
        /// 0 も有効値として記録する（死亡時。ゲームオーバー後は Continue/Load でデータごと置き換わる）。
        /// </summary>
        public void SetCurrentHealth(int health)
        {
            var data = _repository.Data?.Player;
            if (data == null || data.CurrentHealth == health)
                return;

            data.CurrentHealth = health;
            _repository.MarkDirty();
        }

        /// <summary>
        /// 最大 HP（0 = 未設定）。他メンバーと異なりマスタ由来のランタイム値であり、
        /// セーブリポジトリを経由しない（Dirty 化もしない）。
        /// </summary>
        public int MaxHealth { get; private set; }

        /// <summary>最大 HP を記録する。プレイヤー初期化時にマスタ適用後の実効値を共有する。</summary>
        public void SetMaxHealth(int maxHealth)
        {
            MaxHealth = maxHealth;
        }

        /// <summary>HP が満タンで回復アイテムを使用できないか。MaxHealth 未設定（0 以下）は満タン扱いにしない。</summary>
        public bool IsHealthFull => MaxHealth > 0 && CurrentHealth >= MaxHealth;
    }
}
