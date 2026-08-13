using System;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Scriptable.Database.Validation;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// Horror エネミーマスタの検証（撃破時ドロップの抽選グループに対応する抽選行の存在）。
    /// 実資産に依存しないよう、レコード供給をスタブに差し替えて validator を直接与える。
    /// </summary>
    public class HorrorEnemyMasterValidatorTests
    {
        // 外部キーの参照先を解決できるよう、ドロップ行が参照する表もまとめて対象にする。
        private static readonly Type[] _recordTypes =
        {
            typeof(HorrorEnemyMaster),
            typeof(HorrorEnemyDropMaster),
            typeof(HorrorItemMaster),
        };

        private static HorrorEnemyMaster Enemy(int id = 1, int dropGroupId = 0) => new()
        {
            Id = id,
            DropGroupId = dropGroupId,
        };

        private static HorrorEnemyDropMaster Drop(int id = 1, int dropGroupId = 1) => new()
        {
            Id = id,
            DropGroupId = dropGroupId,
            ItemId = 4,
            DropRate = 3000,
            Count = 12,
        };

        private static ValidationResult Execute(HorrorEnemyMaster[] enemies, params HorrorEnemyDropMaster[] drops)
        {
            var getter = new StubRecordGetter();
            getter.Add(enemies);
            getter.Add(drops);
            getter.Add(new HorrorItemMaster { Id = 4, ModelAssetName = "HorrorDropHandgunAmmo" });

            var validators = new object[] { new HorrorEnemyMasterDropGroupValidator() };

            return ValidationExecutor.Create(_recordTypes, getter, validators).Execute<HorrorEnemyMaster>();
        }

        [Test]
        public void DropGroupId_None_HasNoErrors()
        {
            var result = Execute(new[] { Enemy(dropGroupId: 0) });

            Assert.IsFalse(result.HasErrors, result.DescribeErrors());
        }

        [Test]
        public void DropGroupId_WithMatchingDropRow_HasNoErrors()
        {
            var result = Execute(new[] { Enemy(dropGroupId: 1) }, Drop(dropGroupId: 1));

            Assert.IsFalse(result.HasErrors, result.DescribeErrors());
        }

        [Test]
        public void DropGroupId_WithoutMatchingDropRow_ReportsErrorKeyedByPrimaryKey()
        {
            var result = Execute(new[] { Enemy(dropGroupId: 9) }, Drop(dropGroupId: 1));

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains(nameof(HorrorEnemyDropMaster), result.Errors["1"][0]);
        }
    }
}
