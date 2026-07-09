using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Shared.SaveData;
using Game.Shared.Services;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorRespawnSaveServiceTests
    {
        private const string SaveKey = "horror_respawn";

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private HorrorRespawnSaveService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _service = new HorrorRespawnSaveService(_mockStorage, _mockDatabase);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未記録）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorRespawnSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorRespawnSaveData>(null));
            await _service.LoadAsync();
        }

        [Test]
        public async Task Load_WhenNoFile_CreatesDataWithNoSavepoint()
        {
            await LoadDefaultData();

            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.Data.Version, Is.EqualTo(1));
            Assert.That(_service.LastSavepointId, Is.EqualTo(0));
            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task SetLastSavepoint_RecordsAndMarksDirty()
        {
            await LoadDefaultData();

            _service.SetLastSavepoint(10);

            Assert.That(_service.LastSavepointId, Is.EqualTo(10));
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public async Task SetLastSavepoint_SameId_DoesNotMarkDirty()
        {
            await LoadDefaultData();
            _service.SetLastSavepoint(10);
            await _service.SaveAsync();
            Assert.That(_service.IsDirty, Is.False);

            _service.SetLastSavepoint(10);

            Assert.That(_service.IsDirty, Is.False);
        }

        [Test]
        public async Task SetLastSavepoint_Zero_IgnoredAndNotDirty()
        {
            await LoadDefaultData();

            _service.SetLastSavepoint(0);

            Assert.That(_service.LastSavepointId, Is.EqualTo(0));
            Assert.That(_service.IsDirty, Is.False);
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

        [Test]
        public async Task Load_ExistingDataWithZeroId_DoesNotTouchDatabase()
        {
            // Id=0 は OnDataLoaded の != 0 ガードで DB を参照しない。
            _mockStorage.LoadAsync<HorrorRespawnSaveData>(SaveKey)
                .Returns(UniTask.FromResult(new HorrorRespawnSaveData { LastSavepointId = 0 }));

            await _service.LoadAsync();

            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.LastSavepointId, Is.EqualTo(0));
            _ = _mockDatabase.DidNotReceive().Database;
        }

        [Test]
        public void Serialization_RoundTrip_PreservesLastSavepointId()
        {
            var original = new HorrorRespawnSaveData { Version = 1, LastSavepointId = 42 };

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<HorrorRespawnSaveData>(bytes);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(1));
            Assert.That(restored.LastSavepointId, Is.EqualTo(42));
        }
    }
}
