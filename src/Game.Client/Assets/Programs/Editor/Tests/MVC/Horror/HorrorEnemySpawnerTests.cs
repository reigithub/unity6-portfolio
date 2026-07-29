using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Enemy;
using Game.Horror.Services.Interfaces;
using Game.Horror.Signals;
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
    public class HorrorEnemySpawnerTests
    {
        private IAddressableAssetService _mockAssets;
        private IScriptableDatabaseService _mockDbService;
        private IHorrorEnemyService _mockEnemyService;
        private IMessagePipeService _messagePipe;
        private GameObject _prefab;
        private GameObject _player;
        private HorrorEnemySpawner _spawner;
        private readonly List<GameObject> _markerObjects = new();
        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void Setup()
        {
            // Perception / Controller の Initialize が GameServiceManager 経由で MessagePipe を Resolve するため実物を登録する
            GameServiceManager.StartUp();
            var messagePipe = new MessagePipeService();
            messagePipe.AddMessageBroker<HorrorSignals.Noise.Occurred>();
            messagePipe.AddMessageBroker<HorrorSignals.Player.Died>();
            messagePipe.AddMessageBroker<HorrorSignals.Enemy.Died>();
            messagePipe.AddMessageBroker<HorrorSignals.Enemy.GroupActivated>();
            messagePipe.Build();
            GameServiceManager.Register<IMessagePipeService, MessagePipeService>(messagePipe);
            _messagePipe = messagePipe;

            _mockDbService = Substitute.For<IScriptableDatabaseService>();
            SetupDatabase(new[] { new[] { "1", "10", "1" } });

            _prefab = CreateEnemyPrefab();
            _player = new GameObject("SpawnerTestPlayer");

            _mockAssets = Substitute.For<IAddressableAssetService>();
            _mockAssets.LoadAssetAsync<GameObject>(Arg.Any<string>()).Returns(UniTask.FromResult(_prefab));
            _mockEnemyService = Substitute.For<IHorrorEnemyService>();
            _mockEnemyService.GetActiveGroupIds().Returns(new HashSet<int> { 1, 2 });

            _spawner = new HorrorEnemySpawner(_mockAssets, _mockDbService, _messagePipe, _mockEnemyService);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner?.Dispose();
            foreach (var markerObject in _markerObjects)
            {
                if (markerObject != null) Object.DestroyImmediate(markerObject);
            }

            _markerObjects.Clear();
            if (_player != null) Object.DestroyImmediate(_player);
            if (_prefab != null) Object.DestroyImmediate(_prefab);
            foreach (var obj in _createdObjects)
                Object.DestroyImmediate(obj);
            _createdObjects.Clear();
            GameServiceManager.Shutdown();
        }

        // registry 構築の検証：SpawnId 未設定・重複・マスタ不在・GroupId 不備・マーカー不在は
        // 撃破時ではなくシーン起動時に LogError で決定的に検出する。

        [Test]
        public async Task InitializeAsync_SpawnIdZero_LogsError()
        {
            SetupDatabase(Array.Empty<string[]>());
            var marker = CreateMarker(0);
            LogAssert.Expect(LogType.Error, $"[HorrorEnemySpawner] {marker.name} の SpawnId が未設定(0)です");

            await _spawner.InitializeAsync(_player, new[] { marker });
        }

        [Test]
        public async Task InitializeAsync_DuplicateSpawnId_LogsErrorAndSpawnsFirstOnly()
        {
            var first = CreateMarker(1);
            var second = CreateMarker(1);
            LogAssert.Expect(LogType.Error, $"[HorrorEnemySpawner] SpawnId=1 が複数の {nameof(HorrorEnemyStart)} で重複しています");

            await _spawner.InitializeAsync(_player, new[] { first, second });

            Assert.That(CountActiveEnemies(FindPoolParent()), Is.EqualTo(1));
        }

        [Test]
        public async Task InitializeAsync_SpawnMasterMissing_LogsError()
        {
            SetupDatabase(Array.Empty<string[]>());
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawner] HorrorEnemySpawnMaster (Id=42) が見つかりません。");

            await _spawner.InitializeAsync(_player, new[] { CreateMarker(42) });
        }

        [Test]
        public async Task InitializeAsync_EnemyMasterMissing_LogsError()
        {
            SetupDatabase(new[] { new[] { "5", "77", "1" } });
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawner] HorrorEnemyMaster (Id=77) が見つかりません。");

            await _spawner.InitializeAsync(_player, new[] { CreateMarker(5) });
        }

        [Test]
        public async Task InitializeAsync_GroupIdZero_LogsErrorAndExcludesEntry()
        {
            SetupDatabase(new[] { new[] { "7", "10", "0" } });
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawner] HorrorEnemySpawnMaster (Id=7) の GroupId が未設定(0)です");

            await _spawner.InitializeAsync(_player, new[] { CreateMarker(7) });

            Assert.That(CountActiveEnemies(FindPoolParent()), Is.Zero);
        }

        [Test]
        public async Task InitializeAsync_GroupMasterMissing_LogsError()
        {
            SetupDatabase(new[] { new[] { "8", "10", "9" } });
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawner] HorrorEnemyGroupMaster (Id=9) が見つかりません。");

            await _spawner.InitializeAsync(_player, new[] { CreateMarker(8) });
        }

        [Test]
        public async Task InitializeAsync_MarkerMissingForMasterRow_LogsError()
        {
            SetupDatabase(new[] { new[] { "1", "10", "1" }, new[] { "2", "10", "1" } });
            LogAssert.Expect(LogType.Error, $"[HorrorEnemySpawner] HorrorEnemySpawnMaster (Id=2) に対応する {nameof(HorrorEnemyStart)} マーカーがシーンにありません");

            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1) });
        }

        // 初期スポーン：活性グループの未撃破エントリのみマーカーの位置・向きで起動する。
        // 撃破済み（セーブデータからの自己復元）と非活性グループ（未起動の連鎖先）は生成しない。

        [Test]
        public async Task InitializeAsync_SpawnsEnemyAtMarkerPose()
        {
            var position = new Vector3(3f, 0f, -2f);
            var rotation = Quaternion.Euler(0f, 90f, 0f);
            var marker = CreateMarker(1, position, rotation);

            await _spawner.InitializeAsync(_player, new[] { marker });

            var poolParent = FindPoolParent();
            Assert.That(CountActiveEnemies(poolParent), Is.EqualTo(1));
            var enemy = GetSingleActiveEnemy(poolParent);
            Assert.That(enemy.transform.position, Is.EqualTo(position));

            // transform.rotation は set/get で正規化され厳密一致しないため角度差で比較する
            Assert.That(Quaternion.Angle(enemy.transform.rotation, rotation), Is.LessThan(0.01f));
        }

        [Test]
        public async Task InitializeAsync_DefeatedEntry_NotSpawned()
        {
            _mockEnemyService.IsDefeated(1).Returns(true);

            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1) });

            Assert.That(CountActiveEnemies(FindPoolParent()), Is.Zero);
        }

        [Test]
        public async Task InitializeAsync_InactiveGroup_NotSpawned()
        {
            SetupDatabase(new[] { new[] { "1", "10", "1" }, new[] { "2", "10", "2" } });
            _mockEnemyService.GetActiveGroupIds().Returns(new HashSet<int> { 1 });

            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1), CreateMarker(2) });

            Assert.That(CountActiveEnemies(FindPoolParent()), Is.EqualTo(1));
        }

        // ランタイム連鎖：GroupActivated シグナルの受信で所属エントリをスポーンする。Dispose 後は反応しない。

        [Test]
        public async Task GroupActivated_SpawnsGroupEntries()
        {
            SetupDatabase(new[] { new[] { "1", "10", "1" }, new[] { "2", "10", "2" } });
            _mockEnemyService.GetActiveGroupIds().Returns(new HashSet<int> { 1 });
            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1), CreateMarker(2) });
            var poolParent = FindPoolParent();
            Assert.That(CountActiveEnemies(poolParent), Is.EqualTo(1));

            _messagePipe.Publish(new HorrorSignals.Enemy.GroupActivated(2));

            Assert.That(CountActiveEnemies(poolParent), Is.EqualTo(2));
        }

        [Test]
        public async Task Dispose_UnsubscribesGroupActivated()
        {
            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1) });

            _spawner.Dispose();

            Assert.DoesNotThrow(() => _messagePipe.Publish(new HorrorSignals.Enemy.GroupActivated(1)));
        }

        // 貸出・返却のライフサイクル：返却で非アクティブ化され、同じ SpawnId を再スポーンできる（プール再利用）。
        // 二重スポーン・未貸出返却は無音で握りつぶさず LogError で顕在化する。

        [Test]
        public async Task ReturnToPool_DeactivatesAndAllowsRespawn()
        {
            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1) });
            var poolParent = FindPoolParent();
            var enemy = GetSingleActiveEnemy(poolParent);

            _spawner.ReturnToPool(1);
            Assert.That(enemy.activeSelf, Is.False);

            Assert.That(_spawner.TrySpawn(1), Is.True);
            Assert.That(CountActiveEnemies(poolParent), Is.EqualTo(1));
        }

        [Test]
        public async Task TrySpawn_WhileRented_LogsErrorAndReturnsFalse()
        {
            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1) });
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawner] SpawnId=1 は貸出中のため二重スポーンできません");

            Assert.That(_spawner.TrySpawn(1), Is.False);
        }

        [Test]
        public async Task TrySpawn_UnknownSpawnId_LogsErrorAndReturnsFalse()
        {
            SetupDatabase(Array.Empty<string[]>());
            await _spawner.InitializeAsync(_player, Array.Empty<HorrorEnemyStart>());
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawner] 未登録の SpawnId=42 はスポーンできません");

            Assert.That(_spawner.TrySpawn(42), Is.False);
        }

        [Test]
        public async Task ReturnToPool_NotRented_LogsError()
        {
            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1) });
            _spawner.ReturnToPool(1);
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawner] 貸出中でない SpawnId=1 が返却されました（二重返却の疑い）");

            _spawner.ReturnToPool(1);
        }

        // 破棄：貸出中とプール内の個体・プール親を破棄し、ロード済み prefab を Release する。

        [Test]
        public async Task Dispose_DestroysPoolAndReleasesPrefab()
        {
            await _spawner.InitializeAsync(_player, new[] { CreateMarker(1) });

            _spawner.Dispose();

            Assert.That(GameObject.Find("HorrorEnemyPool"), Is.Null);
            _mockAssets.Received(1).Release(_prefab);
        }

        /// <summary>
        /// 実テーブル + 実 DB を組み立てて mock サービスへ接続する（HorrorSaveRepositoryTests と同じ手法）。
        /// マーカー不在検証があるため、spawn 行は各テストが使うマーカーと一致させる（既定は Id=1 → 敵種10・Group1）。
        /// 敵種は 10 のみ定義（77 は EnemyMaster 不在ケース用）、グループは 1,2 のみ定義（9 はグループ不在ケース用）。
        /// </summary>
        private void SetupDatabase(string[][] spawnRows)
        {
            var spawnTable = ScriptableObject.CreateInstance<HorrorEnemySpawnMasterTable>();
            spawnTable.EditorImportRows(new[] { "Id", "EnemyMasterId", "GroupId" }, spawnRows, mergeByPrimaryKey: false);

            var enemyTable = ScriptableObject.CreateInstance<HorrorEnemyMasterTable>();
            enemyTable.EditorImportRows(
                new[] { "Id", "ModelAssetName", "MaxHealth" },
                new[] { new[] { "10", "TestEnemy", "10" } },
                mergeByPrimaryKey: false);

            var groupTable = ScriptableObject.CreateInstance<HorrorEnemyGroupMasterTable>();
            groupTable.EditorImportRows(
                new[] { "Id", "IsInitialSpawn", "NextGroupIdOnEliminated", "AdditionalKillThreshold", "AdditionalGroupId" },
                new[]
                {
                    new[] { "1", "1", "0", "0", "0" },
                    new[] { "2", "0", "0", "0", "0" },
                },
                mergeByPrimaryKey: false);

            var database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(database);
            so.FindProperty("horrorEnemySpawnMasterTable").objectReferenceValue = spawnTable;
            so.FindProperty("horrorEnemyMasterTable").objectReferenceValue = enemyTable;
            so.FindProperty("horrorEnemyGroupMasterTable").objectReferenceValue = groupTable;
            so.ApplyModifiedPropertiesWithoutUndo();

            _createdObjects.Add(spawnTable);
            _createdObjects.Add(enemyTable);
            _createdObjects.Add(groupTable);
            _createdObjects.Add(database);

            _mockDbService.Database.Returns(database);
        }

        /// <summary>
        /// HorrorEnemyController + HorrorEnemyPerception を持つテスト用 prefab 相当の GameObject を作る。
        /// _perception は SerializedObject で結線する（NavMeshAgent / Animator は null ガード済み経路のため付けない）。
        /// </summary>
        private static GameObject CreateEnemyPrefab()
        {
            var prefab = new GameObject("SpawnerTestEnemyPrefab");
            var perception = prefab.AddComponent<HorrorEnemyPerception>();
            var controller = prefab.AddComponent<HorrorEnemyController>();
            var so = new SerializedObject(controller);
            so.FindProperty("_perception").objectReferenceValue = perception;
            so.ApplyModifiedPropertiesWithoutUndo();
            return prefab;
        }

        private HorrorEnemyStart CreateMarker(int spawnId, Vector3? position = null, Quaternion? rotation = null)
        {
            var markerObject = new GameObject($"EnemyStart_{spawnId}");
            _markerObjects.Add(markerObject);
            if (position.HasValue) markerObject.transform.position = position.Value;
            if (rotation.HasValue) markerObject.transform.rotation = rotation.Value;

            var marker = markerObject.AddComponent<HorrorEnemyStart>();
            var so = new SerializedObject(marker);
            so.FindProperty("_spawnId").intValue = spawnId;
            so.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        private static Transform FindPoolParent()
        {
            var poolParent = GameObject.Find("HorrorEnemyPool");
            Assert.That(poolParent, Is.Not.Null, "プール親 HorrorEnemyPool が見つかりません");
            return poolParent.transform;
        }

        private static int CountActiveEnemies(Transform poolParent)
        {
            var count = 0;
            foreach (Transform child in poolParent)
            {
                if (child.gameObject.activeSelf) count++;
            }

            return count;
        }

        private static GameObject GetSingleActiveEnemy(Transform poolParent)
        {
            foreach (Transform child in poolParent)
            {
                if (child.gameObject.activeSelf) return child.gameObject;
            }

            Assert.Fail("アクティブな敵が見つかりません");
            return null;
        }
    }
}
