using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Horror.Signals;
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
        private IMessagePipeService _messagePipe;
        private HorrorEnemyService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);

            var messagePipe = new MessagePipeService();
            messagePipe.AddMessageBroker<HorrorSignals.Enemy.Died>();
            messagePipe.Build();
            _messagePipe = messagePipe;

            _service = new HorrorEnemyService(_repository, _messagePipe);
            _service.Startup(); // GameServiceManager.Register が呼ぶ Startup 相当（購読開始）
        }

        [TearDown]
        public void TearDown()
        {
            _service.Shutdown();
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未記録）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        // 撃破記録（Enemy.Died Publish 起点）：記録+Dirty 化、重複は冪等、未ロード・不正 Id は LogError の上で no-op。

        [Test]
        public async Task Died_RecordsAndMarksDirty()
        {
            await LoadDefaultData();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task Died_Twice_RecordsOnce_AndSecondPublishDoesNotMarkDirty()
        {
            await LoadDefaultData();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1));
            await _repository.SaveBySlotAsync(0);
            Assert.That(_repository.IsDirty, Is.False);

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public void Died_WhenDataNull_LogsErrorAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "[HorrorEnemyService] セーブデータ未ロードのため MarkDefeated(1) を無視しました");

            Assert.DoesNotThrow(() => _messagePipe.Publish(new HorrorSignals.Enemy.Died(1)));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task Died_InvalidId_LogsErrorAndDoesNotRecord(int spawnId)
        {
            await LoadDefaultData();
            LogAssert.Expect(LogType.Error, $"[HorrorEnemyService] 無効なスポーン Id のため MarkDefeated({spawnId}) を無視しました");

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(spawnId));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.Empty);
            Assert.That(_repository.IsDirty, Is.False);
        }

        // 購読ライフサイクル：Shutdown 後は Publish しても記録されない（購読解除の回帰固定）。

        [Test]
        public async Task Shutdown_StopsRecording()
        {
            await LoadDefaultData();
            _service.Shutdown();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.Empty);
            Assert.That(_repository.IsDirty, Is.False);
        }

        // 撃破判定：記録済みのみ true。未ロードは無音で false（敵を出す方向へフェイルオープン）。

        [Test]
        public async Task IsDefeated_ReturnsTrueForRecorded_FalseForUnrecorded()
        {
            await LoadDefaultData();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(2));

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
