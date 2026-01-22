using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Weapon;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// レベルアップダイアログのルートコンポーネント
    /// UI Toolkit (UXML/USS) 使用
    /// </summary>
    public class SurvivorPlayerLevelUpDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        [Inject] private IAddressableAssetService _assetService;

        private readonly Subject<SurvivorWeaponUpgradeOption> _onOptionSelected = new();
        private readonly List<Button> _optionButtons = new();
        private readonly Dictionary<string, Sprite> _iconCache = new();

        public Observable<SurvivorWeaponUpgradeOption> OnOptionSelected => _onOptionSelected;

        // UI Elements
        private VisualElement _root;
        private Label _titleText;
        private Label _levelText;
        private VisualElement _optionsContainer;

        protected override void OnDestroy()
        {
            _onOptionSelected.Dispose();
            base.OnDestroy();
        }

        private void Awake()
        {
            QueryUIElements();
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;
            _titleText = _root.Q<Label>("title-text");
            _levelText = _root.Q<Label>("level-text");
            _optionsContainer = _root.Q<VisualElement>("options-container");
        }

        public void Initialize(List<SurvivorWeaponUpgradeOption> options, int playerLevel)
        {
            // タイトル更新
            if (_titleText != null)
            {
                _titleText.text = "LEVEL UP!";
            }

            if (_levelText != null)
            {
                _levelText.text = $"Lv.{playerLevel}";
            }

            // 既存のボタンをクリア
            ClearOptionButtons();

            // 選択肢ボタンを生成
            foreach (var option in options)
            {
                CreateOptionButton(option);
            }
        }

        private void ClearOptionButtons()
        {
            foreach (var button in _optionButtons)
            {
                button.RemoveFromHierarchy();
            }
            _optionButtons.Clear();
        }

        private void CreateOptionButton(SurvivorWeaponUpgradeOption option)
        {
            if (_optionsContainer == null) return;

            // ボタン作成
            var button = new Button();
            button.AddToClassList("option-button");

            if (option.IsNewWeapon)
            {
                button.AddToClassList("option-button--new");
            }

            // サムネイル領域
            var thumbnail = new VisualElement();
            thumbnail.AddToClassList("option__thumbnail");

            var placeholder = new Label("?");
            placeholder.AddToClassList("option__thumbnail-placeholder");

            // アイコンを非同期で読み込み
            if (!string.IsNullOrEmpty(option.IconAssetName))
            {
                thumbnail.AddToClassList("option__thumbnail--loading");
                LoadIconAsync(option.IconAssetName, thumbnail, placeholder).Forget();
            }
            else
            {
                thumbnail.AddToClassList("option__thumbnail--empty");
            }

            thumbnail.Add(placeholder);
            button.Add(thumbnail);

            // コンテンツ領域
            var content = new VisualElement();
            content.AddToClassList("option__content");

            // ヘッダー部分（武器名のみ）
            var header = new VisualElement();
            header.AddToClassList("option__header");

            // バッジ（サムネイルの下、武器名の上に表示）
            if (option.IsNewWeapon)
            {
                // NEWバッジ（新規武器の場合）
                var newBadge = new Label("NEW");
                newBadge.AddToClassList("option__new-badge");
                header.Add(newBadge);
            }
            else
            {
                // レベルバッジ（既存武器のレベルアップ時）
                var levelBadge = new Label($"Lv.{option.CurrentLevel} → Lv.{option.CurrentLevel + 1}");
                levelBadge.AddToClassList("option__level-badge");
                header.Add(levelBadge);
            }

            var nameLabel = new Label(option.WeaponName);
            nameLabel.AddToClassList("option__name");
            header.Add(nameLabel);

            content.Add(header);

            // 説明文
            if (!string.IsNullOrEmpty(option.Description))
            {
                var description = new Label(option.Description);
                description.AddToClassList("option__description");
                content.Add(description);
            }

            // 追加性能テキスト（レベルアップ時のみ）
            if (!string.IsNullOrEmpty(option.UpgradeEffect))
            {
                var upgradeEffect = new Label(option.UpgradeEffect);
                upgradeEffect.AddToClassList("option__upgrade-effect");
                content.Add(upgradeEffect);
            }

            button.Add(content);

            // クリックイベント
            button.clicked += () => _onOptionSelected.OnNext(option);

            _optionsContainer.Add(button);
            _optionButtons.Add(button);
        }

        private async UniTaskVoid LoadIconAsync(string iconAssetName, VisualElement thumbnail, Label placeholder)
        {
            try
            {
                Sprite sprite;

                // キャッシュチェック
                if (_iconCache.TryGetValue(iconAssetName, out var cachedSprite))
                {
                    sprite = cachedSprite;
                }
                else
                {
                    // Addressablesから読み込み
                    sprite = await _assetService.LoadAssetAsync<Sprite>(iconAssetName);
                    if (sprite != null)
                    {
                        _iconCache[iconAssetName] = sprite;
                    }
                }

                if (sprite != null && thumbnail != null)
                {
                    // 背景画像として設定
                    thumbnail.style.backgroundImage = new StyleBackground(sprite);
                    thumbnail.RemoveFromClassList("option__thumbnail--loading");
                    thumbnail.RemoveFromClassList("option__thumbnail--empty");

                    // プレースホルダーを非表示
                    if (placeholder != null)
                    {
                        placeholder.style.display = DisplayStyle.None;
                    }
                }
                else
                {
                    // 読み込み失敗時はプレースホルダーを表示
                    thumbnail?.RemoveFromClassList("option__thumbnail--loading");
                    thumbnail?.AddToClassList("option__thumbnail--empty");
                }
            }
            catch
            {
                // エラー時はプレースホルダーを表示
                thumbnail?.RemoveFromClassList("option__thumbnail--loading");
                thumbnail?.AddToClassList("option__thumbnail--empty");
            }
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
