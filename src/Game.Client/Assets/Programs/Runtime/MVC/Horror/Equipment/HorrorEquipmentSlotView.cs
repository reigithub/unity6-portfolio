using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Interfaces;
using R3;
using R3.Triggers;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Horror.Equipment
{
    /// <summary>
    /// ショートカットダイアログ / HUD 共用の1スロット（D-Pad 1〜4）。登録アイテムのアイコンを表示し、
    /// 選択(ISelectHandler)・決定(Button)を通知する。選択されるのは Selectable（子 Button）自身なので、
    /// このコンポーネントは Button と同じ GameObject に付与する。
    /// ※インベントリの HorrorInventorySlotView と同イディオムだが、挙動分岐に備えて独立実装とする。
    /// </summary>
    public class HorrorEquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;   // 登録アイテムのアイコン表示
        [SerializeField] private Image _frameImage;  // 枠表示（HUD の装備中ハイライト等。ダイアログ側は未配線=null許容）

        /// <summary>このスロットで決定された通知。Component が購読して登録する。</summary>
        public Observable<Unit> OnClick => _button.OnClickAsObservable();

        /// <summary>このスロットが選択された通知。Component が購読して現在スロットを追跡する。</summary>
        public Observable<BaseEventData> OnSelect => _button.OnSelectAsObservable();

        public void SetSlot(IObjectInfo info) => SetIcon(info);

        public void SetEmpty() => SetIcon(null);

        private void SetIcon(IObjectInfo item)
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

            var iconService = GameServiceManager.Resolve<IHorrorIconService>();
            var icon = iconService.GetSprite(item.IconAssetName);

            if (_iconImage != null)
            {
                _iconImage.sprite = icon;
                _iconImage.enabled = icon != null;
            }
        }

        /// <summary>枠の色を設定する（HUD の装備中ハイライト表示用）。未配線なら何もしない。</summary>
        public void SetFrameColor(Color color)
        {
            if (_frameImage != null)
                _frameImage.color = color;
        }
    }
}
