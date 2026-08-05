using System.Collections.Generic;
using System.Linq;
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
using Object = UnityEngine.Object;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorCraftServiceTests
    {
        private const string SaveKey = "horror_save";

        // マスターの構成: 素材 6（x2）→ 成果物 5、素材 6+8 → 成果物 7（x10）、素材行の無いグループ 99 を指すレシピ 3
        private const int ResultItemId = 5;
        private const int MaterialItemId = 6;
        private const int StackableResultItemId = 7;
        private const int SecondMaterialItemId = 8;

        private const int SingleResultCraftId = 1;
        private const int MultiMaterialCraftId = 2;
        private const int MissingMaterialGroupCraftId = 3;

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorSaveRepository _repository;
        private IHorrorInventoryService _inventoryService;
        private IHorrorCraftService _service;

        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            SetupDatabase();

            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
            _inventoryService = new HorrorInventoryService(_repository);
            _service = new HorrorCraftService(_mockDatabase, _inventoryService);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var obj in _createdObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }

            _createdObjects.Clear();
        }

        private void SetupDatabase()
        {
            var itemTable = ScriptableObject.CreateInstance<HorrorItemMasterTable>();
            itemTable.EditorImportRows(
                new[] { "Id", "MaxCount" },
                new[]
                {
                    new[] { ResultItemId.ToString(), "1" },
                    new[] { MaterialItemId.ToString(), "10" },
                    new[] { StackableResultItemId.ToString(), "10" },
                    new[] { SecondMaterialItemId.ToString(), "10" },
                },
                mergeByPrimaryKey: false);

            var craftTable = ScriptableObject.CreateInstance<HorrorCraftMasterTable>();
            craftTable.EditorImportRows(
                new[] { "Id", "ResultObjectCategory", "ResultObjectId", "ResultCount", "MaterialGroupId" },
                new[]
                {
                    new[] { SingleResultCraftId.ToString(), "Item", ResultItemId.ToString(), "1", "1" },
                    new[] { MultiMaterialCraftId.ToString(), "Item", StackableResultItemId.ToString(), "10", "2" },
                    new[] { MissingMaterialGroupCraftId.ToString(), "Item", ResultItemId.ToString(), "1", "99" },
                },
                mergeByPrimaryKey: false);

            var materialTable = ScriptableObject.CreateInstance<HorrorCraftMaterialMasterTable>();
            materialTable.EditorImportRows(
                new[] { "Id", "MaterialGroupId", "ObjectCategory", "ObjectId", "Count" },
                new[]
                {
                    new[] { "1", "1", "Item", MaterialItemId.ToString(), "2" },
                    new[] { "2", "2", "Item", MaterialItemId.ToString(), "1" },
                    new[] { "3", "2", "Item", SecondMaterialItemId.ToString(), "1" },
                },
                mergeByPrimaryKey: false);

            var database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(database);
            so.FindProperty("horrorItemMasterTable").objectReferenceValue = itemTable;
            so.FindProperty("horrorCraftMasterTable").objectReferenceValue = craftTable;
            so.FindProperty("horrorCraftMaterialMasterTable").objectReferenceValue = materialTable;
            so.ApplyModifiedPropertiesWithoutUndo();

            _createdObjects.Add(itemTable);
            _createdObjects.Add(craftTable);
            _createdObjects.Add(materialTable);
            _createdObjects.Add(database);

            _mockDatabase.Database.Returns(database);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（空インベントリ）が走る
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        /// <summary>本命アイテムと衝突しないダミーアイテムで指定数のスロットを占有する。</summary>
        private void FillSlotsWithDummies(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _repository.Data.Inventory.Slots.Add(new HorrorInventorySlotData
                {
                    ObjectCategory = ObjectCategory.Item,
                    Id = 1000 + i,
                    Count = 1,
                    SlotNo = _repository.Data.Inventory.Slots.Count
                });
            }
        }

        [Test]
        public async Task Recipes_ReturnsAllRows()
        {
            await LoadDefaultData();

            Assert.That(_service.Recipes.Select(x => x.Id),
                Is.EquivalentTo(new[] { SingleResultCraftId, MultiMaterialCraftId, MissingMaterialGroupCraftId }));
        }

        [Test]
        public async Task GetMaterials_ReturnsRowsOfTheGroup()
        {
            await LoadDefaultData();

            var materials = _service.GetMaterials(MultiMaterialCraftId);

            Assert.That(materials.Select(x => x.ObjectId),
                Is.EquivalentTo(new[] { MaterialItemId, SecondMaterialItemId }));
        }

        [Test]
        public async Task GetMaterials_UnknownRecipe_ReturnsEmpty()
        {
            await LoadDefaultData();

            Assert.That(_service.GetMaterials(999).Count, Is.EqualTo(0));
        }

        [Test]
        public async Task CanCraft_FollowsMaterialPossession()
        {
            await LoadDefaultData();

            Assert.That(_service.CanCraft(SingleResultCraftId), Is.False, "素材なし");

            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 1, 10);
            Assert.That(_service.CanCraft(SingleResultCraftId), Is.False, "素材が 1 個で不足");

            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 1, 10);
            Assert.That(_service.CanCraft(SingleResultCraftId), Is.True, "素材が 2 個で充足");
        }

        [Test]
        public async Task TryCraft_MaterialsSatisfied_ConsumesMaterialsAndAddsResult()
        {
            await LoadDefaultData();
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 2, 10);

            var ok = _service.TryCraft(SingleResultCraftId);

            Assert.That(ok, Is.True);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, MaterialItemId), Is.EqualTo(0));
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, ResultItemId), Is.EqualTo(1));
        }

        [Test]
        public async Task TryCraft_SurplusMaterial_ConsumesOnlyRequiredCount()
        {
            await LoadDefaultData();
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 5, 10);

            var ok = _service.TryCraft(SingleResultCraftId);

            Assert.That(ok, Is.True);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, MaterialItemId), Is.EqualTo(3));
        }

        [Test]
        public async Task TryCraft_InsufficientMaterials_DoesNotChangeInventory()
        {
            await LoadDefaultData();
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 1, 10);

            var ok = _service.TryCraft(SingleResultCraftId);

            Assert.That(ok, Is.False);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, MaterialItemId), Is.EqualTo(1));
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, ResultItemId), Is.EqualTo(0));
        }

        [Test]
        public async Task TryCraft_MultipleMaterials_ConsumesEveryKind()
        {
            await LoadDefaultData();
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 1, 10);
            _inventoryService.TryAdd(ObjectCategory.Item, SecondMaterialItemId, 1, 10);

            var ok = _service.TryCraft(MultiMaterialCraftId);

            Assert.That(ok, Is.True);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, MaterialItemId), Is.EqualTo(0));
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, SecondMaterialItemId), Is.EqualTo(0));
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, StackableResultItemId), Is.EqualTo(10),
                "ResultCount の分だけ付与される");
        }

        [Test]
        public async Task TryCraft_MissingOneOfMaterials_DoesNotConsumeTheOther()
        {
            await LoadDefaultData();
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 1, 10);

            var ok = _service.TryCraft(MultiMaterialCraftId);

            Assert.That(ok, Is.False);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, MaterialItemId), Is.EqualTo(1));
        }

        [Test]
        public async Task TryCraft_NoFreeSlotAfterConsumption_DoesNotConsumeMaterials()
        {
            await LoadDefaultData();
            // 素材の山が消費後も残るため空き位置が生まれず、成果物（MaxCount 1）を置けない
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1);
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 3, 10);

            var ok = _service.TryCraft(SingleResultCraftId);

            Assert.That(ok, Is.False);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, MaterialItemId), Is.EqualTo(3));
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, ResultItemId), Is.EqualTo(0));
        }

        [Test]
        public async Task TryCraft_ConsumptionFreesSlotForResult_Succeeds()
        {
            await LoadDefaultData();
            // 素材をちょうど使い切るため 1 枠空き、そこへ成果物が入る
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1);
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 2, 10);

            var ok = _service.TryCraft(SingleResultCraftId);

            Assert.That(ok, Is.True);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, ResultItemId), Is.EqualTo(1));
        }

        [Test]
        public async Task TryCraft_UnknownRecipe_ReturnsFalse()
        {
            await LoadDefaultData();
            _inventoryService.TryAdd(ObjectCategory.Item, MaterialItemId, 2, 10);

            Assert.That(_service.TryCraft(999), Is.False);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, MaterialItemId), Is.EqualTo(2));
        }

        [Test]
        public async Task TryCraft_RecipeWithoutMaterialRows_ReturnsFalse()
        {
            await LoadDefaultData();

            Assert.That(_service.CanCraft(MissingMaterialGroupCraftId), Is.False);
            Assert.That(_service.TryCraft(MissingMaterialGroupCraftId), Is.False);
            Assert.That(_inventoryService.GetCount(ObjectCategory.Item, ResultItemId), Is.EqualTo(0));
        }
    }
}
