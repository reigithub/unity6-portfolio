using Game.Horror.Inventory;
using Game.Shared.Scriptable.Database.Tables;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorInventoryServiceTests
    {
        private HorrorInventoryService _service;

        [SetUp]
        public void Setup()
        {
            _service = new HorrorInventoryService();
        }

        #region 新規追加

        [Test]
        public void Add_NewItem_AddsEntryWithCorrectCount()
        {
            var master = new HorrorItemMaster { Id = 1, MaxQuantity = 5 };

            var added = _service.Add(master, 3);

            Assert.That(_service.Entries.Count, Is.EqualTo(1));
            Assert.That(_service.Entries[0].Count, Is.EqualTo(3));
            Assert.That(added, Is.EqualTo(3));
        }

        #endregion

        #region スタック加算

        [Test]
        public void Add_SameId_StacksCount()
        {
            var master = new HorrorItemMaster { Id = 1, MaxQuantity = 10 };
            _service.Add(master, 3);

            _service.Add(master, 4);

            Assert.That(_service.Entries.Count, Is.EqualTo(1));
            Assert.That(_service.Entries[0].Count, Is.EqualTo(7));
        }

        #endregion

        #region MaxQuantity 頭打ち

        [Test]
        public void Add_ExceedMaxQuantity_ClampsAtMax()
        {
            var master = new HorrorItemMaster { Id = 1, MaxQuantity = 5 };
            _service.Add(master, 3);

            var added = _service.Add(master, 5);

            Assert.That(_service.Entries[0].Count, Is.EqualTo(5));
            // 実加算数は要求値(5)より小さい(2)
            Assert.That(added, Is.EqualTo(2));
        }

        [Test]
        public void Add_NewItem_ExceedMaxQuantity_ClampsAtMax()
        {
            var master = new HorrorItemMaster { Id = 1, MaxQuantity = 3 };

            var added = _service.Add(master, 10);

            Assert.That(_service.Entries[0].Count, Is.EqualTo(3));
            Assert.That(added, Is.EqualTo(3));
        }

        #endregion

        #region 追加順保持

        [Test]
        public void Add_MultipleItems_PreservesInsertionOrder()
        {
            var masterA = new HorrorItemMaster { Id = 1, MaxQuantity = 5 };
            var masterB = new HorrorItemMaster { Id = 2, MaxQuantity = 5 };
            var masterC = new HorrorItemMaster { Id = 3, MaxQuantity = 5 };

            _service.Add(masterA, 1);
            _service.Add(masterB, 1);
            _service.Add(masterC, 1);

            Assert.That(_service.Entries[0].Master.Id, Is.EqualTo(1));
            Assert.That(_service.Entries[1].Master.Id, Is.EqualTo(2));
            Assert.That(_service.Entries[2].Master.Id, Is.EqualTo(3));
        }

        #endregion

        #region 無効引数ガード

        [Test]
        public void Add_ZeroCount_ReturnsZeroAndNoChange()
        {
            var master = new HorrorItemMaster { Id = 1, MaxQuantity = 5 };

            var added = _service.Add(master, 0);

            Assert.That(added, Is.EqualTo(0));
            Assert.That(_service.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void Add_NegativeCount_ReturnsZeroAndNoChange()
        {
            var master = new HorrorItemMaster { Id = 1, MaxQuantity = 5 };

            var added = _service.Add(master, -1);

            Assert.That(added, Is.EqualTo(0));
            Assert.That(_service.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void Add_NullMaster_ReturnsZeroAndNoChange()
        {
            var added = _service.Add(null, 1);

            Assert.That(added, Is.EqualTo(0));
            Assert.That(_service.Entries.Count, Is.EqualTo(0));
        }

        #endregion

        #region Clear

        [Test]
        public void Clear_RemovesAllEntries()
        {
            var master = new HorrorItemMaster { Id = 1, MaxQuantity = 5 };
            _service.Add(master, 2);

            _service.Clear();

            Assert.That(_service.Entries.Count, Is.EqualTo(0));
        }

        #endregion
    }
}
