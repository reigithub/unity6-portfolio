using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Shared.Interfaces;
using R3;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Horror.Equipment
{
    /// <summary>
    /// ショートカットダイアログの1スロット（D-Pad 1〜4）。登録アイテムのアイコンを表示し、
    /// 選択(ISelectHandler)・決定(Button)を通知する。選択されるのは Selectable（子 Button）自身なので、
    /// このコンポーネントは Button と同じ GameObject に付与する。
    /// ※インベントリの HorrorInventorySlotView と同イディオムだが、挙動分岐に備えて独立実装とする。
    /// </summary>
    public class HorrorEquipmentShortcutSlotView : MonoBehaviour, ISelectHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;   // 登録アイテムのアイコン表示

        private readonly Subject<HorrorEquipmentShortcutSlotView> _onSelected = new();

        /// <summary>このスロットが選択された通知。Component が購読して現在スロットを追跡する。</summary>
        public Observable<HorrorEquipmentShortcutSlotView> OnSelected => _onSelected;

        private readonly Subject<HorrorEquipmentShortcutSlotView> _onSubmit = new();

        /// <summary>このスロットで決定された通知。Component が購読して登録する。</summary>
        public Observable<HorrorEquipmentShortcutSlotView> OnSubmit => _onSubmit;

        public void Initialize()
        {
            _button.OnClickAsObservable()
                .Subscribe(_ => _onSubmit.OnNext(this))
                .AddTo(this);
        }

        public void SetItem(IHorrorInventorySlotInfo info)
        {
            LoadIconAsync(info).Forget();
        }

        public void SetEmpty()
        {
            LoadIconAsync(null).Forget();
        }

        private async UniTask LoadIconAsync(IHorrorInventorySlotInfo item)
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

            var assetService = GameServiceManager.Get<AddressableAssetService>();
            var icon = await assetService.LoadAssetAsync<Sprite>(item.IconAssetName);

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

        private void OnDestroy()
        {
            _onSelected.Dispose();
            _onSubmit.Dispose();
        }
    }
}
