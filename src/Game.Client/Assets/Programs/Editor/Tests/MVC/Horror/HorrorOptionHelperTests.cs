using Game.Horror.SaveData;
using NUnit.Framework;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorOptionHelperTests
    {
        // targetFrameRate は uncapped/limit のみで決まる（VSync の優劣は Unity が裁定）。
        // 上限解除 → -1（無制限）、それ以外は上限値。

        [Test]
        public void ResolveTargetFrameRate_Uncapped_ReturnsMinusOne()
        {
            Assert.That(HorrorOptionHelper.ResolveTargetFrameRate(uncapped: true, limit: 60), Is.EqualTo(-1));
        }

        [Test]
        public void ResolveTargetFrameRate_Capped_ReturnsLimit()
        {
            Assert.That(HorrorOptionHelper.ResolveTargetFrameRate(uncapped: false, limit: 120), Is.EqualTo(120));
        }
    }
}
