using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorPlayerServiceTests
    {
        private const string SaveKey = "horror_save";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorSaveRepository _repository;
        private IHorrorPlayerService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
            _service = new HorrorPlayerService(_repository);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未記録）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        [Test]
        public async Task SetLastSavepoint_RecordsAndMarksDirty()
        {
            await LoadDefaultData();

            _service.SetLastSavepoint(10);

            Assert.That(_service.LastSavepointId, Is.EqualTo(10));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task SetLastSavepoint_SameId_DoesNotMarkDirty()
        {
            await LoadDefaultData();
            _service.SetLastSavepoint(10);
            await _repository.SaveAsync();
            Assert.That(_repository.IsDirty, Is.False);

            _service.SetLastSavepoint(10);

            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task SetLastSavepoint_Zero_IgnoredAndNotDirty()
        {
            await LoadDefaultData();

            _service.SetLastSavepoint(0);

            Assert.That(_service.LastSavepointId, Is.EqualTo(0));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public void SetLastSavepoint_WhenDataNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _service.SetLastSavepoint(10));
        }

        [Test]
        public void LastSavepointId_WhenDataNull_ReturnsZero()
        {
            Assert.That(_service.LastSavepointId, Is.EqualTo(0));
        }
    }
}
