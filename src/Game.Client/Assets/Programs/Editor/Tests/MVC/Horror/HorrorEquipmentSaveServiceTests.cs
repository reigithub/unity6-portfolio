using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorEquipmentSaveServiceTests
    {
        private const string SaveKey = "horror_equipment";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorInventorySaveService _mockInventory;
        private HorrorEquipmentSaveService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _mockInventory = Substitute.For<IHorrorInventorySaveService>();
            _service = new HorrorEquipmentSaveService(_mockStorage, _mockDatabase, _mockInventory);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未装備）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorEquipmentSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorEquipmentSaveData>(null));
            await _service.LoadAsync();
        }

        [Test]
        public async Task Load_WhenNoFile_CreatesUnequippedData()
        {
            await LoadDefaultData();

            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.Data.Version, Is.EqualTo(1));
            Assert.That(_service.TryGetEquipped(out _, out _), Is.False);
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task TryEquip_WeaponPossessed_SucceedsAndMarksDirty()
        {
            await LoadDefaultData();
            _mockInventory.HasItem(InventorySlotType.Weapon, 5).Returns(true);

            var ok = _service.TryEquip(InventorySlotType.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.True);
            Assert.That(type, Is.EqualTo(InventorySlotType.Weapon));
            Assert.That(id, Is.EqualTo(5));
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public async Task TryEquip_Item_ReturnsFalse()
        {
            await LoadDefaultData();
            _mockInventory.HasItem(InventorySlotType.Item, 3).Returns(true);

            var ok = _service.TryEquip(InventorySlotType.Item, 3);

            Assert.That(ok, Is.False);
            Assert.That(_service.TryGetEquipped(out _, out _), Is.False);
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task TryEquip_NotPossessed_ReturnsFalse()
        {
            await LoadDefaultData();
            _mockInventory.HasItem(InventorySlotType.Weapon, 5).Returns(false);

            var ok = _service.TryEquip(InventorySlotType.Weapon, 5);

            Assert.That(ok, Is.False);
            Assert.That(_service.TryGetEquipped(out _, out _), Is.False);
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task TryEquip_SameWeaponAgain_ReturnsTrueIdempotently()
        {
            await LoadDefaultData();
            _mockInventory.HasItem(InventorySlotType.Weapon, 5).Returns(true);
            _service.TryEquip(InventorySlotType.Weapon, 5);

            var ok = _service.TryEquip(InventorySlotType.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.True);
            Assert.That(type, Is.EqualTo(InventorySlotType.Weapon));
            Assert.That(id, Is.EqualTo(5));
        }

        [Test]
        public async Task TryGetEquipped_Unequipped_ReturnsFalse()
        {
            await LoadDefaultData();

            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.False);
            Assert.That(type, Is.EqualTo(InventorySlotType.None));
            Assert.That(id, Is.EqualTo(0));
        }

        [Test]
        public void Mutators_WhenDataNull_DoNotThrowAndReturnFalse()
        {
            // LoadAsync 未実行 ＝ Data は null
            Assert.DoesNotThrow(() =>
            {
                Assert.That(_service.TryEquip(InventorySlotType.Weapon, 1), Is.False);
                Assert.That(_service.TryGetEquipped(out _, out _), Is.False);
            });
        }

        [Test]
        public void Serialization_RoundTrip_PreservesEquippedState()
        {
            var original = new HorrorEquipmentSaveData
            {
                Version = 1,
                SlotType = InventorySlotType.Weapon,
                Id = 5,
            };

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<HorrorEquipmentSaveData>(bytes);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(1));
            Assert.That(restored.SlotType, Is.EqualTo(InventorySlotType.Weapon));
            Assert.That(restored.Id, Is.EqualTo(5));
        }
    }
}
