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
        private readonly List<HorrorInventoryEntry> _entries = new();

        /// <summary>現在の所持アイテム一覧（追加順）。</summary>
        public IReadOnlyList<HorrorInventoryEntry> Entries => _entries;

        /// <summary>
        /// アイテムをインベントリに追加する。
        /// 同一 Id が既に存在する場合はスタック加算し MaxQuantity で頭打ちする。
        /// </summary>
        /// <param name="master">追加するアイテムのマスターデータ。</param>
        /// <param name="count">追加数量。</param>
        /// <returns>実際に加算できた数。0 の場合は上限到達または無効引数。</returns>
        public int Add(HorrorItemMaster master, int count)
        {
            if (master == null || count <= 0)
                return 0;

            var entry = _entries.Find(e => e.Master.Id == master.Id);
            if (entry != null)
            {
                var newCount = Mathf.Min(entry.Count + count, master.MaxQuantity);
                var added = newCount - entry.Count;
                entry.Count = newCount;
                return added;
            }
            else
            {
                var addCount = Mathf.Min(count, master.MaxQuantity);
                _entries.Add(new HorrorInventoryEntry(master, addCount));
                return addCount;
            }
        }

        /// <summary>所持アイテムを全件削除する。</summary>
        public void Clear() => _entries.Clear();

        /// <summary>モード離脱時に呼ばれる。メモリリーク防止のため所持データを破棄する。</summary>
        public void Shutdown() => _entries.Clear();
    }

    /// <summary>
    /// インベントリ内の1エントリ（アイテム種別と所持数）。
    /// Master の参照を直接保持することで MaxQuantity クランプに DB を再引きしない設計。
    /// </summary>
    public class HorrorInventoryEntry
    {
        /// <summary>このエントリのアイテムマスターデータ（読み取り専用）。</summary>
        public HorrorItemMaster Master { get; }

        /// <summary>現在の所持数。</summary>
        public int Count { get; internal set; }

        /// <param name="master">アイテムのマスターデータ。</param>
        /// <param name="count">初期所持数。</param>
        public HorrorInventoryEntry(HorrorItemMaster master, int count)
        {
            Master = master;
            Count = count;
        }
    }
}
