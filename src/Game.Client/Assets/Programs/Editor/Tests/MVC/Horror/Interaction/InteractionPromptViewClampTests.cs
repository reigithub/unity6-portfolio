using Game.Horror.Interaction;
using Game.Shared.Enums;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.MVC.Horror.Interaction
{
    /// <summary>
    /// <see cref="InteractionPromptView.CalculateClampedPosition"/> /
    /// <see cref="InteractionPromptView.GetArrowPlacement"/> の純関数検証。
    /// クランプ矩形は [margin, screenSize - margin]、境界含む側は非クランプという仕様をここで固定する。
    /// </summary>
    [TestFixture]
    public class InteractionPromptViewClampTests
    {
        // 基本条件: 1920x1080・マージン100。クランプ矩形は [100,100]-[1820,980]、中心 (960,540)
        private static readonly Vector2 ScreenSize = new(1920f, 1080f);
        private static readonly Vector2 Margin = new(100f, 100f);

        private static Vector3 Calculate(Vector3 screenPoint, out InteractionPromptArrow arrow)
            => InteractionPromptView.CalculateClampedPosition(screenPoint, ScreenSize, Margin, out arrow);

        // 矩形内は位置そのまま（z のみ 0 化）・矢印なし
        [Test]
        public void Clamp_InsideRect_ReturnsUnclamped()
        {
            var result = Calculate(new Vector3(960f, 540f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(960f, 540f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.None));
        }

        // マージン境界ちょうどは矩形内（境界含む）＝非クランプ
        [Test]
        public void Clamp_OnMarginBoundary_ReturnsUnclamped()
        {
            var result = Calculate(new Vector3(100f, 540f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(100f, 540f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.None));
        }

        // 左外: x を左辺へクランプ、はみ出していない y は保持
        [Test]
        public void Clamp_LeftOutside_ClampsToLeftEdge()
        {
            var result = Calculate(new Vector3(-50f, 540f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(100f, 540f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Left));
        }

        // 右外
        [Test]
        public void Clamp_RightOutside_ClampsToRightEdge()
        {
            var result = Calculate(new Vector3(2000f, 540f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(1820f, 540f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Right));
        }

        // 上外
        [Test]
        public void Clamp_TopOutside_ClampsToTopEdge()
        {
            var result = Calculate(new Vector3(960f, 1200f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(960f, 980f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Up));
        }

        // 下外（足元アイテムの主ケース）
        [Test]
        public void Clamp_BottomOutside_ClampsToBottomEdge()
        {
            var result = Calculate(new Vector3(960f, -50f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(960f, 100f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Down));
        }

        // 下辺クランプ中も x は対象へ追従してスライドする（辺上の位置保持）
        [Test]
        public void Clamp_BottomOutside_KeepsHorizontalTracking()
        {
            var result = Calculate(new Vector3(500f, -50f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(500f, 100f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Down));
        }

        // コーナー: はみ出しの大きい軸（x: 600 > y: 200）の辺が矢印になる
        [Test]
        public void Clamp_CornerXDominant_ArrowIsHorizontal()
        {
            var result = Calculate(new Vector3(-500f, -100f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(100f, 100f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Left));
        }

        // コーナー: はみ出しの大きい軸（y: 600 > x: 200）の辺が矢印になる
        [Test]
        public void Clamp_CornerYDominant_ArrowIsVertical()
        {
            var result = Calculate(new Vector3(-100f, -500f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(100f, 100f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Down));
        }

        // コーナー等はみ出し（300/300）は垂直優先（主用途の足元=下方向が勝つタイブレーク仕様の固定）
        [Test]
        public void Clamp_CornerEqualOvershoot_PrefersVertical()
        {
            var result = Calculate(new Vector3(-200f, -200f, 5f), out var arrow);
            Assert.That(result, Is.EqualTo(new Vector3(100f, 100f, 0f)));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Down));
        }

        // カメラ背後は点対称反転で正しい側の辺へ出る。
        // 入力 (960,1200) は反転後 (960,-120) → 下辺、x=960 は保持相当
        [Test]
        public void Clamp_BehindCamera_InvertsToCorrectEdge()
        {
            var result = Calculate(new Vector3(960f, 1200f, -1f), out var arrow);
            Assert.That(result.x, Is.EqualTo(960f).Within(1e-3f));
            Assert.That(result.y, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(result.z, Is.Zero);
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Down));
        }

        // 真後ろ（反転後が画面中心に一致）は既定で下辺中央（足元・背後が主用途）
        [Test]
        public void Clamp_DirectlyBehind_DefaultsToBottomCenter()
        {
            var result = Calculate(new Vector3(960f, 540f, -1f), out var arrow);
            Assert.That(result.x, Is.EqualTo(960f).Within(1e-3f));
            Assert.That(result.y, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Down));
        }

        // 背後・斜め: 中心→対象方向レイと矩形境界の交点に載る（座標が矩形を出ない）。
        // 入力 (0,0) は反転後 (1920,1080)、方向 (960,540) → 上辺 y=980 が先に当たる
        [Test]
        public void Clamp_BehindDiagonal_LandsOnRectBoundary()
        {
            var result = Calculate(new Vector3(0f, 0f, -1f), out var arrow);
            Assert.That(result.y, Is.EqualTo(980f).Within(1e-3f));
            Assert.That(result.x, Is.InRange(100f, 1820f));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Up));
        }

        // z == 0（カメラ平面上）は背後扱い（IsInFrontOfCamera と相補の境界定義）
        [Test]
        public void Clamp_ZeroZ_TreatedAsBehind()
        {
            var result = Calculate(new Vector3(960f, 1200f, 0f), out var arrow);
            Assert.That(result.y, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(arrow, Is.EqualTo(InteractionPromptArrow.Down));
        }

        // 矢印配置の検証ヘルパー。internal 型をシグネチャに含むため private とし、
        // public テストメソッド（NUnit 要件）とのアクセシビリティ不一致（CS0051）を避ける
        private static void AssertArrowPlacement(
            InteractionPromptArrow arrow, Vector2 expectedPosition, float expectedRotation)
        {
            InteractionPromptView.GetArrowPlacement(arrow, 90f, out var position, out var rotation);
            Assert.That(position, Is.EqualTo(expectedPosition));
            Assert.That(rotation, Is.EqualTo(expectedRotation));
        }

        // 矢印配置: 4 方向のオフセット位置と z 回転（上向きスプライト基準）
        [Test]
        public void ArrowPlacement_Up() => AssertArrowPlacement(InteractionPromptArrow.Up, new Vector2(0f, 90f), 0f);

        [Test]
        public void ArrowPlacement_Down() => AssertArrowPlacement(InteractionPromptArrow.Down, new Vector2(0f, -90f), 180f);

        [Test]
        public void ArrowPlacement_Left() => AssertArrowPlacement(InteractionPromptArrow.Left, new Vector2(-90f, 0f), 90f);

        [Test]
        public void ArrowPlacement_Right() => AssertArrowPlacement(InteractionPromptArrow.Right, new Vector2(90f, 0f), 270f);

        // None は原点・無回転（呼び出し側で非表示にする契約）
        [Test]
        public void ArrowPlacement_None_ReturnsIdentity()
        {
            InteractionPromptView.GetArrowPlacement(InteractionPromptArrow.None, 90f, out var position, out var rotation);
            Assert.That(position, Is.EqualTo(Vector2.zero));
            Assert.That(rotation, Is.Zero);
        }
    }
}
