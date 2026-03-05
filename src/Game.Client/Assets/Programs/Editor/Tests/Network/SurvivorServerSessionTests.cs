using System.Collections.Generic;
using System.Reflection;
using Game.Shared.Network.Survivor;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Network
{
    /// <summary>
    /// SurvivorUnityServerSession のセッション管理ロジックテスト。
    /// Mirror の NetworkServer コールバックを直接テストできないため、
    /// リフレクションでフィールドを操作してセッションライフサイクルを検証する。
    /// </summary>
    [TestFixture]
    public class SurvivorServerSessionTests
    {
        private GameObject _go;
        private SurvivorUnityServerSession _session;

        private FieldInfo _expectedPlayerCountField;
        private FieldInfo _connectedPlayerCountField;
        private FieldInfo _sessionStartedField;
        private FieldInfo _stageLoadedField;
        private FieldInfo _stageIdField;
        private FieldInfo _connectionUserIdsField;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestServerSession");
            _session = _go.AddComponent<SurvivorUnityServerSession>();

            var type = typeof(SurvivorUnityServerSession);
            var flags = BindingFlags.NonPublic | BindingFlags.Instance;

            _expectedPlayerCountField = type.GetField("_expectedPlayerCount", flags);
            _connectedPlayerCountField = type.GetField("_connectedPlayerCount", flags);
            _sessionStartedField = type.GetField("_sessionStarted", flags);
            _stageLoadedField = type.GetField("_stageLoaded", flags);
            _stageIdField = type.GetField("_stageId", flags);
            _connectionUserIdsField = type.GetField("_connectionUserIds", flags);

            Assert.That(_expectedPlayerCountField, Is.Not.Null, "_expectedPlayerCount field not found");
            Assert.That(_connectedPlayerCountField, Is.Not.Null, "_connectedPlayerCount field not found");
            Assert.That(_sessionStartedField, Is.Not.Null, "_sessionStarted field not found");
            Assert.That(_stageLoadedField, Is.Not.Null, "_stageLoaded field not found");
            Assert.That(_stageIdField, Is.Not.Null, "_stageId field not found");
        }

        [TearDown]
        public void TearDown()
        {
            // StopSession で Mirror コールバック解除を試みるが、EditMode では
            // NetworkServer が動作していないため、手動クリーンアップ
            if (_go != null) Object.DestroyImmediate(_go);

            // シングルトン参照をクリア（他テストへの影響を防止）
            typeof(SurvivorUnityServerSession)
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
                ?.SetValue(null, null);
        }

        #region Helpers

        private int GetExpectedPlayerCount()
        {
            return (int)_expectedPlayerCountField.GetValue(_session);
        }

        private int GetConnectedPlayerCount()
        {
            return (int)_connectedPlayerCountField.GetValue(_session);
        }

        private bool GetSessionStarted()
        {
            return (bool)_sessionStartedField.GetValue(_session);
        }

        private bool GetStageLoaded()
        {
            return (bool)_stageLoadedField.GetValue(_session);
        }

        private int GetStageId()
        {
            return (int)_stageIdField.GetValue(_session);
        }

        /// <summary>
        /// OnClientAuthenticated の状態更新ロジックを再現する。
        /// Mirror の NetworkConnectionToClient を使わず、フィールドを直接操作。
        /// </summary>
        private bool SimulateClientAuthenticated(int stageId, string userId)
        {
            var connectedCount = GetConnectedPlayerCount() + 1;
            _connectedPlayerCountField.SetValue(_session, connectedCount);

            // stageId 設定（初回で確定）
            if (!GetStageLoaded())
            {
                _stageIdField.SetValue(_session, stageId);
                _stageLoadedField.SetValue(_session, true);
            }

            // セッション開始（初回のみ）
            if (!GetSessionStarted() && GetStageLoaded())
            {
                _sessionStartedField.SetValue(_session, true);
            }

            // 全員揃ったか
            return connectedCount >= GetExpectedPlayerCount();
        }

        /// <summary>
        /// OnClientDisconnected の状態更新ロジックを再現する。
        /// </summary>
        private int SimulateClientDisconnected()
        {
            var connectedCount = GetConnectedPlayerCount() - 1;
            _connectedPlayerCountField.SetValue(_session, connectedCount);
            return connectedCount;
        }

        #endregion

        #region StartSession

        [Test]
        public void StartSession_SetsExpectedPlayerCount()
        {
            _session.StartSession(4);

            Assert.That(GetExpectedPlayerCount(), Is.EqualTo(4));
        }

        [Test]
        public void StartSession_DefaultsToSinglePlayer()
        {
            _session.StartSession();

            Assert.That(GetExpectedPlayerCount(), Is.EqualTo(1));
        }

        [Test]
        public void StartSession_ResetsConnectedCount()
        {
            _connectedPlayerCountField.SetValue(_session, 3);

            _session.StartSession(2);

            Assert.That(GetConnectedPlayerCount(), Is.EqualTo(0));
        }

        #endregion

        #region Singleton Instance

        [Test]
        public void Instance_IsSetOnAwake()
        {
            // EditMode では Awake が自動呼び出しされないため、リフレクションで手動起動
            var awakeMethod = typeof(SurvivorUnityServerSession)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            awakeMethod?.Invoke(_session, null);

            Assert.That(SurvivorUnityServerSession.Instance, Is.EqualTo(_session));
        }

        #endregion

        #region Client Authentication Simulation

        [Test]
        public void SimulateAuth_AllPlayersReady_WhenExpectedCountReached()
        {
            _session.StartSession(2);

            var ready1 = SimulateClientAuthenticated(1, "user1");
            var ready2 = SimulateClientAuthenticated(1, "user2");

            Assert.That(ready1, Is.False);
            Assert.That(ready2, Is.True);
        }

        [Test]
        public void SimulateAuth_SinglePlayer_ImmediatelyReady()
        {
            _session.StartSession(1);

            var ready = SimulateClientAuthenticated(1, "user1");

            Assert.That(ready, Is.True);
        }

        [Test]
        public void SimulateAuth_SetsStageId_OnFirstConnection()
        {
            _session.StartSession(2);

            SimulateClientAuthenticated(5, "user1");

            Assert.That(GetStageId(), Is.EqualTo(5));
            Assert.That(GetStageLoaded(), Is.True);
        }

        [Test]
        public void SimulateAuth_DoesNotChangeStageId_OnSubsequentConnections()
        {
            _session.StartSession(2);

            SimulateClientAuthenticated(5, "user1");
            SimulateClientAuthenticated(3, "user2"); // 異なる stageId

            // 最初の接続で確定した stageId が維持される
            Assert.That(GetStageId(), Is.EqualTo(5));
        }

        [Test]
        public void SimulateAuth_StartsSession_OnFirstAuthentication()
        {
            _session.StartSession(2);

            Assert.That(GetSessionStarted(), Is.False);

            SimulateClientAuthenticated(1, "user1");

            Assert.That(GetSessionStarted(), Is.True);
        }

        #endregion

        #region Client Disconnection Simulation

        [Test]
        public void SimulateDisconnect_DecrementsConnectedCount()
        {
            _session.StartSession(2);
            SimulateClientAuthenticated(1, "user1");
            SimulateClientAuthenticated(1, "user2");

            Assert.That(GetConnectedPlayerCount(), Is.EqualTo(2));

            SimulateClientDisconnected();

            Assert.That(GetConnectedPlayerCount(), Is.EqualTo(1));
        }

        [Test]
        public void SimulateDisconnect_ReturnsZero_WhenAllDisconnected()
        {
            _session.StartSession(2);
            SimulateClientAuthenticated(1, "user1");
            SimulateClientAuthenticated(1, "user2");

            SimulateClientDisconnected();
            var remaining = SimulateClientDisconnected();

            Assert.That(remaining, Is.EqualTo(0));
        }

        #endregion

        #region StopSession

        [Test]
        public void StopSession_ResetsSessionState()
        {
            _session.StartSession(2);
            SimulateClientAuthenticated(5, "user1");

            _session.StopSession();

            Assert.That(GetConnectedPlayerCount(), Is.EqualTo(0));
            Assert.That(GetSessionStarted(), Is.False);
            Assert.That(GetStageLoaded(), Is.False);
        }

        #endregion
    }
}
