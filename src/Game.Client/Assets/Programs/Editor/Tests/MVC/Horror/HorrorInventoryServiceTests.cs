using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.Constants;
using Game.Horror.Inventory;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorInventoryServiceTests
    {
        private const string SaveKey = "horror_save";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorSaveRepository _repository;
        private IHorrorInventoryService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
            _service = new HorrorInventoryService(_repository);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（空インベントリ）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        [Test]
        public async Task GetCount_NotPossessed_ReturnsZero()
        {
            await LoadDefaultData();

            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(0));
        }

        [Test]
        public async Task GetCount_AfterTryAdd_ReturnsAddedCount()
        {
            await LoadDefaultData();
            _service.TryAdd(ObjectCategory.Item, 3, 4, 10);

            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(4));
        }

        [Test]
        public async Task TryConsume_FullAmount_RemovesSlotAndReturnsTrue()
        {
            await LoadDefaultData();
            _service.TryAdd(ObjectCategory.Item, 3, 4, 10);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 4);

            Assert.That(ok, Is.True);
            Assert.That(_service.HasObject(ObjectCategory.Item, 3), Is.False);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(0));
        }

        [Test]
        public async Task TryConsume_PartialAmount_ReturnsTrueAndLeavesRemainder()
        {
            await LoadDefaultData();
            _service.TryAdd(ObjectCategory.Item, 3, 4, 10);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 1);

            Assert.That(ok, Is.True);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(3));
        }

        [Test]
        public async Task TryConsume_InsufficientCount_ReturnsFalseAndLeavesUnchanged()
        {
            await LoadDefaultData();
            _service.TryAdd(ObjectCategory.Item, 3, 2, 10);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 5);

            Assert.That(ok, Is.False);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(2));
        }

        [Test]
        public async Task TryConsume_ZeroOrNegativeCount_ReturnsFalse()
        {
            await LoadDefaultData();
            _service.TryAdd(ObjectCategory.Item, 3, 2, 10);

            Assert.That(_service.TryConsume(ObjectCategory.Item, 3, 0), Is.False);
            Assert.That(_service.TryConsume(ObjectCategory.Item, 3, -1), Is.False);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(2));
        }

        [Test]
        public async Task TryConsume_Success_MarksDirty()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 4);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 1);

            Assert.That(ok, Is.True);
            Assert.That(_repository.IsDirty, Is.True);
        }

        /// <summary>Dirty を汚さずスロットを直接登録する（TryAdd 経由だと Dirty になるため）。slotNo 省略時は連番（現在の行数）。</summary>
        private void AddSlotDirect(ObjectCategory category, int id, int count, int? slotNo = null)
            => _repository.Data.Inventory.Slots.Add(new HorrorInventorySlotData
            {
                ObjectCategory = category,
                Id = id,
                Count = count,
                SlotNo = slotNo ?? _repository.Data.Inventory.Slots.Count
            });

        /// <summary>指定位置（SlotNo）の行を取得する。空位置は null。</summary>
        private HorrorInventorySlotData GetSlotAt(int slotNo)
            => _service.Slots.FirstOrDefault(s => s.SlotNo == slotNo);

        /// <summary>本命アイテムと衝突しないダミーアイテムで指定数のスロットを占有する。</summary>
        private void FillSlotsWithDummies(int count)
        {
            for (int i = 0; i < count; i++)
                AddSlotDirect(ObjectCategory.Item, 1000 + i, 1);
        }

        [Test]
        public async Task TryAdd_ExceedsMaxCount_SplitsIntoMultipleSlots()
        {
            await LoadDefaultData();

            var ok = _service.TryAdd(ObjectCategory.Item, 3, 25, 10);

            Assert.That(ok, Is.True);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(25));
            Assert.That(_service.Slots.Count, Is.EqualTo(3));
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(10));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(10));
            Assert.That(GetSlotAt(2).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryAdd_PartialStack_FillsHeadBeforeAppending()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 8);

            var ok = _service.TryAdd(ObjectCategory.Item, 3, 4, 10);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(2));
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(10));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(2));
        }

        [Test]
        public async Task TryAdd_FullStack_AppendsNewSlot()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10);

            // 旧実装では満タンスタックへの追加は false だったが、分割仕様では新規スロットへ入る
            var ok = _service.TryAdd(ObjectCategory.Item, 3, 3, 10);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(2));
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(10));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(3));
        }

        [Test]
        public async Task TryAdd_InsufficientCapacity_ReturnsFalseAndUnchanged()
        {
            await LoadDefaultData();
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1);
            AddSlotDirect(ObjectCategory.Item, 3, 8); // 全スロット占有、受入可能量は既存スタックの空き 2 のみ

            var ok = _service.TryAdd(ObjectCategory.Item, 3, 4, 10);

            Assert.That(ok, Is.False);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(8));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task TryAdd_ExactCapacity_Succeeds()
        {
            await LoadDefaultData();
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1);
            AddSlotDirect(ObjectCategory.Item, 3, 8);

            var ok = _service.TryAdd(ObjectCategory.Item, 3, 2, 10);

            Assert.That(ok, Is.True);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(10));
            Assert.That(_service.Slots.Count, Is.EqualTo(HorrorInventoryConstants.MaxSlotCount));
        }

        [Test]
        public async Task TryAdd_SlotShortage_NeedsTwoSlots_ReturnsFalse()
        {
            await LoadDefaultData();
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1); // 残り 1 スロット

            var ok = _service.TryAdd(ObjectCategory.Item, 3, 15, 10); // 2 スロット必要

            Assert.That(ok, Is.False);
            Assert.That(_service.HasObject(ObjectCategory.Item, 3), Is.False);
        }

        [Test]
        public async Task TryAdd_LastSlot_FitsInOneSlot_Succeeds()
        {
            await LoadDefaultData();
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1); // 残り 1 スロット

            var ok = _service.TryAdd(ObjectCategory.Item, 3, 10, 10);

            Assert.That(ok, Is.True);
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(10));
        }

        [Test]
        public async Task TryAdd_ZeroOrNegativeMaxCount_ReturnsFalse()
        {
            await LoadDefaultData();

            Assert.That(_service.TryAdd(ObjectCategory.Item, 3, 1, 0), Is.False);
            Assert.That(_service.TryAdd(ObjectCategory.Item, 3, 1, -1), Is.False);
            Assert.That(_service.HasObject(ObjectCategory.Item, 3), Is.False);
        }

        [Test]
        public async Task GetCount_MultipleStacks_ReturnsTotal()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10);
            AddSlotDirect(ObjectCategory.Item, 3, 5);

            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(15));
        }

        [Test]
        public async Task HasObject_MultipleStacks_ReturnsTrue()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10);
            AddSlotDirect(ObjectCategory.Item, 3, 5);

            Assert.That(_service.HasObject(ObjectCategory.Item, 3), Is.True);
        }

        [Test]
        public async Task TryConsume_AcrossStacks_ConsumesSmallestFirstAndRemovesEmptied()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10);
            AddSlotDirect(ObjectCategory.Item, 3, 5);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 12);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(1));
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(3));  // 大きい山は跨ぎ分だけ減り、位置は動かない
            Assert.That(GetSlotAt(1), Is.Null);              // 端数の山から消費され使い切りで除去
        }

        [Test]
        public async Task TryConsume_AcrossStacks_InsufficientTotal_ReturnsFalseUnchanged()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10);
            AddSlotDirect(ObjectCategory.Item, 3, 5);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 16);

            Assert.That(ok, Is.False);
            Assert.That(_service.Slots.Count, Is.EqualTo(2));
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(10));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task DiscardSlot_RemovesOnlyTargetStack()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10);
            AddSlotDirect(ObjectCategory.Item, 3, 5);
            AddSlotDirect(ObjectCategory.Item, 4, 3);

            var ok = _service.DiscardSlot(0);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(2));
            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(5));
            Assert.That(_service.GetCount(ObjectCategory.Item, 4), Is.EqualTo(3));
            Assert.That(GetSlotAt(0), Is.Null);              // 破棄位置は空く
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(5));  // 他行の位置は動かない
            Assert.That(GetSlotAt(2).Id, Is.EqualTo(4));
        }

        [Test]
        public async Task DiscardSlot_OutOfRangeOrEmptyPosition_ReturnsFalse()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 1); // SlotNo 0

            Assert.That(_service.DiscardSlot(-1), Is.False, "範囲外（負）");
            Assert.That(_service.DiscardSlot(HorrorInventoryConstants.MaxSlotCount), Is.False, "範囲外（上限）");
            Assert.That(_service.DiscardSlot(1), Is.False, "空位置");
            Assert.That(_service.Slots.Count, Is.EqualTo(1));
        }

        [Test]
        public async Task DiscardSlot_Success_MarksDirty()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 1);

            var ok = _service.DiscardSlot(0);

            Assert.That(ok, Is.True);
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task DiscardSlot_MiddleSlot_KeepsOtherSlotNos()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10, 0);
            AddSlotDirect(ObjectCategory.Item, 4, 5, 1);
            AddSlotDirect(ObjectCategory.Item, 5, 2, 2);

            var ok = _service.DiscardSlot(1);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(0).Id, Is.EqualTo(3));    // 前詰めされない
            Assert.That(GetSlotAt(1), Is.Null);
            Assert.That(GetSlotAt(2).Id, Is.EqualTo(5));
            Assert.That(_service.DiscardSlot(1), Is.False); // 空いた位置の再破棄は false
        }

        [Test]
        public async Task TryAdd_AfterDiscard_ReusesLowestFreeSlotNo()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 1, 0);
            AddSlotDirect(ObjectCategory.Item, 4, 1, 1);
            AddSlotDirect(ObjectCategory.Item, 5, 1, 2);
            _service.DiscardSlot(1);

            var ok = _service.TryAdd(ObjectCategory.Item, 6, 1, 10);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(1).Id, Is.EqualTo(6)); // 破棄跡（最小の空き位置）に入る
        }

        [Test]
        public async Task TryAdd_MultipleNewStacks_FillAscendingGaps()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 1, 0);
            AddSlotDirect(ObjectCategory.Item, 4, 1, 2);
            AddSlotDirect(ObjectCategory.Item, 5, 1, 4);

            var ok = _service.TryAdd(ObjectCategory.Item, 6, 15, 10);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(1).Id, Is.EqualTo(6));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(10));
            Assert.That(GetSlotAt(3).Id, Is.EqualTo(6));
            Assert.That(GetSlotAt(3).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryAdd_FillsExistingStacksInSlotNoOrder()
        {
            await LoadDefaultData();
            // List 順は SlotNo 降順に登録し、充填が List 順でなく SlotNo 昇順で行われることを検証する
            AddSlotDirect(ObjectCategory.Item, 3, 5, 3);
            AddSlotDirect(ObjectCategory.Item, 3, 5, 1);

            var ok = _service.TryAdd(ObjectCategory.Item, 3, 5, 10);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(10)); // 若い位置が先に満ちる
            Assert.That(GetSlotAt(3).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryConsume_ConsumesFromSmallestCountFirst()
        {
            await LoadDefaultData();
            // 端数の山（SlotNo 2）が大きい山（SlotNo 0）より画面の遅い位置にある配置で、
            // 消費が位置順でなく Count 昇順で行われることを検証する
            AddSlotDirect(ObjectCategory.Item, 3, 5, 2);
            AddSlotDirect(ObjectCategory.Item, 3, 10, 0);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 12);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(1));
            Assert.That(GetSlotAt(2), Is.Null);              // 端数の山から消費され使い切りで除去
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(3));  // 大きい山は跨ぎ分だけ減り、位置は動かない
        }

        [Test]
        public async Task TryConsume_EmptiedRow_IsRemoved_OthersKeepSlotNo()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 3, 0);
            AddSlotDirect(ObjectCategory.Item, 4, 2, 1);
            AddSlotDirect(ObjectCategory.Item, 3, 4, 2);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 3);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(0), Is.Null);
            Assert.That(GetSlotAt(1).Id, Is.EqualTo(4));
            Assert.That(GetSlotAt(2).Count, Is.EqualTo(4));
        }

        [Test]
        public async Task TryConsume_EqualCounts_TieBreaksBySlotNoAscending()
        {
            await LoadDefaultData();
            // 同数の山は画面の若い位置（SlotNo 昇順）から消費される
            AddSlotDirect(ObjectCategory.Item, 3, 5, 3);
            AddSlotDirect(ObjectCategory.Item, 3, 5, 1);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 5);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(1), Is.Null);
            Assert.That(GetSlotAt(3).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryConsume_FullStackPreserved_WhilePartialSuffices()
        {
            await LoadDefaultData();
            // 端数で足りる消費では満杯の山（Count = maxCount）に手を付けない
            AddSlotDirect(ObjectCategory.Item, 3, 10, 0);
            AddSlotDirect(ObjectCategory.Item, 3, 2, 1);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 1);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(10));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(1));
        }

        [Test]
        public async Task TryConsume_SpansToNextSmallestAfterExhaustingSmallest()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 2, 0);
            AddSlotDirect(ObjectCategory.Item, 3, 5, 1);
            AddSlotDirect(ObjectCategory.Item, 3, 10, 2);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 4);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(0), Is.Null);              // 最小の山を使い切り
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(3));  // 次に少ない山へ跨ぐ
            Assert.That(GetSlotAt(2).Count, Is.EqualTo(10)); // 最大の山は不変
        }

        [Test]
        public async Task TryConsumeAt_ConsumesOnlyTargetSlot()
        {
            await LoadDefaultData();
            // 端数の山（SlotNo 1）があっても、指定した大きい山（SlotNo 0）だけが減る
            AddSlotDirect(ObjectCategory.Item, 3, 10, 0);
            AddSlotDirect(ObjectCategory.Item, 3, 5, 1);

            var ok = _service.TryConsumeAt(ObjectCategory.Item, 3, 0, 4);

            Assert.That(ok, Is.True);
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(6));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryConsumeAt_FullAmount_RemovesRowOthersKeepSlotNo()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 3, 0);
            AddSlotDirect(ObjectCategory.Item, 3, 5, 1);

            var ok = _service.TryConsumeAt(ObjectCategory.Item, 3, 0, 3);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(1));
            Assert.That(GetSlotAt(0), Is.Null);
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryConsumeAt_EmptyPosition_ReturnsFalse()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 5, 0);

            var ok = _service.TryConsumeAt(ObjectCategory.Item, 3, 1, 1);

            Assert.That(ok, Is.False);
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryConsumeAt_DifferentItem_ReturnsFalseUnchanged()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 5, 0);

            var ok = _service.TryConsumeAt(ObjectCategory.Item, 4, 0, 1);

            Assert.That(ok, Is.False);
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryConsumeAt_InsufficientCount_ReturnsFalseWithoutSpill()
        {
            await LoadDefaultData();
            // 指定した山の不足を他の同種スロットで補わない（部分消費もしない）
            AddSlotDirect(ObjectCategory.Item, 3, 2, 0);
            AddSlotDirect(ObjectCategory.Item, 3, 10, 1);

            var ok = _service.TryConsumeAt(ObjectCategory.Item, 3, 0, 3);

            Assert.That(ok, Is.False);
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(2));
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(10));
        }

        [Test]
        public async Task TryConsumeAt_OutOfRangeOrNonPositiveCount_ReturnsFalse()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 5, 0);

            Assert.That(_service.TryConsumeAt(ObjectCategory.Item, 3, -1, 1), Is.False, "範囲外（下限）");
            Assert.That(_service.TryConsumeAt(ObjectCategory.Item, 3, HorrorInventoryConstants.MaxSlotCount, 1), Is.False, "範囲外（上限）");
            Assert.That(_service.TryConsumeAt(ObjectCategory.Item, 3, 0, 0), Is.False, "count = 0");
            Assert.That(_service.TryConsumeAt(ObjectCategory.Item, 3, 0, -1), Is.False, "count < 0");
            Assert.That(GetSlotAt(0).Count, Is.EqualTo(5));
        }

        [Test]
        public async Task TryConsumeAt_Success_MarksDirty()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 5, 0);

            var ok = _service.TryConsumeAt(ObjectCategory.Item, 3, 0, 1);

            Assert.That(ok, Is.True);
            Assert.That(_repository.IsDirty, Is.True);
        }

        private static HorrorObjectAmount[] Amounts(params (int id, int count)[] items)
            => items.Select(x => new HorrorObjectAmount
            {
                Category = ObjectCategory.Item,
                Id = x.id,
                Count = x.count
            }).ToArray();

        [Test]
        public async Task CanAddAfterConsume_MaterialsAndFreeSlot_ReturnsTrue()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 2, 0);

            var ok = _service.CanAddAfterConsume(Amounts((3, 2)), ObjectCategory.Item, 4, 1, 1);

            Assert.That(ok, Is.True);
        }

        [Test]
        public async Task CanAddAfterConsume_InsufficientMaterials_ReturnsFalse()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 1, 0);

            var ok = _service.CanAddAfterConsume(Amounts((3, 2)), ObjectCategory.Item, 4, 1, 1);

            Assert.That(ok, Is.False);
        }

        [Test]
        public async Task CanAddAfterConsume_ConsumptionFreesTheOnlySlot_ReturnsTrue()
        {
            await LoadDefaultData();
            // 全スロット占有。素材の山を消費して空く 1 枠へ成果物が入る
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1);
            AddSlotDirect(ObjectCategory.Item, 3, 2);

            var ok = _service.CanAddAfterConsume(Amounts((3, 2)), ObjectCategory.Item, 4, 1, 1);

            Assert.That(ok, Is.True);
        }

        [Test]
        public async Task CanAddAfterConsume_ConsumptionLeavesRemainder_ReturnsFalseWhenSlotsAreFull()
        {
            await LoadDefaultData();
            // 素材の山が消費後も残る（残数 1）ため空き枠が生まれず、成果物を置けない
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 1);
            AddSlotDirect(ObjectCategory.Item, 3, 3);

            var ok = _service.CanAddAfterConsume(Amounts((3, 2)), ObjectCategory.Item, 4, 1, 1);

            Assert.That(ok, Is.False);
        }

        [Test]
        public async Task CanAddAfterConsume_MultipleMaterials_JudgesEachKind()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 2, 0);
            AddSlotDirect(ObjectCategory.Item, 5, 1, 1);

            Assert.That(
                _service.CanAddAfterConsume(Amounts((3, 2), (5, 1)), ObjectCategory.Item, 4, 1, 1),
                Is.True, "両方の素材が足りる");
            Assert.That(
                _service.CanAddAfterConsume(Amounts((3, 2), (5, 2)), ObjectCategory.Item, 4, 1, 1),
                Is.False, "片方の素材が足りない");
        }

        [Test]
        public async Task CanAddAfterConsume_ConsumesSmallestStackFirst_FreesSlotForResult()
        {
            await LoadDefaultData();
            // 端数の山（1 個）から先に消費されるため 1 枠空く。大きい山から消費する順序では空かない
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 2);
            AddSlotDirect(ObjectCategory.Item, 3, 5);
            AddSlotDirect(ObjectCategory.Item, 3, 1);

            var ok = _service.CanAddAfterConsume(Amounts((3, 3)), ObjectCategory.Item, 4, 1, 1);

            Assert.That(ok, Is.True);
        }

        [Test]
        public async Task CanAddAfterConsume_StacksWithResult_UsesRemainingCapacity()
        {
            await LoadDefaultData();
            // 全スロット占有だが成果物と同種のスタックに空きがあるため積める
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount - 2);
            AddSlotDirect(ObjectCategory.Item, 3, 2);
            AddSlotDirect(ObjectCategory.Item, 4, 8);

            var ok = _service.CanAddAfterConsume(Amounts((3, 1)), ObjectCategory.Item, 4, 2, 10);

            Assert.That(ok, Is.True);
        }

        [Test]
        public async Task CanAddAfterConsume_DoesNotMutateInventory()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 2, 0);

            _service.CanAddAfterConsume(Amounts((3, 2)), ObjectCategory.Item, 4, 1, 1);

            Assert.That(_service.GetCount(ObjectCategory.Item, 3), Is.EqualTo(2));
            Assert.That(_service.Slots.Count, Is.EqualTo(1));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task CanAddAfterConsume_NonPositiveAmounts_ReturnsFalse()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 2, 0);

            Assert.That(
                _service.CanAddAfterConsume(Amounts((3, 2)), ObjectCategory.Item, 4, 0, 1),
                Is.False, "追加数 0");
            Assert.That(
                _service.CanAddAfterConsume(Amounts((3, 2)), ObjectCategory.Item, 4, 1, 0),
                Is.False, "スタック上限 0");
            Assert.That(
                _service.CanAddAfterConsume(Amounts((3, 0)), ObjectCategory.Item, 4, 1, 1),
                Is.False, "消費数 0");
        }

        [Test]
        public async Task CanAddAfterConsume_NoConsumption_JudgesCapacityOnly()
        {
            await LoadDefaultData();
            FillSlotsWithDummies(HorrorInventoryConstants.MaxSlotCount);

            Assert.That(
                _service.CanAddAfterConsume(null, ObjectCategory.Item, 4, 1, 1),
                Is.False, "空きがない");

            _service.DiscardSlot(0);

            Assert.That(
                _service.CanAddAfterConsume(null, ObjectCategory.Item, 4, 1, 1),
                Is.True, "空きが生まれた");
        }
    }
}
