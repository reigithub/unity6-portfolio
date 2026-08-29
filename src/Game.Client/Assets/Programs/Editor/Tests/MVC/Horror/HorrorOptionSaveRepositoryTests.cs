using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Shared.SaveData;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorOptionSaveRepositoryTests
    {
        private const string SaveKey = "horror_option";

        private ISaveDataStorage _mockStorage;
        private HorrorOptionSaveRepository _repository;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _repository = new HorrorOptionSaveRepository(_mockStorage);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData が走る
            _mockStorage.LoadAsync<HorrorOptionSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorOptionSaveData>(null));
            await _repository.LoadAsync();
        }

        #region Default Data

        [Test]
        public async Task Load_WhenNoFile_CreatesDefaultData()
        {
            await LoadDefaultData();

            Assert.That(_repository.Data, Is.Not.Null);
            Assert.That(_repository.Data.Version, Is.EqualTo(1));
            Assert.That(_repository.Data.LanguageCode, Is.EqualTo("ja"));
            Assert.That(_repository.Data.InputBindingOverridesJson, Is.EqualTo(""));
            Assert.That(_repository.Data.CameraFov, Is.EqualTo(60f));
            Assert.That(_repository.Data.DisplayMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(_repository.Data.MasterVolume, Is.EqualTo(5f));
            Assert.That(_repository.IsDirty, Is.False);
        }

        #endregion

        #region Migration

        [Test]
        public async Task Load_WhenOldVersion_MigratesToCurrentVersionWithDefaults()
        {
            // Version 1 の旧データ（InputBindingOverridesJson が未設定 ＝ 既定 ""）
            var oldData = new HorrorOptionSaveData { Version = 1 };
            _mockStorage.LoadAsync<HorrorOptionSaveData>(SaveKey)
                .Returns(UniTask.FromResult(oldData));

            await _repository.LoadAsync();

            Assert.That(_repository.Data.Version, Is.EqualTo(1), "現行バージョンへマイグレーションされる");
            Assert.That(_repository.Data.InputBindingOverridesJson, Is.EqualTo(""), "新フィールドは既定値で補完される");
        }

        #endregion

        #region Serialization

        [Test]
        public void Serialization_RoundTrip_PreservesAllProperties()
        {
            var original = new HorrorOptionSaveData
            {
                Version = 1,
                LanguageCode = "en",
                CameraControlHorizontal = true,
                CameraControlVertical = true,
                CameraSensitivityHorizontal = 2.5f,
                CameraSensitivityVertical = 0.5f,
                CameraAcceleration = 15f,
                CameraShake = 0.25f,
                CameraFov = 100f,
                DisplayMode = FullScreenMode.ExclusiveFullScreen,
                ResolutionWidth = 2560,
                ResolutionHeight = 1440,
                FrameRateLimit = 144,
                UncappedFrameRate = true,
                VSync = true,
                MasterVolume = 0.6f,
                BgmVolume = 0.4f,
                VoiceVolume = 0.7f,
                SeVolume = 0.3f,
                InputBindingOverridesJson = "{\"bindings\":[{\"action\":\"Player/Jump\",\"path\":\"<Keyboard>/j\"}]}",
            };

            var bytes = MemoryPackSerializer.Serialize(original);
            var restored = MemoryPackSerializer.Deserialize<HorrorOptionSaveData>(bytes);

            Assert.That(restored, Is.Not.Null);
            Assert.That(restored.Version, Is.EqualTo(original.Version));
            Assert.That(restored.LanguageCode, Is.EqualTo(original.LanguageCode));
            Assert.That(restored.CameraControlHorizontal, Is.EqualTo(original.CameraControlHorizontal));
            Assert.That(restored.CameraControlVertical, Is.EqualTo(original.CameraControlVertical));
            Assert.That(restored.CameraSensitivityHorizontal, Is.EqualTo(original.CameraSensitivityHorizontal));
            Assert.That(restored.CameraSensitivityVertical, Is.EqualTo(original.CameraSensitivityVertical));
            Assert.That(restored.CameraAcceleration, Is.EqualTo(original.CameraAcceleration));
            Assert.That(restored.CameraShake, Is.EqualTo(original.CameraShake));
            Assert.That(restored.CameraFov, Is.EqualTo(original.CameraFov));
            Assert.That(restored.DisplayMode, Is.EqualTo(original.DisplayMode));
            Assert.That(restored.ResolutionWidth, Is.EqualTo(original.ResolutionWidth));
            Assert.That(restored.ResolutionHeight, Is.EqualTo(original.ResolutionHeight));
            Assert.That(restored.FrameRateLimit, Is.EqualTo(original.FrameRateLimit));
            Assert.That(restored.UncappedFrameRate, Is.EqualTo(original.UncappedFrameRate));
            Assert.That(restored.VSync, Is.EqualTo(original.VSync));
            Assert.That(restored.MasterVolume, Is.EqualTo(original.MasterVolume));
            Assert.That(restored.BgmVolume, Is.EqualTo(original.BgmVolume));
            Assert.That(restored.VoiceVolume, Is.EqualTo(original.VoiceVolume));
            Assert.That(restored.SeVolume, Is.EqualTo(original.SeVolume));
            Assert.That(restored.InputBindingOverridesJson, Is.EqualTo(original.InputBindingOverridesJson));
        }

        #endregion

        #region OnDataChanged

        [Test]
        public async Task SaveAsync_FiresOnDataChanged_WithSavedData()
        {
            await LoadDefaultData();
            _repository.Data.CameraControlHorizontal = true;   // 変更（dirty）
            _repository.MarkDirty();

            HorrorOptionSaveData received = null;
            using var sub = _repository.OnDataChanged.Subscribe(d => received = d);

            await _repository.SaveAsync();

            Assert.That(received, Is.SameAs(_repository.Data), "保存されたデータが通知される");
            Assert.That(received.CameraControlHorizontal, Is.True);
        }

        #endregion
    }
}
