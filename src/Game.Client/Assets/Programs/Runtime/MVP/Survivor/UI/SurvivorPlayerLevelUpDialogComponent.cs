using System.Collections.Generic;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Weapon;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.UI
{
    /// <summary>
    /// レベルアップダイアログのルートコンポーネント
    /// UI Toolkit (UXML/USS) 使用
    /// </summary>
    public class SurvivorPlayerLevelUpDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<SurvivorWeaponUpgradeOption> _onOptionSelected = new();
        private readonly List<Button> _optionButtons = new();

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

            // ヘッダー部分（武器名 + レベルバッジ）
            var header = new VisualElement();
            header.AddToClassList("option__header");

            var nameLabel = new Label(option.WeaponName);
            nameLabel.AddToClassList("option__name");
            header.Add(nameLabel);

            var levelBadge = new Label(option.IsNewWeapon ? "NEW" : $"Lv.{option.CurrentLevel + 1}");
            levelBadge.AddToClassList("option__level-badge");
            if (option.IsNewWeapon)
            {
                levelBadge.AddToClassList("option__level-badge--new");
            }
            header.Add(levelBadge);

            button.Add(header);

            // 説明文
            var description = new Label(option.Description);
            description.AddToClassList("option__description");
            button.Add(description);

            // クリックイベント
            button.clicked += () => _onOptionSelected.OnNext(option);

            _optionsContainer.Add(button);
            _optionButtons.Add(button);
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
