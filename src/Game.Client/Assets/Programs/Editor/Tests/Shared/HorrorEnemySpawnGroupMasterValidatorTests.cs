using System;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Scriptable.Database.Validation;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    /// <summary>
    /// Horror スポーングループマスタの検証（追加スポーンの宣言・所属スポーンエントリの存在）。
    /// 実資産に依存しないよう、レコード供給をスタブに差し替えて validator を直接与える。
    /// </summary>
    public class HorrorEnemySpawnGroupMasterValidatorTests
    {
        // 外部キーの参照先を解決できるよう、スポーングループが参照する表もまとめて対象にする。
        private static readonly Type[] _recordTypes =
        {
            typeof(HorrorEnemySpawnGroupMaster),
            typeof(HorrorEnemySpawnMaster),
            typeof(HorrorEnemyMaster),
        };

        private static HorrorEnemySpawnGroupMaster Group(int id = 1, int threshold = 0, int additionalGroupId = 0) => new()
        {
            Id = id,
            IsInitialSpawn = true,
            AdditionalKillThreshold = threshold,
            AdditionalGroupId = additionalGroupId,
        };

        private static HorrorEnemySpawnMaster Spawn(int id, int spawnGroupId) => new()
        {
            Id = id,
            EnemyMasterId = 1,
            SpawnGroupId = spawnGroupId,
        };

        private static ValidationResult Execute(HorrorEnemySpawnGroupMaster[] groups, params HorrorEnemySpawnMaster[] spawns)
        {
            var getter = new StubRecordGetter();
            getter.Add(groups);
            getter.Add(spawns);
            getter.Add(Array.Empty<HorrorEnemyMaster>());

            var validators = new object[]
            {
                new HorrorEnemySpawnGroupAdditionalSpawnValidator(),
                new HorrorEnemySpawnGroupMembershipValidator(),
            };

            return ValidationExecutor.Create(_recordTypes, getter, validators).Execute<HorrorEnemySpawnGroupMaster>();
        }

        // ---- 追加スポーンの宣言 ----

        [Test]
        public void AdditionalSpawn_BothUnset_HasNoErrors()
        {
            Assert.IsFalse(Execute(new[] { Group() }, Spawn(1, 1)).HasErrors);
        }

        [Test]
        public void AdditionalSpawn_BothSet_HasNoErrors()
        {
            var groups = new[] { Group(1, threshold: 2, additionalGroupId: 2), Group(2) };

            Assert.IsFalse(Execute(groups, Spawn(1, 1), Spawn(2, 2)).HasErrors);
        }

        [TestCase(2, 0, TestName = "閾値のみ設定")]
        [TestCase(0, 2, TestName = "起動先グループのみ設定")]
        public void AdditionalSpawn_OnlyOneSide_ReportsErrorKeyedByPrimaryKey(int threshold, int additionalGroupId)
        {
            var groups = new[] { Group(1, threshold, additionalGroupId), Group(2) };

            var result = Execute(groups, Spawn(1, 1), Spawn(2, 2));

            Assert.IsTrue(result.HasErrors);
            StringAssert.Contains(nameof(HorrorEnemySpawnGroupMaster.AdditionalKillThreshold), result.Errors["1"][0]);
        }

        // ---- 所属スポーンエントリ ----

        [Test]
        public void Membership_EveryGroupHasEntry_HasNoErrors()
        {
            var groups = new[] { Group(1), Group(2) };

            Assert.IsFalse(Execute(groups, Spawn(1, 1), Spawn(2, 2)).HasErrors);
        }

        [Test]
        public void Membership_NoEntry_ReportsErrorKeyedByPrimaryKey()
        {
            var groups = new[] { Group(1), Group(2) };

            var result = Execute(groups, Spawn(1, 1));

            Assert.IsTrue(result.HasErrors);
            Assert.IsFalse(result.Errors.ContainsKey("1"), "所属エントリのあるグループは報告しない。");
            StringAssert.Contains(nameof(HorrorEnemySpawnMaster.SpawnGroupId), result.Errors["2"][0]);
        }
    }
}
