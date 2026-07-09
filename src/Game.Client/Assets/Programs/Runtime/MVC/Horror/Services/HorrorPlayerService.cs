using Game.Core.Services;
using Game.Horror.Services.Interfaces;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror プレイヤー状態（復帰地点等）を扱うドメインサービス。
    /// 座標ではなく InteractionId のみを永続化し、位置解決はシーン側（リスポーン Transform）に委ねる。
    /// </summary>
    public class HorrorPlayerService : IHorrorPlayerService
    {
        private readonly HorrorSaveRepository _repository;

        public HorrorPlayerService(HorrorSaveRepository repository)
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
    }
}
