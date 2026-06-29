using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror インベントリのセーブサービス。所持アイテムの状態保持と永続化を担う。
    /// </summary>
    public class HorrorInventorySaveService : SaveServiceBase<HorrorInventorySaveData>, IHorrorInventorySaveService, IGameService
    {
        protected override string SaveKey => "horror_inventory";
        protected override int CurrentVersion => 1;

        private readonly IScriptableDatabaseService _databaseService;

        public HorrorInventorySaveService(ISaveDataStorage storage, IScriptableDatabaseService databaseService) : base(storage)
        {
            _databaseService = databaseService;
        }

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

            var items = Data.Items;
            var item = items.Find(x => x.ItemId == master.Id);
            if (item != null)
                item.Count = Mathf.Min(item.Count + addCount, master.MaxQuantity);
            else
                items.Add(new HorrorInventoryItem { ItemId = master.Id, Count = Mathf.Min(addCount, master.MaxQuantity) });

            MarkDirty();
        }

        public bool HasItem(int itemId)
        {
            foreach (var item in Data.Items)
            {
                if (item.ItemId == itemId)
                    return true;
            }

            return false;
        }

        protected override void OnDataLoaded(HorrorInventorySaveData data)
        {
            var database = _databaseService.Database;
            // 逆順走査
            for (int i = data.Items.Count - 1; i >= 0; i--)
            {
                var id = data.Items[i].ItemId;
                if (!database.HorrorItemMasterTable.TryFindById(id, out var master))
                    data.Items.RemoveAt(i);
                else
                    data.Items[i].Count = Mathf.Min(data.Items[i].Count, master.MaxQuantity);
            }
        }

        protected override int GetDataVersion(HorrorInventorySaveData data) => data.Version;

        protected override void MigrateData(HorrorInventorySaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorInventorySaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
