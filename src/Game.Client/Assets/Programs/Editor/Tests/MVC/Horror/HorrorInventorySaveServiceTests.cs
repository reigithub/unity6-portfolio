using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Shared.Enums;
using Game.Shared.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorInventorySaveServiceTests
    {
        private const string SaveKey = "horror_inventory";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private HorrorInventorySaveService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _service = new HorrorInventorySaveService(_mockStorage, _mockDatabase);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（空インベントリ）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorInventorySaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorInventorySaveData>(null));
            await _service.LoadAsync();
        }

        private static IHorrorInventorySlotInfo MakeInfo(InventorySlotType type, int id, int maxCount)
        {
            var info = Substitute.For<IHorrorInventorySlotInfo>();
            info.SlotType.Returns(type);
            info.Id.Returns(id);
            info.MaxCount.Returns(maxCount);
            return info;
        }

        [Test]
        public async Task GetCount_NotPossessed_ReturnsZero()
        {
            await LoadDefaultData();

            Assert.That(_service.GetCount(InventorySlotType.Item, 3), Is.EqualTo(0));
        }

        [Test]
        public async Task GetCount_AfterTryAdd_ReturnsAddedCount()
        {
            await LoadDefaultData();
            var info = MakeInfo(InventorySlotType.Item, 3, 10);

            _service.TryAdd(info, 4);

            Assert.That(_service.GetCount(InventorySlotType.Item, 3), Is.EqualTo(4));
        }

        [Test]
        public async Task TryConsume_FullAmount_RemovesSlotAndReturnsTrue()
        {
            await LoadDefaultData();
            var info = MakeInfo(InventorySlotType.Item, 3, 10);
            _service.TryAdd(info, 4);

            var ok = _service.TryConsume(InventorySlotType.Item, 3, 4);

            Assert.That(ok, Is.True);
            Assert.That(_service.HasItem(InventorySlotType.Item, 3), Is.False);
            Assert.That(_service.GetCount(InventorySlotType.Item, 3), Is.EqualTo(0));
        }

        [Test]
        public async Task TryConsume_PartialAmount_ReturnsTrueAndLeavesRemainder()
        {
            await LoadDefaultData();
            var info = MakeInfo(InventorySlotType.Item, 3, 10);
            _service.TryAdd(info, 4);

            var ok = _service.TryConsume(InventorySlotType.Item, 3, 1);

            Assert.That(ok, Is.True);
            Assert.That(_service.GetCount(InventorySlotType.Item, 3), Is.EqualTo(3));
        }

        [Test]
        public async Task TryConsume_InsufficientCount_ReturnsFalseAndLeavesUnchanged()
        {
            await LoadDefaultData();
            var info = MakeInfo(InventorySlotType.Item, 3, 10);
            _service.TryAdd(info, 2);

            var ok = _service.TryConsume(InventorySlotType.Item, 3, 5);

            Assert.That(ok, Is.False);
            Assert.That(_service.GetCount(InventorySlotType.Item, 3), Is.EqualTo(2));
        }

        [Test]
        public async Task TryConsume_ZeroOrNegativeCount_ReturnsFalse()
        {
            await LoadDefaultData();
            var info = MakeInfo(InventorySlotType.Item, 3, 10);
            _service.TryAdd(info, 2);

            Assert.That(_service.TryConsume(InventorySlotType.Item, 3, 0), Is.False);
            Assert.That(_service.TryConsume(InventorySlotType.Item, 3, -1), Is.False);
            Assert.That(_service.GetCount(InventorySlotType.Item, 3), Is.EqualTo(2));
        }

        [Test]
        public async Task TryConsume_Success_MarksDirty()
        {
            await LoadDefaultData();
            // TryAdd 経由だと既に Dirty になるため、直接 Slots へ登録して Dirty を汚さず前提を作る。
            _service.Data.Slots.Add(new HorrorInventorySlotData { SlotType = InventorySlotType.Item, Id = 3, Count = 4 });

            var ok = _service.TryConsume(InventorySlotType.Item, 3, 1);

            Assert.That(ok, Is.True);
            Assert.That(_service.IsDirty, Is.True);
        }
    }
}
