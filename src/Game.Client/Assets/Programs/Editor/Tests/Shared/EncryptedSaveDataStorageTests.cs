using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Shared.SaveData;
using MemoryPack;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            _encryptionKey = GenerateRandomKey();
            _hmacKey = GenerateRandomKey();

            _storage = new EncryptedSaveDataStorage(_innerStorage, new FixedSaveDataKeyProvider(_encryptionKey, _hmacKey));
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

        private static byte[] GenerateRandomKey()
        {
            var key = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(key);
            return key;
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

        [Test]
        public async Task SaveBytesAndLoadBytes_RoundTrip_DataMatches()
        {
            // Arrange
            var key = GetTestKey("byte_roundtrip");
            var plainBytes = new byte[] { 1, 2, 3, 4, 5, 255, 0, 128 };

            // Act
            await _storage.SaveBytesAsync(key, plainBytes);
            var loaded = await _storage.LoadBytesAsync(key);

            // Assert
            Assert.That(loaded, Is.EqualTo(plainBytes));
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
            // ヘッダー(55B)の後のデータ部分を1バイト変更
            if (fileBytes.Length > 55)
            {
                fileBytes[55] ^= 0xFF;
            }
            await File.WriteAllBytesAsync(path, fileBytes);

            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act
            LogAssert.Expect(LogType.Error, new Regex("HMAC verification failed"));
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
            // HMAC: offset 23 (4 + 1 + 1 + 1 + 16 = 23) から32バイト
            fileBytes[23] ^= 0xFF;
            await File.WriteAllBytesAsync(path, fileBytes);

            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act
            LogAssert.Expect(LogType.Error, new Regex("HMAC verification failed"));
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

        [Test]
        public async Task LoadAsync_LegacyMigrationResaveFails_ReturnsLegacyDataAnyway()
        {
            // Arrange: レガシー平文を返すが、再保存(SaveBytesAsync)が失敗するinnerをモック
            var key = "legacy_resave_fail";
            var legacyData = new AudioSaveData { Version = 1, MasterVolume = 4, BgmVolume = 6, VoiceVolume = 9, SeVolume = 2 };
            var legacyBytes = MemoryPackSerializer.Serialize(legacyData);

            var mockInner = Substitute.For<ISaveDataStorage>();
            mockInner.LoadBytesAsync(key).Returns(UniTask.FromResult(legacyBytes));
            mockInner.SaveBytesAsync(key, Arg.Any<byte[]>()).Returns(UniTask.FromException(new IOException("mock save failure")));

            var storage = new EncryptedSaveDataStorage(mockInner, new FixedSaveDataKeyProvider(_encryptionKey, _hmacKey));

            // Act
            LogAssert.Expect(LogType.Error, new Regex("Failed to re-save"));
            var result = await storage.LoadAsync<AudioSaveData>(key, null);

            // Assert: 再保存に失敗してもロード済みのレガシーデータは破棄されない
            Assert.That(result, Is.Not.Null);
            Assert.That(result.MasterVolume, Is.EqualTo(4));
            Assert.That(result.BgmVolume, Is.EqualTo(6));
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
            var differentEncryptionKey = GenerateRandomKey();
            var differentHmacKey = GenerateRandomKey();
            var storageB = new EncryptedSaveDataStorage(_innerStorage, new FixedSaveDataKeyProvider(differentEncryptionKey, differentHmacKey));

            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act: 鍵Bで読み込み
            LogAssert.Expect(LogType.Error, new Regex("HMAC verification failed"));
            var result = await storageB.LoadAsync(key, defaultValue);

            // Assert: HMAC検証失敗でデフォルト値が返る
            Assert.That(result, Is.SameAs(defaultValue));
        }

        #endregion

        #region 異なるKeySourceテスト

        [Test]
        public async Task LoadAsync_DifferentKeySource_ReturnsDefault()
        {
            // Arrange: KeySourceId=0x12のプロバイダーAで保存
            var key = GetTestKey("keysource_mismatch");
            var providerA = new FixedSaveDataKeyProvider(GenerateRandomKey(), GenerateRandomKey(), 0x12);
            var storageA = new EncryptedSaveDataStorage(_innerStorage, providerA);
            await storageA.SaveAsync(key, new AudioSaveData { MasterVolume = 3 });

            // 異なるKeySourceId=0x22のプロバイダーCで読み込み
            var providerC = new FixedSaveDataKeyProvider(GenerateRandomKey(), GenerateRandomKey(), 0x22);
            var storageC = new EncryptedSaveDataStorage(_innerStorage, providerC);

            var defaultValue = new AudioSaveData { MasterVolume = 99 };

            // Act
            LogAssert.Expect(LogType.Error, new Regex("Unknown key source"));
            var result = await storageC.LoadAsync(key, defaultValue);

            // Assert: KeySourceIdが一致せずデフォルト値が返る
            Assert.That(result, Is.SameAs(defaultValue));
        }

        #endregion

        #region Salt世代アップグレードテスト

        [Test]
        public async Task LoadAsync_SaltVersionUpgrade_ResavesWithLatestSaltVersion()
        {
            // Arrange: Salt世代1・2の両方を導出可能な鍵マップを用意し、世代1のproviderで保存
            var key = GetTestKey("salt_upgrade");
            const byte keySourceId = 0x30;
            var keysBySaltVersion = new Dictionary<byte, (byte[] EncryptionKey, byte[] HmacKey)>
            {
                { 1, (GenerateRandomKey(), GenerateRandomKey()) },
                { 2, (GenerateRandomKey(), GenerateRandomKey()) },
            };

            var providerGen1 = new FixedSaveDataKeyProvider(keysBySaltVersion, currentSaltVersion: 1, keySourceId);
            var storageGen1 = new EncryptedSaveDataStorage(_innerStorage, providerGen1);
            await storageGen1.SaveAsync(key, new AudioSaveData { MasterVolume = 6 });

            // Act: 同じ鍵マップでCurrentSaltVersion=2のproviderを使い読み込み
            var providerGen2 = new FixedSaveDataKeyProvider(keysBySaltVersion, currentSaltVersion: 2, keySourceId);
            var storageGen2 = new EncryptedSaveDataStorage(_innerStorage, providerGen2);
            var loaded = await storageGen2.LoadAsync<AudioSaveData>(key);

            // Assert: 復号に成功し、世代2で自動再保存される
            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.MasterVolume, Is.EqualTo(6));

            var path = storageGen2.GetFullPath(key);
            var fileBytes = await File.ReadAllBytesAsync(path);
            Assert.That(fileBytes[6], Is.EqualTo(2)); // SaltVersionヘッダバイト（offset 6）
        }

        #endregion

        #region アトミック書き込みテスト

        [Test]
        public async Task SaveAsync_AtomicWrite_DoesNotLeaveTempFile()
        {
            // Arrange
            var key = GetTestKey("atomic_tmp");

            // Act
            await _storage.SaveAsync(key, new AudioSaveData { MasterVolume = 1 });

            // Assert: 一時ファイルが残っていない
            var path = _storage.GetFullPath(key);
            Assert.That(File.Exists(path + ".tmp"), Is.False);
        }

        #endregion

        #region 委譲テスト

        [Test]
        public async Task DeleteAsync_DelegatesToInner()
        {
            // Arrange
            var mockInner = Substitute.For<ISaveDataStorage>();
            mockInner.DeleteAsync("test_key").Returns(UniTask.CompletedTask);
            var storage = new EncryptedSaveDataStorage(mockInner, new FixedSaveDataKeyProvider(_encryptionKey, _hmacKey));

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
            var storage = new EncryptedSaveDataStorage(mockInner, new FixedSaveDataKeyProvider(_encryptionKey, _hmacKey));

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
            var storage = new EncryptedSaveDataStorage(mockInner, new FixedSaveDataKeyProvider(_encryptionKey, _hmacKey));

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
            var storage = new EncryptedSaveDataStorage(mockInner, new FixedSaveDataKeyProvider(_encryptionKey, _hmacKey));

            // Act
            var result = storage.BasePath;

            // Assert
            Assert.That(result, Is.EqualTo("/some/path"));
            _ = mockInner.Received(1).BasePath;
        }

        #endregion
    }
}
