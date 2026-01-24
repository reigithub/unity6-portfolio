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
    /// 武器入れ替えダイアログのルートコンポーネント
    /// UI Toolkit (UXML/USS) 使用
    /// </summary>
    public class SurvivorWeaponReplaceDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        [Inject] private IAddressableAssetService _assetService;

        private readonly Subject<int> _onWeaponSelected = new();
        private readonly Subject<Unit> _onCancelClicked = new();
        private readonly List<Button> _weaponButtons = new();
        private readonly Dictionary<string, Sprite> _iconCache = new();

        public Observable<int> OnWeaponSelected => _onWeaponSelected;
        public Observable<Unit> OnCancelClicked => _onCancelClicked;

        // UI Elements
        private VisualElement _root;
        private VisualElement _previewIcon;
        private Label _previewName;
        private VisualElement _weaponsContainer;
        private Button _cancelButton;

        protected override void OnDestroy()
        {
            _onWeaponSelected.Dispose();
            _onCancelClicked.Dispose();
            base.OnDestroy();
        }

        private void Awake()
        {
            QueryUIElements();
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;
            _previewIcon = _root.Q<VisualElement>("preview-icon");
            _previewName = _root.Q<Label>("preview-name");
            _weaponsContainer = _root.Q<VisualElement>("weapons-container");
            _cancelButton = _root.Q<Button>("cancel-button");

            // キャンセルボタンのクリックイベント
            if (_cancelButton != null)
            {
                _cancelButton.clicked += () => _onCancelClicked.OnNext(Unit.Default);
            }
        }

        public void Initialize(SurvivorWeaponUpgradeOption newWeapon, IReadOnlyList<SurvivorWeaponBase> currentWeapons)
        {
            // 新規武器のプレビュー表示
            if (_previewName != null)
            {
                _previewName.text = newWeapon.WeaponName;
            }

            // アイコンを非同期で読み込み
            if (_previewIcon != null && !string.IsNullOrEmpty(newWeapon.IconAssetName))
            {
                LoadIconAsync(newWeapon.IconAssetName, _previewIcon).Forget();
            }

            // 既存のボタンをクリア
            ClearWeaponButtons();

            // 現在の所持武器ボタンを生成
            foreach (var weapon in currentWeapons)
            {
                CreateWeaponButton(weapon);
            }
        }

        private void ClearWeaponButtons()
        {
            foreach (var button in _weaponButtons)
            {
                button.RemoveFromHierarchy();
            }
            _weaponButtons.Clear();
        }

        private void CreateWeaponButton(SurvivorWeaponBase weapon)
        {
            if (_weaponsContainer == null) return;

            // ボタン作成
            var button = new Button();
            button.AddToClassList("weapon-button");

            // アイコン
            var icon = new VisualElement();
            icon.AddToClassList("weapon__icon");

            var placeholder = new Label("?");
            placeholder.AddToClassList("weapon__icon-placeholder");

            // アイコンを非同期で読み込み
            if (!string.IsNullOrEmpty(weapon.IconAssetName))
            {
                LoadIconAsync(weapon.IconAssetName, icon, placeholder).Forget();
            }
            else
            {
                icon.AddToClassList("weapon__icon--empty");
            }

            icon.Add(placeholder);
            button.Add(icon);

            // レベルバッジ
            var levelBadge = new Label($"Lv.{weapon.Level}");
            levelBadge.AddToClassList("weapon__level");
            button.Add(levelBadge);

            // 武器名
            var nameLabel = new Label(weapon.Name);
            nameLabel.AddToClassList("weapon__name");
            button.Add(nameLabel);

            // クリックイベント（武器IDを発火）
            var weaponId = weapon.WeaponId;
            button.clicked += () => _onWeaponSelected.OnNext(weaponId);

            _weaponsContainer.Add(button);
            _weaponButtons.Add(button);
        }

        private async UniTaskVoid LoadIconAsync(string iconAssetName, VisualElement target, Label placeholder = null)
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

                if (sprite != null && target != null)
                {
                    // 背景画像として設定
                    target.style.backgroundImage = new StyleBackground(sprite);
                    target.RemoveFromClassList("weapon__icon--empty");

                    // プレースホルダーを非表示
                    if (placeholder != null)
                    {
                        placeholder.style.display = DisplayStyle.None;
                    }
                }
                else
                {
                    target?.AddToClassList("weapon__icon--empty");
                }
            }
            catch
            {
                target?.AddToClassList("weapon__icon--empty");
            }
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
