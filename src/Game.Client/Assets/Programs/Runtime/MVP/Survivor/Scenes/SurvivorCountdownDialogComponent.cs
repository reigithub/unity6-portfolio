using DG.Tweening;
using Game.MVP.Core.Scenes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorカウントダウンダイアログのルートコンポーネント
    /// 3, 2, 1, GO! のカウントダウン表示
    /// </summary>
    public class SurvivorCountdownDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private VisualElement _root;
        private Label _countLabel;

        private void Awake()
        {
            QueryUIElements();
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;
            _countLabel = _root.Q<Label>("count-label");
        }

        /// <summary>
        /// カウント数値を表示（3, 2, 1）
        /// </summary>
        public void ShowCount(int count)
        {
            if (_countLabel == null) return;

            _countLabel.text = count.ToString();
            _countLabel.RemoveFromClassList("countdown-go");
            _countLabel.AddToClassList("countdown-number");

            // アニメーション効果（スケールパルス）
            PlayPulseAnimation();
        }

        /// <summary>
        /// GO! を表示
        /// </summary>
        public void ShowGo()
        {
            if (_countLabel == null) return;

            _countLabel.text = "GO!";
            _countLabel.RemoveFromClassList("countdown-number");
            _countLabel.AddToClassList("countdown-go");

            // アニメーション効果
            PlayPulseAnimation();
        }

        private void PlayPulseAnimation()
        {
            // USS transition で対応するため、クラス切り替えでアニメーション
            _countLabel.RemoveFromClassList("countdown-pulse");

            // 次フレームでクラス追加（トランジションをトリガー）
            _root.schedule.Execute(() =>
            {
                _countLabel.AddToClassList("countdown-pulse");
            });
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
