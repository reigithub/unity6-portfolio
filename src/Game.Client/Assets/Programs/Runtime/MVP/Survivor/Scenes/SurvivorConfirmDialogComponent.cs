using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// 確認ダイアログのView Component
    /// </summary>
    public class SurvivorConfirmDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<bool> _onResultSelected = new();
        public Observable<bool> OnResultSelected => _onResultSelected;

        // UI Element References
        private VisualElement _root;
        private Label _titleLabel;
        private Label _messageLabel;
        private Button _confirmButton;
        private Button _cancelButton;

        protected override void OnDestroy()
        {
            _onResultSelected.Dispose();
            base.OnDestroy();
        }

        private void Awake()
        {
            QueryUIElements();
            SetupEventHandlers();
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;

            _titleLabel = _root.Q<Label>("title");
            _messageLabel = _root.Q<Label>("message");
            _confirmButton = _root.Q<Button>("confirm-button");
            _cancelButton = _root.Q<Button>("cancel-button");
        }

        private void SetupEventHandlers()
        {
            _confirmButton?.RegisterCallback<ClickEvent>(_ =>
                _onResultSelected.OnNext(true));

            _cancelButton?.RegisterCallback<ClickEvent>(_ =>
                _onResultSelected.OnNext(false));
        }

        /// <summary>
        /// タイトルを設定
        /// </summary>
        public void SetTitle(string title)
        {
            if (_titleLabel != null)
            {
                _titleLabel.text = title;
            }
        }

        /// <summary>
        /// メッセージを設定
        /// </summary>
        public void SetMessage(string message)
        {
            if (_messageLabel != null)
            {
                _messageLabel.text = message;
            }
        }

        /// <summary>
        /// 確認ボタンのテキストを設定
        /// </summary>
        public void SetConfirmButtonText(string text)
        {
            if (_confirmButton != null)
            {
                _confirmButton.text = text;
            }
        }

        /// <summary>
        /// キャンセルボタンのテキストを設定
        /// </summary>
        public void SetCancelButtonText(string text)
        {
            if (_cancelButton != null)
            {
                _cancelButton.text = text;
            }
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
