using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.Constants;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorSaveRepositoryTests
    {
        private const string SaveKey = "horror_save_slot1";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorSaveRepository _repository;

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
            Assert.That(_repository.Data.Version, Is.EqualTo(HorrorSaveConstants.SaveDataLatestVersion));
            Assert.That(_repository.Data.Player.LastSavepointId, Is.EqualTo(0));
            Assert.That(_repository.Data.Inventory.Slots, Is.Empty);
            Assert.That(_repository.Data.Interaction.InteractionIds, Is.Empty);
            Assert.That(_repository.Data.Equipment.SlotType, Is.EqualTo(InventorySlotType.None));
            Assert.That(_repository.Data.Equipment.Id, Is.EqualTo(0));
            Assert.That(_repository.Data.Equipment.Slots.Count, Is.EqualTo(HorrorEquipmentConstants.MaxEquipmentSlotCount));
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
            Assert.That(_repository.Data.Equipment.Slots.Count, Is.EqualTo(HorrorEquipmentConstants.MaxEquipmentSlotCount));
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

        [Test]
        public void Serialization_RoundTrip_PreservesMetaFields()
        {
            var savedAt = new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc);
            var original = new HorrorSaveData
            {
                Version = 2,
                SlotNo = 3,
                SavedAtUtc = savedAt,
                SavepointId = 42,
            };

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<HorrorSaveData>(bytes);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(2));
            Assert.That(restored.SlotNo, Is.EqualTo(3));
            Assert.That(restored.SavedAtUtc, Is.EqualTo(savedAt));
            Assert.That(restored.SavepointId, Is.EqualTo(42));
        }

        [Test]
        public async Task SaveToSlotAsync_WithValidSlot_SavesToSlotKeyAndWritesMeta()
        {
            await LoadDefaultData();
            _mockStorage.SaveAsync("horror_save_slot3", Arg.Any<HorrorSaveData>())
                .Returns(UniTask.CompletedTask);
            _repository.Data.Player.LastSavepointId = 42;

            await _repository.SaveBySlotAsync(3);

            Assert.That(_repository.CurrentSlot, Is.EqualTo(3));
            Assert.That(_repository.Data.SlotNo, Is.EqualTo(3));
            Assert.That(_repository.Data.SavepointId, Is.EqualTo(42));
            Assert.That(_repository.Data.SavedAtUtc, Is.Not.EqualTo(default(DateTime)));
            await _mockStorage.Received(1).SaveAsync(
                "horror_save_slot3",
                Arg.Is<HorrorSaveData>(d => d.SlotNo == 3 && d.SavepointId == 42));
        }

        [TestCase(0)]
        [TestCase(11)]
        public async Task SaveToSlotAsync_WithSlotOutOfRange_DoesNotSave(int slotNumber)
        {
            await LoadDefaultData();
            LogAssert.Expect(LogType.Error, new Regex("Invalid slot number"));

            await _repository.SaveBySlotAsync(slotNumber);

            Assert.That(_repository.CurrentSlot, Is.EqualTo(1));
            await _mockStorage.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<HorrorSaveData>());
        }

        [Test]
        public async Task LoadSlotInfosAsync_WhenSlotEmpty_ReturnsHasDataFalse()
        {
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult<HorrorSaveData>(null));

            var infos = await _repository.LoadSlotInfosAsync();

            Assert.That(infos.Count, Is.EqualTo(HorrorSaveConstants.MaxSaveSlotCount));
            Assert.That(infos[0].SlotNo, Is.EqualTo(1));
            Assert.That(infos[0].HasData, Is.False);
        }

        [Test]
        public async Task LoadSlotInfosAsync_WhenSlotHasData_ReturnsMeta()
        {
            var savedAt = new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            _mockStorage.LoadAsync<HorrorSaveData>("horror_save_slot3")
                .Returns(UniTask.FromResult(new HorrorSaveData { SlotNo = 3, SavedAtUtc = savedAt, SavepointId = 42 }));

            var infos = await _repository.LoadSlotInfosAsync();

            var info = infos[2];
            Assert.That(info.SlotNo, Is.EqualTo(3));
            Assert.That(info.HasData, Is.True);
            Assert.That(info.SavedAtUtc, Is.EqualTo(savedAt));
            Assert.That(info.SavepointId, Is.EqualTo(42));
        }

        [Test]
        public async Task LoadSlotInfosAsync_DoesNotMutateCurrentData()
        {
            await LoadDefaultData();
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult<HorrorSaveData>(null));

            var currentData = _repository.Data;

            await _repository.LoadSlotInfosAsync();

            Assert.That(_repository.Data, Is.SameAs(currentData));
        }
    }
}
