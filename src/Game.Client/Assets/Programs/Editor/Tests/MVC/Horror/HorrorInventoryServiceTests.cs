using System.Linq;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.Constants;
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
        public async Task TryConsume_AcrossStacks_ConsumesFromHeadAndRemovesEmptied()
        {
            await LoadDefaultData();
            AddSlotDirect(ObjectCategory.Item, 3, 10);
            AddSlotDirect(ObjectCategory.Item, 3, 5);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 12);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(1));
            Assert.That(GetSlotAt(0), Is.Null);              // 若い位置から消費され使い切りで除去
            Assert.That(GetSlotAt(1).Count, Is.EqualTo(3));  // 残る行の位置は動かない
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
        public async Task TryConsume_ConsumesInSlotNoOrder()
        {
            await LoadDefaultData();
            // List 順は SlotNo 降順に登録し、消費が List 順でなく SlotNo 昇順で行われることを検証する
            AddSlotDirect(ObjectCategory.Item, 3, 5, 2);
            AddSlotDirect(ObjectCategory.Item, 3, 10, 0);

            var ok = _service.TryConsume(ObjectCategory.Item, 3, 12);

            Assert.That(ok, Is.True);
            Assert.That(_service.Slots.Count, Is.EqualTo(1));
            Assert.That(GetSlotAt(0), Is.Null);              // 若い位置から消費され使い切りで除去
            Assert.That(GetSlotAt(2).Count, Is.EqualTo(3));  // 位置は動かない
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
    }
}
