using Game.Horror.Services.Interfaces;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror のインタラクション履歴（訪問済み判定）を扱うドメインサービス。
    /// </summary>
    public class HorrorInteractionService : IHorrorInteractionService
    {
        private readonly HorrorSaveRepository _repository;

        public HorrorInteractionService(HorrorSaveRepository repository)
        {
            _repository = repository;
        }

        public void Add(HorrorInteractionMaster master)
        {
            var data = _repository.Data?.Interaction;
            if (data == null || master == null) return;

            if (!Contains(master.Id))
            {
                data.InteractionIds.Add(master.Id);
                _repository.MarkDirty();
            }
        }

        public bool Contains(int id)
        {
            var data = _repository.Data?.Interaction;
            return data != null && data.InteractionIds.Contains(id);
        }
    }
}
