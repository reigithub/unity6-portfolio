#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// 追加スポーン（キル数到達での連鎖）の宣言が揃っているかを検証する。
    /// 閾値と起動先グループは 2 列で 1 つの条件を成し、片側だけでは連鎖が起きないため属性では表せない。
    /// </summary>
    public sealed class HorrorEnemySpawnGroupAdditionalSpawnValidator : IRecordValidator<HorrorEnemySpawnGroupMaster>
    {
        public void Validate(HorrorEnemySpawnGroupMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            if ((record.AdditionalGroupId != 0) == (record.AdditionalKillThreshold > 0)) return;

            result.AddError(record.Id.ToString(),
                $"AdditionalKillThreshold={record.AdditionalKillThreshold} と AdditionalGroupId={record.AdditionalGroupId} は" +
                " 両方設定するか両方 0 にしてください。");
        }
    }

    /// <summary>
    /// スポーングループに所属するスポーンエントリが存在するかを検証する。
    /// 所属 0 件のグループは起動しても 1 体も出ず、全滅連鎖も進まない（空集合を全滅扱いにしないため）。
    /// 逆参照の集計はレコード単位では判定できないため <see cref="IRecordsValidator{TRecord}"/> で書く。
    /// </summary>
    public sealed class HorrorEnemySpawnGroupMembershipValidator : IRecordsValidator<HorrorEnemySpawnGroupMaster>
    {
        public void Validate(
            IReadOnlyList<HorrorEnemySpawnGroupMaster> allRecords, ValidationResult result, IRecordGetter recordGetter)
        {
            var referencedSpawnGroupIds = new HashSet<int>();
            foreach (var spawn in recordGetter.GetAll<HorrorEnemySpawnMaster>())
            {
                if (spawn is null) continue;

                referencedSpawnGroupIds.Add(spawn.SpawnGroupId);
            }

            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];
                if (record is null) continue;

                if (referencedSpawnGroupIds.Contains(record.Id)) continue;

                result.AddError(record.Id.ToString(),
                    $"この Id を {nameof(HorrorEnemySpawnMaster)}.{nameof(HorrorEnemySpawnMaster.SpawnGroupId)} に持つスポーンエントリがありません。");
            }
        }
    }
}
#endif
