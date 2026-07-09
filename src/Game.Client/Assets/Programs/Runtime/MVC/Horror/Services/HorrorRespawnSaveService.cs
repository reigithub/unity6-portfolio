using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Services;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// 復帰地点（最後に使ったセーブポイント）のセーブサービス。
    /// 座標ではなく InteractionId のみを永続化し、位置解決はシーン側（リスポーン Transform）に委ねる。
    /// </summary>
    public class HorrorRespawnSaveService : SaveServiceBase<HorrorRespawnSaveData>, IHorrorRespawnSaveService, IGameService
    {
        protected override string SaveKey => "horror_respawn";
        protected override int CurrentVersion => 1;

        private readonly IScriptableDatabaseService _databaseService;

        public HorrorRespawnSaveService(ISaveDataStorage storage, IScriptableDatabaseService databaseService) : base(storage)
        {
            _databaseService = databaseService;
        }

        /// <summary>最後に使ったセーブポイントの InteractionId（0 = 未記録・未ロード）。</summary>
        public int LastSavepointId => Data?.LastSavepointId ?? 0;

        /// <summary>
        /// 最後に使ったセーブポイントを記録する。未ロード・Id 0・同値の場合は何もしない（同値で Dirty にしない）。
        /// </summary>
        public void SetLastSavepoint(int interactionId)
        {
            if (Data == null || interactionId == 0 || Data.LastSavepointId == interactionId)
                return;

            Data.LastSavepointId = interactionId;
            MarkDirty();
        }

        protected override void OnDataLoaded(HorrorRespawnSaveData data)
        {
            // マスター不在 Id は未記録(0)へ戻す。シーン内に該当セーブポイントが無いケースは復元側のフォールバックが担う
            if (data.LastSavepointId != 0
                && !_databaseService.Database.HorrorInteractionMasterTable.TryFindById(data.LastSavepointId, out _))
            {
                data.LastSavepointId = 0;
            }
        }

        protected override int GetDataVersion(HorrorRespawnSaveData data) => data.Version;

        protected override void MigrateData(HorrorRespawnSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorRespawnSaveService] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
