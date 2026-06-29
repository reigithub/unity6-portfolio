using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.SaveData;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.Shared
{
    [TestFixture]
    public class AudioSaveServiceTests
    {
        private ISaveDataStorage _mockStorage;
        private IAudioService _mockAudioService;
        private AudioSaveService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockAudioService = Substitute.For<IAudioService>();
            _service = new AudioSaveService(_mockStorage, _mockAudioService);
        }

        #region SetMasterVolume Tests

        [Test]
        public async Task SetMasterVolume_SetsValueAndApplies()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetMasterVolume(5);

            // Assert
            Assert.That(_service.Data.MasterVolume, Is.EqualTo(5));
            _mockAudioService.Received(1).SetVolume(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        }

        [Test]
        public async Task SetMasterVolume_ClampsToMax()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetMasterVolume(15);

            // Assert
            Assert.That(_service.Data.MasterVolume, Is.EqualTo(10));
        }

        [Test]
        public async Task SetMasterVolume_ClampsToMin()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetMasterVolume(-5);

            // Assert
            Assert.That(_service.Data.MasterVolume, Is.EqualTo(0));
        }

        [Test]
        public async Task SetMasterVolume_MarksDirty()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetMasterVolume(5);

            // Assert
            Assert.That(_service.IsDirty, Is.True);
        }

        [Test]
        public void SetMasterVolume_WhenDataNull_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.SetMasterVolume(5));
        }

        #endregion

        #region SetBgmVolume Tests

        [Test]
        public async Task SetBgmVolume_SetsValueAndApplies()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetBgmVolume(8);

            // Assert
            Assert.That(_service.Data.BgmVolume, Is.EqualTo(8));
            _mockAudioService.Received(1).SetVolume(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        }

        [Test]
        public async Task SetBgmVolume_ClampsToMax()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetBgmVolume(100);

            // Assert
            Assert.That(_service.Data.BgmVolume, Is.EqualTo(10));
        }

        [Test]
        public async Task SetBgmVolume_ClampsToMin()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetBgmVolume(-10);

            // Assert
            Assert.That(_service.Data.BgmVolume, Is.EqualTo(0));
        }

        #endregion

        #region SetVoiceVolume Tests

        [Test]
        public async Task SetVoiceVolume_SetsValueAndApplies()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetVoiceVolume(6);

            // Assert
            Assert.That(_service.Data.VoiceVolume, Is.EqualTo(6));
        }

        [Test]
        public async Task SetVoiceVolume_Clamps()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetVoiceVolume(20);

            // Assert
            Assert.That(_service.Data.VoiceVolume, Is.EqualTo(10));
        }

        #endregion

        #region SetSeVolume Tests

        [Test]
        public async Task SetSeVolume_SetsValueAndApplies()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetSeVolume(3);

            // Assert
            Assert.That(_service.Data.SeVolume, Is.EqualTo(3));
        }

        [Test]
        public async Task SetSeVolume_Clamps()
        {
            // Arrange
            await LoadData();

            // Act
            _service.SetSeVolume(-1);

            // Assert
            Assert.That(_service.Data.SeVolume, Is.EqualTo(0));
        }

        #endregion

        #region ApplyToAudioService Tests

        [Test]
        public async Task ApplyToAudioService_CalculatesCorrectVolumes_AllMax()
        {
            // Arrange
            await LoadData(masterVolume: 10, bgmVolume: 10, voiceVolume: 10, seVolume: 10);

            // Act
            _service.ApplyToAudioService();

            // Assert
            _mockAudioService.Received().SetVolume(10f,10f, 10f, 10f);
        }

        [Test]
        public void ApplyToAudioService_WhenDataNull_DoesNotThrow()
        {
            // Act & Assert
            Assert.DoesNotThrow(() => _service.ApplyToAudioService());
            _mockAudioService.DidNotReceive().SetVolume(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        }

        #endregion

        #region OnDataLoaded Tests

        [Test]
        public async Task OnDataLoaded_AppliesVolumes()
        {
            // Arrange - Load data without clearing received calls
            var data = new AudioSaveData
            {
                MasterVolume = 8,
                BgmVolume = 6,
                VoiceVolume = 9,
                SeVolume = 4
            };
            _mockStorage.LoadAsync<AudioSaveData>("audio_settings")
                .Returns(UniTask.FromResult(data));

            // Act
            await _service.LoadAsync();

            // Assert - ApplyToAudioService should be called during load
            _mockAudioService.Received().SetVolume(Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>(), Arg.Any<float>());
        }

        #endregion

        #region Default Data Tests

        [Test]
        public async Task CreateNewData_ReturnsDefaultValues()
        {
            // Arrange
            _mockStorage.LoadAsync<AudioSaveData>("audio_settings")
                .Returns(UniTask.FromResult<AudioSaveData>(null));

            // Act
            await _service.LoadAsync();

            // Assert
            Assert.That(_service.Data, Is.Not.Null);
            Assert.That(_service.Data.MasterVolume, Is.EqualTo(5)); // Default
            Assert.That(_service.Data.BgmVolume, Is.EqualTo(3));    // Default
            Assert.That(_service.Data.VoiceVolume, Is.EqualTo(7)); // Default
            Assert.That(_service.Data.SeVolume, Is.EqualTo(5));     // Default
        }

        #endregion

        #region Helper Methods

        private async Task LoadData(int masterVolume = 7, int bgmVolume = 7, int voiceVolume = 10, int seVolume = 7)
        {
            var data = new AudioSaveData
            {
                MasterVolume = masterVolume,
                BgmVolume = bgmVolume,
                VoiceVolume = voiceVolume,
                SeVolume = seVolume
            };
            _mockStorage.LoadAsync<AudioSaveData>("audio_settings")
                .Returns(UniTask.FromResult(data));
            await _service.LoadAsync();

            // Reset received calls after load
            _mockAudioService.ClearReceivedCalls();
        }

        #endregion
    }
}
