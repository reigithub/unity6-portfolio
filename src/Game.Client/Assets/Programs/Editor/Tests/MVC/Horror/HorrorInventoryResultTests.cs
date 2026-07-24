using Game.Horror.Dialogs;
using Game.Shared.Enums;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorInventoryResultTests
    {
        // HasEquipRequest は EquipCategory の番兵（None）で判定する。
        // default 返却（多重要求時・強制クローズ時）で誤発火しないことが最重要契約。

        [Test]
        public void HasEquipRequest_Default_IsFalse()
            => Assert.That(default(HorrorInventoryResult).HasEquipRequest, Is.False);

        [Test]
        public void HasEquipRequest_WeaponCategory_IsTrue()
        {
            var result = new HorrorInventoryResult { EquipCategory = ObjectCategory.Weapon, EquipId = 1 };
            Assert.That(result.HasEquipRequest, Is.True);
        }

        [Test]
        public void HasEquipRequest_NoneCategory_IsFalse()
        {
            var result = new HorrorInventoryResult { EquipCategory = ObjectCategory.None, EquipId = 1 };
            Assert.That(result.HasEquipRequest, Is.False);
        }

        // HasUseRequest も UseCategory の番兵（None）で判定する。Equip 予約との併存を許容する。

        [Test]
        public void HasUseRequest_Default_IsFalse()
            => Assert.That(default(HorrorInventoryResult).HasUseRequest, Is.False);

        [Test]
        public void HasUseRequest_ItemCategory_IsTrue()
        {
            var result = new HorrorInventoryResult { UseCategory = ObjectCategory.Item, UseId = 5 };
            Assert.That(result.HasUseRequest, Is.True);
        }

        [Test]
        public void HasUseRequest_NoneCategory_IsFalse()
        {
            var result = new HorrorInventoryResult { UseCategory = ObjectCategory.None, UseId = 5 };
            Assert.That(result.HasUseRequest, Is.False);
        }

        [Test]
        public void HasUseRequest_WithEquipRequest_BothTrue()
        {
            var result = new HorrorInventoryResult
            {
                EquipCategory = ObjectCategory.Weapon,
                EquipId = 1,
                UseCategory = ObjectCategory.Item,
                UseId = 5,
            };

            Assert.That(result.HasEquipRequest, Is.True);
            Assert.That(result.HasUseRequest, Is.True);
        }

    }
}
