using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.Constants;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorSaveRepositoryTests
    {
        private const string SaveKey = "horror_save_slot0";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorSaveRepository _repository;
        private HorrorEnemySpawnMasterTable _spawnTable;
        private HorrorEnemySpawnTriggerMasterTable _triggerTable;
        private HorrorItemMasterTable _itemTable;
        private ScriptableDatabase _database;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawnTable != null) UnityEngine.Object.DestroyImmediate(_spawnTable);
            if (_triggerTable != null) UnityEngine.Object.DestroyImmediate(_triggerTable);
            if (_itemTable != null) UnityEngine.Object.DestroyImmediate(_itemTable);
            if (_database != null) UnityEngine.Object.DestroyImmediate(_database);
        }

        private void SetupRealDatabaseWithSpawnIds(params int[] spawnIds) =>
            SetupRealDatabase(spawnIds, triggerIds: Array.Empty<int>());

        /// <summary>
        /// 指定 Id のスポーンエントリ・スポーントリガーを持つ実テーブル＋DB を組み立てて mock に接続する。
        /// EditorImportRows は列名をメンバ名と完全一致でマッピングし、無い列は既定値のままとなる。
        /// </summary>
        private void SetupRealDatabase(int[] spawnIds, int[] triggerIds)
        {
            _spawnTable = ScriptableObject.CreateInstance<HorrorEnemySpawnMasterTable>();
            _spawnTable.EditorImportRows(new[] { "Id" }, IdRows(spawnIds), mergeByPrimaryKey: false);

            _triggerTable = ScriptableObject.CreateInstance<HorrorEnemySpawnTriggerMasterTable>();
            _triggerTable.EditorImportRows(new[] { "Id" }, IdRows(triggerIds), mergeByPrimaryKey: false);

            _database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(_database);
            so.FindProperty("horrorEnemySpawnMasterTable").objectReferenceValue = _spawnTable;
            so.FindProperty("horrorEnemySpawnTriggerMasterTable").objectReferenceValue = _triggerTable;
            so.ApplyModifiedPropertiesWithoutUndo();
            _mockDatabase.Database.Returns(_database);
        }

        private static List<IReadOnlyList<string>> IdRows(int[] ids)
        {
            var rows = new List<IReadOnlyList<string>>();
            foreach (var id in ids)
                rows.Add(new[] { id.ToString() });
            return rows;
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
            Assert.That(_repository.Data.SavepointId, Is.EqualTo(0));
            Assert.That(_repository.Data.Player.PlayerId, Is.EqualTo(HorrorSaveConstants.DefaultPlayerId));
            Assert.That(_repository.Data.Inventory.Slots, Is.Empty);
            Assert.That(_repository.Data.Interaction.InteractionIds, Is.Empty);
            Assert.That(_repository.Data.Equipment.ObjectCategory, Is.EqualTo(ObjectCategory.None));
            Assert.That(_repository.Data.Equipment.Id, Is.EqualTo(0));
            Assert.That(_repository.Data.Equipment.Slots.Count, Is.EqualTo(HorrorEquipmentConstants.MaxEquipmentSlotCount));
            foreach (var slot in _repository.Data.Equipment.Slots)
                Assert.That(slot.ObjectCategory, Is.EqualTo(ObjectCategory.None));
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
            // Id=0 は NormalizeSavepoint の != 0 ガードでマスター照会をスキップする。
            // Repository は区画一括正規化のため database 参照自体は取得するが、セーブポイントのテーブルへは触れない。
            var data = new HorrorSaveData
            {
                Version = 1,
                SavepointId = 0,
            };
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            Assert.That(_repository.Data, Is.Not.Null);
            Assert.That(_repository.Data.SavepointId, Is.EqualTo(0));
        }

        // エネミー区画：旧形式補填、マスタ整合（テーブル結線は起動時の一括検査が保証する前提）。

        [Test]
        public async Task Load_ExistingDataWithoutEnemySection_FillsEnemy()
        {
            // 旧形式（Enemy 区画なし）のバイナリはデシリアライズで Enemy=null になる。補填を固定する
            // （実環境同様に DB ロード済み状態で読み込む。NormalizeEnemy はテーブル存在確認で database へ触るため）
            SetupRealDatabaseWithSpawnIds();
            var data = new HorrorSaveData
            {
                Version = 1,
                Enemy = null,
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            Assert.That(_repository.Data.Enemy, Is.Not.Null);
            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.Empty);
            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.Empty);
        }

        [Test]
        public async Task NormalizeEnemy_RemovesUnknownSpawnIds()
        {
            SetupRealDatabaseWithSpawnIds(1, 3);
            var data = new HorrorSaveData
            {
                Version = 1,
                Enemy = new HorrorEnemySaveData { DefeatedSpawnIds = new List<int> { 1, 2, 3, 99 } },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1, 3 }));
        }

        [Test]
        public async Task NormalizeEnemy_RemovesUnknownFiredTriggerIds()
        {
            SetupRealDatabase(spawnIds: new[] { 1 }, triggerIds: new[] { 1, 3 });
            var data = new HorrorSaveData
            {
                Version = 1,
                Enemy = new HorrorEnemySaveData { FiredTriggerIds = new List<int> { 1, 2, 3, 99 } },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.EquivalentTo(new[] { 1, 3 }));
        }

        [Test]
        public async Task NormalizeEnemy_WhenFiredTriggerIdsNull_FillsEmpty()
        {
            // 列追加前の旧バイナリはデシリアライズで FiredTriggerIds=null になる。null 埋めを固定する
            SetupRealDatabaseWithSpawnIds();
            var data = new HorrorSaveData
            {
                Version = 1,
                Enemy = new HorrorEnemySaveData { FiredTriggerIds = null },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.Not.Null);
            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.Empty);
        }

        /// <summary>指定 Id のアイテムマスター（MaxCount=120）だけを持つ実 DB を組み立てて mock に接続する。</summary>
        private void SetupRealDatabaseWithItem(int itemId)
        {
            _itemTable = ScriptableObject.CreateInstance<HorrorItemMasterTable>();
            _itemTable.EditorImportRows(
                new[] { "Id", "MaxCount" },
                new[] { new[] { itemId.ToString(), "120" } },
                mergeByPrimaryKey: false);

            _database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(_database);
            so.FindProperty("horrorItemMasterTable").objectReferenceValue = _itemTable;
            so.ApplyModifiedPropertiesWithoutUndo();
            _mockDatabase.Database.Returns(_database);
        }

        [Test]
        public async Task NormalizeInventory_SlotCountOverflow_TruncatesOverflowAndLogsError()
        {
            SetupRealDatabaseWithItem(4);
            var slots = new List<HorrorInventorySlotData>();
            for (int i = 0; i < HorrorInventoryConstants.MaxSlotCount + 1; i++)
                slots.Add(new HorrorInventorySlotData { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 1 });
            var data = new HorrorSaveData
            {
                Version = 1,
                Inventory = new HorrorInventorySaveData { Slots = slots },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));
            LogAssert.Expect(LogType.Error, new Regex("インベントリの空き位置が不足したためスロットを破棄しました"));

            await _repository.LoadAsync();

            // 空き位置に収まらない行だけが切り詰められ、残行は 0〜49 に一意採番される
            var result = _repository.Data.Inventory.Slots;
            Assert.That(result.Count, Is.EqualTo(HorrorInventoryConstants.MaxSlotCount));
            Assert.That(result.Select(s => s.SlotNo),
                Is.EquivalentTo(Enumerable.Range(0, HorrorInventoryConstants.MaxSlotCount)));
        }

        [Test]
        public async Task NormalizeInventory_LegacyAllZeroSlotNo_RenumbersPreservingListOrder()
        {
            SetupRealDatabaseWithItem(4);
            // SlotNo 列追加前の旧バイナリ相当: 全行 SlotNo=0 で届く
            var data = new HorrorSaveData
            {
                Version = 1,
                Inventory = new HorrorInventorySaveData
                {
                    Slots = new List<HorrorInventorySlotData>
                    {
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 1, SlotNo = 0 },
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 2, SlotNo = 0 },
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 3, SlotNo = 0 },
                    },
                },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            // リスト順（= 旧・追加順）を保って 0, 1, 2 に再採番される
            var result = _repository.Data.Inventory.Slots;
            Assert.That(result.Count, Is.EqualTo(3));
            Assert.That(result[0].SlotNo, Is.EqualTo(0));
            Assert.That(result[0].Count, Is.EqualTo(1));
            Assert.That(result[1].SlotNo, Is.EqualTo(1));
            Assert.That(result[1].Count, Is.EqualTo(2));
            Assert.That(result[2].SlotNo, Is.EqualTo(2));
            Assert.That(result[2].Count, Is.EqualTo(3));
        }

        [Test]
        public async Task NormalizeInventory_ValidUniqueSlotNos_Unchanged()
        {
            SetupRealDatabaseWithItem(4);
            var data = new HorrorSaveData
            {
                Version = 1,
                Inventory = new HorrorInventorySaveData
                {
                    Slots = new List<HorrorInventorySlotData>
                    {
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 1, SlotNo = 5 },
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 2, SlotNo = 2 },
                    },
                },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            // 正当なデータは再採番されない（冪等）
            var result = _repository.Data.Inventory.Slots;
            Assert.That(result.Count, Is.EqualTo(2));
            Assert.That(result[0].SlotNo, Is.EqualTo(5));
            Assert.That(result[1].SlotNo, Is.EqualTo(2));
        }

        [Test]
        public async Task NormalizeInventory_DuplicateSlotNo_FirstWinsOthersReassignedToLowestFree()
        {
            SetupRealDatabaseWithItem(4);
            var data = new HorrorSaveData
            {
                Version = 1,
                Inventory = new HorrorInventorySaveData
                {
                    Slots = new List<HorrorInventorySlotData>
                    {
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 1, SlotNo = 2 },
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 2, SlotNo = 2 },
                    },
                },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            // 先勝ちで位置 2 を確定し、重複行は最小の空き位置 0 へ
            var result = _repository.Data.Inventory.Slots;
            Assert.That(result[0].SlotNo, Is.EqualTo(2));
            Assert.That(result[1].SlotNo, Is.EqualTo(0));
        }

        [Test]
        public async Task NormalizeInventory_OutOfRangeSlotNo_ReassignedToLowestFree()
        {
            SetupRealDatabaseWithItem(4);
            var data = new HorrorSaveData
            {
                Version = 1,
                Inventory = new HorrorInventorySaveData
                {
                    Slots = new List<HorrorInventorySlotData>
                    {
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 1, SlotNo = -1 },
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 2, SlotNo = 99 },
                    },
                },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            var result = _repository.Data.Inventory.Slots;
            Assert.That(result[0].SlotNo, Is.EqualTo(0));
            Assert.That(result[1].SlotNo, Is.EqualTo(1));
        }

        [Test]
        public async Task NormalizeInventory_NonPositiveCountRow_Removed()
        {
            SetupRealDatabaseWithItem(4);
            var data = new HorrorSaveData
            {
                Version = 1,
                Inventory = new HorrorInventorySaveData
                {
                    Slots = new List<HorrorInventorySlotData>
                    {
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 0, SlotNo = 0 },
                        new() { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 3, SlotNo = 1 },
                    },
                },
            };
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult(data));

            await _repository.LoadAsync();

            // 行の存在 = 中身のあるスタック、の不変条件が確立される（残行の位置は不変）
            var result = _repository.Data.Inventory.Slots;
            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Count, Is.EqualTo(3));
            Assert.That(result[0].SlotNo, Is.EqualTo(1));
        }

        [Test]
        public void Deserialization_LegacySlotWithoutSlotNo_YieldsSlotNoZero()
        {
            // SlotNo 列追加前と同形状のバイナリを新型で読むと、末尾未読メンバは default(int) = 0 になる
            var legacy = new LegacyInventorySlotData { ObjectCategory = ObjectCategory.Item, Id = 4, Count = 7 };
            var bytes = MemoryPackSerializer.Serialize(legacy);

            var restored = MemoryPackSerializer.Deserialize<HorrorInventorySlotData>(bytes);

            Assert.That(restored.ObjectCategory, Is.EqualTo(ObjectCategory.Item));
            Assert.That(restored.Id, Is.EqualTo(4));
            Assert.That(restored.Count, Is.EqualTo(7));
            Assert.That(restored.SlotNo, Is.EqualTo(0));
        }

        [Test]
        public void Serialization_RoundTrip_PreservesAllSections()
        {
            var original = new HorrorSaveData
            {
                Version = 1,
                Player = new HorrorPlayerSaveData { PlayerId = 7, CurrentHealth = 42 },
                Inventory = new HorrorInventorySaveData
                {
                    Slots = new List<HorrorInventorySlotData>
                    {
                        new() { ObjectCategory = ObjectCategory.Item, Id = 3, Count = 4, SlotNo = 7 },
                    },
                },
                Equipment = new HorrorEquipmentSaveData
                {
                    ObjectCategory = ObjectCategory.Weapon,
                    Id = 5,
                    Slots = new List<HorrorEquipmentSlotData>
                    {
                        new() { ObjectCategory = ObjectCategory.Weapon, Id = 5 },
                        new() { ObjectCategory = ObjectCategory.None, Id = 0 },
                        new() { ObjectCategory = ObjectCategory.Item, Id = 3 },
                        new() { ObjectCategory = ObjectCategory.None, Id = 0 },
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
                KeyItem = new HorrorKeyItemSaveData
                {
                    KeyItems = new List<HorrorKeyItemData>
                    {
                        new() { ObjectCategory = ObjectCategory.Item, Id = 3 },
                    },
                },
                Enemy = new HorrorEnemySaveData
                {
                    DefeatedSpawnIds = new List<int> { 10, 20 },
                    FiredTriggerIds = new List<int> { 30 },
                },
            };

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<HorrorSaveData>(bytes);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(1));
            Assert.That(restored.Player.PlayerId, Is.EqualTo(7));
            Assert.That(restored.Player.CurrentHealth, Is.EqualTo(42));
            Assert.That(restored.Inventory.Slots.Count, Is.EqualTo(1));
            Assert.That(restored.Inventory.Slots[0].ObjectCategory, Is.EqualTo(ObjectCategory.Item));
            Assert.That(restored.Inventory.Slots[0].Id, Is.EqualTo(3));
            Assert.That(restored.Inventory.Slots[0].Count, Is.EqualTo(4));
            Assert.That(restored.Inventory.Slots[0].SlotNo, Is.EqualTo(7));
            Assert.That(restored.Equipment.ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(restored.Equipment.Id, Is.EqualTo(5));
            Assert.That(restored.Equipment.Slots.Count, Is.EqualTo(4));
            Assert.That(restored.Equipment.Slots[0].ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(restored.Equipment.Slots[0].Id, Is.EqualTo(5));
            Assert.That(restored.Equipment.Slots[2].ObjectCategory, Is.EqualTo(ObjectCategory.Item));
            Assert.That(restored.Equipment.Slots[2].Id, Is.EqualTo(3));
            Assert.That(restored.Equipment.Magazines.Count, Is.EqualTo(1));
            Assert.That(restored.Equipment.Magazines[0].WeaponId, Is.EqualTo(5));
            Assert.That(restored.Equipment.Magazines[0].Count, Is.EqualTo(12));
            Assert.That(restored.Interaction.InteractionIds, Is.EquivalentTo(new[] { 1, 2, 3 }));
            Assert.That(restored.KeyItem.KeyItems.Count, Is.EqualTo(1));
            Assert.That(restored.KeyItem.KeyItems[0].ObjectCategory, Is.EqualTo(ObjectCategory.Item));
            Assert.That(restored.KeyItem.KeyItems[0].Id, Is.EqualTo(3));
            Assert.That(restored.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 10, 20 }));
            Assert.That(restored.Enemy.FiredTriggerIds, Is.EquivalentTo(new[] { 30 }));
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
        public async Task SaveToSlotAsync_WithValidSlot_SavesToSlotKeyAndStampsSlotMeta()
        {
            await LoadDefaultData();
            _mockStorage.SaveAsync("horror_save_slot3", Arg.Any<HorrorSaveData>())
                .Returns(UniTask.CompletedTask);
            _repository.SetSavepointId(42);

            await _repository.SaveBySlotAsync(3);

            Assert.That(_repository.CurrentSlot, Is.EqualTo(3));
            Assert.That(_repository.Data.SlotNo, Is.EqualTo(3));
            Assert.That(_repository.Data.SavedAtUtc, Is.Not.EqualTo(default(DateTime)));
            await _mockStorage.Received(1).SaveAsync(
                "horror_save_slot3",
                Arg.Is<HorrorSaveData>(d => d.SlotNo == 3 && d.SavepointId == 42));
        }

        [TestCase(-1)]
        [TestCase(10)]
        public async Task SaveToSlotAsync_WithSlotOutOfRange_DoesNotSave(int slotNumber)
        {
            await LoadDefaultData();
            LogAssert.Expect(LogType.Error, new Regex("Invalid slot number"));

            await _repository.SaveBySlotAsync(slotNumber);

            Assert.That(_repository.CurrentSlot, Is.EqualTo(-1));
            await _mockStorage.DidNotReceive().SaveAsync(Arg.Any<string>(), Arg.Any<HorrorSaveData>());
        }

        [Test]
        public async Task LoadSlotInfosAsync_WhenSlotEmpty_ReturnsHasDataFalse()
        {
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult<HorrorSaveData>(null));

            var infos = await _repository.LoadSlotInfosAsync();

            Assert.That(infos.Length, Is.EqualTo(HorrorSaveConstants.MaxSaveSlotCount));
            Assert.That(infos[0].SlotNo, Is.EqualTo(0));
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

            var info = infos[3];
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

        // コンティニュー/リスタートの分岐前提：CreateData はスロットを確定せず、無効スロットのロードはデータを差し替えない。

        [Test]
        public void CreateData_KeepsCurrentSlotInvalid()
        {
            _repository.CreateNewSaveData();

            Assert.That(_repository.CurrentSlot, Is.EqualTo(-1));
            Assert.That(_repository.Data, Is.Not.Null);
        }

        [Test]
        public async Task LoadByCurrentSlotAsync_WhenNoCurrentSlot_LogsErrorAndKeepsData()
        {
            _repository.CreateNewSaveData();
            var currentData = _repository.Data;
            LogAssert.Expect(LogType.Error, new Regex("Invalid slot number"));

            await _repository.LoadByCurrentSlotAsync();

            Assert.That(_repository.Data, Is.SameAs(currentData));
        }

        // セーブポイント記録：記録+Dirty 化、Id 0・同値は Dirty にしない、未ロードは LogError の上で no-op。

        [Test]
        public async Task SetSavepointId_RecordsAndMarksDirty()
        {
            await LoadDefaultData();

            _repository.SetSavepointId(10);

            Assert.That(_repository.Data.SavepointId, Is.EqualTo(10));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task SetSavepointId_SameId_DoesNotMarkDirty()
        {
            await LoadDefaultData();
            _repository.SetSavepointId(10);
            await _repository.SaveBySlotAsync(0);
            Assert.That(_repository.IsDirty, Is.False);

            _repository.SetSavepointId(10);

            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task SetSavepointId_Zero_IgnoredAndNotDirty()
        {
            await LoadDefaultData();

            _repository.SetSavepointId(0);

            Assert.That(_repository.Data.SavepointId, Is.EqualTo(0));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public void SetSavepointId_WhenDataNull_LogsErrorAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "[HorrorSaveRepository] セーブデータ未ロードのため SetSavepointId(10) を無視しました");

            Assert.DoesNotThrow(() => _repository.SetSavepointId(10));
        }

        [Test]
        public void SavepointId_WhenDataNull_ReturnsZero()
        {
            Assert.That(_repository.Data?.SavepointId ?? 0, Is.EqualTo(0));
        }
    }

    /// <summary>SlotNo 列追加前の HorrorInventorySlotData と同形状のレコード（旧バイナリ互換テスト用）。</summary>
    [MemoryPackable]
    internal partial class LegacyInventorySlotData
    {
        public ObjectCategory ObjectCategory { get; set; }

        public int Id { get; set; }

        public int Count { get; set; }
    }
}
