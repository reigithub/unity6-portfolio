using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Core.UI
{
    /// <summary>
    /// ScrollRect の自動スクロール部品。
    /// content 配下の Selectable を index リストとして保持し、ScrollTo(index) で
    /// 対象が viewport に収まる最小限だけスクロールする。
    ///
    /// トリガー（毎フレーム監視なし）:
    ///   各 Selectable に AutoScrollItemReporter を自動付与し、選択された瞬間（ISelectHandler）に ScrollTo する。
    ///   押しっぱなしのリピート移動も毎回 selectHandler が飛ぶため追従する。
    ///   OnEnable 時のみ端フレームへ遅延したワンショットで Rebuild + 現在選択へ初期スクロール（Dropdown を開いた状態に対応）。
    /// </summary>
    [RequireComponent(typeof(ScrollRect))]
    public class AutoScrollRect : MonoBehaviour
    {
        private const float Epsilon = 0.01f;

        private ScrollRect _scrollRect;
        private readonly List<RectTransform> _items = new();

        private bool _scrollScheduled;

        /// <summary>index リスト（content 配下の Selectable）</summary>
        public IReadOnlyList<RectTransform> Items => _items;

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
            Rebuild();
        }

        private void OnEnable()
        {
            // Dropdown を開いた直後など、有効化時点の選択アイテムへスクロール。
            // 複製リストのアイテム生成・toggle.Select() 完了後に処理するため端フレームへ遅延。
            ScheduleInitialScroll();
        }

        /// <summary>
        /// content 配下の Selectable（ナビゲート対象）を index リストとして再収集し、
        /// 各 Selectable に AutoScrollItemReporter を確保する（深さ無関係、動的生成にも対応）。
        /// </summary>
        public void Rebuild()
        {
            _items.Clear();
            if (_scrollRect == null || _scrollRect.content == null) return;

            var selectables = _scrollRect.content.GetComponentsInChildren<Selectable>(false);
            foreach (var selectable in selectables)
            {
                _items.Add((RectTransform)selectable.transform);

                if (!selectable.TryGetComponent<AutoScrollItemReporter>(out var reporter))
                    reporter = selectable.gameObject.AddComponent<AutoScrollItemReporter>();
                reporter.Owner = this;
            }
        }

        /// <summary>index で指定したアイテムが見切れない最小限だけスクロールする。</summary>
        public void ScrollTo(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            ScrollTo(_items[index]);
        }

        /// <summary>対象 RectTransform が viewport に収まる最小限だけスクロールする。</summary>
        public void ScrollTo(RectTransform target)
        {
            if (target == null || _scrollRect == null || _scrollRect.content == null) return;

            // Canvas.ForceUpdateCanvases();
            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            // 対象の上端/下端を viewport ローカル Y に変換
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float targetMin = float.PositiveInfinity;
            float targetMax = float.NegativeInfinity;
            for (int i = 0; i < 4; i++)
            {
                var lp = viewport.InverseTransformPoint(corners[i]);
                targetMin = Mathf.Min(targetMin, lp.y);
                targetMax = Mathf.Max(targetMax, lp.y);
            }

            var vp = viewport.rect;

            float delta = 0f;
            if (targetMax > vp.yMax) delta = targetMax - vp.yMax;       // 上にはみ出し
            else if (targetMin < vp.yMin) delta = targetMin - vp.yMin;  // 下にはみ出し

            if (Mathf.Abs(delta) < Epsilon) return; // 収まっていれば何もしない（最小スクロール）

            var pos = _scrollRect.content.anchoredPosition;
            pos.y -= delta;
            _scrollRect.content.anchoredPosition = pos;
        }

        /// <summary>
        /// OnEnable 時のワンショット。端フレームへ遅延して Rebuild（reporter 付与 + 収集）後、
        /// 現在の選択アイテムへ初期スクロールする。多重起動は _scrollScheduled でガード。
        /// </summary>
        private void ScheduleInitialScroll()
        {
            if (_scrollScheduled) return;
            _scrollScheduled = true;
            InitialScrollDeferredAsync(this.GetCancellationTokenOnDestroy()).Forget();
        }

        private async UniTaskVoid InitialScrollDeferredAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }
            finally
            {
                _scrollScheduled = false;
            }

            // 動的生成（Dropdown の複製リスト）に追従するため再収集 + reporter 付与
            Rebuild();

            var selected = EventSystem.current.currentSelectedGameObject;
            if (selected == null) return;

            var index = FindItemIndex(selected.transform);
            if (index >= 0) ScrollTo(index);
        }

        /// <summary>選択中 Transform を含むアイテム（行）の index を祖先一致で求める。</summary>
        private int FindItemIndex(Transform selected)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (selected == _items[i] || selected.IsChildOf(_items[i]))
                    return i;
            }

            return -1;
        }
    }
}
