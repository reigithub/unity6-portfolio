using System.Collections.Generic;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services.Interfaces;

namespace Game.Horror.Services.Interfaces
{
    /// <summary>
    /// クラフト（素材を消費して成果物を得る合成）を扱うドメインサービスのインターフェース。
    /// </summary>
    public interface IHorrorCraftService : IGameService
    {
        /// <summary>全レシピ（解放条件は持たず、素材不足のレシピも含む）。</summary>
        IReadOnlyList<HorrorCraftMaster> Recipes { get; }

        /// <summary>レシピが要求する素材一覧。未知のレシピは空。</summary>
        IReadOnlyList<HorrorCraftMaterialMaster> GetMaterials(int craftId);

        /// <summary>
        /// 実行可能か（素材が足りていて、消費後の空きに成果物が全量入るか）を判定する。インベントリは変更しない。
        /// </summary>
        bool CanCraft(int craftId);

        /// <summary>
        /// クラフトを実行する。<see cref="CanCraft"/> が false のときは何もせず false（部分消費しない）。
        /// </summary>
        bool TryCraft(int craftId);
    }
}
