using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Interfaces;
using Game.Shared.Services.Interfaces;
using R3;
using R3.Triggers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    public class HorrorKeyItemView : MonoBehaviour
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _descText;

        private readonly Subject<HorrorKeyItemView> _onSelected = new();
        public Observable<HorrorKeyItemView> OnSelected => _onSelected;

        private readonly Subject<HorrorKeyItemView> _onSubmit = new();
        public Observable<HorrorKeyItemView> OnSubmit => _onSubmit;

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
            _button.OnClickAsObservable()
                .Subscribe(_ => _onSubmit.OnNext(this))
                .AddTo(this);
            _button.OnSelectAsObservable()
                .Subscribe(_ => _onSelected.OnNext(this))
                .AddTo(this);
        }

        public void SetItem(IObjectInfo info)
        {
            _itemInfo = info;
            SetIcon();
            SetText();
        }

        private void SetText()
        {
            _nameText.text = _localizationService.GetStringByPropTexts(_itemInfo.Name);
            _descText.text = _localizationService.GetStringByPropTexts(_itemInfo.Description);
        }

        private void SetIcon()
        {
            Sprite sprite = null;

            if (_itemInfo != null && !string.IsNullOrEmpty(_itemInfo.IconAssetName))
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
