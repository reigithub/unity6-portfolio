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

        // ズーム係数：scrollDelta 0 は現在値維持、それ以外は符号に応じて zoomStep 分増減し min〜max にクランプ。

        [Test]
        public void CalculateZoom_ZeroScroll_KeepsCurrent()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1f, 0f, 0.1f, 0.5f, 2f), Is.EqualTo(1f));

        [Test]
        public void CalculateZoom_PositiveScroll_DecreasesByStep()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1f, 1f, 0.1f, 0.5f, 2f), Is.EqualTo(0.9f).Within(1e-4f));

        [Test]
        public void CalculateZoom_NegativeScroll_IncreasesByStep()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1f, -1f, 0.1f, 0.5f, 2f), Is.EqualTo(1.1f).Within(1e-4f));

        [Test]
        public void CalculateZoom_BelowMin_ClampsToMin()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(0.55f, 1f, 0.1f, 0.5f, 2f), Is.EqualTo(0.5f).Within(1e-4f));

        [Test]
        public void CalculateZoom_AboveMax_ClampsToMax()
            => Assert.That(HorrorItemPreviewView.CalculateZoom(1.95f, -1f, 0.1f, 0.5f, 2f), Is.EqualTo(2f).Within(1e-4f));

        // RenderTexture の幅：描画先の縦横比に合わせる（高さ基準）。サイズ未確定なら正方形へフォールバック。

        [Test]
        public void CalculateTextureWidth_WideRect_MatchesAspect()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(1920f, 1080f, 1024), Is.EqualTo(1820));

        [Test]
        public void CalculateTextureWidth_SquareRect_ReturnsTextureSize()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(1024f, 1024f, 1024), Is.EqualTo(1024));

        [Test]
        public void CalculateTextureWidth_ZeroWidth_FallsBackToSquare()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(0f, 1080f, 1024), Is.EqualTo(1024));

        [Test]
        public void CalculateTextureWidth_ZeroHeight_FallsBackToSquare()
            => Assert.That(HorrorItemPreviewView.CalculateTextureWidth(1920f, 0f, 1024), Is.EqualTo(1024));
    }
}
