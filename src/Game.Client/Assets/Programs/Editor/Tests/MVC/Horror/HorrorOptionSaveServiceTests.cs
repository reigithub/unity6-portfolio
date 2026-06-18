using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Shared.Enums;
using Game.Shared.SaveData;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;
using R3;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorOptionSaveServiceTests
    {
        private const string SaveKey = "horror_option_settings";

        private ISaveDataStorage _mockStorage;
        private HorrorOptionSaveService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _service = new HorrorOptionSaveService(_mockStorage);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData が走る
            _mockStorage.LoadAsync<HorrorOptionSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorOptionSaveData>(null));
            await _service.LoadAsync();
        }

        #region Default Data

        [Test]
        public async Task Load_WhenNoFile_CreatesDefaultData()
        {
            await LoadDefaultData();

            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.Data.Version, Is.EqualTo(1));
            Assert.That(_service.Data.LanguageCode, Is.EqualTo("ja"));
            Assert.That(_service.Data.InputBindingOverridesJson, Is.EqualTo(""));
            Assert.That(_service.Data.CameraFov, Is.EqualTo(60f));
            Assert.That(_service.Data.DisplayMode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(_service.Data.MasterVolume, Is.EqualTo(1f));
            Assert.That(_service.IsDirty, Is.False);
        }

        #endregion

        #region Setters

        [Test]
        public async Task SetCameraFov_SetsValueAndMarksDirty()
        {
            await LoadDefaultData();

            _service.SetCameraFov(90f);

            Assert.That(_service.Data.CameraFov, Is.EqualTo(90f));
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public async Task SetResolution_SetsWidthAndHeight()
        {
            await LoadDefaultData();

            _service.SetResolution(1920, 1080);

            Assert.That(_service.Data.ResolutionWidth, Is.EqualTo(1920));
            Assert.That(_service.Data.ResolutionHeight, Is.EqualTo(1080));
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public async Task SetDisplayMode_StoresEnum()
        {
            await LoadDefaultData();

            _service.SetDisplayMode(FullScreenMode.Windowed);

            Assert.That(_service.Data.DisplayMode, Is.EqualTo(FullScreenMode.Windowed));
        }

        #endregion

        #region Volume Clamp

        [Test]
        public async Task SetMasterVolume_ClampsToMax()
        {
            await LoadDefaultData();

            _service.SetMasterVolume(5f);

            Assert.That(_service.Data.MasterVolume, Is.EqualTo(1f));
        }

        [Test]
        public async Task SetMasterVolume_ClampsToMin()
        {
            await LoadDefaultData();

            _service.SetMasterVolume(-1f);

            Assert.That(_service.Data.MasterVolume, Is.EqualTo(0f));
        }

        #endregion

        #region Input Binding Overrides

        [Test]
        public async Task SetInputBindingOverrides_SetsValueAndMarksDirty()
        {
            await LoadDefaultData();

            _service.SetInputBindingOverrides("{\"bindings\":[]}");

            Assert.That(_service.Data.InputBindingOverridesJson, Is.EqualTo("{\"bindings\":[]}"));
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public async Task SetInputBindingOverrides_WhenNull_StoresEmptyString()
        {
            await LoadDefaultData();

            _service.SetInputBindingOverrides(null);

            Assert.That(_service.Data.InputBindingOverridesJson, Is.EqualTo(""));
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

            await _service.LoadAsync();

            Assert.That(_service.Data.Version, Is.EqualTo(1), "現行バージョンへマイグレーションされる");
            Assert.That(_service.Data.InputBindingOverridesJson, Is.EqualTo(""), "新フィールドは既定値で補完される");
        }

        #endregion

        #region Null Guard

        [Test]
        public void Setters_WhenDataNull_DoNotThrow()
        {
            // LoadAsync 未実行 ＝ Data は null
            Assert.DoesNotThrow(() =>
            {
                _service.SetCameraFov(90f);
                _service.SetResolution(1920, 1080);
                _service.SetMasterVolume(0.5f);
                _service.SetVSync(true);
                _service.SetInputBindingOverrides("{}");
            });
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

        #region OnSaved

        [Test]
        public async Task SaveAsync_FiresOnSaved_WithSavedData()
        {
            await LoadDefaultData();
            _service.SetCameraControlHorizontal(true);   // 変更（dirty）

            HorrorOptionSaveData received = null;
            using var sub = _service.OnSaved.Subscribe(d => received = d);

            await _service.SaveAsync();

            Assert.That(received, Is.SameAs(_service.Data), "保存されたデータが通知される");
            Assert.That(received.CameraControlHorizontal, Is.True);
        }

        #endregion
    }
}
