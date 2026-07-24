using Game.Core.Services;
using Game.Horror.Database;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Interfaces;
using Game.Shared.Services;
using Game.Shared.Services.Interfaces;
using R3;
using R3.Triggers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    public class HorrorKeyItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descText;

        public Observable<Unit> OnSubmit => _button.OnClickAsObservable();
        public Observable<BaseEventData> OnSelected => _button.OnSelectAsObservable();
        public Selectable Selectable => _button;

        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;

        private IObjectInfo _itemInfo;

        public void Initialize()
        {
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();

            _localizationService.OnLocaleChanged
                .Subscribe(_ => SetText())
                .AddTo(this);
        }

        public void SetItem(ObjectCategory category, int id)
        {
            if (!HorrorDatabaseHelper.TryGetInfo(category, id, out var info))
                return;

            _itemInfo = info;
            SetText();
            SetIcon();
        }

        private void SetText()
        {
            _nameText.text = _localizationService.GetStringByPropTexts(_itemInfo.Name);
            _descText.text = _localizationService.GetStringByPropTexts(_itemInfo.Description);
        }

        private void SetIcon()
        {
            Sprite sprite = null;

            if (!string.IsNullOrEmpty(_itemInfo.IconAssetName))
            {
                sprite = _iconService.GetSprite(_itemInfo.IconAssetName);
            }

            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }
        }
    }
}
