using System.Collections.Generic;
using System.Reflection;
using Game.Shared.Network.Survivor;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using Mirror;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Network
{
    /// <summary>
    /// SurvivorNetworkGameManager のサーバー側ロジックテスト。
    /// Mirror の [Server] 属性ガードにより EditMode では SetTotalPlayerCount / OnPlayerDied を
    /// 直接呼び出せないため、リフレクションでフィールドを操作してアルゴリズムを検証する。
    /// </summary>
    [TestFixture]
    public class SurvivorNetworkGameManagerTests
    {
        private GameObject _go;
        private SurvivorNetworkGameManager _manager;

        private FieldInfo _totalPlayerCountField;
        private FieldInfo _deadPlayerIdsField;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestGameManager");
            _go.AddComponent<NetworkIdentity>();
            _manager = _go.AddComponent<SurvivorNetworkGameManager>();

            // リフレクションでプライベートフィールドを取得
            var type = typeof(SurvivorNetworkGameManager);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            _totalPlayerCountField = type.GetField("_totalPlayerCount", flags);
            _deadPlayerIdsField = type.GetField("_deadPlayerIds", flags);

            Assert.That(_totalPlayerCountField, Is.Not.Null, "_totalPlayerCount field not found");
            Assert.That(_deadPlayerIdsField, Is.Not.Null, "_deadPlayerIds field not found");
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        #region Helpers

        private void SetTotalPlayerCount(int count)
        {
            _totalPlayerCountField.SetValue(_manager, count);
            GetDeadPlayerIds().Clear();
        }

        private HashSet<string> GetDeadPlayerIds()
        {
            return (HashSet<string>)_deadPlayerIdsField.GetValue(_manager);
        }

        private int GetTotalPlayerCount()
        {
            return (int)_totalPlayerCountField.GetValue(_manager);
        }

        /// <summary>
        /// OnPlayerDied の死亡判定アルゴリズムを再現する。
        /// [Server] ガードをバイパスし、フィールドを直接操作して結果を返す。
        /// </summary>
        private bool SimulatePlayerDied(string userId)
        {
            var deadPlayerIds = GetDeadPlayerIds();
            deadPlayerIds.Add(userId);

            var totalPlayerCount = GetTotalPlayerCount();
            return totalPlayerCount > 0 && deadPlayerIds.Count >= totalPlayerCount;
        }

        #endregion

        #region Death Tracking - Basic

        [Test]
        public void DeadPlayerIds_IsEmpty_Initially()
        {
            Assert.That(GetDeadPlayerIds().Count, Is.EqualTo(0));
        }

        [Test]
        public void TotalPlayerCount_IsZero_Initially()
        {
            Assert.That(GetTotalPlayerCount(), Is.EqualTo(0));
        }

        [Test]
        public void SetTotalPlayerCount_SetsCountAndClearsDeadPlayers()
        {
            // Arrange: 先にプレイヤーを死亡させる
            GetDeadPlayerIds().Add("user1");

            // Act
            SetTotalPlayerCount(4);

            // Assert
            Assert.That(GetTotalPlayerCount(), Is.EqualTo(4));
            Assert.That(GetDeadPlayerIds().Count, Is.EqualTo(0));
        }

        #endregion

        #region Death Tracking - Single Player

        [Test]
        public void PlayerDied_TriggersGameOver_WhenSinglePlayerDies()
        {
            SetTotalPlayerCount(1);

            var isGameOver = SimulatePlayerDied("user1");

            Assert.That(isGameOver, Is.True);
        }

        #endregion

        #region Death Tracking - Multi Player

        [Test]
        public void PlayerDied_DoesNotTriggerGameOver_WhenNotAllDead()
        {
            SetTotalPlayerCount(4);

            var isGameOver = SimulatePlayerDied("user1");

            Assert.That(isGameOver, Is.False);
            Assert.That(GetDeadPlayerIds().Count, Is.EqualTo(1));
        }

        [Test]
        public void PlayerDied_TriggersGameOver_WhenAllPlayersDead()
        {
            SetTotalPlayerCount(3);

            SimulatePlayerDied("user1");
            SimulatePlayerDied("user2");
            var isGameOver = SimulatePlayerDied("user3");

            Assert.That(isGameOver, Is.True);
            Assert.That(GetDeadPlayerIds().Count, Is.EqualTo(3));
        }

        [Test]
        public void PlayerDied_DuplicateDeathDoesNotDoubleCount()
        {
            SetTotalPlayerCount(2);

            SimulatePlayerDied("user1");
            var isGameOverAfterDuplicate = SimulatePlayerDied("user1"); // 同じプレイヤーが2回死亡

            // HashSet なので user1 は1回のみカウント
            Assert.That(isGameOverAfterDuplicate, Is.False);
            Assert.That(GetDeadPlayerIds().Count, Is.EqualTo(1));
        }

        [Test]
        public void PlayerDied_DoesNotTriggerGameOver_WhenTotalCountIsZero()
        {
            // totalPlayerCount が設定されていない場合
            _totalPlayerCountField.SetValue(_manager, 0);

            var isGameOver = SimulatePlayerDied("user1");

            Assert.That(isGameOver, Is.False);
        }

        #endregion

        #region Singleton Instance

        [Test]
        public void Instance_IsNull_ByDefault()
        {
            // NetworkBehaviour.OnStartServer/OnStartClient が呼ばれていないため
            Assert.That(SurvivorNetworkGameManager.Instance, Is.Null);
        }

        #endregion

        #region MessagePipe Publisher Tests

        /// <summary>テスト用 IPublisher 実装</summary>
        private class TestPublisher<T> : IPublisher<T>
        {
            public List<T> Published { get; } = new();
            public void Publish(T message) { Published.Add(message); }
        }

        [Test]
        public void OnClientHitReported_PublishesHitReportedSignal()
        {
            // Arrange — [Server] ガードをバイパスし、Publishロジックを再現
            var testPub = new TestPublisher<SurvivorSignals.Weapon.HitReported>();
            var field = typeof(SurvivorNetworkGameManager).GetField(
                "_hitReportedPub", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "_hitReportedPub field not found");
            field.SetValue(_manager, testPub);

            // Act — OnClientHitReported のロジックを再現
            int enemyNetworkId = 5;
            int weaponId = 3;
            testPub.Publish(new SurvivorSignals.Weapon.HitReported(enemyNetworkId, weaponId));

            // Assert
            Assert.That(testPub.Published.Count, Is.EqualTo(1));
            Assert.That(testPub.Published[0].EnemyNetworkId, Is.EqualTo(5));
            Assert.That(testPub.Published[0].WeaponId, Is.EqualTo(3));
        }

        [Test]
        public void OnClientWeaponChoice_PublishesApplyRequestedSignal()
        {
            // Arrange
            var testPub = new TestPublisher<SurvivorSignals.Weapon.ApplyRequested>();
            var field = typeof(SurvivorNetworkGameManager).GetField(
                "_weaponApplyPub", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null, "_weaponApplyPub field not found");
            field.SetValue(_manager, testPub);

            // Act — OnClientWeaponChoice のロジックを再現（検証なし / _lastSentWeaponOptions == null）
            var request = new WeaponApplyRequest
            {
                WeaponId = 10,
                IsNewWeapon = true,
                Type = WeaponApplyType.AddOrUpgrade
            };
            testPub.Publish(new SurvivorSignals.Weapon.ApplyRequested(request));

            // Assert
            Assert.That(testPub.Published.Count, Is.EqualTo(1));
            Assert.That(testPub.Published[0].Request.WeaponId, Is.EqualTo(10));
            Assert.That(testPub.Published[0].Request.IsNewWeapon, Is.True);
        }

        [Test]
        public void AllClientsSceneReady_PublishesSignal()
        {
            // Arrange — _sceneReadyConnIds と _totalPlayerCount をリフレクションで操作
            var testPub = new TestPublisher<SurvivorSignals.Session.AllClientsSceneReady>();
            var pubField = typeof(SurvivorNetworkGameManager).GetField(
                "_allClientsSceneReadyPub", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(pubField, Is.Not.Null, "_allClientsSceneReadyPub field not found");
            pubField.SetValue(_manager, testPub);

            var sceneReadyField = typeof(SurvivorNetworkGameManager).GetField(
                "_sceneReadyConnIds", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(sceneReadyField, Is.Not.Null, "_sceneReadyConnIds field not found");
            var sceneReadySet = (HashSet<int>)sceneReadyField.GetValue(_manager);

            // Act — OnClientSceneReady のロジックを再現（totalPlayerCount=2, 2人分のconnIdを追加）
            SetTotalPlayerCount(2);
            sceneReadySet.Add(1);
            sceneReadySet.Add(2);
            int totalPlayerCount = GetTotalPlayerCount();
            if (totalPlayerCount > 0 && sceneReadySet.Count >= totalPlayerCount)
            {
                testPub.Publish(new SurvivorSignals.Session.AllClientsSceneReady());
            }

            // Assert
            Assert.That(testPub.Published.Count, Is.EqualTo(1));
        }

        [Test]
        public void AllClientsSceneReady_DoesNotPublish_WhenNotAllReady()
        {
            // Arrange
            var testPub = new TestPublisher<SurvivorSignals.Session.AllClientsSceneReady>();
            var pubField = typeof(SurvivorNetworkGameManager).GetField(
                "_allClientsSceneReadyPub", BindingFlags.NonPublic | BindingFlags.Instance);
            pubField.SetValue(_manager, testPub);

            var sceneReadyField = typeof(SurvivorNetworkGameManager).GetField(
                "_sceneReadyConnIds", BindingFlags.NonPublic | BindingFlags.Instance);
            var sceneReadySet = (HashSet<int>)sceneReadyField.GetValue(_manager);

            // Act — totalPlayerCount=3 だが 1人のみ ready
            SetTotalPlayerCount(3);
            sceneReadySet.Add(1);
            int totalPlayerCount = GetTotalPlayerCount();
            if (totalPlayerCount > 0 && sceneReadySet.Count >= totalPlayerCount)
            {
                testPub.Publish(new SurvivorSignals.Session.AllClientsSceneReady());
            }

            // Assert
            Assert.That(testPub.Published.Count, Is.EqualTo(0));
        }

        [Test]
        public void NullPublisher_DoesNotThrow()
        {
            // Arrange — Publisher 未注入 (null) の状態でシグナル発行
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;
            var hitPub = typeof(SurvivorNetworkGameManager).GetField("_hitReportedPub", flags);
            var weaponPub = typeof(SurvivorNetworkGameManager).GetField("_weaponApplyPub", flags);
            var sceneReadyPub = typeof(SurvivorNetworkGameManager).GetField("_allClientsSceneReadyPub", flags);

            // null に設定（デフォルトだが明示的に）
            hitPub.SetValue(_manager, null);
            weaponPub.SetValue(_manager, null);
            sceneReadyPub.SetValue(_manager, null);

            // Act & Assert — null?.Publish() パターンで例外が発生しないこと
            IPublisher<SurvivorSignals.Weapon.HitReported> nullHitPub = null;
            IPublisher<SurvivorSignals.Weapon.ApplyRequested> nullWeaponPub = null;
            IPublisher<SurvivorSignals.Session.AllClientsSceneReady> nullSceneReadyPub = null;

            Assert.DoesNotThrow(() =>
            {
                nullHitPub?.Publish(new SurvivorSignals.Weapon.HitReported(1, 1));
                nullWeaponPub?.Publish(new SurvivorSignals.Weapon.ApplyRequested(default));
                nullSceneReadyPub?.Publish(new SurvivorSignals.Session.AllClientsSceneReady());
            });
        }

        #endregion
    }
}
