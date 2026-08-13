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
        [SerializeField] private bool _rebuildOnEnable;

        private ScrollRect _scrollRect;
        private readonly List<RectTransform> _items = new();
        public IReadOnlyList<RectTransform> Items => _items;

        private bool _scrollScheduled;

        private const float Epsilon = 0.01f;

        private void Awake()
        {
            _scrollRect = GetComponent<ScrollRect>();
            Rebuild();
        }

        private void OnEnable()
        {
            if (!_rebuildOnEnable) return;
            if (_items.Count == 0) Rebuild();
        }

        /// <summary>
        /// アイテム（Content 直下の子）を index リストとして再収集し、
        /// Content 配下の各 Selectable に AutoScrollItemReporter を確保する。
        /// Selectable は深さ無関係に集める（カスタムアイテムが Selectable を子に持つ構造に対応）。
        /// </summary>
        public void Rebuild()
        {
            _items.Clear();
            if (_scrollRect == null || _scrollRect.content == null) return;

            var content = _scrollRect.content;
            // if (!content.TryGetComponent<ContentSizeFitter>(out var contentSizeFitter))
            // {
            //     contentSizeFitter = content.gameObject.AddComponent<ContentSizeFitter>();
            //     contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            //     contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // }

            // アイテム = Content 直下の子（標準 ScrollView 構造）
            for (int i = 0; i < content.childCount; i++)
            {
                if (content.GetChild(i) is RectTransform rt)
                    _items.Add(rt);
            }

            // ナビゲート対象（Selectable）に reporter を付与（カスタムアイテムの子 Selectable も含む）
            var selectables = content.GetComponentsInChildren<Selectable>(false);
            foreach (var selectable in selectables)
            {
                if (!selectable.TryGetComponent<AutoScrollItemReporter>(out var reporter))
                    reporter = selectable.gameObject.AddComponent<AutoScrollItemReporter>();
                reporter.Owner = this;
            }
        }

        /// <summary>
        /// reporter（Selectable）からの選択通知。選択された Selectable を含むアイテムへスクロールし、
        /// そのアイテムの選択カーソル（白背景 Image）を有効化する。
        /// </summary>
        public void OnItemSelected(Transform selected)
        {
            var index = FindItemIndex(selected);
            if (index < 0) return;

            ScrollTo(index);
        }

        /// <summary>
        /// reporter（Selectable）からの選択解除通知。選択された Selectable を含むアイテムの
        /// 選択カーソル（白背景 Image）を無効化する（スクロールはしない）。
        /// </summary>
        public void OnItemDeselected(Transform deselected)
        {
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

        /// <summary>index で指定したアイテムが見切れない最小限だけスクロールする。</summary>
        public void ScrollTo(int index)
        {
            if (index < 0 || index >= _items.Count) return;
            ScrollTo(_items[index]);
        }

        /// <summary>
        /// 対象 RectTransform が viewport の縦範囲からはみ出している分だけ content を動かし、
        /// はみ出した端を viewport の端にぴったり合わせる（＝見切れを解消する最小スクロール）。
        ///
        /// 座標系: すべて viewport のローカル空間で縦方向(Y)のみを比較する。
        ///   viewport.rect では yMax = 上端 / yMin = 下端。
        ///   target の四隅をこの空間へ変換し、その Y 範囲を [targetMin(下端), targetMax(上端)] とする。
        /// </summary>
        public void ScrollTo(RectTransform target)
        {
            if (target == null || _scrollRect == null || _scrollRect.content == null) return;

            // 直前のアイテム生成・レイアウト変更（ContentSizeFitter / VerticalLayoutGroup）が
            // まだ反映されていないと GetWorldCorners が古い位置を返し delta を誤算出する。
            // 測定前に Canvas とレイアウトを確定させる。
            // Canvas.ForceUpdateCanvases();

            // viewport 未設定の ScrollRect では自身の RectTransform がクリップ領域を兼ねる。
            var viewport = _scrollRect.viewport != null
                ? _scrollRect.viewport
                : (RectTransform)_scrollRect.transform;

            // target の縦の占有範囲を viewport ローカル Y で求める。
            // GetWorldCorners → InverseTransformPoint で、回転やネスト階層に依存せず実際の表示位置を得る。
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float targetMin = float.PositiveInfinity; // 下端
            float targetMax = float.NegativeInfinity; // 上端
            for (int i = 0; i < 4; i++)
            {
                var lp = viewport.InverseTransformPoint(corners[i]);
                targetMin = Mathf.Min(targetMin, lp.y);
                targetMax = Mathf.Max(targetMax, lp.y);
            }

            var vp = viewport.rect;

            // delta = target が viewport からはみ出している量（符号付き）。
            // 上にはみ出し → 正、下にはみ出し → 負。
            // 両端同時にはみ出すケース（target が viewport より 高い）は上端優先。
            // 収まっていれば 0。
            float delta = 0f;
            if (targetMax > vp.yMax)
                delta = targetMax - vp.yMax;
            else if (targetMin < vp.yMin)
                delta = targetMin - vp.yMin;

            // 既に収まっているなら動かさない
            if (Mathf.Abs(delta) < Epsilon) return;

            // content を縦に delta 分ずらすと、その子である target も同じだけ動く。
            // y を -delta することで、はみ出していた端が viewport の端にちょうど一致する。
            var pos = _scrollRect.content.anchoredPosition;
            pos.y -= delta;
            _scrollRect.content.anchoredPosition = pos;
        }

        // private void OnEnable()
        // {
        //     // Dropdown を開いた直後など、有効化時点の選択アイテムへスクロール。
        //     // 複製リストのアイテム生成・toggle.Select() 完了後に処理するため端フレームへ遅延。
        //     ScheduleInitialScroll();
        // }
        //
        // /// <summary>
        // /// OnEnable 時のワンショット。端フレームへ遅延して Rebuild（reporter 付与 + 収集）後、
        // /// 現在の選択アイテムへ初期スクロールする。多重起動は _scrollScheduled でガード。
        // /// </summary>
        // private void ScheduleInitialScroll()
        // {
        //     if (_scrollScheduled) return;
        //     _scrollScheduled = true;
        //     InitialScrollDeferredAsync(this.GetCancellationTokenOnDestroy()).Forget();
        // }
        //
        // private async UniTaskVoid InitialScrollDeferredAsync(CancellationToken ct)
        // {
        //     try
        //     {
        //         await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, ct);
        //     }
        //     catch (System.OperationCanceledException)
        //     {
        //         return;
        //     }
        //     finally
        //     {
        //         _scrollScheduled = false;
        //     }
        //
        //     // 動的生成（Dropdown の複製リスト）に追従するため再収集 + reporter 付与
        //     Rebuild();
        //
        //     var selected = EventSystem.current.currentSelectedGameObject;
        //     if (selected == null) return;
        //
        //     var index = FindItemIndex(selected.transform);
        //     if (index >= 0) ScrollTo(index);
        // }
    }
}
