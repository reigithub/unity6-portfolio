using System.Collections.Generic;
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
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorEquipmentServiceTests
    {
        private const string SaveKey = "horror_save";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorInventoryService _mockInventory;
        private IHorrorSaveRepository _repository;
        private IHorrorEquipmentService _service;
        private HorrorWeaponMasterTable _weaponTable;
        private ScriptableDatabase _database;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _mockInventory = Substitute.For<IHorrorInventoryService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
            _service = new HorrorEquipmentService(_repository, _mockInventory, _mockDatabase);
        }

        [TearDown]
        public void TearDown()
        {
            if (_weaponTable != null) Object.DestroyImmediate(_weaponTable);
            if (_database != null) Object.DestroyImmediate(_database);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未装備＋4空スロット）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        private int CountOf(ObjectCategory type, int id)
        {
            int count = 0;
            foreach (var s in _repository.Data.Equipment.Slots)
                if (s.ObjectCategory == type && s.Id == id)
                    count++;
            return count;
        }

        [Test]
        public async Task TryEquip_WeaponPossessed_SucceedsAndMarksDirty()
        {
            await LoadDefaultData();
            _mockInventory.HasObject(ObjectCategory.Weapon, 5).Returns(true);

            var ok = _service.TryEquip(ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.True);
            Assert.That(type, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(id, Is.EqualTo(5));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task TryEquip_Item_ReturnsFalse()
        {
            await LoadDefaultData();
            _mockInventory.HasObject(ObjectCategory.Item, 3).Returns(true);

            var ok = _service.TryEquip(ObjectCategory.Item, 3);

            Assert.That(ok, Is.False);
            Assert.That(_service.TryGetEquipped(out _, out _), Is.False);
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task TryEquip_NotPossessed_ReturnsFalse()
        {
            await LoadDefaultData();
            _mockInventory.HasObject(ObjectCategory.Weapon, 5).Returns(false);

            var ok = _service.TryEquip(ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.False);
            Assert.That(_service.TryGetEquipped(out _, out _), Is.False);
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task TryEquip_SameWeaponAgain_ReturnsTrueIdempotently()
        {
            await LoadDefaultData();
            _mockInventory.HasObject(ObjectCategory.Weapon, 5).Returns(true);
            _service.TryEquip(ObjectCategory.Weapon, 5);

            var ok = _service.TryEquip(ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.True);
            Assert.That(type, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(id, Is.EqualTo(5));
        }

        [Test]
        public async Task TryGetEquipped_Unequipped_ReturnsFalse()
        {
            await LoadDefaultData();

            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.False);
            Assert.That(type, Is.EqualTo(ObjectCategory.None));
            Assert.That(id, Is.EqualTo(0));
        }

        [Test]
        public void Mutators_WhenDataNull_DoNotThrowAndReturnFalse()
        {
            // LoadAsync 未実行 ＝ Data は null
            Assert.DoesNotThrow(() =>
            {
                Assert.That(_service.TryEquip(ObjectCategory.Weapon, 1), Is.False);
                Assert.That(_service.TryGetEquipped(out _, out _), Is.False);
            });
        }

        [Test]
        public async Task Set_RegistersAndMarksDirty()
        {
            await LoadDefaultData();

            var ok = _service.TrySetSlot(1, ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetSlot(1, out var slot), Is.True);
            Assert.That(slot.ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(slot.Id, Is.EqualTo(5));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task Clear_RemovesRegistrationAndMarksDirty()
        {
            await LoadDefaultData();
            _service.TrySetSlot(2, ObjectCategory.Item, 3);

            var ok = _service.ClearSlot(2);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetSlot(2, out _), Is.False);
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task Set_OutOfRange_ReturnsFalse()
        {
            await LoadDefaultData();

            Assert.That(_service.TrySetSlot(-1, ObjectCategory.Item, 1), Is.False);
            Assert.That(_service.TrySetSlot(HorrorEquipmentConstants.MaxEquipmentSlotCount, ObjectCategory.Item, 1), Is.False);
        }

        [Test]
        public async Task Assign_RegisteredElsewhere_ToEmptySlot_Moves()
        {
            await LoadDefaultData();
            _service.TrySetSlot(0, ObjectCategory.Weapon, 5);

            var ok = _service.TryAssignSlot(1, ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetSlot(1, out var dest), Is.True);
            Assert.That(dest.ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(dest.Id, Is.EqualTo(5));
            Assert.That(_service.TryGetSlot(0, out _), Is.False, "旧スロットは空になる（移動）");
        }

        [Test]
        public async Task Assign_RegisteredElsewhere_ToOccupiedSlot_Swaps()
        {
            await LoadDefaultData();
            _service.TrySetSlot(0, ObjectCategory.Weapon, 5);
            _service.TrySetSlot(1, ObjectCategory.Item, 3);

            var ok = _service.TryAssignSlot(1, ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetSlot(1, out var dest), Is.True);
            Assert.That(dest.ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(dest.Id, Is.EqualTo(5));
            Assert.That(_service.TryGetSlot(0, out var src), Is.True, "元 dest の item が旧スロットへ入替");
            Assert.That(src.ObjectCategory, Is.EqualTo(ObjectCategory.Item));
            Assert.That(src.Id, Is.EqualTo(3));
        }

        [Test]
        public async Task Assign_Unregistered_ToOccupiedSlot_Overwrites()
        {
            await LoadDefaultData();
            _service.TrySetSlot(1, ObjectCategory.Item, 3);

            var ok = _service.TryAssignSlot(1, ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.True);
            Assert.That(_service.TryGetSlot(1, out var dest), Is.True);
            Assert.That(dest.ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(dest.Id, Is.EqualTo(5));
            Assert.That(CountOf(ObjectCategory.Item, 3), Is.EqualTo(0), "元の item は上書きで消える");
        }

        [Test]
        public async Task Assign_ToSameSlot_ReturnsFalseAndNoChange()
        {
            await LoadDefaultData();
            _service.TrySetSlot(2, ObjectCategory.Weapon, 5);

            var ok = _service.TryAssignSlot(2, ObjectCategory.Weapon, 5);

            Assert.That(ok, Is.False);
            Assert.That(_service.TryGetSlot(2, out var slot), Is.True);
            Assert.That(slot.ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(slot.Id, Is.EqualTo(5));
        }

        [Test]
        public async Task Assign_KeepsSingleRegistration()
        {
            await LoadDefaultData();
            _service.TrySetSlot(0, ObjectCategory.Weapon, 5);

            _service.TryAssignSlot(2, ObjectCategory.Weapon, 5);

            Assert.That(CountOf(ObjectCategory.Weapon, 5), Is.EqualTo(1), "同一装備は常に1スロットのみ");
            Assert.That(_service.TryGetSlot(2, out _), Is.True);
            Assert.That(_service.TryGetSlot(0, out _), Is.False);
        }

        [Test]
        public void SlotMutators_WhenDataNull_DoNotThrowAndReturnFalse()
        {
            // LoadAsync 未実行 ＝ Data は null
            Assert.DoesNotThrow(() =>
            {
                Assert.That(_service.TrySetSlot(0, ObjectCategory.Item, 1), Is.False);
                Assert.That(_service.ClearSlot(0), Is.False);
                Assert.That(_service.TryGetSlot(0, out _), Is.False);
            });
        }

        [Test]
        public async Task TryEquip_DoesNotAffectSlots()
        {
            await LoadDefaultData();
            _service.TrySetSlot(0, ObjectCategory.Weapon, 5);
            _mockInventory.HasObject(ObjectCategory.Weapon, 7).Returns(true);

            _service.TryEquip(ObjectCategory.Weapon, 7);

            Assert.That(_service.TryGetSlot(0, out var slot), Is.True);
            Assert.That(slot.ObjectCategory, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(slot.Id, Is.EqualTo(5));
        }

        [Test]
        public async Task Assign_DoesNotAffectEquipped()
        {
            await LoadDefaultData();
            _mockInventory.HasObject(ObjectCategory.Weapon, 5).Returns(true);
            _service.TryEquip(ObjectCategory.Weapon, 5);

            _service.TryAssignSlot(0, ObjectCategory.Item, 3);

            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.True);
            Assert.That(type, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(id, Is.EqualTo(5));
        }

        [Test]
        public async Task Clear_DoesNotAffectEquipped()
        {
            await LoadDefaultData();
            _mockInventory.HasObject(ObjectCategory.Weapon, 5).Returns(true);
            _service.TrySetSlot(0, ObjectCategory.Weapon, 5);
            _service.TryEquip(ObjectCategory.Weapon, 5);

            _service.ClearSlot(0);

            Assert.That(_service.TryGetSlot(0, out _), Is.False);
            Assert.That(_service.TryGetEquipped(out var type, out var id), Is.True);
            Assert.That(type, Is.EqualTo(ObjectCategory.Weapon));
            Assert.That(id, Is.EqualTo(5));
        }

        [Test]
        public async Task GetMagazineCount_Unrecorded_ReturnsMagazineSizeAsFull()
        {
            await LoadDefaultData();

            var count = _service.GetMagazineCount(5, 30);

            Assert.That(count, Is.EqualTo(30));
        }

        [Test]
        public async Task SetMagazineCount_ThenGetMagazineCount_RoundTrips()
        {
            await LoadDefaultData();

            _service.SetMagazineCount(5, 12);

            Assert.That(_service.GetMagazineCount(5, 30), Is.EqualTo(12));
        }

        [Test]
        public async Task SetMagazineCount_NegativeValue_ClampsToZero()
        {
            await LoadDefaultData();

            _service.SetMagazineCount(5, -3);

            Assert.That(_service.GetMagazineCount(5, 30), Is.EqualTo(0));
        }

        [Test]
        public async Task GetMagazineCount_RecordedValueExceedsMagazineSize_ClampsToSize()
        {
            await LoadDefaultData();
            _service.SetMagazineCount(5, 999);

            var count = _service.GetMagazineCount(5, 30);

            Assert.That(count, Is.EqualTo(30));
        }

        [Test]
        public async Task SetMagazineCount_MarksDirty()
        {
            await LoadDefaultData();

            _service.SetMagazineCount(5, 12);

            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public void MagazineMutators_WhenDataNull_DoNotThrow()
        {
            // LoadAsync 未実行 ＝ Data は null
            Assert.DoesNotThrow(() =>
            {
                _service.SetMagazineCount(5, 12);
                Assert.That(_service.GetMagazineCount(5, 30), Is.EqualTo(30));
            });
        }

        // 装備中武器のマスター解決：未ロード・未装備は null、解決失敗は LogError の上 null（Id 単位で1回のみ）、
        // 解決成功は装備切替に追従する。

        /// <summary>
        /// 指定 Id の武器レコードを持つ実テーブル＋DB を組み立てて mock に接続する。
        /// EditorImportRows は列名をメンバ名と完全一致でマッピングし、無い列は既定値のままとなるため Id 列のみ投入する。
        /// </summary>
        private void SetupRealDatabase(params int[] weaponIds)
        {
            _weaponTable = ScriptableObject.CreateInstance<HorrorWeaponMasterTable>();
            var rows = new List<IReadOnlyList<string>>();
            foreach (var id in weaponIds)
                rows.Add(new[] { id.ToString() });
            _weaponTable.EditorImportRows(new[] { "Id" }, rows, mergeByPrimaryKey: false);

            _database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(_database);
            so.FindProperty("horrorWeaponMasterTable").objectReferenceValue = _weaponTable;
            so.ApplyModifiedPropertiesWithoutUndo();
            _mockDatabase.Database.Returns(_database);
        }

        [Test]
        public void EquippedWeaponMaster_WhenDataNull_ReturnsNull()
        {
            Assert.That(_service.EquippedWeaponMaster, Is.Null);
        }

        [Test]
        public async Task EquippedWeaponMaster_Unequipped_ReturnsNull()
        {
            await LoadDefaultData();

            Assert.That(_service.EquippedWeaponMaster, Is.Null);
        }

        [Test]
        public async Task EquippedWeaponMaster_MasterNotFound_LogsErrorOnceAndReturnsNull()
        {
            await LoadDefaultData();
            _mockInventory.HasObject(ObjectCategory.Weapon, 5).Returns(true);
            _service.TryEquip(ObjectCategory.Weapon, 5);

            LogAssert.Expect(LogType.Error, "装備中の武器マスターが見つかりません Id=5");

            Assert.That(_service.EquippedWeaponMaster, Is.Null);
            Assert.That(_service.EquippedWeaponMaster, Is.Null); // 2回目の読みは LogError を再発しない（再発すれば未期待ログでテストが落ちる）
        }

        [Test]
        public async Task EquippedWeaponMaster_Resolved_FollowsEquipChange()
        {
            await LoadDefaultData();
            SetupRealDatabase(5, 7);
            _mockInventory.HasObject(ObjectCategory.Weapon, 5).Returns(true);
            _mockInventory.HasObject(ObjectCategory.Weapon, 7).Returns(true);

            _service.TryEquip(ObjectCategory.Weapon, 5);
            Assert.That(_service.EquippedWeaponMaster, Is.Not.Null);
            Assert.That(_service.EquippedWeaponMaster.Id, Is.EqualTo(5));

            _service.TryEquip(ObjectCategory.Weapon, 7);
            Assert.That(_service.EquippedWeaponMaster, Is.Not.Null);
            Assert.That(_service.EquippedWeaponMaster.Id, Is.EqualTo(7));
        }
    }
}
