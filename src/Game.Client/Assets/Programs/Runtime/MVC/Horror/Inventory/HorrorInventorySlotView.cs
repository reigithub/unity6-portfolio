using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Interfaces;
using R3;
using R3.Triggers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// グリッド内の1スロット。アイコンと個数を表示し、選択時に枠を点灯して詳細表示へ通知する。
    /// 選択されるのは Selectable（子 Button）自身なので、このコンポーネントは Button と同じ GameObject に付与する。
    /// マウスホバー（PointerEventReceiver）・パッド/キー操作のどちらも EventSystem の選択に集約され OnSelect に届く。
    /// </summary>
    public class HorrorInventorySlotView : MonoBehaviour
    {
        [SerializeField] private RectTransform _rectTransform;
        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;           // アイコン表示（自身の Image を流用）
        [SerializeField] private TextMeshProUGUI _countText; // 個数（スタック > 1 のときのみ表示）

        [SerializeField] private Image _inputActionIcon;

        private readonly Subject<HorrorInventorySlotView> _onSelected = new();

        /// <summary>このスロットが選択された通知。Component が購読して詳細表示を更新する。</summary>
        public Observable<HorrorInventorySlotView> OnSelected => _onSelected;

        private readonly Subject<HorrorInventorySlotView> _onSubmit = new();

        /// <summary>このスロットで決定された通知。Component が購読してサブメニューを開く。</summary>
        public Observable<HorrorInventorySlotView> OnSubmit => _onSubmit;

        /// <summary>サブメニューの表示位置決めに用いる自身の RectTransform。</summary>
        public RectTransform RectTransform => _rectTransform;

        public Selectable Selectable => _button;

        public IObjectInfo SlotInfo { get; private set; }

        private IHorrorIconService _iconService;
        private IHorrorEquipmentService _equipmentService;
        private IInputSystemService _inputSystemService;
        private IInputActionIconService _inputActionIconService;

        public void Initialize()
        {
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();
            _equipmentService = GameServiceManager.Resolve<IHorrorEquipmentService>();
            _inputSystemService = GameServiceManager.Resolve<IInputSystemService>();
            _inputActionIconService = GameServiceManager.Resolve<IInputActionIconService>();

            _button.OnClickAsObservable()
                .Subscribe(_ => _onSubmit.OnNext(this))
                .AddTo(this);
            _button.OnSelectAsObservable()
                .Subscribe(_ => _onSelected.OnNext(this))
                .AddTo(this);

            _inputSystemService.OnControlSchemeChanged.Subscribe(_ => SetInputActionIcon(SlotInfo)).AddTo(this);
            _inputSystemService.OnDeviceChanged.Subscribe(_ => SetInputActionIcon(SlotInfo)).AddTo(this);
        }

        public void SetSlot(IObjectInfo info, int count)
        {
            SlotInfo = info;
            SetIcon(info?.IconAssetName);
            if (_countText != null)
            {
                var show = info != null && count > 1;
                _countText.gameObject.SetActive(show);
                if (show) _countText.text = count.ToString();
            }
            SetInputActionIcon(info);
        }

        public void SetEmpty()
        {
            SlotInfo = null;
            SetIcon(null);
            if (_countText != null)
                _countText.gameObject.SetActive(false);
            SetInputActionIcon(null);
        }

        public void RefreshSlot()
        {
            SetIcon(SlotInfo?.IconAssetName);
            SetInputActionIcon(SlotInfo);
        }

        private void SetIcon(string assetName)
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(assetName))
            {
                sprite = _iconService.GetSprite(assetName);
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = sprite;
                _iconImage.enabled = sprite != null;
            }
        }

        private void SetInputActionIcon(IObjectInfo item)
        {
            Sprite sprite = null;
            if (item != null)
            {
                var dir = _equipmentService.GetSlotInputDirection(item.ObjectCategory, item.Id);
                if (!string.IsNullOrEmpty(dir))
                {
                    var info = _inputSystemService.GetBindingInfo(_inputSystemService.Player.Equip, partName: dir);
                    sprite = _inputActionIconService.GetSprite(info);
                }
            }

            if (_inputActionIcon != null)
            {
                _inputActionIcon.sprite = sprite;
                _inputActionIcon.enabled = sprite != null;
            }
        }
    }
}
