using System.Collections.Generic;
using Game.Core.Services;
using Game.Shared.Scriptable.Database.Tables;
using UnityEngine;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// プレイヤーが所持するアイテム一覧を管理するサービス。
    /// GameServiceManager 経由で取得する。同一 Id のアイテムはスタック管理し MaxQuantity で頭打ちする。
    /// </summary>
    public class HorrorInventoryService : IGameService
    {
        /// <summary>現在の所持アイテム一覧（追加順）。</summary>
        private readonly List<HorrorInventoryItem> _items = new();
        public IReadOnlyList<HorrorInventoryItem> Items => _items;

        /// <summary>
        /// アイテムをインベントリに追加する。
        /// 同一 Id が既に存在する場合はスタック加算し MaxQuantity で頭打ちする。
        /// </summary>
        /// <param name="master">追加するアイテムのマスターデータ。</param>
        /// <param name="addCount">追加数量。</param>
        public void Add(HorrorItemMaster master, int addCount)
        {
            if (master == null || addCount <= 0)
                return;

            var item = _items.Find(x => x.ItemId == master.Id);
            if (item != null)
                item.Count = Mathf.Min(item.Count + addCount, master.MaxQuantity);
            else
                _items.Add(new HorrorInventoryItem(master.Id, addCount));
        }

        /// <summary>所持アイテムを全件削除する。</summary>
        public void Clear() => _items.Clear();

        /// <summary>モード離脱時に呼ばれる。メモリリーク防止のため所持データを破棄する。</summary>
        public void Shutdown() => _items.Clear();
    }

    public class HorrorInventoryItem
    {
        public int ItemId { get; }

        public int Count { get; internal set; }

        public HorrorInventoryItem(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }
    }
}
