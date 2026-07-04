using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorEquipmentShortcutSaveServiceTests
    {
        private const string SaveKey = "horror_equipment_shortcut";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private HorrorEquipmentShortcutSaveService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _service = new HorrorEquipmentShortcutSaveService(_mockStorage, _mockDatabase);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（4空スロット）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorEquipmentShortcutSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorEquipmentShortcutSaveData>(null));
            await _service.LoadAsync();
        }

        [Test]
        public async Task Load_WhenNoFile_CreatesFourEmptySlots()
        {
            await LoadDefaultData();

            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.Data.Version, Is.EqualTo(1));
            Assert.That(_service.Data.Slots.Count, Is.EqualTo(HorrorEquipmentShortcutSaveService.SlotCount));
            for (int i = 0; i < HorrorEquipmentShortcutSaveService.SlotCount; i++)
                Assert.That(_service.TryGet(i, out _), Is.False, $"slot {i} は初期空");
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task Set_RegistersAndMarksDirty()
        {
            await LoadDefaultData();

            var ok = _service.Set(1, InventorySlotType.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGet(1, out var slot), Is.True);
            Assert.That(slot.SlotType, Is.EqualTo(InventorySlotType.Weapon));
            Assert.That(slot.Id, Is.EqualTo(5));
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public async Task Clear_RemovesRegistrationAndMarksDirty()
        {
            await LoadDefaultData();
            _service.Set(2, InventorySlotType.Item, 3);

            var ok = _service.Clear(2);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGet(2, out _), Is.False);
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public async Task Set_OutOfRange_ReturnsFalse()
        {
            await LoadDefaultData();

            Assert.That(_service.Set(-1, InventorySlotType.Item, 1), Is.False);
            Assert.That(_service.Set(HorrorEquipmentShortcutSaveService.SlotCount, InventorySlotType.Item, 1), Is.False);
        }

        [Test]
        public void Mutators_WhenDataNull_DoNotThrowAndReturnFalse()
        {
            // LoadAsync 未実行 ＝ Data は null
            Assert.DoesNotThrow(() =>
            {
                Assert.That(_service.Set(0, InventorySlotType.Item, 1), Is.False);
                Assert.That(_service.Clear(0), Is.False);
                Assert.That(_service.TryGet(0, out _), Is.False);
            });
        }

        [Test]
        public void Serialization_RoundTrip_PreservesSlots()
        {
            var original = new HorrorEquipmentShortcutSaveData
            {
                Version = 1,
                Slots = new List<HorrorEquipmentShortcutSlotData>
                {
                    new() { SlotType = InventorySlotType.Weapon, Id = 5 },
                    new() { SlotType = InventorySlotType.None, Id = 0 },
                    new() { SlotType = InventorySlotType.Item, Id = 3 },
                    new() { SlotType = InventorySlotType.None, Id = 0 },
                },
            };

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<HorrorEquipmentShortcutSaveData>(bytes);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(1));
            Assert.That(restored.Slots.Count, Is.EqualTo(4));
            Assert.That(restored.Slots[0].SlotType, Is.EqualTo(InventorySlotType.Weapon));
            Assert.That(restored.Slots[0].Id, Is.EqualTo(5));
            Assert.That(restored.Slots[2].SlotType, Is.EqualTo(InventorySlotType.Item));
            Assert.That(restored.Slots[2].Id, Is.EqualTo(3));
        }
    }
}
