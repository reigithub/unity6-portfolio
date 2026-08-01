#if UNITY_EDITOR
using System.Collections.Generic;
using Game.Shared.Scriptable.Database.Tables;

namespace Game.Shared.Scriptable.Database.Validation
{
    /// <summary>
    /// 抽選グループ内の DropRate 合計が 10000（万分率の 100%）を超えないことを検証する。
    /// 超過すると累積抽選で末尾行が当選不能になるためデータミス。不足は「ドロップなし」の正当仕様なので許容する。
    /// グループ合算はレコード単位では判定できないため <see cref="IRecordsValidator{TRecord}"/> で書く。
    /// </summary>
    public sealed class HorrorEnemyDropGroupRateSumValidator : IRecordsValidator<HorrorEnemyDropMaster>
    {
        private const int MaxRateSum = 10000;

        public void Validate(
            IReadOnlyList<HorrorEnemyDropMaster> allRecords, ValidationResult result, IRecordGetter recordGetter)
        {
            var rateSums = new Dictionary<int, int>();
            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];
                if (record is null) continue;

                rateSums.TryGetValue(record.DropGroupId, out var sum);
                rateSums[record.DropGroupId] = sum + record.DropRate;
            }

            // 修正対象を特定しやすいよう、超過グループに属する全行へエラーを付ける
            for (int i = 0; i < allRecords.Count; i++)
            {
                var record = allRecords[i];
                if (record is null) continue;

                var sum = rateSums[record.DropGroupId];
                if (sum <= MaxRateSum) continue;

                result.AddError(record.Id.ToString(),
                    $"DropGroupId={record.DropGroupId} の {nameof(HorrorEnemyDropMaster.DropRate)} 合計が {sum} です。" +
                    $"{MaxRateSum}（万分率の 100%）以下にしてください。");
            }
        }
    }

    /// <summary>
    /// ドロップ対象アイテムの制約を検証する。
    /// ModelAssetName が空だとドロップ品プレハブを解決できず、キーアイテムは未回収ドロップが永続化されないため配布できない。
    /// 参照先アイテムの存在自体は <see cref="ForeignKeyAttribute"/> の宣言が担保する。
    /// </summary>
    public sealed class HorrorEnemyDropItemValidator : IRecordValidator<HorrorEnemyDropMaster>
    {
        public void Validate(HorrorEnemyDropMaster record, ValidationResult result, IRecordGetter recordGetter)
        {
            foreach (var item in recordGetter.GetAll<HorrorItemMaster>())
            {
                if (item is null || item.Id != record.ItemId) continue;

                if (string.IsNullOrEmpty(item.ModelAssetName))
                {
                    result.AddError(record.Id.ToString(),
                        $"ItemId={record.ItemId} の {nameof(HorrorItemMaster.ModelAssetName)} が空のため、ドロップ品プレハブを解決できません。");
                }

                if (item.KeyItem)
                {
                    result.AddError(record.Id.ToString(),
                        $"ItemId={record.ItemId} はキーアイテムです。未回収ドロップは永続化されないため配布できません。");
                }

                return;
            }
        }
    }
}
#endif
