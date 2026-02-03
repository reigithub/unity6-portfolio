using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// アカウントリンクダイアログのView Component
    /// </summary>
    public class SurvivorAccountLinkDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<Unit> _onCloseClicked = new();
        private readonly Subject<Unit> _onLinkEmailClicked = new();
        private readonly Subject<(string email, string password, string displayName)> _onSubmitLinkClicked = new();
        private readonly Subject<Unit> _onUnlinkClicked = new();
        private readonly Subject<Unit> _onBackToStatusClicked = new();

        public Observable<Unit> OnCloseClicked => _onCloseClicked;
        public Observable<Unit> OnLinkEmailClicked => _onLinkEmailClicked;
        public Observable<(string email, string password, string displayName)> OnSubmitLinkClicked => _onSubmitLinkClicked;
        public Observable<Unit> OnUnlinkClicked => _onUnlinkClicked;
        public Observable<Unit> OnBackToStatusClicked => _onBackToStatusClicked;

        // UI Element References
        private VisualElement _root;
        private Button _closeButton;

        // Status View
        private VisualElement _statusView;
        private Label _statusAuthType;
        private Label _statusDisplayName;
        private VisualElement _statusEmailRow;
        private Label _statusEmail;
        private Button _linkEmailButton;
        private Button _unlinkEmailButton;

        // Link Form View
        private VisualElement _linkFormView;
        private TextField _formEmail;
        private TextField _formPassword;
        private TextField _formDisplayName;
        private Button _submitLinkButton;
        private Button _backButton;

        // Loading View
        private VisualElement _loadingView;

        // Message
        private Label _messageLabel;

        protected override void OnDestroy()
        {
            _onCloseClicked.Dispose();
            _onLinkEmailClicked.Dispose();
            _onSubmitLinkClicked.Dispose();
            _onUnlinkClicked.Dispose();
            _onBackToStatusClicked.Dispose();
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
            _closeButton = _root.Q<Button>("close-button");

            // Status View
            _statusView = _root.Q<VisualElement>("status-view");
            _statusAuthType = _root.Q<Label>("status-auth-type");
            _statusDisplayName = _root.Q<Label>("status-display-name");
            _statusEmailRow = _root.Q<VisualElement>("status-email-row");
            _statusEmail = _root.Q<Label>("status-email");
            _linkEmailButton = _root.Q<Button>("link-email-button");
            _unlinkEmailButton = _root.Q<Button>("unlink-email-button");

            // Link Form View
            _linkFormView = _root.Q<VisualElement>("link-form-view");
            _formEmail = _root.Q<TextField>("form-email");
            _formPassword = _root.Q<TextField>("form-password");
            _formDisplayName = _root.Q<TextField>("form-display-name");
            _submitLinkButton = _root.Q<Button>("submit-link-button");
            _backButton = _root.Q<Button>("back-button");

            // Loading View
            _loadingView = _root.Q<VisualElement>("loading-view");

            // Message
            _messageLabel = _root.Q<Label>("message-label");
        }

        private void SetupEventHandlers()
        {
            _closeButton?.RegisterCallback<ClickEvent>(_ =>
                _onCloseClicked.OnNext(Unit.Default));

            _linkEmailButton?.RegisterCallback<ClickEvent>(_ =>
                _onLinkEmailClicked.OnNext(Unit.Default));

            _unlinkEmailButton?.RegisterCallback<ClickEvent>(_ =>
                _onUnlinkClicked.OnNext(Unit.Default));

            _submitLinkButton?.RegisterCallback<ClickEvent>(_ =>
                _onSubmitLinkClicked.OnNext((
                    _formEmail?.value ?? "",
                    _formPassword?.value ?? "",
                    _formDisplayName?.value ?? "")));

            _backButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackToStatusClicked.OnNext(Unit.Default));
        }

        /// <summary>
        /// StatusView を表示
        /// </summary>
        public void ShowStatusView(bool isGuest, string displayName, string email)
        {
            _statusView?.RemoveFromClassList("view-panel--hidden");
            _linkFormView?.AddToClassList("view-panel--hidden");
            _loadingView?.AddToClassList("view-panel--hidden");

            if (_statusAuthType != null)
                _statusAuthType.text = isGuest ? "Guest" : "Email";

            if (_statusDisplayName != null)
                _statusDisplayName.text = displayName ?? "-";

            if (_statusEmailRow != null)
                _statusEmailRow.style.display = isGuest ? DisplayStyle.None : DisplayStyle.Flex;

            if (_statusEmail != null)
                _statusEmail.text = string.IsNullOrEmpty(email) ? "-" : email;

            if (_linkEmailButton != null)
                _linkEmailButton.style.display = isGuest ? DisplayStyle.Flex : DisplayStyle.None;

            if (_unlinkEmailButton != null)
                _unlinkEmailButton.style.display = isGuest ? DisplayStyle.None : DisplayStyle.Flex;

            HideMessage();
        }

        /// <summary>
        /// LinkFormView を表示
        /// </summary>
        public void ShowLinkForm(string currentDisplayName)
        {
            _statusView?.AddToClassList("view-panel--hidden");
            _linkFormView?.RemoveFromClassList("view-panel--hidden");
            _loadingView?.AddToClassList("view-panel--hidden");

            // DisplayName のデフォルト値を設定
            if (_formDisplayName != null)
                _formDisplayName.value = currentDisplayName ?? "";

            if (_formEmail != null)
                _formEmail.value = "";

            if (_formPassword != null)
                _formPassword.value = "";

            HideMessage();
        }

        /// <summary>
        /// ローディング表示
        /// </summary>
        public void ShowLoading()
        {
            _statusView?.AddToClassList("view-panel--hidden");
            _linkFormView?.AddToClassList("view-panel--hidden");
            _loadingView?.RemoveFromClassList("view-panel--hidden");
            HideMessage();
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        public void ShowError(string message)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = message;
            _messageLabel.RemoveFromClassList("message-label--success");
            _messageLabel.AddToClassList("message-label--error");
        }

        /// <summary>
        /// 成功メッセージを表示
        /// </summary>
        public void ShowSuccess(string message)
        {
            if (_messageLabel == null) return;
            _messageLabel.text = message;
            _messageLabel.RemoveFromClassList("message-label--error");
            _messageLabel.AddToClassList("message-label--success");
        }

        /// <summary>
        /// メッセージを非表示
        /// </summary>
        public void HideMessage()
        {
            if (_messageLabel == null) return;
            _messageLabel.RemoveFromClassList("message-label--error");
            _messageLabel.RemoveFromClassList("message-label--success");
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
