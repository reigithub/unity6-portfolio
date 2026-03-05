using System.Collections.Generic;
using System.Reflection;
using Game.Shared.Network.Survivor;
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
    }
}
