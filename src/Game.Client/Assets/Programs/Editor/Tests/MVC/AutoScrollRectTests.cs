using System.Reflection;
using Game.Core.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Tests.MVC
{
    [TestFixture]
    public class AutoScrollRectTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            return rt;
        }

        /// <summary>
        /// 縦 ScrollView を構築（OnEnable を走らせないため root は非アクティブ）。
        /// 実 prefab を模して、行は content 直下ではなく「中間コンテナ」の下にネストし、
        /// 各行に Selectable(Button) を付ける（Rebuild が深さ無関係に Selectable を集めることを検証）。
        /// </summary>
        private AutoScrollRect BuildScrollView(int rowCount, float rowHeight, float viewportHeight,
            out RectTransform viewport, out RectTransform[] rows)
        {
            _root = new GameObject("Root", typeof(RectTransform));
            _root.SetActive(false); // Awake/OnEnable を抑止（InputService 非依存でジオメトリのみ検証）

            const float width = 200f;
            viewport = NewRect("Viewport", _root.transform, new Vector2(width, viewportHeight), Vector2.zero);

            float contentHeight = rowCount * rowHeight;
            float contentStartY = (viewportHeight - contentHeight) / 2f; // row0 を viewport 上端に合わせる
            var content = NewRect("Content", viewport, new Vector2(width, contentHeight), new Vector2(0f, contentStartY));

            // 標準 ScrollView 構造: アイテム（行）は Content の直下の子。
            // 各行は Selectable を「子」に持つカスタムアイテム（Selectable は行 root ではなく子）。
            rows = new RectTransform[rowCount];
            for (int i = 0; i < rowCount; i++)
            {
                float rowCenterY = contentHeight / 2f - rowHeight / 2f - i * rowHeight; // content ローカル
                rows[i] = NewRect($"Row{i}", content, new Vector2(width, rowHeight), new Vector2(0f, rowCenterY));
                rows[i].gameObject.AddComponent<Image>().enabled = false; // 選択カーソル（行 root の白背景。初期は無効）
                var control = NewRect("Control", rows[i], new Vector2(width, rowHeight), Vector2.zero);
                control.gameObject.AddComponent<Button>(); // ナビゲート対象（行の子の Selectable）
            }

            var scrollRect = _root.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.viewport = viewport;
            scrollRect.content = content;

            var autoScroll = _root.AddComponent<AutoScrollRect>();
            // 非アクティブのため Awake は未実行。_scrollRect を直接注入してから収集する。
            typeof(AutoScrollRect)
                .GetField("_scrollRect", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(autoScroll, scrollRect);
            autoScroll.Rebuild();

            // アイテム = Content 直下の子（行）が全て収集されていること
            Assert.AreEqual(rowCount, autoScroll.Items.Count, "アイテム（Content 直下の行）が全て収集されること");

            return autoScroll;
        }

        /// <summary>対象 RectTransform の viewport ローカル Y 範囲を求める（テスト側の検証用）。</summary>
        private static void GetViewportLocalY(RectTransform target, RectTransform viewport, out float min, out float max)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            min = float.PositiveInfinity;
            max = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                var lp = viewport.InverseTransformPoint(corners[i]);
                min = Mathf.Min(min, lp.y);
                max = Mathf.Max(max, lp.y);
            }
        }

        [Test]
        public void ScrollTo_ClippedBottomItem_BecomesFullyVisible()
        {
            // viewport=100, row=40, 3行 → row2 は下にはみ出して見切れる
            var autoScroll = BuildScrollView(3, 40f, 100f, out var viewport, out var rows);

            // 事前条件: row2 は見切れている
            GetViewportLocalY(rows[2], viewport, out var beforeMin, out _);
            Assert.Less(beforeMin, viewport.rect.yMin, "前提: row2 は初期状態で下に見切れているはず");

            autoScroll.ScrollTo(2);

            GetViewportLocalY(rows[2], viewport, out var afterMin, out var afterMax);
            Assert.GreaterOrEqual(afterMin, viewport.rect.yMin - 0.01f, "row2 の下端が viewport 内に収まること");
            Assert.LessOrEqual(afterMax, viewport.rect.yMax + 0.01f, "row2 の上端が viewport を超えないこと");
        }

        [Test]
        public void ScrollTo_AlreadyVisibleItem_DoesNotScroll()
        {
            var autoScroll = BuildScrollView(3, 40f, 100f, out _, out _);
            var content = ((ScrollRect)typeof(AutoScrollRect)
                .GetField("_scrollRect", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(autoScroll)).content;

            var before = content.anchoredPosition;
            autoScroll.ScrollTo(0); // row0 は最初から見えている → 動かないはず

            Assert.AreEqual(before.y, content.anchoredPosition.y, 0.01f, "見えているアイテムへの ScrollTo は最小スクロール（不動）");
        }

        [Test]
        public void ScrollTo_BackToTopItem_KeepsItemFullyVisible()
        {
            var autoScroll = BuildScrollView(3, 40f, 100f, out var viewport, out var rows);

            autoScroll.ScrollTo(2); // 一旦下へ
            autoScroll.ScrollTo(0); // 上の row0 へ戻る

            GetViewportLocalY(rows[0], viewport, out var min, out var max);
            Assert.GreaterOrEqual(min, viewport.rect.yMin - 0.01f, "row0 の下端が viewport 内");
            Assert.LessOrEqual(max, viewport.rect.yMax + 0.01f, "row0 の上端が viewport 内");
        }

        [Test]
        public void Rebuild_AttachesReporterToChildSelectable_WithOwner()
        {
            var autoScroll = BuildScrollView(3, 40f, 100f, out _, out var rows);

            foreach (var row in rows)
            {
                var reporter = row.GetComponentInChildren<AutoScrollItemReporter>();
                Assert.IsNotNull(reporter, "行の子 Selectable に reporter が付与されること");
                Assert.AreSame(autoScroll, reporter.Owner, "reporter.Owner が AutoScrollRect 自身であること");
            }
        }

        [Test]
        public void ReporterOnSelect_ScrollsContainingItemIntoView()
        {
            // 子 Selectable の reporter.OnSelect が呼ばれると、それを含む「アイテム（行）」が viewport 内へスクロールされること
            var autoScroll = BuildScrollView(3, 40f, 100f, out var viewport, out var rows);

            GetViewportLocalY(rows[2], viewport, out var beforeMin, out _);
            Assert.Less(beforeMin, viewport.rect.yMin, "前提: row2 は初期状態で下に見切れているはず");

            // reporter は行 root ではなく「子 Selectable」に付与されている
            var reporter = rows[2].GetComponentInChildren<AutoScrollItemReporter>();
            Assert.AreNotSame(rows[2], reporter.transform, "reporter は行 root ではなく子に付与されていること");
            reporter.OnSelect(null); // 子 Selectable の選択を模擬

            // スクロール対象は内側の Selectable ではなく「行（アイテム）」であること
            GetViewportLocalY(rows[2], viewport, out var afterMin, out var afterMax);
            Assert.GreaterOrEqual(afterMin, viewport.rect.yMin - 0.01f, "row2（アイテム）の下端が viewport 内に収まること");
            Assert.LessOrEqual(afterMax, viewport.rect.yMax + 0.01f, "row2（アイテム）の上端が viewport を超えないこと");
        }
    }
}
