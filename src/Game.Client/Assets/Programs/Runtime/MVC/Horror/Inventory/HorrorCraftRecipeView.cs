using Game.Core.Services;
using Game.Horror.Database;
using Game.Horror.Services.Interfaces;
using Game.Shared.Enums;
using Game.Shared.Interfaces;
using Game.Shared.Services.Interfaces;
using R3;
using R3.Triggers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// クラフトタブのレシピ一覧に並ぶ 1 行。成果物のアイコン・名前と現在の所持数を表示する。
    /// 決定（クリック）ではクラフトせず選択のみ行い、実行は長押し（<see cref="HorrorCraftView"/>）が担うため、
    /// マウス押下の継続を <see cref="IsPointerHeld"/> で公開する。
    /// </summary>
    public class HorrorCraftRecipeView : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private TextMeshProUGUI _possessedCountText;

        public Observable<HorrorCraftRecipeView> OnSelected => _button.OnSelectAsObservable().Select(_ => this);

        public Selectable Selectable => _button;

        /// <summary>この行が指すレシピ（<see cref="Game.Shared.Scriptable.Database.Tables.HorrorCraftMaster"/> の Id）。</summary>
        public int CraftId { get; private set; }

        /// <summary>成果物の種別（所持数の再取得に使う）。</summary>
        public ObjectCategory ResultCategory { get; private set; }

        /// <summary>成果物の Id（所持数の再取得に使う）。</summary>
        public int ResultObjectId { get; private set; }

        /// <summary>成果物のマスター情報。解決できないレシピでは null。</summary>
        public IObjectInfo ResultInfo => _resultInfo;

        /// <summary>この行の上でポインタが押されているか（長押し判定用）。</summary>
        public bool IsPointerHeld { get; private set; }

        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;

        private IObjectInfo _resultInfo;
        private int _resultCount;

        public void Initialize()
        {
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();

            _localizationService.OnLocaleChanged
                .Subscribe(_ => SetName())
                .AddTo(this);
        }

        /// <summary>レシピの成果物を表示に反映する。</summary>
        public void SetRecipe(int craftId, ObjectCategory resultCategory, int resultObjectId, int resultCount)
        {
            CraftId = craftId;
            ResultCategory = resultCategory;
            ResultObjectId = resultObjectId;
            _resultCount = resultCount;
            _resultInfo = HorrorDatabaseHelper.TryGetInfo(resultCategory, resultObjectId, out var info) ? info : null;

            SetName();
            SetIcon();
        }

        /// <summary>成果物の所持数を反映する（クラフト後の再表示にも使う）。</summary>
        public void SetPossessedCount(int count)
        {
            if (_possessedCountText != null)
                _possessedCountText.text = count.ToString();
        }

        private void SetName()
        {
            if (_nameText == null) return;

            var name = _localizationService.GetStringByPropTexts(_resultInfo?.Name);
            // 1 回で複数個できるレシピは個数まで見せる（弾薬など）
            _nameText.text = _resultCount > 1 ? $"{name} x{_resultCount}" : name;
        }

        private void SetIcon()
        {
            Sprite sprite = null;
            if (!string.IsNullOrEmpty(_resultInfo?.IconAssetName))
                sprite = _iconService.GetSprite(_resultInfo.IconAssetName);

            if (_icon != null)
            {
                _icon.sprite = sprite;
                _icon.enabled = sprite != null;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            IsPointerHeld = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            IsPointerHeld = false;
        }

        // タブ切替などで非表示になると PointerUp が届かないため、押下状態を持ち越さない
        private void OnDisable() => IsPointerHeld = false;
    }
}
