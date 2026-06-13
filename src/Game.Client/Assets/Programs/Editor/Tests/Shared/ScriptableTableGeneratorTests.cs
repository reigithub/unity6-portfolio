using System.Collections.Generic;
using System.Reflection;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Samples;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Shared
{
    public class ScriptableTableGeneratorTests
    {
        // sealed な生成テーブルには編集 API が無いため、protected records をリフレクションで投入する。
        private static WeaponLevelMasterTable Make()
        {
            var t = ScriptableObject.CreateInstance<WeaponLevelMasterTable>();
            var data = new[]
            {
                new WeaponLevelMaster { Id = 1, WeaponId = 10, Level = 1 },
                new WeaponLevelMaster { Id = 2, WeaponId = 10, Level = 2 },
                new WeaponLevelMaster { Id = 3, WeaponId = 20, Level = 1 },
                new WeaponLevelMaster { Id = 4, WeaponId = 10, Level = 3 },
            };
            typeof(ScriptableTable<WeaponLevelMaster>)
                .GetField("records", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(t, data);
            return t;
        }

        [Test]
        public void FindById_Generated()
            => Assert.AreEqual(20, Make().FindById(3).WeaponId);

        [Test]
        public void FindByWeaponId_Many()
            => Assert.AreEqual(3, Make().FindByWeaponId(10).Count);

        [Test]
        public void FindByWeaponId_Single()
            => Assert.AreEqual(1, Make().FindByWeaponId(20).Count);

        [Test]
        public void FindByWeaponId_None_IsEmpty()
            => Assert.IsTrue(Make().FindByWeaponId(99).IsEmpty);

        // index1 (WeaponId, Level) は複合ユニーク → 単一レコードを返す。
        [Test]
        public void FindByCompositeKey_Exact()
            => Assert.AreEqual(2, Make().FindByWeaponIdAndLevel((10, 2)).Id);

        [Test]
        public void TryFindByCompositeKey_Hit()
        {
            Assert.IsTrue(Make().TryFindByWeaponIdAndLevel((10, 2), out var r));
            Assert.AreEqual(2, r.Id);
        }

        [Test]
        public void FindByCompositeKey_Missing_Throws()
            => Assert.Throws<KeyNotFoundException>(() => Make().FindByWeaponIdAndLevel((10, 99)));

        [Test]
        public void FindRangeByWeaponId()
            => Assert.AreEqual(3, Make().FindRangeByWeaponId(10, 10).Count);

        [Test]
        public void FindClosestByWeaponId_Lower_ReturnsAllOfClosestKey()
        {
            // WeaponId=15 は無いので最近傍(下側)は 10。WeaponId=10 の全 3 件が返る（非ユニーク）。
            var range = Make().FindClosestByWeaponId(15, lower: true);
            Assert.AreEqual(3, range.Count);
            Assert.AreEqual(10, range[0].WeaponId);
        }
    }
}
