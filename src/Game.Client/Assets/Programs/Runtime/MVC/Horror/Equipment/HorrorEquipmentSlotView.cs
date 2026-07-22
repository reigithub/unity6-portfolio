using Game.Core.Services;
using Game.Horror.Database;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
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
    /// </summary>
    public class HorrorEquipmentSlotView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _iconImage;   // 登録アイテムのアイコン表示
        [SerializeField] private Image _frameImage;  // 枠表示（HUD の装備中ハイライト等。ダイアログ側は未配線=null許容）

        public Observable<Unit> OnClick => _button.OnClickAsObservable();
        public Observable<BaseEventData> OnSelect => _button.OnSelectAsObservable();

        private IHorrorIconService _iconService;

        public void Initialize()
            => _iconService = GameServiceManager.Resolve<IHorrorIconService>();

        public void SetSlot(ObjectCategory category, int id)
        {
            if (HorrorDatabaseHelper.TryGetInfo(category, id, out var info))
                SetIcon(info.IconAssetName);
            else
                SetEmpty();
        }

        public void SetEmpty() => SetIcon(null);

        private void SetIcon(string iconAssetName)
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(iconAssetName))
            {
                sprite = _iconService.GetSprite(iconAssetName);
            }

            if (_iconImage != null)
            {
                _iconImage.sprite = sprite;
                _iconImage.enabled = sprite != null;
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
