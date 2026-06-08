using Game.Horror.SaveData;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorOptionHelperTests
    {
        // フレームレート三者の結合: VSync 有効 or 上限解除 → -1、それ以外は上限値。

        [Test]
        public void ResolveTargetFrameRate_VSyncOn_ReturnsMinusOne()
        {
            Assert.That(HorrorOptionHelper.ResolveTargetFrameRate(vSync: true, uncapped: false, limit: 60), Is.EqualTo(-1));
        }

        [Test]
        public void ResolveTargetFrameRate_Uncapped_ReturnsMinusOne()
        {
            Assert.That(HorrorOptionHelper.ResolveTargetFrameRate(vSync: false, uncapped: true, limit: 60), Is.EqualTo(-1));
        }

        [Test]
        public void ResolveTargetFrameRate_VSyncOnAndUncapped_ReturnsMinusOne()
        {
            Assert.That(HorrorOptionHelper.ResolveTargetFrameRate(vSync: true, uncapped: true, limit: 144), Is.EqualTo(-1));
        }

        [Test]
        public void ResolveTargetFrameRate_Capped_ReturnsLimit()
        {
            Assert.That(HorrorOptionHelper.ResolveTargetFrameRate(vSync: false, uncapped: false, limit: 120), Is.EqualTo(120));
        }
    }
}
