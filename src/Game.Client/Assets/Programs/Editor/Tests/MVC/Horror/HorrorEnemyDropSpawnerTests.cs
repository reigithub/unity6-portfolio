using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Enemy;
using Game.Horror.Interaction;
using Game.Horror.Services.Interfaces;
using Game.Horror.Signals;
using Game.Shared.Enums;
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
    public class HorrorEnemyDropSpawnerTests
    {
        private IAddressableAssetService _mockAssets;
        private IScriptableDatabaseService _mockDbService;
        private IHorrorInventoryService _mockInventory;
        private IMessagePipeService _messagePipe;
        private GameObject _prefab;
        private HorrorEnemyDropSpawner _spawner;
        private readonly List<Object> _createdObjects = new();

        [SetUp]
        public void Setup()
        {
            // HorrorDropItemInteractable.Interact が GameServiceManager 経由でインベントリを Resolve するため登録する
            GameServiceManager.StartUp();
            var messagePipe = new MessagePipeService();
            messagePipe.AddMessageBroker<HorrorSignals.Enemy.Died>();
            messagePipe.Build();
            GameServiceManager.Register<IMessagePipeService, MessagePipeService>(messagePipe);
            _messagePipe = messagePipe;

            _mockInventory = Substitute.For<IHorrorInventoryService>();
            GameServiceManager.Register<IHorrorInventoryService>(_mockInventory);

            _mockDbService = Substitute.For<IScriptableDatabaseService>();
            SetupDatabase();

            _prefab = new GameObject("DropSpawnerTestPrefab");
            _prefab.AddComponent<HorrorDropItemInteractable>();

            _mockAssets = Substitute.For<IAddressableAssetService>();
            _mockAssets.LoadAssetAsync<GameObject>(Arg.Any<string>()).Returns(UniTask.FromResult(_prefab));

            _spawner = new HorrorEnemyDropSpawner(_mockAssets, _mockDbService, _messagePipe);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner?.Dispose();
            if (_prefab != null) Object.DestroyImmediate(_prefab);
            foreach (var obj in _createdObjects)
                Object.DestroyImmediate(obj);
            _createdObjects.Clear();
            GameServiceManager.Shutdown();
        }

        // 累積抽選の純関数：roll は [0, 10000)。当選行の index、余り区間（合計 < 10000）は -1。

        [Test]
        public void RollDropIndex_EmptyRows_ReturnsMinusOne()
        {
            Assert.That(HorrorEnemyDropSpawner.RollDropIndex(new List<HorrorEnemyDropMaster>(), 0), Is.EqualTo(-1));
        }

        [Test]
        public void RollDropIndex_RollZero_SelectsFirstRow()
        {
            Assert.That(HorrorEnemyDropSpawner.RollDropIndex(Rows(3000), 0), Is.EqualTo(0));
        }

        [Test]
        public void RollDropIndex_RollJustInsideRate_SelectsRow()
        {
            Assert.That(HorrorEnemyDropSpawner.RollDropIndex(Rows(3000), 2999), Is.EqualTo(0));
        }

        [Test]
        public void RollDropIndex_RollJustOutsideRate_ReturnsMinusOne()
        {
            Assert.That(HorrorEnemyDropSpawner.RollDropIndex(Rows(3000), 3000), Is.EqualTo(-1));
        }

        [Test]
        public void RollDropIndex_SecondRow_SelectedByCumulativeRange()
        {
            var rows = Rows(3000, 6000);

            Assert.That(HorrorEnemyDropSpawner.RollDropIndex(rows, 3000), Is.EqualTo(1));
            Assert.That(HorrorEnemyDropSpawner.RollDropIndex(rows, 8999), Is.EqualTo(1));
            Assert.That(HorrorEnemyDropSpawner.RollDropIndex(rows, 9000), Is.EqualTo(-1));
        }

        // 撃破シグナル受信：必中グループ（DropRate=10000）で決定論化し、死亡位置への生成・非対象の無反応・データ齟齬の顕在化を確認する。

        [Test]
        public async Task EnemyDied_DropGroup_SpawnsDropAtDeathPosition()
        {
            await _spawner.InitializeAsync();
            var position = new Vector3(3f, 0f, -2f);

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, position));

            var drop = GetSingleActiveDrop(FindPoolParent());
            Assert.That(drop.transform.position, Is.EqualTo(position + Vector3.up * 0.05f));
        }

        [Test]
        public async Task EnemyDied_NoDropGroup_DoesNotSpawn()
        {
            await _spawner.InitializeAsync();

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(2, Vector3.zero));

            Assert.That(CountActiveDrops(FindPoolParent()), Is.Zero);
        }

        [Test]
        public async Task EnemyDied_MissingDropRows_LogsError()
        {
            await _spawner.InitializeAsync();
            LogAssert.Expect(LogType.Error, $"[{nameof(HorrorEnemyDropSpawner)}] DropGroupId=9 の {nameof(HorrorEnemyDropMaster)} 行がありません");

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(3, Vector3.zero));

            Assert.That(CountActiveDrops(FindPoolParent()), Is.Zero);
        }

        // 拾得：インベントリ加算成功でプールへ返却（非アクティブ化）、満杯（TryAdd 失敗）は残置する。

        [Test]
        public async Task Interact_AddsToInventoryAndReturnsToPool()
        {
            _mockInventory.TryAdd(ObjectCategory.Item, 4, 12, 120).Returns(true);
            await _spawner.InitializeAsync();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            var drop = GetSingleActiveDrop(FindPoolParent());

            drop.GetComponent<HorrorDropItemInteractable>().Interact();

            _mockInventory.Received(1).TryAdd(ObjectCategory.Item, 4, 12, 120);
            Assert.That(drop.activeSelf, Is.False);
        }

        [Test]
        public async Task Interact_InventoryFull_LeavesDropActive()
        {
            _mockInventory.TryAdd(ObjectCategory.Item, 4, 12, 120).Returns(false);
            await _spawner.InitializeAsync();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            var drop = GetSingleActiveDrop(FindPoolParent());

            drop.GetComponent<HorrorDropItemInteractable>().Interact();

            Assert.That(drop.activeSelf, Is.True);
        }

        [Test]
        public async Task EnemyDied_AfterCollect_ReusesPooledInstance()
        {
            _mockInventory.TryAdd(ObjectCategory.Item, 4, 12, 120).Returns(true);
            await _spawner.InitializeAsync();
            var poolParent = FindPoolParent();
            var initialChildCount = poolParent.childCount;

            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            GetSingleActiveDrop(poolParent).GetComponent<HorrorDropItemInteractable>().Interact();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.one));

            Assert.That(CountActiveDrops(poolParent), Is.EqualTo(1));
            Assert.That(poolParent.childCount, Is.EqualTo(initialChildCount), "プール再利用ではなく新規生成されています");
        }

        // 返却・破棄：未貸出返却は LogError で顕在化し、Dispose 後は撃破シグナルに反応しない。

        [Test]
        public async Task ReturnToPool_NotRented_LogsError()
        {
            _mockInventory.TryAdd(ObjectCategory.Item, 4, 12, 120).Returns(true);
            await _spawner.InitializeAsync();
            _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero));
            var drop = GetSingleActiveDrop(FindPoolParent()).GetComponent<HorrorDropItemInteractable>();
            _spawner.ReturnToPool(drop);
            LogAssert.Expect(LogType.Error, $"[{nameof(HorrorEnemyDropSpawner)}] 貸出中でないドロップ品が返却されました（二重返却の疑い）");

            _spawner.ReturnToPool(drop);
        }

        [Test]
        public async Task Dispose_UnsubscribesAndDestroysPool()
        {
            await _spawner.InitializeAsync();

            _spawner.Dispose();

            Assert.DoesNotThrow(() => _messagePipe.Publish(new HorrorSignals.Enemy.Died(1, Vector3.zero)));
            Assert.That(GameObject.Find("HorrorDropItemPool"), Is.Null);
            _mockAssets.Received(1).Release(_prefab);
        }

        /// <summary>
        /// 実テーブル + 実 DB を組み立てて mock サービスへ接続する（HorrorEnemySpawnerTests と同じ手法）。
        /// 敵種 10 = 必中グループ1（弾薬 Id=4 × 12 個）、敵種 11 = ドロップなし、敵種 12 = 行なしグループ9（データ齟齬）。
        /// </summary>
        private void SetupDatabase()
        {
            var dropTable = ScriptableObject.CreateInstance<HorrorEnemyDropMasterTable>();
            dropTable.EditorImportRows(
                new[] { "Id", "DropGroupId", "ItemId", "DropRate", "Count" },
                new[] { new[] { "1", "1", "4", "10000", "12" } },
                mergeByPrimaryKey: false);

            var itemTable = ScriptableObject.CreateInstance<HorrorItemMasterTable>();
            itemTable.EditorImportRows(
                new[] { "Id", "ModelAssetName", "MaxCount" },
                new[] { new[] { "4", "TestDropItem", "120" } },
                mergeByPrimaryKey: false);

            var enemyTable = ScriptableObject.CreateInstance<HorrorEnemyMasterTable>();
            enemyTable.EditorImportRows(
                new[] { "Id", "DropGroupId" },
                new[] { new[] { "10", "1" }, new[] { "11", "0" }, new[] { "12", "9" } },
                mergeByPrimaryKey: false);

            var spawnTable = ScriptableObject.CreateInstance<HorrorEnemySpawnMasterTable>();
            spawnTable.EditorImportRows(
                new[] { "Id", "EnemyMasterId", "SpawnGroupId" },
                new[] { new[] { "1", "10", "1" }, new[] { "2", "11", "1" }, new[] { "3", "12", "1" } },
                mergeByPrimaryKey: false);

            var database = ScriptableObject.CreateInstance<ScriptableDatabase>();
            var so = new SerializedObject(database);
            so.FindProperty("horrorEnemyDropMasterTable").objectReferenceValue = dropTable;
            so.FindProperty("horrorItemMasterTable").objectReferenceValue = itemTable;
            so.FindProperty("horrorEnemyMasterTable").objectReferenceValue = enemyTable;
            so.FindProperty("horrorEnemySpawnMasterTable").objectReferenceValue = spawnTable;
            so.ApplyModifiedPropertiesWithoutUndo();

            _createdObjects.Add(dropTable);
            _createdObjects.Add(itemTable);
            _createdObjects.Add(enemyTable);
            _createdObjects.Add(spawnTable);
            _createdObjects.Add(database);

            _mockDbService.Database.Returns(database);
        }

        /// <summary>純関数テスト用の抽選行（DropRate のみ意味を持つ）。</summary>
        private static List<HorrorEnemyDropMaster> Rows(params int[] dropRates)
        {
            var rows = new List<HorrorEnemyDropMaster>(dropRates.Length);
            for (var i = 0; i < dropRates.Length; i++)
                rows.Add(new HorrorEnemyDropMaster { Id = i + 1, DropGroupId = 1, ItemId = 4, DropRate = dropRates[i], Count = 1 });

            return rows;
        }

        private static Transform FindPoolParent()
        {
            var poolParent = GameObject.Find("HorrorDropItemPool");
            Assert.That(poolParent, Is.Not.Null, "プール親 HorrorDropItemPool が見つかりません");
            return poolParent.transform;
        }

        private static int CountActiveDrops(Transform poolParent)
        {
            var count = 0;
            foreach (Transform child in poolParent)
            {
                if (child.gameObject.activeSelf) count++;
            }

            return count;
        }

        private static GameObject GetSingleActiveDrop(Transform poolParent)
        {
            foreach (Transform child in poolParent)
            {
                if (child.gameObject.activeSelf) return child.gameObject;
            }

            Assert.Fail("アクティブなドロップ品が見つかりません");
            return null;
        }
    }
}
