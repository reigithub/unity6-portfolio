using Game.Core.Services;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// Horror のインタラクション履歴（訪問済み判定）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorInteractionService : IGameService
    {
        /// <summary>インタラクション記録を追加する。未記録なら追加して Dirty にする。</summary>
        void Add(HorrorInteractionMaster master);

        /// <summary>指定 Id が記録済みか判定する。</summary>
        bool Contains(int id);
    }
}
