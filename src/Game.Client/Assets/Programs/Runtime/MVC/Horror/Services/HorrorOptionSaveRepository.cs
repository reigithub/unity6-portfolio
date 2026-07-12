using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using UnityEngine;

namespace Game.Horror.Services
{
    /// <summary>
    /// Horror オプション設定の永続化リポジトリ。<see cref="SaveRepositoryBase{TData}"/> を継承し、
    /// 読み込み・保存・マイグレーションを担う。設定値の変更（クランプ・正規化・Dirty 化）は
    /// <see cref="HorrorOptionService"/> が担い、本クラスは持たない。
    /// 生成・ロード済みインスタンスを GameServiceManager.Register で共有登録して使う（IGameService）。
    /// </summary>
    public class HorrorOptionSaveRepository : SaveRepositoryBase<HorrorOptionSaveData>, IHorrorOptionSaveRepository, IGameService
    {
        protected override string SaveKey => "horror_option";
        protected override int CurrentVersion => 1;

        public HorrorOptionSaveRepository(ISaveDataStorage storage) : base(storage)
        {
        }

        protected override int GetDataVersion(HorrorOptionSaveData data) => data.Version;

        protected override void MigrateData(HorrorOptionSaveData data, int fromVersion)
        {
            data.Version = CurrentVersion;
            Debug.Log($"[HorrorOptionSaveRepository] Migrated from version {fromVersion} to {CurrentVersion}");
        }
    }
}
