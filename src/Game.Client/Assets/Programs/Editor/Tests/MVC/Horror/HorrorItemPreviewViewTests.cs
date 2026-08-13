using Game.Horror.Dialogs;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror
{
    [TestFixture]
    public class HorrorItemPreviewViewTests
    {
        // フィットスケール：targetSize / 最長辺。最長辺が 0 以下（Renderer 無し等）はゼロ除算ガードで 1。

        [Test]
        public void CalculateFitScale_ZeroBounds_IsOne()
            => Assert.That(HorrorItemPreviewView.CalculateFitScale(Vector3.zero, 1f), Is.EqualTo(1f));

        [Test]
        public void CalculateFitScale_UniformBounds_ReturnsHalf()
            => Assert.That(HorrorItemPreviewView.CalculateFitScale(new Vector3(2f, 2f, 2f), 1f), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void CalculateFitScale_LongestOnX_UsesLongestSide()
            => Assert.That(HorrorItemPreviewView.CalculateFitScale(new Vector3(4f, 1f, 1f), 1f), Is.EqualTo(0.25f).Within(1e-4f));

        [Test]
        public void CalculateFitScale_LongestOnY_UsesLongestSide()
            => Assert.That(HorrorItemPreviewView.CalculateFitScale(new Vector3(1f, 3f, 2f), 1f), Is.EqualTo(1f / 3f).Within(1e-4f));

        // ズーム係数：変化量を指数で適用し min〜max にクランプ。正の入力＝拡大＝係数を減らす。

        [Test]
        public void CalculateZoom_ZeroDelta_KeepsCurrent()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1f, 0f, 0.5f, 2f), Is.EqualTo(1f).Within(1e-4f));

        [Test]
        public void CalculateZoom_PositiveDelta_DecreasesZoom()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1f, 0.1f, 0.5f, 2f), Is.EqualTo(0.9048f).Within(1e-4f));

        [Test]
        public void CalculateZoom_NegativeDelta_IncreasesZoom()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1f, -0.1f, 0.5f, 2f), Is.EqualTo(1.1052f).Within(1e-4f));

        // 現在値が違っても同じ変化量なら同じ倍率になる（拡大側でも縮小側でも効きが一定）。
        [Test]
        public void CalculateZoom_SameDelta_KeepsConstantRatio()
        {
            var ratioAtOne = HorrorItemPreviewView.CalculateZoom(1f, 0.1f, 0.5f, 2f) / 1f;
            var ratioAtOneAndHalf = HorrorItemPreviewView.CalculateZoom(1.5f, 0.1f, 0.5f, 2f) / 1.5f;
            Assert.That(ratioAtOneAndHalf, Is.EqualTo(ratioAtOne).Within(1e-4f));
        }

        // 変化量が2倍なら倍率は2乗になる（入力量が結果へ反映される）。
        [Test]
        public void CalculateZoom_DoubleDelta_SquaresRatio()
        {
            var single = HorrorItemPreviewView.CalculateZoom(1f, 0.1f, 0.5f, 2f);
            var doubled = HorrorItemPreviewView.CalculateZoom(1f, 0.2f, 0.5f, 2f);
            Assert.That(doubled, Is.EqualTo(single * single).Within(1e-4f));
        }

        [Test]
        public void CalculateZoom_BelowMin_ClampsToMin()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(0.55f, 1f, 0.5f, 2f), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void CalculateZoom_AboveMax_ClampsToMax()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1.95f, -1f, 0.5f, 2f), Is.EqualTo(2f).Within(1e-4f));

        // RenderTexture の幅：縦横比に対応する幅（高さ基準）。1px 未満には潰さない。

        [Test]
        public void CalculateTextureWidth_WideAspect_MatchesAspect()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(1920f / 1080f, 1024), Is.EqualTo(1820));

        [Test]
        public void CalculateTextureWidth_SquareAspect_ReturnsTextureSize()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(1f, 1024), Is.EqualTo(1024));

        [Test]
        public void CalculateTextureWidth_TallAspect_ReturnsNarrowerWidth()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(9f / 16f, 1024), Is.EqualTo(576));

        [Test]
        public void CalculateTextureWidth_NonPositiveAspect_ClampsToOne()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(0f, 1024), Is.EqualTo(1));
    }
}
