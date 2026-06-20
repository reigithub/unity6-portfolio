using NUnit.Framework;
using Game.Shared.Interaction;

namespace Game.Tests.Shared.Interaction
{
    [TestFixture]
    public class InteractionPromptViewTests
    {
        // 深度が2倍ならスケールも2倍（距離比例で見かけサイズが相殺される）
        [Test]
        public void Scale_IsProportionalToDepth()
        {
            float near = InteractionPromptView.CalculateUniformLocalScale(2f, 60f, 0.05f, 1f);
            float far = InteractionPromptView.CalculateUniformLocalScale(4f, 60f, 0.05f, 1f);
            Assert.AreEqual(near * 2f, far, 1e-4f);
        }

        // 親 lossyScale が2倍なら、最終ワールドスケールを保つため localScale は半分になる
        [Test]
        public void Scale_CancelsParentLossyScale()
        {
            float unit = InteractionPromptView.CalculateUniformLocalScale(3f, 60f, 0.05f, 1f);
            float scaled = InteractionPromptView.CalculateUniformLocalScale(3f, 60f, 0.05f, 2f);
            Assert.AreEqual(unit / 2f, scaled, 1e-4f);
        }

        // 既知の fov/depth/factor で期待値に一致（fov=90°,depth=1 → worldHeight=2、factor=0.1 → 0.2）
        [Test]
        public void Scale_MatchesExpectedValue()
        {
            float scale = InteractionPromptView.CalculateUniformLocalScale(1f, 90f, 0.1f, 1f);
            Assert.AreEqual(0.2f, scale, 1e-4f);
        }
    }
}
