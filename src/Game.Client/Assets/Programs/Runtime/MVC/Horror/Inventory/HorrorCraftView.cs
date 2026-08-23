using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Services.Interfaces;
using Game.Shared.Extensions;
using Game.Shared.Services.Interfaces;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Horror.Inventory
{
    /// <summary>
    /// インベントリダイアログのクラフトタブのビュー
    /// レシピ一覧・選択レシピの詳細（成果物と素材の必要数/所持数）の表示、 長押しゲージの描画・入力通知（行のポインタ押下・選択）
    /// </summary>
    public class HorrorCraftView : MonoBehaviour
    {
        #region SerializeField

        [SerializeField] private Transform _recipeContentRoot;
        [SerializeField] private HorrorCraftRecipeView _recipePrefab;

        [SerializeField] private Image _resultIcon;
        [SerializeField] private TextMeshProUGUI _resultNameText;
        [SerializeField] private TextMeshProUGUI _resultDescriptionText;

        [SerializeField] private Transform _materialContentRoot;
        [SerializeField] private HorrorCraftMaterialView _materialPrefab;

        [Tooltip("長押しの進捗ゲージ（Slider: 値域 0〜1）。押下中のみ表示する")]
        [SerializeField] private Slider _holdGauge;

        #endregion

        private readonly List<HorrorCraftRecipeView> _recipeViews = new();
        private readonly CompositeDisposable _disposables = new();

        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;
        private IHorrorCraftService _craftService;
        private IHorrorInventoryService _inventoryService;

        private HorrorCraftRecipeView _selected;

        /// <summary>選択中レシピの CraftId（未選択は null）。</summary>
        public int? SelectedCraftId => _selected != null ? _selected.CraftId : null;

        /// <summary>選択中の行の上でポインタが押され続けているか（長押しの保持判定用）。</summary>
        public bool IsSelectedPointerHeld => _selected != null && _selected.IsPointerHeld;

        /// <summary>クラフトタブが表示中か。</summary>
        public bool IsVisible => isActiveAndEnabled;

        /// <summary>いずれかのレシピ行の上でポインタが押された瞬間の通知（長押し開始のトリガー用）。</summary>
        private readonly Subject<Unit> _recipePointerPressed = new();
        public Observable<Unit> OnRecipePointerPressed => _recipePointerPressed;

        public void Initialize()
        {
            _localizationService = GameServiceManager.Resolve<ILocalizationService>();
            _iconService = GameServiceManager.Resolve<IHorrorIconService>();
            _craftService = GameServiceManager.Resolve<IHorrorCraftService>();
            _inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();

            _localizationService.OnLocaleChanged
                .Subscribe(_ => UpdateDetail(_selected))
                .AddTo(_disposables);

            // 在庫の変化（自クラフト・他タブでの使用や破棄）に所持数と素材表示を追従させる
            _inventoryService.SlotsChanged
                .ThrottleLastFrame(1) // 同一フレームの連続変更（クラフト＝素材数+1回発行）を最終状態1回に合流する
                .Subscribe(_ =>
                {
                    RefreshPossessedCounts();
                    UpdateDetail(_selected);
                })
                .AddTo(_disposables);

            BuildRecipes();
            ValidateHoldGauge();
            SetHoldProgress(0f);
        }

        #region Recipes

        private void BuildRecipes()
        {
            foreach (Transform child in _recipeContentRoot)
            {
                child.gameObject.SafeDestroy();
            }

            _recipeViews.Clear();

            foreach (var recipe in _craftService.Recipes)
            {
                var view = Instantiate(_recipePrefab, _recipeContentRoot);
                view.Initialize();
                view.SetRecipe(recipe.Id, recipe.ResultObjectCategory, recipe.ResultObjectId, recipe.ResultCount);
                view.SetPossessedCount(_inventoryService.GetCount(recipe.ResultObjectCategory, recipe.ResultObjectId));
                view.OnSelected.Subscribe(Select).AddTo(_disposables);
                view.OnPointerPressed.Subscribe(_ => _recipePointerPressed.OnNext(Unit.Default)).AddTo(_disposables);
                _recipeViews.Add(view);
            }

            // 一覧に入る前から詳細が埋まっているよう、先頭を初期選択にする（フォーカス自体は TabGroup が移す）
            if (_recipeViews.Count > 0)
                Select(_recipeViews[0]);
        }

        private void Select(HorrorCraftRecipeView view)
        {
            if (view == null) return;

            _selected = view;
            UpdateDetail(view);
        }

        private void RefreshPossessedCounts()
        {
            foreach (var view in _recipeViews)
            {
                view.SetPossessedCount(_inventoryService.GetCount(view.ResultCategory, view.ResultObjectId));
            }
        }

        #endregion

        #region Detail

        private void UpdateDetail(HorrorCraftRecipeView view)
        {
            if (view == null) return;

            var info = view.ResultInfo;

            if (_resultIcon != null)
            {
                var sprite = !string.IsNullOrEmpty(info?.IconAssetName)
                    ? _iconService.GetSprite(info.IconAssetName)
                    : null;
                _resultIcon.sprite = sprite;
                _resultIcon.enabled = sprite != null;
            }

            if (_resultNameText != null)
                _resultNameText.text = _localizationService.GetStringByPropTexts(info?.Name);

            if (_resultDescriptionText != null)
                _resultDescriptionText.text = _localizationService.GetStringByPropTexts(info?.Description);

            BuildMaterials(view.CraftId);
        }

        private void BuildMaterials(int craftId)
        {
            foreach (Transform child in _materialContentRoot)
            {
                child.gameObject.SafeDestroy();
            }

            foreach (var material in _craftService.GetMaterials(craftId))
            {
                var view = Instantiate(_materialPrefab, _materialContentRoot);
                view.Initialize();
                view.SetMaterial(
                    material.ObjectCategory,
                    material.ObjectId,
                    material.Count,
                    _inventoryService.GetCount(material.ObjectCategory, material.ObjectId));
            }
        }

        #endregion

        #region Hold

        /// <summary>長押しゲージの進捗（0〜1）を表示する。0 でゲージ非表示（Dialog の長押しフローから呼ぶ）。</summary>
        public void SetHoldProgress(float progress01)
        {
            if (_holdGauge == null) return;

            bool active = progress01 > 0f;
            if (_holdGauge.gameObject.activeSelf != active)
                _holdGauge.gameObject.SetActive(active);

            _holdGauge.SetValueWithoutNotify(Mathf.Clamp01(progress01));
        }

        // 進捗は 0〜1 で渡すため、prefab 側の値域がこれと異なるとゲージ表示が破綻する
        private void ValidateHoldGauge()
        {
            if (_holdGauge == null) return;
            if (Mathf.Approximately(_holdGauge.minValue, 0f) && Mathf.Approximately(_holdGauge.maxValue, 1f)) return;

            Debug.LogError(
                $"[{nameof(HorrorCraftView)}] {nameof(_holdGauge)} の値域は min=0 / max=1 である必要があります"
                + $"（現在 min={_holdGauge.minValue} / max={_holdGauge.maxValue}）", this);
        }

        // タブ非表示ではゲージ表示を持ち越さない
        private void OnDisable() => SetHoldProgress(0f);

        #endregion

        private void OnDestroy()
        {
            _disposables.Dispose();
            _recipePointerPressed.Dispose();
        }
    }
}
