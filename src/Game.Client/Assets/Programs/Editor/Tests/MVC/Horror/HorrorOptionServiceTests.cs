using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorOptionServiceTests
    {
        private const string SaveKey = "horror_option";

        private ISaveDataStorage _mockStorage;
        private IHorrorOptionSaveRepository _repository;
        private IHorrorOptionService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _repository = new HorrorOptionSaveRepository(_mockStorage);
            _service = new HorrorOptionService(_repository);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData が走る
            _mockStorage.LoadAsync<HorrorOptionSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorOptionSaveData>(null));
            await _repository.LoadAsync();
        }

        #region Setters

        [Test]
        public async Task SetCameraFov_SetsValueAndMarksDirty()
        {
            await LoadDefaultData();

            _service.SetCameraFov(90f);

            Assert.That(_repository.Data.CameraFov, Is.EqualTo(90f));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task SetResolution_SetsWidthAndHeight()
        {
            await LoadDefaultData();

            _service.SetResolution(1920, 1080);

            Assert.That(_repository.Data.ResolutionWidth, Is.EqualTo(1920));
            Assert.That(_repository.Data.ResolutionHeight, Is.EqualTo(1080));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task SetDisplayMode_StoresEnum()
        {
            await LoadDefaultData();

            _service.SetDisplayMode(FullScreenMode.Windowed);

            Assert.That(_repository.Data.DisplayMode, Is.EqualTo(FullScreenMode.Windowed));
        }

        #endregion

        #region Volume Clamp

        [Test]
        public async Task SetMasterVolume_ClampsToMax()
        {
            await LoadDefaultData();

            // 音量範囲は 1〜10。上限超えは 10 にクランプされる
            _service.SetMasterVolume(15f);

            Assert.That(_repository.Data.MasterVolume, Is.EqualTo(10f));
        }

        [Test]
        public async Task SetMasterVolume_ClampsToMin()
        {
            await LoadDefaultData();

            // 音量範囲は 1〜10。下限割れは 1 にクランプされる
            _service.SetMasterVolume(-1f);

            Assert.That(_repository.Data.MasterVolume, Is.EqualTo(1f));
        }

        #endregion

        #region Input Binding Overrides

        [Test]
        public async Task SetInputBindingOverrides_SetsValueAndMarksDirty()
        {
            await LoadDefaultData();

            _service.SetInputBindingOverrides("{\"bindings\":[]}");

            Assert.That(_repository.Data.InputBindingOverridesJson, Is.EqualTo("{\"bindings\":[]}"));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task SetInputBindingOverrides_WhenNull_StoresEmptyString()
        {
            await LoadDefaultData();

            _service.SetInputBindingOverrides(null);

            Assert.That(_repository.Data.InputBindingOverridesJson, Is.EqualTo(""));
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
    }
}
