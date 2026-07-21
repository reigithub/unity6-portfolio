using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Interfaces;
using Game.Shared.Services.Interfaces;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// 選択中アイテムの詳細（拡大アイコン・名前・説明）を表示するパネル。
    /// 空スロット選択時は Clear で内容を消す。
    /// </summary>
    public class HorrorInventorySlotDetailView : MonoBehaviour
    {
        [SerializeField] private Image _largeIcon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descriptionText;

        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;

        public void Initialize()
        {
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();
        }

        public void SetSlotDetail(IObjectInfo info)
        {
            SetIcon(info);

            if (_nameText != null)
                _nameText.text = _localizationService.GetStringByPropTexts(info?.Name);

            if (_descriptionText != null)
                _descriptionText.text = _localizationService.GetStringByPropTexts(info?.Description);
        }

        private void SetIcon(IObjectInfo item)
        {
            Sprite sprite = null;
            if (item != null && !string.IsNullOrEmpty(item.IconAssetName))
            {
                sprite = _iconService.GetSprite(item.IconAssetName);
            }

            if (_largeIcon != null)
            {
                _largeIcon.sprite = sprite;
                _largeIcon.enabled = sprite != null;
            }
        }

        public void Clear() => SetSlotDetail(null);
    }
}
