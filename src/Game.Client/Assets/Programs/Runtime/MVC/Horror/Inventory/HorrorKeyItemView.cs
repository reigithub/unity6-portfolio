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

        public IObjectInfo ItemInfo { get; private set; }

        private IHorrorIconService _iconService;

        public void Initialize()
        {
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();

            _button.OnClickAsObservable()
                .Subscribe(_ => _onSubmit.OnNext(this))
                .AddTo(this);
            _button.OnSelectAsObservable()
                .Subscribe(_ => _onSelected.OnNext(this))
                .AddTo(this);
        }

        public void SetItem(IObjectInfo info, int count)
        {
            ItemInfo = info;
            SetIcon(info);
        }

        public void SetEmpty()
        {
            ItemInfo = null;
            SetIcon(null);
        }

        private void SetIcon(IObjectInfo item)
        {
            Sprite sprite = null;

            if (item != null && !string.IsNullOrEmpty(item.IconAssetName))
            {
                sprite = _iconService.GetSprite(item.IconAssetName);
            }

            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }
        }
    }
}
