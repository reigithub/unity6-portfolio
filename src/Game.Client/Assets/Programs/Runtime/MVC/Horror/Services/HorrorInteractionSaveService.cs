using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    public class HorrorInteractionSaveService : SaveServiceBase<HorrorInteractionSaveData>, IHorrorInteractionSaveService, IGameService
    {
        protected override string SaveKey => "horror_interaction";
        protected override int CurrentVersion => 1;

        private readonly IScriptableDatabaseService _databaseService;

        public HorrorInteractionSaveService(ISaveDataStorage storage, IScriptableDatabaseService databaseService) : base(storage)
        {
            _databaseService = databaseService;
        }

        public void Add(HorrorInteractionMaster master)
        {
            if (master == null) return;

            if (!Contains(master.Id))
            {
                Data.InteractionIds.Add(master.Id);
                MarkDirty();
            }
        }

        public bool Contains(int id) => Data.InteractionIds.Contains(id);

        protected override void OnDataLoaded(HorrorInteractionSaveData data)
        {
            var database = _databaseService.Database;
            // 逆順走査
            for (int i = data.InteractionIds.Count - 1; i >= 0; i--)
            {
                var id = data.InteractionIds[i];
                if (!database.HorrorInteractionMasterTable.TryFindById(id, out _))
                    data.InteractionIds.RemoveAt(i);
            }
        }

        protected override int GetDataVersion(HorrorInteractionSaveData data) => data.Version;

        protected override void MigrateData(HorrorInteractionSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorInteractionSaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
