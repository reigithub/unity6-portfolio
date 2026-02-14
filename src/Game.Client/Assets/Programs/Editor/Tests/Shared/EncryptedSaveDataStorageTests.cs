using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.SaveData;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Shared
{
    [TestFixture]
    public class EncryptedSaveDataStorageTests
    {
        private string _tempDir;
        private SaveDataStorage _innerStorage;
        private EncryptedSaveDataStorage _storage;
        private byte[] _encryptionKey;
        private byte[] _hmacKey;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Application.temporaryCachePath, "EncryptedSaveDataStorageTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _innerStorage = new SaveDataStorage();

            // テスト用の固定鍵を生成
            _encryptionKey = new byte[32];
            _hmacKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(_encryptionKey);
                rng.GetBytes(_hmacKey);
            }

            _storage = new EncryptedSaveDataStorage(_innerStorage, _encryptionKey, _hmacKey);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }

        /// <summary>
        /// テスト用のキーを生成（一時ディレクトリ配下にファイルが作られるようフルパスを指定）
        /// </summary>
        private string GetTestKey(string name = "test")
        {
            return Path.Combine(_tempDir, name + ".bin");
        }

        #region ラウンドトリップテスト

        [Test]
        public async Task SaveAndLoad_RoundTrip_DataMatches()
        {
            // Arrange
            var key = GetTestKey("roundtrip");
            var original = new AudioSaveData
            {
                Version = 1,
                MasterVolume = 5,
                BgmVolume = 3,
                VoiceVolume = 8,
                SeVolume = 6
            };

            // Act
            await _storage.SaveAsync(key, original);
            var loaded = await _storage.LoadAsync<AudioSaveData>(key);

            // Assert
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Version, Is.EqualTo(original.Version));
            Assert.That(loaded.MasterVolume, Is.EqualTo(original.MasterVolume));
            Assert.That(loaded.BgmVolume, Is.EqualTo(original.BgmVolume));
            Assert.That(loaded.VoiceVolume, Is.EqualTo(original.VoiceVolume));
            Assert.That(loaded.SeVolume, Is.EqualTo(original.SeVolume));
        }

        #endregion

        #region 存在しないキーテスト

        [Test]
        public async Task LoadAsync_NonExistentKey_ReturnsDefault()
        {
            // Arrange
            var key = GetTestKey("nonexistent");
            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act
            var result = await _storage.LoadAsync(key, defaultValue);

            // Assert
            Assert.That(result, Is.SameAs(defaultValue));
            Assert.That(result.MasterVolume, Is.EqualTo(99));
        }

        [Test]
        public async Task LoadAsync_NonExistentKey_WithoutDefault_ReturnsNull()
        {
            // Arrange
            var key = GetTestKey("nonexistent2");

            // Act
            var result = await _storage.LoadAsync<AudioSaveData>(key);

            // Assert
            Assert.That(result, Is.Null);
        }

        #endregion

        #region 改竄検知テスト（データ部分）

        [Test]
        public async Task LoadAsync_TamperedEncryptedData_ReturnsDefault()
        {
            // Arrange
            var key = GetTestKey("tampered_data");
            var data = new AudioSaveData { MasterVolume = 5, BgmVolume = 3 };
            await _storage.SaveAsync(key, data);

            // ファイルを読み込んで暗号化データ部分を改竄
            var path = _storage.GetFullPath(key);
            var fileBytes = await File.ReadAllBytesAsync(path);
            // ヘッダー(53B)の後のデータ部分を1バイト変更
            if (fileBytes.Length > 53)
            {
                fileBytes[53] ^= 0xFF;
            }
            await File.WriteAllBytesAsync(path, fileBytes);

            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act
            var result = await _storage.LoadAsync(key, defaultValue);

            // Assert
            Assert.That(result, Is.SameAs(defaultValue));
        }

        #endregion

        #region 改竄検知テスト（HMAC部分）

        [Test]
        public async Task LoadAsync_TamperedHmac_ReturnsDefault()
        {
            // Arrange
            var key = GetTestKey("tampered_hmac");
            var data = new AudioSaveData { MasterVolume = 5 };
            await _storage.SaveAsync(key, data);

            // ファイルを読み込んでHMAC部分を改竄
            var path = _storage.GetFullPath(key);
            var fileBytes = await File.ReadAllBytesAsync(path);
            // HMAC: offset 21 (4 + 1 + 16 = 21) から32バイト
            fileBytes[21] ^= 0xFF;
            await File.WriteAllBytesAsync(path, fileBytes);

            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act
            var result = await _storage.LoadAsync(key, defaultValue);

            // Assert
            Assert.That(result, Is.SameAs(defaultValue));
        }

        #endregion

        #region 後方互換性テスト

        [Test]
        public async Task LoadAsync_LegacyUnencryptedData_LoadsAndMigrates()
        {
            // Arrange: レガシー形式（平文MemoryPack）のファイルを直接書き込み
            var key = GetTestKey("legacy");
            var path = _storage.GetFullPath(key);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var legacyData = new AudioSaveData
            {
                Version = 1,
                MasterVolume = 4,
                BgmVolume = 6,
                VoiceVolume = 9,
                SeVolume = 2
            };
            var legacyBytes = MemoryPackSerializer.Serialize(legacyData);
            await File.WriteAllBytesAsync(path, legacyBytes);

            // Act: 暗号化ストレージで読み込み
            var loaded = await _storage.LoadAsync<AudioSaveData>(key);

            // Assert: データが正しく読み込まれる
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.MasterVolume, Is.EqualTo(4));
            Assert.That(loaded.BgmVolume, Is.EqualTo(6));
            Assert.That(loaded.VoiceVolume, Is.EqualTo(9));
            Assert.That(loaded.SeVolume, Is.EqualTo(2));

            // 暗号化形式で再保存されていることを確認（先頭4バイトがマジック "ESDS"）
            var migratedBytes = await File.ReadAllBytesAsync(path);
            Assert.That(migratedBytes[0], Is.EqualTo(0x45)); // 'E'
            Assert.That(migratedBytes[1], Is.EqualTo(0x53)); // 'S'
            Assert.That(migratedBytes[2], Is.EqualTo(0x44)); // 'D'
            Assert.That(migratedBytes[3], Is.EqualTo(0x53)); // 'S'
        }

        #endregion

        #region 異なる鍵テスト

        [Test]
        public async Task LoadAsync_DifferentKey_ReturnsDefault()
        {
            // Arrange: 鍵Aで保存
            var key = GetTestKey("different_key");
            var data = new AudioSaveData { MasterVolume = 7 };
            await _storage.SaveAsync(key, data);

            // 鍵Bで新しいストレージを作成
            var differentEncryptionKey = new byte[32];
            var differentHmacKey = new byte[32];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(differentEncryptionKey);
                rng.GetBytes(differentHmacKey);
            }
            var storageB = new EncryptedSaveDataStorage(_innerStorage, differentEncryptionKey, differentHmacKey);

            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act: 鍵Bで読み込み
            var result = await storageB.LoadAsync(key, defaultValue);

            // Assert: HMAC検証失敗でデフォルト値が返る
            Assert.That(result, Is.SameAs(defaultValue));
        }

        #endregion

        #region 委譲テスト

        [Test]
        public async Task DeleteAsync_DelegatesToInner()
        {
            // Arrange
            var mockInner = Substitute.For<ISaveDataStorage>();
            mockInner.DeleteAsync("test_key").Returns(UniTask.CompletedTask);
            var storage = new EncryptedSaveDataStorage(mockInner, _encryptionKey, _hmacKey);

            // Act
            await storage.DeleteAsync("test_key");

            // Assert
            await mockInner.Received(1).DeleteAsync("test_key");
        }

        [Test]
        public void Exists_DelegatesToInner()
        {
            // Arrange
            var mockInner = Substitute.For<ISaveDataStorage>();
            mockInner.Exists("test_key").Returns(true);
            var storage = new EncryptedSaveDataStorage(mockInner, _encryptionKey, _hmacKey);

            // Act
            var result = storage.Exists("test_key");

            // Assert
            Assert.That(result, Is.True);
            mockInner.Received(1).Exists("test_key");
        }

        [Test]
        public void GetFullPath_DelegatesToInner()
        {
            // Arrange
            var mockInner = Substitute.For<ISaveDataStorage>();
            mockInner.GetFullPath("test_key").Returns("/some/path/test_key.bin");
            var storage = new EncryptedSaveDataStorage(mockInner, _encryptionKey, _hmacKey);

            // Act
            var result = storage.GetFullPath("test_key");

            // Assert
            Assert.That(result, Is.EqualTo("/some/path/test_key.bin"));
            mockInner.Received(1).GetFullPath("test_key");
        }

        [Test]
        public void BasePath_DelegatesToInner()
        {
            // Arrange
            var mockInner = Substitute.For<ISaveDataStorage>();
            mockInner.BasePath.Returns("/some/path");
            var storage = new EncryptedSaveDataStorage(mockInner, _encryptionKey, _hmacKey);

            // Act
            var result = storage.BasePath;

            // Assert
            Assert.That(result, Is.EqualTo("/some/path"));
            _ = mockInner.Received(1).BasePath;
        }

        #endregion
    }
}
