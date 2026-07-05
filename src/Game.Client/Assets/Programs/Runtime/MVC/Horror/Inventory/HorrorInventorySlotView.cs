using Game.Core.Services;
using Game.Horror.Services;
using Game.Shared.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// グリッド内の1スロット。アイコンと個数を表示し、選択時に枠を点灯して詳細表示へ通知する。
    /// 選択されるのは Selectable（子 Button）自身なので、このコンポーネントは Button と同じ GameObject に付与する。
    /// マウスホバー（PointerEventReceiver）・パッド/キー操作のどちらも EventSystem の選択に集約され OnSelect に届く。
    /// </summary>
    public class HorrorInventorySlotView : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;            // アイコン表示（自身の Image を流用）
        [SerializeField] private TextMeshProUGUI _countText;  // 個数（スタック > 1 のときのみ表示）

        private readonly Subject<HorrorInventorySlotView> _onSelected = new();

        /// <summary>このスロットが選択された通知。Component が購読して詳細表示を更新する。</summary>
        public Observable<HorrorInventorySlotView> OnSelected => _onSelected;

        private readonly Subject<HorrorInventorySlotView> _onSubmit = new();

        /// <summary>このスロットで決定された通知。Component が購読してサブメニューを開く。</summary>
        public Observable<HorrorInventorySlotView> OnSubmit => _onSubmit;

        /// <summary>サブメニューの表示位置決めに用いる自身の RectTransform。</summary>
        public RectTransform RectTransform => _rectTransform;

        public IHorrorInventorySlotInfo SlotInfo { get; private set; }

        public void Initialize()
        {
            _button.OnClickAsObservable()
                .Subscribe(_ => _onSubmit.OnNext(this))
                .AddTo(this);
        }

        public void SetSlot(IHorrorInventorySlotInfo info, int count)
        {
            SlotInfo = info;
            SetIcon(info);
            if (_countText != null)
            {
                var show = info != null && count > 1;
                _countText.gameObject.SetActive(show);
                if (show) _countText.text = count.ToString();
            }
        }

        public void SetEmpty()
        {
            SlotInfo = null;
            SetIcon(null);
            if (_countText != null)
                _countText.gameObject.SetActive(false);
        }

        private void SetIcon(IHorrorInventorySlotInfo item)
        {
            if (item == null || string.IsNullOrEmpty(item.IconAssetName))
            {
                if (_iconImage != null)
                {
                    _iconImage.sprite = null;
                    _iconImage.enabled = false;
                }
                return;
            }

            var iconService = GameServiceManager.Get<HorrorIconService>();
            var icon = iconService.GetSprite(item.IconAssetName);

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
            _onSelected.OnNext(this);
        }

        public void OnDeselect(BaseEventData eventData)
        {
        }

        private void OnDestroy()
        {
            _onSelected.Dispose();
            _onSubmit.Dispose();
        }
    }
}
