using System.Reflection;
using Game.Horror.Interaction;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.MVC.Horror.Interaction
{
    [TestFixture]
    public class InteractionPromptViewTests
    {
        // 深度が2倍ならスケールも2倍（距離比例で見かけサイズが相殺される）
        // スケール計算はワールド空間実装（バックアップ）側にのみ残る。ScreenSpace 版は距離非依存のため対象外。
        [Test]
        public void Scale_IsProportionalToDepth()
        {
            float near = WorldSpaceInteractionPromptView.CalculateUniformLocalScale(2f, 60f, 0.05f, 1f);
            float far = WorldSpaceInteractionPromptView.CalculateUniformLocalScale(4f, 60f, 0.05f, 1f);
            Assert.AreEqual(near * 2f, far, 1e-4f);
        }

        // 親 lossyScale が2倍なら、最終ワールドスケールを保つため localScale は半分になる
        [Test]
        public void Scale_CancelsParentLossyScale()
        {
            float unit = WorldSpaceInteractionPromptView.CalculateUniformLocalScale(3f, 60f, 0.05f, 1f);
            float scaled = WorldSpaceInteractionPromptView.CalculateUniformLocalScale(3f, 60f, 0.05f, 2f);
            Assert.AreEqual(unit / 2f, scaled, 1e-4f);
        }

        // 既知の fov/depth/factor で期待値に一致（fov=90°,depth=1 → worldHeight=2、factor=0.1 → 0.2）
        [Test]
        public void Scale_MatchesExpectedValue()
        {
            float scale = WorldSpaceInteractionPromptView.CalculateUniformLocalScale(1f, 90f, 0.1f, 1f);
            Assert.AreEqual(0.2f, scale, 1e-4f);
        }

        // z > 0（カメラ前方への射影深度が正）は表示可能
        [Test]
        public void IsInFrontOfCamera_PositiveZ_ReturnsTrue()
        {
            Assert.That(InteractionPromptView.IsInFrontOfCamera(new Vector3(100f, 200f, 5f)), Is.True);
        }

        // z == 0 はカメラ平面上＝背後扱い（非表示）
        [Test]
        public void IsInFrontOfCamera_ZeroZ_ReturnsFalse()
        {
            Assert.That(InteractionPromptView.IsInFrontOfCamera(new Vector3(100f, 200f, 0f)), Is.False);
        }

        // z < 0 はカメラ背後（非表示）
        [Test]
        public void IsInFrontOfCamera_NegativeZ_ReturnsFalse()
        {
            Assert.That(InteractionPromptView.IsInFrontOfCamera(new Vector3(100f, 200f, -5f)), Is.False);
        }

        // _holdGauge（private）を注入し、SetHoldProgress の表示/非表示・fillAmount を検証する
        // ルートは RectTransform 必須（Awake で transform を RectTransform へキャストするため）
        private static (InteractionPromptView view, Image gauge) CreateViewWithGauge()
        {
            var viewGo = new GameObject("PromptView", typeof(RectTransform));
            var view = viewGo.AddComponent<InteractionPromptView>();

            var gaugeGo = new GameObject("HoldGauge");
            gaugeGo.transform.SetParent(viewGo.transform);
            var image = gaugeGo.AddComponent<Image>();

            typeof(InteractionPromptView)
                .GetField("_holdGauge", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(view, image);

            return (view, image);
        }

        // 進捗 > 0：ゲージを表示し fillAmount に進捗を反映する
        [Test]
        public void SetHoldProgress_Positive_ShowsGaugeAndSetsFill()
        {
            var (view, gauge) = CreateViewWithGauge();
            try
            {
                view.SetHoldProgress(0.5f);
                Assert.That(gauge.gameObject.activeSelf, Is.True);
                Assert.That(gauge.fillAmount, Is.EqualTo(0.5f).Within(1e-4f));
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        // 進捗 0：中断・完了とみなしてゲージを非表示にする
        [Test]
        public void SetHoldProgress_Zero_HidesGauge()
        {
            var (view, gauge) = CreateViewWithGauge();
            try
            {
                view.SetHoldProgress(0.5f); // 一旦表示
                view.SetHoldProgress(0f);
                Assert.That(gauge.gameObject.activeSelf, Is.False);
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }

        // 進捗 > 1：fillAmount は 1 にクランプされる
        [Test]
        public void SetHoldProgress_AboveOne_ClampsFill()
        {
            var (view, gauge) = CreateViewWithGauge();
            try
            {
                view.SetHoldProgress(1.5f);
                Assert.That(gauge.fillAmount, Is.EqualTo(1f));
            }
            finally { Object.DestroyImmediate(view.gameObject); }
        }
    }
}
