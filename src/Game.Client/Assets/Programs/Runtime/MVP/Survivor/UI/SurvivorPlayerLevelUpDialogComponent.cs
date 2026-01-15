using System.Collections.Generic;
using DG.Tweening;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Weapon;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Survivor.UI
{
    /// <summary>
    /// レベルアップダイアログのルートコンポーネント
    /// </summary>
    public class SurvivorPlayerLevelUpDialogComponent : GameSceneComponent
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI _titleText;

        [SerializeField] private Transform _optionsContainer;
        [SerializeField] private GameObject _optionButtonPrefab;

        private readonly Subject<WeaponUpgradeOption> _onOptionSelected = new();
        private readonly List<GameObject> _optionButtons = new();

        public Observable<WeaponUpgradeOption> OnOptionSelected => _onOptionSelected;

        protected override void OnDestroy()
        {
            _onOptionSelected.Dispose();
            base.OnDestroy();
        }

        public void Initialize(List<WeaponUpgradeOption> options, int playerLevel)
        {
            // タイトル更新
            if (_titleText != null)
            {
                _titleText.text = $"Level Up! Lv.{playerLevel}";
            }

            // 既存のボタンをクリア
            foreach (var button in _optionButtons)
            {
                Destroy(button);
            }

            _optionButtons.Clear();

            // 選択肢ボタンを生成
            foreach (var option in options)
            {
                CreateOptionButton(option);
            }
        }

        private void CreateOptionButton(WeaponUpgradeOption option)
        {
            if (_optionButtonPrefab == null || _optionsContainer == null) return;

            var buttonObj = Instantiate(_optionButtonPrefab, _optionsContainer);
            _optionButtons.Add(buttonObj);

            // ボタンテキスト設定
            var textComponent = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (textComponent != null)
            {
                textComponent.text = option.Description;
            }

            // ボタンクリックイベント
            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                button.OnClickAsObservable()
                    .Subscribe(_ => _onOptionSelected.OnNext(option))
                    .AddTo(Disposables);
            }
        }
    }
}