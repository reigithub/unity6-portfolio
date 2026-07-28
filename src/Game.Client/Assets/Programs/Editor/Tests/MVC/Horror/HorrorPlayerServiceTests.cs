using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Horror.Constants;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.SaveData;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
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
        private HorrorPlayerMasterTable _playerTable;
        private ScriptableDatabase _database;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);
            _service = new HorrorPlayerService(_repository, _mockDatabase);
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerTable != null) Object.DestroyImmediate(_playerTable);
            if (_database != null) Object.DestroyImmediate(_database);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未記録）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(SaveKey)
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        /// <summary>
        /// 指定 (Id, MaxHealth) のプレイヤーレコードを持つ実テーブル＋DB を組み立てて mock に接続する。
        /// EditorImportRows は列名をメンバ名と完全一致でマッピングし、無い列は既定値のままとなる。
        /// </summary>
        private void SetupRealDatabase(params (int Id, int MaxHealth)[] records)
        {
            _playerTable = ScriptableObject.CreateInstance<HorrorPlayerMasterTable>();
            var rows = new List<IReadOnlyList<string>>();
            foreach (var record in records)
                rows.Add(new[] { record.Id.ToString(), record.MaxHealth.ToString() });
            _playerTable.EditorImportRows(new[] { "Id", "MaxHealth" }, rows, mergeByPrimaryKey: false);

            _database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(_database);
            so.FindProperty("horrorPlayerMasterTable").objectReferenceValue = _playerTable;
            so.ApplyModifiedPropertiesWithoutUndo();
            _mockDatabase.Database.Returns(_database);
        }

        /// <summary>新規データを作り、操作対象のプレイヤー Id だけを差し替える（書き込み API は無いため直接設定する）。</summary>
        private void CreateDataWithPlayerId(int playerId)
        {
            _repository.CreateNewSaveData();
            _repository.Data.Player.PlayerId = playerId;
        }

        // プレイヤーマスターの解決：プレイヤー生成に先立って確定させる。要求 Id が引けなければ既定 Id へフォールバックする。

        [Test]
        public void PlayerMaster_BeforeResolve_IsNull()
        {
            Assert.That(_service.PlayerMaster, Is.Null);
        }

        [Test]
        public async Task ResolvePlayerMaster_NewData_ResolvesDefaultMaster()
        {
            SetupRealDatabase((1, 100));
            await LoadDefaultData();

            Assert.That(_service.ResolvePlayerMaster(), Is.True);
            Assert.That(_service.PlayerMaster.Id, Is.EqualTo(HorrorSaveConstants.DefaultPlayerId));
        }

        [Test]
        public void ResolvePlayerMaster_WhenDataNull_ResolvesDefaultMaster()
        {
            SetupRealDatabase((1, 100));

            Assert.That(_service.ResolvePlayerMaster(), Is.True);
            Assert.That(_service.PlayerMaster.Id, Is.EqualTo(HorrorSaveConstants.DefaultPlayerId));
            Assert.That(_repository.IsDirty, Is.False); // 書き戻す先が無いため Dirty にしない
        }

        [Test]
        public void ResolvePlayerMaster_UsesSavedPlayerId()
        {
            SetupRealDatabase((1, 100), (7, 50));
            CreateDataWithPlayerId(7);

            Assert.That(_service.ResolvePlayerMaster(), Is.True);
            Assert.That(_service.PlayerMaster.Id, Is.EqualTo(7));
        }

        [Test]
        public void ResolvePlayerMaster_MasterNotFound_FallsBackToDefaultAndAlignsSaveData()
        {
            SetupRealDatabase((1, 100));
            CreateDataWithPlayerId(7);

            LogAssert.Expect(LogType.Warning, "プレイヤーマスターが見つかりません Id=7。既定 Id=1 で代替します");

            Assert.That(_service.ResolvePlayerMaster(), Is.True);
            Assert.That(_service.PlayerMaster.Id, Is.EqualTo(HorrorSaveConstants.DefaultPlayerId));
            Assert.That(_repository.Data.Player.PlayerId, Is.EqualTo(HorrorSaveConstants.DefaultPlayerId)); // 記録も実体へ合わせる
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public void ResolvePlayerMaster_DefaultAlsoMissing_LogsErrorAndReturnsFalse()
        {
            SetupRealDatabase((7, 50));
            CreateDataWithPlayerId(9);

            LogAssert.Expect(LogType.Error, "プレイヤーマスターが見つかりません Id=9（既定 Id=1 も未登録）");

            Assert.That(_service.ResolvePlayerMaster(), Is.False);
            Assert.That(_service.PlayerMaster, Is.Null);
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

        // 最大 HP：解決済みマスタ由来のランタイム値。セーブリポジトリ非経由のため Dirty 化しない。

        [Test]
        public async Task MaxHealth_FromResolvedMaster()
        {
            SetupRealDatabase((1, 100));
            await LoadDefaultData();
            _service.ResolvePlayerMaster();

            Assert.That(_service.MaxHealth, Is.EqualTo(100));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public async Task MaxHealth_BeforeResolve_IsZero()
        {
            await LoadDefaultData();

            Assert.That(_service.MaxHealth, Is.EqualTo(0));
        }

        // 満タン判定：MaxHealth 未解決（0）は満タン扱いにしない（誤って使用不能にならない）。

        [Test]
        public async Task IsHealthFull_AtMax_IsTrue()
        {
            SetupRealDatabase((1, 100));
            await LoadDefaultData();
            _service.ResolvePlayerMaster();
            _service.SetCurrentHealth(100);

            Assert.That(_service.IsHealthFull, Is.True);
        }

        [Test]
        public async Task IsHealthFull_BelowMax_IsFalse()
        {
            SetupRealDatabase((1, 100));
            await LoadDefaultData();
            _service.ResolvePlayerMaster();
            _service.SetCurrentHealth(99);

            Assert.That(_service.IsHealthFull, Is.False);
        }

        [Test]
        public async Task IsHealthFull_OverMax_IsTrue()
        {
            SetupRealDatabase((1, 100));
            await LoadDefaultData();
            _service.ResolvePlayerMaster();
            _service.SetCurrentHealth(150);

            Assert.That(_service.IsHealthFull, Is.True);
        }

        [Test]
        public async Task IsHealthFull_BeforeResolve_IsFalse()
        {
            await LoadDefaultData();
            _service.SetCurrentHealth(40);

            Assert.That(_service.IsHealthFull, Is.False);
        }
    }
}
