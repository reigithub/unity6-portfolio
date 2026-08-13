using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Horror.Services;
using Game.Horror.Services.Interfaces;
using Game.Horror.Signals;
using Game.Shared.SaveData;
using Game.Shared.Scriptable.Database;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorEnemyServiceTests
    {
        // 標準データ: Group1 = エントリ1,2,3（初期。全滅→Group2 / 2キル→Group3）、Group2 = 4,5、Group3 = 6
        private static readonly string[][] StandardSpawnRows =
        {
            new[] { "1", "1", "1" },
            new[] { "2", "1", "1" },
            new[] { "3", "1", "1" },
            new[] { "4", "1", "2" },
            new[] { "5", "1", "2" },
            new[] { "6", "1", "3" },
        };

        private static readonly string[][] StandardGroupRows =
        {
            new[] { "1", "1", "2", "2", "3" },
            new[] { "2", "0", "0", "0", "0" },
            new[] { "3", "0", "0", "0", "0" },
        };

        // トリガー1 → Group2（非初期）を起動する構成
        private static readonly string[][] StandardTriggerRows =
        {
            new[] { "1", "2" },
        };

        private readonly List<Object> _createdObjects = new();
        private readonly List<int> _activatedGroups = new();

        private ISaveDataStorage _mockStorage;
        private IScriptableDatabaseService _mockDatabase;
        private IHorrorSaveRepository _repository;
        private IMessagePipeService _messagePipe;
        private HorrorEnemyService _service;
        private IDisposable _groupSubscription;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISaveDataStorage>();
            _mockDatabase = Substitute.For<IScriptableDatabaseService>();
            SetupDatabase(StandardSpawnRows, StandardGroupRows);
            _repository = new HorrorSaveRepository(_mockStorage, _mockDatabase);

            var messagePipe = new MessagePipeService();
            messagePipe.AddMessageBroker<HorrorSignals.Enemy.Died>();
            messagePipe.AddMessageBroker<HorrorSignals.Enemy.SpawnGroupActivated>();
            messagePipe.Build();
            _messagePipe = messagePipe;

            _service = new HorrorEnemyService(_repository, _mockDatabase, _messagePipe);
            _service.Startup(); // GameServiceManager.Register が呼ぶ Startup 相当（購読開始）

            _activatedGroups.Clear();
            _groupSubscription = _messagePipe.Subscribe<HorrorSignals.Enemy.SpawnGroupActivated>(evt => _activatedGroups.Add(evt.SpawnGroupId));
        }

        [TearDown]
        public void TearDown()
        {
            _groupSubscription?.Dispose();
            _service.Shutdown();

            foreach (var obj in _createdObjects)
                Object.DestroyImmediate(obj);
            _createdObjects.Clear();
        }

        /// <summary>実テーブル + 実 ScriptableDatabase を構成してモックの Database に差し込む（テスト毎の差し替え可）。</summary>
        private void SetupDatabase(string[][] spawnRows, string[][] groupRows, string[][] triggerRows = null)
        {
            var spawnTable = ScriptableObject.CreateInstance<HorrorEnemySpawnMasterTable>();
            spawnTable.EditorImportRows(new[] { "Id", "EnemyMasterId", "SpawnGroupId" }, spawnRows, mergeByPrimaryKey: false);

            var groupTable = ScriptableObject.CreateInstance<HorrorEnemySpawnGroupMasterTable>();
            groupTable.EditorImportRows(
                new[] { "Id", "IsInitialSpawn", "NextGroupIdOnEliminated", "AdditionalKillThreshold", "AdditionalGroupId" },
                groupRows,
                mergeByPrimaryKey: false);

            // トリガー表はサービスが常に参照しうるため、未指定でも空表を結線して実環境（全表割当済み）と揃える
            var triggerTable = ScriptableObject.CreateInstance<HorrorEnemySpawnTriggerMasterTable>();
            triggerTable.EditorImportRows(new[] { "Id", "SpawnGroupId" }, triggerRows ?? Array.Empty<string[]>(), mergeByPrimaryKey: false);

            var database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(database);
            so.FindProperty("horrorEnemySpawnMasterTable").objectReferenceValue = spawnTable;
            so.FindProperty("horrorEnemySpawnGroupMasterTable").objectReferenceValue = groupTable;
            so.FindProperty("horrorEnemySpawnTriggerMasterTable").objectReferenceValue = triggerTable;
            so.ApplyModifiedPropertiesWithoutUndo();

            _createdObjects.Add(spawnTable);
            _createdObjects.Add(groupTable);
            _createdObjects.Add(triggerTable);
            _createdObjects.Add(database);

            _mockDatabase.Database.Returns(database);
        }

        private async Task LoadDefaultData()
        {
            // 保存ファイルが無い状態 → CreateNewData（未記録）が走る。DB は参照しない。
            _mockStorage.LoadAsync<HorrorSaveData>(Arg.Any<string>())
                .Returns(UniTask.FromResult<HorrorSaveData>(null));
            await _repository.LoadAsync();
        }

        /// <summary>ロード済みセーブデータの途中状態を直接構成する（ランタイム経路の副作用を混ぜない）。</summary>
        private async Task LoadDataWithDefeats(params int[] defeatedSpawnIds)
        {
            await LoadDefaultData();
            _repository.Data.Enemy.DefeatedSpawnIds.AddRange(defeatedSpawnIds);
        }

        // 撃破記録（Enemy.Died Publish 起点）：記録+Dirty 化、重複は冪等、未ロード・不正 Id は LogError の上で no-op。

        [Test]
        public async Task Died_RecordsAndMarksDirty()
        {
            await LoadDefaultData();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.True);
        }

        [Test]
        public async Task Died_Twice_RecordsOnce_AndSecondPublishDoesNotMarkDirty()
        {
            await LoadDefaultData();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            await _repository.SaveBySlotAsync(0);
            Assert.That(_repository.IsDirty, Is.False);

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.False);
        }

        [Test]
        public void Died_WhenDataNull_LogsErrorAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "[HorrorEnemyService] セーブデータ未ロードのため撃破記録 (SpawnId=1) を無視しました");

            Assert.DoesNotThrow(() => _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero)));
        }

        [TestCase(0)]
        [TestCase(-1)]
        public async Task Died_InvalidId_LogsErrorAndDoesNotRecord(int spawnId)
        {
            await LoadDefaultData();
            LogAssert.Expect(LogType.Error, $"[HorrorEnemyService] 無効なスポーン Id のため撃破記録 (SpawnId={spawnId}) を無視しました");

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(spawnId, Vector3.zero));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.Empty);
            Assert.That(_repository.IsDirty, Is.False);
        }

        // 購読ライフサイクル：Shutdown 後は Publish しても記録されない（購読解除の回帰固定）。

        [Test]
        public async Task Shutdown_StopsRecording()
        {
            await LoadDefaultData();
            _service.Shutdown();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.Empty);
            Assert.That(_repository.IsDirty, Is.False);
        }

        // 撃破判定：記録済みのみ true。未ロードは無音で false（敵を出す方向へフェイルオープン）。

        [Test]
        public async Task IsDefeated_ReturnsTrueForRecorded_FalseForUnrecorded()
        {
            await LoadDefaultData();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(2, Vector3.zero));

            Assert.That(_service.IsDefeated(2), Is.True);
            Assert.That(_service.IsDefeated(3), Is.False);
        }

        [Test]
        public void IsDefeated_WhenDataNull_ReturnsFalseWithoutLog()
        {
            Assert.That(_service.IsDefeated(1), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        // スポーングループ判定：所属エントリと撃破記録の突き合わせ。未ロード・所属0件は無音で 0 / false。

        [Test]
        public async Task GetDefeatedCount_CountsOnlyGroupMembers()
        {
            await LoadDataWithDefeats(1, 2, 4);

            Assert.That(_service.GetDefeatedCount(1), Is.EqualTo(2));
            Assert.That(_service.GetDefeatedCount(2), Is.EqualTo(1));
            Assert.That(_service.GetDefeatedCount(3), Is.Zero);
        }

        [Test]
        public void GetDefeatedCount_WhenDataNull_ReturnsZeroWithoutLog()
        {
            Assert.That(_service.GetDefeatedCount(1), Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public async Task IsSpawnGroupEliminated_TrueWhenAllDefeated_FalseWhenPartial()
        {
            await LoadDataWithDefeats(4, 5, 1);

            Assert.That(_service.IsSpawnGroupEliminated(2), Is.True);
            Assert.That(_service.IsSpawnGroupEliminated(1), Is.False);
        }

        [Test]
        public async Task IsSpawnGroupEliminated_EmptyGroup_ReturnsFalse()
        {
            await LoadDataWithDefeats(1, 2, 3, 4, 5, 6);

            // 所属エントリ0件（未知グループ含む）を全滅扱いにすると、起動した瞬間に空連鎖が走るため false
            Assert.That(_service.IsSpawnGroupEliminated(999), Is.False);
        }

        // スポーングループ進行（ランタイム連鎖）：閾値到達で追加グループ、全滅で次グループ。発火は一度きり。

        [Test]
        public async Task Died_ThresholdReached_ActivatesAdditionalGroup()
        {
            await LoadDefaultData();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            Assert.That(_activatedGroups, Is.Empty);

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(2, Vector3.zero));
            Assert.That(_activatedGroups, Is.EqualTo(new[] { 3 }));
        }

        [Test]
        public async Task Died_GroupEliminated_ActivatesNextGroup_AndAdditionalFiresOnce()
        {
            await LoadDefaultData();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(2, Vector3.zero)); // 閾値2到達 → Group3
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(3, Vector3.zero)); // 全滅 → Group2（閾値は成立し続けるが Group3 は再発火しない）

            Assert.That(_activatedGroups, Is.EqualTo(new[] { 3, 2 }));
        }

        [Test]
        public async Task Died_SameKill_ActivatesBothThresholdAndNext()
        {
            // Group1 = エントリ1,2（閾値2 + 全滅が同一キルで成立する構成）
            SetupDatabase(
                new[]
                {
                    new[] { "1", "1", "1" },
                    new[] { "2", "1", "1" },
                    new[] { "3", "1", "2" },
                    new[] { "4", "1", "3" },
                },
                StandardGroupRows);
            await LoadDefaultData();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            Assert.That(_activatedGroups, Is.Empty);

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(2, Vector3.zero));
            Assert.That(_activatedGroups, Is.EquivalentTo(new[] { 2, 3 }));
        }

        // 進行判定の異常系：判定をスキップしても撃破記録は残す。冪等 no-op（重複）では判定自体を走らせない。

        [Test]
        public async Task Died_SpawnMasterMissing_RecordsButLogsProgressionError_OnceOnDuplicate()
        {
            await LoadDefaultData();
            LogAssert.Expect(LogType.Error, "[HorrorEnemyService] HorrorEnemySpawnMaster (Id=99) が見つからないためスポーングループ進行判定をスキップしました");

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(99, Vector3.zero));
            // 重複 = 記録 no-op → 進行判定も走らず LogError は1回のみ（予期しない LogError はテストを自動失敗させる）
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(99, Vector3.zero));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 99 }));
        }

        [Test]
        public async Task Died_SpawnGroupMasterMissing_RecordsWithoutProgression()
        {
            SetupDatabase(
                new[] { new[] { "1", "1", "7" } },
                new[] { new[] { "1", "0", "0", "0", "0" } });
            await LoadDefaultData();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));

            Assert.That(_repository.Data.Enemy.DefeatedSpawnIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_activatedGroups, Is.Empty);
        }

        // 活性スポーングループ算出（ロード時復元）：初期グループを種に全滅/閾値連鎖を fixpoint で再構築する。

        [Test]
        public async Task GetActiveSpawnGroupIds_FreshData_ReturnsInitialOnly()
        {
            await LoadDefaultData();

            Assert.That(_service.GetActiveSpawnGroupIds(), Is.EquivalentTo(new[] { 1 }));
        }

        [Test]
        public async Task GetActiveSpawnGroupIds_ThresholdReachedInSave_ActivatesAdditional()
        {
            await LoadDataWithDefeats(1, 2);

            Assert.That(_service.GetActiveSpawnGroupIds(), Is.EquivalentTo(new[] { 1, 3 }));
        }

        [Test]
        public async Task GetActiveSpawnGroupIds_EliminatedInSave_ActivatesChain()
        {
            await LoadDataWithDefeats(1, 2, 3);

            Assert.That(_service.GetActiveSpawnGroupIds(), Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public async Task GetActiveSpawnGroupIds_MultiHopChain_ResolvesToFixpoint()
        {
            // Group1(初期) 全滅 → Group2 全滅 → Group3 と2段連鎖した途中状態からの復元
            SetupDatabase(
                new[]
                {
                    new[] { "1", "1", "1" },
                    new[] { "2", "1", "2" },
                    new[] { "3", "1", "3" },
                },
                new[]
                {
                    new[] { "1", "1", "2", "0", "0" },
                    new[] { "2", "0", "3", "0", "0" },
                    new[] { "3", "0", "0", "0", "0" },
                });
            await LoadDataWithDefeats(1, 2);

            Assert.That(_service.GetActiveSpawnGroupIds(), Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public async Task GetActiveSpawnGroupIds_SeedsRuntimeGuard_NoRefireForRestoredGroups()
        {
            await LoadDataWithDefeats(1, 2); // 閾値到達済み → Group3 は復元で起動済み扱い
            _service.GetActiveSpawnGroupIds();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(3, Vector3.zero)); // 全滅 → Group2 のみ新規発火（Group3 は再発火しない）

            Assert.That(_activatedGroups, Is.EqualTo(new[] { 2 }));
        }

        // トリガー発火：記録+Dirty 化+グループ起動。重複・起動済みグループは冪等、未ロード・マスタ不在は LogError の上で no-op。

        [Test]
        public async Task NotifyTriggerPassed_RecordsAndMarksDirtyAndActivatesGroup()
        {
            SetupDatabase(StandardSpawnRows, StandardGroupRows, StandardTriggerRows);
            await LoadDefaultData();

            _service.NotifyTriggerPassed(1);

            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.True);
            Assert.That(_activatedGroups, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public async Task NotifyTriggerPassed_Twice_RecordsOnce_AndSecondDoesNotMarkDirtyOrRepublish()
        {
            SetupDatabase(StandardSpawnRows, StandardGroupRows, StandardTriggerRows);
            await LoadDefaultData();
            _service.NotifyTriggerPassed(1);
            await _repository.SaveBySlotAsync(0);
            Assert.That(_repository.IsDirty, Is.False);

            _service.NotifyTriggerPassed(1);

            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.False);
            Assert.That(_activatedGroups, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public async Task NotifyTriggerPassed_GroupAlreadyActive_RecordsWithoutRepublish()
        {
            SetupDatabase(StandardSpawnRows, StandardGroupRows, new[] { new[] { "1", "3" } });
            await LoadDefaultData();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(2, Vector3.zero)); // 閾値2到達 → Group3 起動済み
            await _repository.SaveBySlotAsync(0);

            _service.NotifyTriggerPassed(1);

            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.EquivalentTo(new[] { 1 }));
            Assert.That(_repository.IsDirty, Is.True, "起動が no-op でも発火記録は残す");
            Assert.That(_activatedGroups, Is.EqualTo(new[] { 3 }), "起動済みグループへは再発行しない");
        }

        [Test]
        public void NotifyTriggerPassed_WhenDataNull_LogsErrorAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, "[HorrorEnemyService] セーブデータ未ロードのためトリガー発火記録 (TriggerId=1) を無視しました");

            Assert.DoesNotThrow(() => _service.NotifyTriggerPassed(1));
        }

        [Test]
        public async Task NotifyTriggerPassed_TriggerMasterMissing_LogsErrorAndDoesNotRecord()
        {
            await LoadDefaultData(); // 既定セットアップのトリガー表は空
            LogAssert.Expect(LogType.Error, "[HorrorEnemyService] HorrorEnemySpawnTriggerMaster (Id=1) が見つからないためトリガー発火を無視しました");

            _service.NotifyTriggerPassed(1);

            Assert.That(_repository.Data.Enemy.FiredTriggerIds, Is.Empty);
            Assert.That(_repository.IsDirty, Is.False);
            Assert.That(_activatedGroups, Is.Empty);
        }

        // トリガー発火判定：記録済みのみ true。未ロードは無音で false（IsDefeated と同じフェイルオープン）。

        [Test]
        public async Task IsTriggerFired_ReturnsTrueForFired_FalseForUnfired()
        {
            SetupDatabase(StandardSpawnRows, StandardGroupRows, new[] { new[] { "1", "2" }, new[] { "2", "3" } });
            await LoadDefaultData();
            _service.NotifyTriggerPassed(1);

            Assert.That(_service.IsTriggerFired(1), Is.True);
            Assert.That(_service.IsTriggerFired(2), Is.False);
        }

        [Test]
        public void IsTriggerFired_WhenDataNull_ReturnsFalseWithoutLog()
        {
            Assert.That(_service.IsTriggerFired(1), Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        // 活性スポーングループ算出（トリガー由来）：発火済みトリガーの起動先を種に含め、その先の連鎖も復元する。

        [Test]
        public async Task GetActiveSpawnGroupIds_FiredTriggerInSave_ActivatesGroup()
        {
            SetupDatabase(StandardSpawnRows, StandardGroupRows, StandardTriggerRows);
            await LoadDefaultData();
            _repository.Data.Enemy.FiredTriggerIds.Add(1);

            Assert.That(_service.GetActiveSpawnGroupIds(), Is.EquivalentTo(new[] { 1, 2 }));
        }

        [Test]
        public async Task GetActiveSpawnGroupIds_FiredTriggerInSave_RestoresChainFromTriggerGroup()
        {
            // Group2（トリガー起動・非初期）全滅 → Group3 と連鎖した途中状態からの復元
            SetupDatabase(
                new[]
                {
                    new[] { "1", "1", "1" },
                    new[] { "2", "1", "2" },
                    new[] { "3", "1", "3" },
                },
                new[]
                {
                    new[] { "1", "1", "0", "0", "0" },
                    new[] { "2", "0", "3", "0", "0" },
                    new[] { "3", "0", "0", "0", "0" },
                },
                StandardTriggerRows);
            await LoadDataWithDefeats(2); // Group2 の唯一のエントリ撃破 = 全滅
            _repository.Data.Enemy.FiredTriggerIds.Add(1);

            Assert.That(_service.GetActiveSpawnGroupIds(), Is.EquivalentTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public async Task GetActiveSpawnGroupIds_AfterRuntimeTriggerFire_KeepsGroupActive()
        {
            // 再計算は内部集合を Clear して再導出するため、ランタイム発火分が FiredTriggerIds から復元されることを固定する
            SetupDatabase(StandardSpawnRows, StandardGroupRows, StandardTriggerRows);
            await LoadDefaultData();
            _service.GetActiveSpawnGroupIds();
            _service.NotifyTriggerPassed(1);

            Assert.That(_service.GetActiveSpawnGroupIds(), Is.EquivalentTo(new[] { 1, 2 }));
        }
    }
}
