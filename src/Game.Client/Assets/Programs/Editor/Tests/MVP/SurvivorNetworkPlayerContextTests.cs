using System;
using Game.MVP.Survivor.Scenes.Models;
using Game.MVP.Survivor.Weapon;
using NUnit.Framework;

namespace Game.Tests.MVP
{
    /// <summary>
    /// SurvivorNetworkPlayerContext のユニットテスト。
    /// PlayerRef 型は Fusion.Runtime に属しテスト asmdef に直接参照されていないため、
    /// 引数位置で default キーワードを使い型推論に任せる。
    /// </summary>
    [TestFixture]
    public class SurvivorNetworkPlayerContextTests
    {
        private SurvivorStageModel _stageModel;
        private SurvivorNetworkWeaponManager _weaponManager;

        [SetUp]
        public void Setup()
        {
            _stageModel = new SurvivorStageModel();
            _weaponManager = new SurvivorNetworkWeaponManager();
        }

        [TearDown]
        public void TearDown()
        {
            _stageModel?.Dispose();
        }

        [Test]
        public void Constructor_AssignsUserId()
        {
            var ctx = new SurvivorNetworkPlayerContext(default, "user-1", _stageModel, _weaponManager);

            Assert.That(ctx.UserId, Is.EqualTo("user-1"));
        }

        [Test]
        public void Constructor_WithNullUserId_DefaultsToEmpty()
        {
            var ctx = new SurvivorNetworkPlayerContext(default, null, _stageModel, _weaponManager);

            Assert.That(ctx.UserId, Is.EqualTo(string.Empty));
        }

        [Test]
        public void Constructor_WithNullStageModel_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SurvivorNetworkPlayerContext(default, string.Empty, null, _weaponManager));
        }

        [Test]
        public void Constructor_WithNullWeaponManager_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SurvivorNetworkPlayerContext(default, string.Empty, _stageModel, null));
        }

        [Test]
        public void StageModelAndWeaponManager_AreSetFromConstructor()
        {
            var ctx = new SurvivorNetworkPlayerContext(default, string.Empty, _stageModel, _weaponManager);

            Assert.That(ctx.StageModel, Is.SameAs(_stageModel));
            Assert.That(ctx.WeaponManager, Is.SameAs(_weaponManager));
        }

        [Test]
        public void PendingLevelUpCount_DefaultZero()
        {
            var ctx = new SurvivorNetworkPlayerContext(default, string.Empty, _stageModel, _weaponManager);

            Assert.That(ctx.PendingLevelUpCount, Is.EqualTo(0));
        }

        [Test]
        public void IsDead_DefaultFalse()
        {
            var ctx = new SurvivorNetworkPlayerContext(default, string.Empty, _stageModel, _weaponManager);

            Assert.That(ctx.IsDead, Is.False);
        }

        [Test]
        public void Dispose_DoesNotDisposeStageModel()
        {
            // PR2 時点: Scoped モデルの Dispose 責任は VContainer に委譲し、Context は触らない
            var ctx = new SurvivorNetworkPlayerContext(default, string.Empty, _stageModel, _weaponManager);

            ctx.Dispose();

            // StageModel が生きていれば Level.Value にアクセスできる (Dispose 済みなら ObjectDisposedException)
            Assert.DoesNotThrow(() => { var _ = _stageModel.Level.Value; });
        }
    }
}
