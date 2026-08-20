using System.Collections.Generic;
using Game.Core.Services;
using Game.Horror.Constants;
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
    /// インベントリダイアログのクラフトタブ。レシピ一覧・選択レシピの詳細（成果物と素材の必要数/所持数）を表示し、
    /// 決定の長押しでクラフトを実行する。
    /// クリック（押して離す）では実行せず、<see cref="HorrorCraftConstants.CraftHoldSeconds"/> の押下継続だけを起点にする。
    /// ダイアログ表示中は <c>timeScale = 0</c> のため、進捗は <see cref="Time.unscaledDeltaTime"/> で積む。
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

        private IInputSystemService _inputService;
        private ILocalizationService _localizationService;
        private IHorrorIconService _iconService;
        private IHorrorCraftService _craftService;
        private IHorrorInventoryService _inventoryService;

        private HorrorCraftRecipeView _selected;
        private float _holdElapsed;

        // 押しっぱなしのままタブへ入った場合や、クラフト直後の押しっぱなしで続けて実行されないよう、
        // 一度離すまで次の長押しを開始しない
        private bool _awaitRelease;

        /// <summary>長押しが進行中か（ダイアログ側でキャンセル・タブ切替を抑止するために見る）。</summary>
        public bool IsCrafting => _holdElapsed > 0f;

        public void Initialize()
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
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
                _recipeViews.Add(view);
            }

            // 一覧に入る前から詳細が埋まっているよう、先頭を初期選択にする（フォーカス自体は TabGroup が移す）
            if (_recipeViews.Count > 0)
                Select(_recipeViews[0]);
        }

        private void Select(HorrorCraftRecipeView view)
        {
            if (view == null) return;

            // 選択が移ったら進行中の長押しは打ち切る
            if (_selected != view)
                ResetHold();

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

        private void Update()
        {
            if (_selected == null || _craftService == null) return;

            bool held = _inputService.UI.Submit.IsPressed() || _selected.IsPointerHeld;

            if (_awaitRelease)
            {
                if (!held) _awaitRelease = false;
                return;
            }

            // 中断条件：押下解除・素材不足（他タブでの破棄などで実行不可へ変わった場合を含む）
            if (!held || !_craftService.CanCraft(_selected.CraftId))
            {
                ResetHold();
                return;
            }

            _holdElapsed += Time.unscaledDeltaTime;
            SetHoldProgress(_holdElapsed / HorrorCraftConstants.CraftHoldSeconds);

            if (_holdElapsed >= HorrorCraftConstants.CraftHoldSeconds)
                Execute();
        }

        private void Execute()
        {
            var craftId = _selected.CraftId;

            ResetHold();
            _awaitRelease = true;
            _craftService.TryCraft(craftId);
        }

        private void ResetHold()
        {
            if (_holdElapsed <= 0f) return;

            _holdElapsed = 0f;
            SetHoldProgress(0f);
        }

        private void SetHoldProgress(float progress01)
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

        // タブ切替で表示が戻ったときに、前回の押下状態・進捗を引き継がない
        private void OnEnable()
        {
            ResetHold();
            _awaitRelease = true;
        }

        private void OnDisable() => ResetHold();

        #endregion

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}
