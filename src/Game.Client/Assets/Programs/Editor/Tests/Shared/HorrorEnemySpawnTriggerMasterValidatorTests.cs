using System;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Scriptable.Database.Validation;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// Horror スポーントリガーマスタの検証（起動先グループが初期スポーングループでないこと）。
    /// 実資産に依存しないよう、レコード供給をスタブに差し替えて validator を直接与える。
    /// </summary>
    public class HorrorEnemySpawnTriggerMasterValidatorTests
    {
        // 外部キーの参照先を解決できるよう、トリガーが参照する表もまとめて対象にする。
        private static readonly Type[] _recordTypes =
        {
            typeof(HorrorEnemySpawnTriggerMaster),
            typeof(HorrorEnemySpawnGroupMaster),
        };

        private static HorrorEnemySpawnTriggerMaster Trigger(int id = 1, int spawnGroupId = 1) => new()
        {
            Id = id,
            SpawnGroupId = spawnGroupId,
        };

        private static HorrorEnemySpawnGroupMaster Group(int id = 1, bool isInitialSpawn = false) => new()
        {
            Id = id,
            IsInitialSpawn = isInitialSpawn,
        };

        private static ValidationResult Execute(HorrorEnemySpawnTriggerMaster[] triggers, params HorrorEnemySpawnGroupMaster[] groups)
        {
            var getter = new StubRecordGetter();
            getter.Add(triggers);
            getter.Add(groups);

            var validators = new object[] { new HorrorEnemySpawnTriggerTargetGroupValidator() };

            return ValidationExecutor.Create(_recordTypes, getter, validators).Execute<HorrorEnemySpawnTriggerMaster>();
        }

        [Test]
        public void TargetGroup_NotInitialSpawn_HasNoErrors()
        {
            Assert.IsFalse(Execute(new[] { Trigger() }, Group()).HasErrors);
        }

        [Test]
        public void TargetGroup_InitialSpawn_ReportsErrorKeyedByPrimaryKey()
        {
            var result = Execute(new[] { Trigger() }, Group(isInitialSpawn: true));

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains(nameof(HorrorEnemySpawnGroupMaster.IsInitialSpawn), result.Errors["1"][0]);
        }
    }
}
