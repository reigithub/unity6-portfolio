using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorEnemyServiceTests
    {
        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorSaveRepository _repository;
        private IHorrorEnemyService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
            _service = new HorrorEnemyService(_repository);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未記録）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        // 撃破記録：記録+Dirty 化、重複は冪等、未ロード・不正 Id は LogError の上で no-op。

        [Test]
        public async Task MarkDefeated_RecordsAndMarksDirty()
        {
            await LoadDefaultData();

            _service.MarkDefeated(1);

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task MarkDefeated_Twice_RecordsOnce_AndSecondCallDoesNotMarkDirty()
        {
            await LoadDefaultData();
            _service.MarkDefeated(1);
            await _repository.SaveBySlotAsync(0);
            Assert.That(_repository.IsDirty, Is.False);

            _service.MarkDefeated(1);

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public void MarkDefeated_WhenDataNull_LogsErrorAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "[HorrorEnemyService] セーブデータ未ロードのため MarkDefeated(1) を無視しました");

            Assert.DoesNotThrow(() => _service.MarkDefeated(1));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task MarkDefeated_InvalidId_LogsErrorAndDoesNotRecord(int spawnId)
        {
            await LoadDefaultData();
            LogAssert.Expect(LogType.Error, $"[HorrorEnemyService] 無効なスポーン Id のため MarkDefeated({spawnId}) を無視しました");

            _service.MarkDefeated(spawnId);

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.Empty);
            Assert.That(_repository.IsDirty, Is.False);
        }

        // 撃破判定：記録済みのみ true。未ロードは無音で false（敵を出す方向へフェイルオープン）。

        [Test]
        public async Task IsDefeated_ReturnsTrueForRecorded_FalseForUnrecorded()
        {
            await LoadDefaultData();
            _service.MarkDefeated(2);

            Assert.That(_service.IsDefeated(2), Is.True);
            Assert.That(_service.IsDefeated(3), Is.False);
        }

        [Test]
        public void IsDefeated_WhenDataNull_ReturnsFalseWithoutLog()
        {
            Assert.That(_service.IsDefeated(1), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
