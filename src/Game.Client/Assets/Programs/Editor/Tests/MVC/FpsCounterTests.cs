using Game.Core.UI;
using NUnit.Framework;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class FpsCounterTests
    {
        [Test]
        public void CalculateFps_OneSecond_ReturnsFrameCount()
        {
            Assert.AreEqual(60f, FpsCounter.CalculateFps(60, 1f));
        }

        [Test]
        public void CalculateFps_HalfSecond_ScalesToPerSecond()
        {
            Assert.AreEqual(60f, FpsCounter.CalculateFps(30, 0.5f));
        }

        [Test]
        public void CalculateFps_ZeroElapsed_ReturnsZero()
        {
            Assert.AreEqual(0f, FpsCounter.CalculateFps(0, 0f));
        }
    }
}
