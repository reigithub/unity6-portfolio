using System;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Scriptable.Database.Validation;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// Horror エネミードロップマスタの検証（グループ内 DropRate 合計の上限・ドロップ対象アイテムの制約）。
    /// 実資産に依存しないよう、レコード供給をスタブに差し替えて validator を直接与える。
    /// </summary>
    public class HorrorEnemyDropMasterValidatorTests
    {
        // 外部キーの参照先を解決できるよう、ドロップ行が参照する表もまとめて対象にする。
        private static readonly Type[] _recordTypes =
        {
            typeof(HorrorEnemyDropMaster),
            typeof(HorrorItemMaster),
        };

        private static HorrorEnemyDropMaster Drop(int id = 1, int dropGroupId = 1, int itemId = 4, int dropRate = 3000, int count = 12) => new()
        {
            Id = id,
            DropGroupId = dropGroupId,
            ItemId = itemId,
            DropRate = dropRate,
            Count = count,
        };

        private static HorrorItemMaster Item(int id = 4, string modelAssetName = "HorrorDropHandgunAmmo", bool keyItem = false) => new()
        {
            Id = id,
            ModelAssetName = modelAssetName,
            KeyItem = keyItem,
        };

        private static ValidationResult Execute(object validator, HorrorEnemyDropMaster[] drops, params HorrorItemMaster[] items)
        {
            var getter = new StubRecordGetter();
            getter.Add(drops);
            getter.Add(items);

            return ValidationExecutor.Create(_recordTypes, getter, new[] { validator }).Execute<HorrorEnemyDropMaster>();
        }

        private static ValidationResult ExecuteRateSum(params HorrorEnemyDropMaster[] drops) =>
            Execute(new HorrorEnemyDropGroupRateSumValidator(), drops, Item());

        private static ValidationResult ExecuteItem(HorrorEnemyDropMaster[] drops, params HorrorItemMaster[] items) =>
            Execute(new HorrorEnemyDropItemValidator(), drops, items);

        [Test]
        public void RateSum_ExactlyFull_HasNoErrors()
        {
            var result = ExecuteRateSum(Drop(id: 1, dropRate: 4000), Drop(id: 2, dropRate: 6000));

            Assert.IsFalse(result.HasErrors, result.DescribeErrors());
        }

        [Test]
        public void RateSum_Exceeded_ReportsErrorForEveryRowInGroup()
        {
            var result = ExecuteRateSum(Drop(id: 1, dropRate: 4001), Drop(id: 2, dropRate: 6000));

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains("10001", result.Errors["1"][0]);
            StringAssert.Contains("10001", result.Errors["2"][0]);
        }

        [Test]
        public void RateSum_SeparateGroups_AreNotSummedTogether()
        {
            var result = ExecuteRateSum(
                Drop(id: 1, dropGroupId: 1, dropRate: 6000),
                Drop(id: 2, dropGroupId: 2, dropRate: 6000));

            Assert.IsFalse(result.HasErrors, result.DescribeErrors());
        }

        [Test]
        public void Item_Valid_HasNoErrors()
        {
            var result = ExecuteItem(new[] { Drop() }, Item());

            Assert.IsFalse(result.HasErrors, result.DescribeErrors());
        }

        [Test]
        public void Item_EmptyModelAssetName_ReportsErrorKeyedByPrimaryKey()
        {
            var result = ExecuteItem(new[] { Drop() }, Item(modelAssetName: ""));

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains(nameof(HorrorItemMaster.ModelAssetName), result.Errors["1"][0]);
        }

        [Test]
        public void Item_KeyItem_ReportsErrorKeyedByPrimaryKey()
        {
            var result = ExecuteItem(new[] { Drop() }, Item(keyItem: true));

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains("キーアイテム", result.Errors["1"][0]);
        }
    }
}
