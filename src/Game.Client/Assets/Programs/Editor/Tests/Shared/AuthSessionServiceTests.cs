using System;
using Game.Shared.SaveData;
using Game.Shared.Services;
using NSubstitute;
using NUnit.Framework;

namespace Game.Tests.Shared.Services
{
    /// <summary>
    /// <see cref="AuthSessionService"/> の unit test。
    /// 主に [D-Phase 1.5] で追加された時間差 refresh 判定 primitives
    /// (<see cref="IAuthSessionService.LastRefreshedAt"/>,
    ///  <see cref="IAuthSessionService.IsRecentlyRefreshed"/>,
    ///  <see cref="IAuthSessionService.MarkRefreshed"/>) を検証する。
    /// </summary>
    [TestFixture]
    public class AuthSessionServiceTests
    {
        private ISessionSaveDataStorage _mockStorage;
        private AuthSessionService _service;

        [SetUp]
        public void Setup()
        {
            _mockStorage = Substitute.For<ISessionSaveDataStorage>();
            _service = new AuthSessionService(_mockStorage);
        }

        [Test]
        public void LastRefreshedAt_Initially_IsNull()
        {
            Assert.IsNull(_service.LastRefreshedAt);
        }

        [Test]
        public void MarkRefreshed_SetsLastRefreshedAtToCurrentUtcTime()
        {
            var before = DateTime.UtcNow;
            _service.MarkRefreshed();
            var after = DateTime.UtcNow;

            Assert.IsNotNull(_service.LastRefreshedAt);
            Assert.GreaterOrEqual(_service.LastRefreshedAt.Value, before);
            Assert.LessOrEqual(_service.LastRefreshedAt.Value, after);
        }

        [Test]
        public void IsRecentlyRefreshed_WithoutMark_ReturnsFalse()
        {
            // 初期 state では LastRefreshedAt == null → false
            Assert.IsFalse(_service.IsRecentlyRefreshed(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public void IsRecentlyRefreshed_WithinThreshold_ReturnsTrue()
        {
            _service.MarkRefreshed();

            // 十分大きい threshold で true を保証 (test latency に耐える)
            Assert.IsTrue(_service.IsRecentlyRefreshed(TimeSpan.FromMinutes(1)));
        }

        [Test]
        public void IsRecentlyRefreshed_Parameterless_UsesDefaultThreshold()
        {
            // 直後に呼べば default threshold (30 秒) 以内なので true
            _service.MarkRefreshed();
            Assert.IsTrue(_service.IsRecentlyRefreshed());

            // MarkRefreshed 呼び出し前は null → false
            var freshService = new AuthSessionService(_mockStorage);
            Assert.IsFalse(freshService.IsRecentlyRefreshed());
        }
    }
}
