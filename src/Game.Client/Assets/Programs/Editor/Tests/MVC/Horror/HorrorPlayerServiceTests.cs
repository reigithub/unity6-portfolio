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

        // 残 HP の永続化：記録+Dirty 化、同値は Dirty にしない、未ロードは LogError の上で no-op。
        // 新規データの既定 0 は「未記録」を意味し、復元側（NormalizeLoadedHealth）が Max へ正規化する前提。

        [Test]
        public async Task SetCurrentHealth_RecordsAndMarksDirty()
        {
            await LoadDefaultData();

            _service.SetCurrentHealth(40);

            Assert.That(_service.CurrentHealth, Is.EqualTo(40));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task SetCurrentHealth_SameValue_DoesNotMarkDirty()
        {
            await LoadDefaultData();
            _service.SetCurrentHealth(40);
            await _repository.SaveBySlotAsync(0);
            Assert.That(_repository.IsDirty, Is.False);

            _service.SetCurrentHealth(40);

            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public void SetCurrentHealth_WhenDataNull_LogsErrorAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "セーブデータ未ロードのため SetCurrentHealth(40) を無視しました");

            Assert.DoesNotThrow(() => _service.SetCurrentHealth(40));
        }

        [Test]
        public void CurrentHealth_WhenDataNull_ReturnsZero()
        {
            Assert.That(_service.CurrentHealth, Is.EqualTo(0));
        }

        [Test]
        public async Task CurrentHealth_NewData_IsZero()
        {
            await LoadDefaultData();

            Assert.That(_service.CurrentHealth, Is.EqualTo(0));
        }

        // 最大 HP：マスタ由来のランタイム値。セーブリポジトリ非経由のためロード不要・Dirty 化しない。

        [Test]
        public void MaxHealth_Initial_IsZero()
        {
            Assert.That(_service.MaxHealth, Is.EqualTo(0));
        }

        [Test]
        public void SetMaxHealth_RecordsValue()
        {
            _service.SetMaxHealth(100);

            Assert.That(_service.MaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void SetMaxHealth_DoesNotMarkDirty()
        {
            _service.SetMaxHealth(100);

            Assert.That(_repository.IsDirty, Is.False);
        }

        // 満タン判定：MaxHealth 未設定（0）は満タン扱いにしない（誤って使用不能にならない）。

        [Test]
        public async Task IsHealthFull_AtMax_IsTrue()
        {
            await LoadDefaultData();
            _service.SetMaxHealth(100);
            _service.SetCurrentHealth(100);

            Assert.That(_service.IsHealthFull, Is.True);
        }

        [Test]
        public async Task IsHealthFull_BelowMax_IsFalse()
        {
            await LoadDefaultData();
            _service.SetMaxHealth(100);
            _service.SetCurrentHealth(99);

            Assert.That(_service.IsHealthFull, Is.False);
        }

        [Test]
        public async Task IsHealthFull_OverMax_IsTrue()
        {
            await LoadDefaultData();
            _service.SetMaxHealth(100);
            _service.SetCurrentHealth(150);

            Assert.That(_service.IsHealthFull, Is.True);
        }

        [Test]
        public async Task IsHealthFull_MaxNotSet_IsFalse()
        {
            await LoadDefaultData();
            _service.SetCurrentHealth(40);

            Assert.That(_service.IsHealthFull, Is.False);
        }
    }
}
