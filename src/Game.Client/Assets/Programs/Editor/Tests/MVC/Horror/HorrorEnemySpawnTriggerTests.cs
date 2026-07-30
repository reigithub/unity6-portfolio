using Game.Core.Services;
using Game.Horror.Enemy;
using Game.Horror.Player;
using Game.Horror.Services.Interfaces;
using NSubstitute;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.MVC.Horror
{
    /// <summary>
    /// エネミースポーントリガーの自己無効化とプレイヤー判別。
    /// EditMode では Unity のライフサイクルが走らないため、Start/OnTriggerEnter の実体（internal）を直接呼ぶ。
    /// </summary>
    [TestFixture]
    public class HorrorEnemySpawnTriggerTests
    {
        private IHorrorEnemyService _mockEnemyService;
        private GameObject _triggerGo;
        private HorrorEnemySpawnTrigger _trigger;
        private GameObject _otherGo;

        [SetUp]
        public void Setup()
        {
            GameServiceManager.StartUp();
            _mockEnemyService = Substitute.For<IHorrorEnemyService>();
            GameServiceManager.Register(_mockEnemyService);

            _triggerGo = new GameObject("SpawnTriggerTest");
            _trigger = _triggerGo.AddComponent<HorrorEnemySpawnTrigger>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_otherGo != null) Object.DestroyImmediate(_otherGo);
            if (_triggerGo != null) Object.DestroyImmediate(_triggerGo);
            GameServiceManager.Shutdown();
        }

        private void SetTriggerId(int triggerId)
        {
            var so = new SerializedObject(_trigger);
            so.FindProperty("_triggerId").intValue = triggerId;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private Collider CreateCollider(bool withPlayerController)
        {
            _otherGo = new GameObject("EnteringCollider");
            if (withPlayerController) _otherGo.AddComponent<HorrorPlayerController>();
            return _otherGo.AddComponent<BoxCollider>();
        }

        [Test]
        public void HandleStart_Fired_DeactivatesSelf()
        {
            SetTriggerId(1);
            _mockEnemyService.IsTriggerFired(1).Returns(true);

            _trigger.HandleStart();

            Assert.That(_triggerGo.activeSelf, Is.False);
        }

        [Test]
        public void HandleStart_NotFired_StaysActive()
        {
            SetTriggerId(1);
            _mockEnemyService.IsTriggerFired(1).Returns(false);

            _trigger.HandleStart();

            Assert.That(_triggerGo.activeSelf, Is.True);
        }

        [Test]
        public void HandleStart_TriggerIdZero_LogsErrorAndStaysActive()
        {
            LogAssert.Expect(LogType.Error, "[HorrorEnemySpawnTrigger] SpawnTriggerTest の TriggerId が未設定(0)です");

            _trigger.HandleStart();

            Assert.That(_triggerGo.activeSelf, Is.True);
            _mockEnemyService.DidNotReceive().IsTriggerFired(Arg.Any<int>());
        }

        [Test]
        public void HandleEnter_NonPlayerCollider_DoesNotNotify()
        {
            SetTriggerId(1);
            _trigger.HandleStart();

            _trigger.HandleEnter(CreateCollider(withPlayerController: false));

            _mockEnemyService.DidNotReceive().NotifyTriggerPassed(Arg.Any<int>());
            Assert.That(_triggerGo.activeSelf, Is.True);
        }

        [Test]
        public void HandleEnter_PlayerCollider_NotifiesAndDeactivatesSelf()
        {
            SetTriggerId(1);
            _trigger.HandleStart();

            _trigger.HandleEnter(CreateCollider(withPlayerController: true));

            _mockEnemyService.Received(1).NotifyTriggerPassed(1);
            Assert.That(_triggerGo.activeSelf, Is.False);
        }
    }
}
