#if UNITY_EDITOR
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// トリガーの起動先グループが初期スポーングループでないことを検証する。
    /// IsInitialSpawn のグループはシーン開始時に出現しており、トリガー起動が常に no-op になるためデータミスの兆候。
    /// 参照先グループの存在自体は <see cref="ForeignKeyAttribute"/> の宣言が担保する。
    /// </summary>
    public sealed class HorrorEnemySpawnTriggerTargetGroupValidator : IRecordValidator<HorrorEnemySpawnTriggerMaster>
    {
        public void Validate(HorrorEnemySpawnTriggerMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            foreach (var group in recordGetter.GetAll<HorrorEnemySpawnGroupMaster>())
            {
                if (group is null || group.Id != record.SpawnGroupId) continue;

                if (group.IsInitialSpawn)
                {
                    result.AddError(record.Id.ToString(),
                        $"SpawnGroupId={record.SpawnGroupId} は {nameof(HorrorEnemySpawnGroupMaster.IsInitialSpawn)}=true のグループです。" +
                        "シーン開始時に出現するため、トリガー起動は常に無効です。");
                }

                return;
            }
        }
    }
}
#endif
