using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.Constants;
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
    public class HorrorSaveRepositoryTests
    {
        private const string SaveKey = "horror_save";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private HorrorSaveRepository _repository;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（全セクション未記録）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        [Test]
        public async Task Load_WhenNoFile_CreatesNewDataWithEmptySectionsAndFourEquipmentSlots()
        {
            await LoadDefaultData();

            Assert.That(_repository.Data, Is.Not.Null);
            Assert.That(_repository.Data.Version, Is.EqualTo(1));
            Assert.That(_repository.Data.Player.LastSavepointId, Is.EqualTo(0));
            Assert.That(_repository.Data.Inventory.Slots, Is.Empty);
            Assert.That(_repository.Data.Interaction.InteractionIds, Is.Empty);
            Assert.That(_repository.Data.Equipment.SlotType, Is.EqualTo(InventorySlotType.None));
            Assert.That(_repository.Data.Equipment.Id, Is.EqualTo(0));
            Assert.That(_repository.Data.Equipment.Slots.Count, Is.EqualTo(HorrorEquipmentConstants.MaxSlotCount));
            foreach (var slot in _repository.Data.Equipment.Slots)
                Assert.That(slot.SlotType, Is.EqualTo(InventorySlotType.None));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task Load_WhenSlotsNull_NormalizesToFourSlots()
        {
            var data = new HorrorSaveData
            {
                Version = 1,
                Equipment = new HorrorEquipmentSaveData { Slots = null },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            Assert.That(_repository.Data.Equipment.Slots, Is.Not.Null);
            Assert.That(_repository.Data.Equipment.Slots.Count, Is.EqualTo(HorrorEquipmentConstants.MaxSlotCount));
        }

        [Test]
        public async Task Load_ExistingDataWithZeroId_DoesNotTouchDatabase()
        {
            // Id=0 は NormalizePlayer の != 0 ガードでマスター照会をスキップする。
            // Repository は区画一括正規化のため database 参照自体は取得するが、Player 区画のテーブルへは触れない。
            var data = new HorrorSaveData
            {
                Version = 1,
                Player = new HorrorPlayerSaveData { LastSavepointId = 0 },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            Assert.That(_repository.Data, Is.Not.Null);
            Assert.That(_repository.Data.Player.LastSavepointId, Is.EqualTo(0));
        }

        [Test]
        public void Serialization_RoundTrip_PreservesAllSections()
        {
            var original = new HorrorSaveData
            {
                Version = 1,
                Player = new HorrorPlayerSaveData { LastSavepointId = 42 },
                Inventory = new HorrorInventorySaveData
                {
                    Slots = new List<HorrorInventorySlotData>
                    {
                        new() { SlotType = InventorySlotType.Item, Id = 3, Count = 4 },
                    },
                },
                Equipment = new HorrorEquipmentSaveData
                {
                    SlotType = InventorySlotType.Weapon,
                    Id = 5,
                    Slots = new List<HorrorEquipmentSlotData>
                    {
                        new() { SlotType = InventorySlotType.Weapon, Id = 5 },
                        new() { SlotType = InventorySlotType.None, Id = 0 },
                        new() { SlotType = InventorySlotType.Item, Id = 3 },
                        new() { SlotType = InventorySlotType.None, Id = 0 },
                    },
                    Magazines = new List<HorrorWeaponMagazineData>
                    {
                        new() { WeaponId = 5, Count = 12 },
                    },
                },
                Interaction = new HorrorInteractionSaveData
                {
                    InteractionIds = new List<int> { 1, 2, 3 },
                },
            };

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<HorrorSaveData>(bytes);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(1));
            Assert.That(restored.Player.LastSavepointId, Is.EqualTo(42));
            Assert.That(restored.Inventory.Slots.Count, Is.EqualTo(1));
            Assert.That(restored.Inventory.Slots[0].SlotType, Is.EqualTo(InventorySlotType.Item));
            Assert.That(restored.Inventory.Slots[0].Id, Is.EqualTo(3));
            Assert.That(restored.Inventory.Slots[0].Count, Is.EqualTo(4));
            Assert.That(restored.Equipment.SlotType, Is.EqualTo(InventorySlotType.Weapon));
            Assert.That(restored.Equipment.Id, Is.EqualTo(5));
            Assert.That(restored.Equipment.Slots.Count, Is.EqualTo(4));
            Assert.That(restored.Equipment.Slots[0].SlotType, Is.EqualTo(InventorySlotType.Weapon));
            Assert.That(restored.Equipment.Slots[0].Id, Is.EqualTo(5));
            Assert.That(restored.Equipment.Slots[2].SlotType, Is.EqualTo(InventorySlotType.Item));
            Assert.That(restored.Equipment.Slots[2].Id, Is.EqualTo(3));
            Assert.That(restored.Equipment.Magazines.Count, Is.EqualTo(1));
            Assert.That(restored.Equipment.Magazines[0].WeaponId, Is.EqualTo(5));
            Assert.That(restored.Equipment.Magazines[0].Count, Is.EqualTo(12));
            Assert.That(restored.Interaction.InteractionIds, Is.EquivalentTo(new[] { 1, 2, 3 }));
        }
    }
}
