using System.Collections.Generic;
using Game.Core.Services;
using Game.Shared.Enums;
using R3;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// スロット位置に開くコンテキストサブメニュー。エントリを動的生成し、決定でアクションを通知する。
    /// 入力ゲート（グリッド遮断・ダイアログ級入力の抑止）は呼び出し側（Dialog / Component）が担当する。
    /// </summary>
    public class HorrorInventoryContextMenu : MonoBehaviour
    {
        [SerializeField] private RectTransform _panel;                          // 表示位置を動かすパネル（自身）
        [SerializeField] private RectTransform _entryContainer;                 // VerticalLayoutGroup コンテナ
        [SerializeField] private HorrorInventoryContextActionView _entryPrefab;

        private readonly Subject<ContextActionType> _onClicked = new();
        public Observable<ContextActionType> OnClicked => _onClicked;

        private readonly Subject<Unit> _onClosed = new();
        public Observable<Unit> OnClosed => _onClosed;

        private readonly List<HorrorInventoryContextActionView> _entries = new();
        private readonly CompositeDisposable _entryDisposables = new();

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _panel.gameObject.SetActive(false);
        }

        /// <summary>指定スロット位置にエントリ列を開く。</summary>
        public void Open(RectTransform anchorSlot, ContextActionType[] entries)
        {
            if (IsOpen || entries == null || entries.Length == 0) return;

            BuildEntries(entries);
            _panel.gameObject.SetActive(true);
            PositionAt(anchorSlot);
            FocusFirst();
            IsOpen = true;
        }

        /// <summary>メニューを閉じる。</summary>
        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            _panel.gameObject.SetActive(false);
            ClearEntries();
            _onClosed.OnNext(Unit.Default);
        }

        private void BuildEntries(ContextActionType[] entries)
        {
            ClearEntries();
            for (int i = 0; i < entries.Length; i++)
            {
                var type = entries[i];
                var entry = Instantiate(_entryPrefab, _entryContainer);
                entry.Initialize(type);
                entry.OnClicked
                    .Subscribe(x => _onClicked.OnNext(x))
                    .AddTo(_entryDisposables);
                _entries.Add(entry);
            }
        }

        private void ClearEntries()
        {
            _entryDisposables.Clear();
            foreach (var entry in _entries)
            {
                if (entry != null) Destroy(entry.gameObject);
            }
            _entries.Clear();
        }

        private void FocusFirst()
        {
            if (_entries.Count == 0) return;
            var inputService = GameServiceManager.Resolve<IInputSystemService>();
            inputService.SetSelectedGameObject(_entries[0].Selectable.gameObject);
        }

        // スロットの右上コーナーを基点に、Canvas 矩形内へクランプして配置する（Overlay 前提）。
        private void PositionAt(RectTransform anchorSlot)
        {
            if (anchorSlot == null) return;
            if (_panel.parent is not RectTransform canvasRect) return;

            // レイアウトを確定させてからサイズを測る
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

            var corners = new Vector3[4]; // 0=BL, 1=TL, 2=TR, 3=BR
            anchorSlot.GetWorldCorners(corners);
            var screenTopRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenTopRight, null, out var local);

            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0f, 1f); // 左上基点：スロット右上から右下へ展開

            var size = _panel.rect.size;
            float halfW = canvasRect.rect.width * 0.5f;
            float halfH = canvasRect.rect.height * 0.5f;

            local.x = Mathf.Clamp(local.x, -halfW, halfW - size.x);
            local.y = Mathf.Clamp(local.y, -halfH + size.y, halfH);

            _panel.anchoredPosition = local;
        }

        private void OnDestroy()
        {
            _entryDisposables.Dispose();
            _onClicked.Dispose();
            _onClosed.Dispose();
        }
    }
}
