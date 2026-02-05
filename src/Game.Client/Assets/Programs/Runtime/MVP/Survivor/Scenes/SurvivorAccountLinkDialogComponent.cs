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
        private readonly Subject<(string email, string password, string confirmPassword)> _onSubmitLinkClicked = new();
        private readonly Subject<Unit> _onUnlinkClicked = new();
        private readonly Subject<Unit> _onBackToStatusClicked = new();
        private readonly Subject<Unit> _onUserIdLoginClicked = new();
        private readonly Subject<(string userId, string password)> _onUserIdLoginSubmitted = new();
        private readonly Subject<Unit> _onForgotPasswordClicked = new();
        private readonly Subject<string> _onForgotPasswordSubmitted = new();
        private readonly Subject<(string token, string newPassword)> _onResetPasswordSubmitted = new();
        private readonly Subject<Unit> _onIssueTransferPasswordClicked = new();
        private readonly Subject<Unit> _onTransferPasswordDoneClicked = new();
        private readonly Subject<Unit> _onReissueTransferPasswordClicked = new();

        public Observable<Unit> OnCloseClicked => _onCloseClicked;
        public Observable<Unit> OnLinkEmailClicked => _onLinkEmailClicked;
        public Observable<(string email, string password, string confirmPassword)> OnSubmitLinkClicked => _onSubmitLinkClicked;
        public Observable<Unit> OnUnlinkClicked => _onUnlinkClicked;
        public Observable<Unit> OnBackToStatusClicked => _onBackToStatusClicked;
        public Observable<Unit> OnUserIdLoginClicked => _onUserIdLoginClicked;
        public Observable<(string userId, string password)> OnUserIdLoginSubmitted => _onUserIdLoginSubmitted;
        public Observable<Unit> OnForgotPasswordClicked => _onForgotPasswordClicked;
        public Observable<string> OnForgotPasswordSubmitted => _onForgotPasswordSubmitted;
        public Observable<(string token, string newPassword)> OnResetPasswordSubmitted => _onResetPasswordSubmitted;
        public Observable<Unit> OnIssueTransferPasswordClicked => _onIssueTransferPasswordClicked;
        public Observable<Unit> OnTransferPasswordDoneClicked => _onTransferPasswordDoneClicked;
        public Observable<Unit> OnReissueTransferPasswordClicked => _onReissueTransferPasswordClicked;

        // UI Element References
        private VisualElement _root;
        private Button _closeButton;

        // Status View
        private VisualElement _statusView;
        private Label _statusAuthType;
        private Label _statusUserId;
        private Label _statusUserName;
        private VisualElement _statusEmailRow;
        private Label _statusEmail;
        private Button _linkEmailButton;
        private Button _issueTransferPasswordButton;
        private Button _userIdLoginButton;
        private Button _unlinkEmailButton;

        // Link Form View
        private VisualElement _linkFormView;
        private TextField _formEmail;
        private TextField _formPassword;
        private TextField _formConfirmPassword;
        private Button _submitLinkButton;
        private Button _backButton;
        private Button _linkForgotPasswordButton;

        // UserId Login View
        private VisualElement _userIdLoginView;
        private TextField _userIdInput;
        private TextField _userIdPassword;
        private Button _userIdLoginSubmitButton;
        private Button _userIdLoginBackButton;
        private Button _userIdForgotPasswordButton;

        // Forgot Password View
        private VisualElement _forgotPasswordView;
        private TextField _forgotEmail;
        private Button _forgotSubmitButton;
        private Button _forgotBackButton;

        // Reset Password View
        private VisualElement _resetPasswordView;
        private TextField _resetToken;
        private TextField _resetNewPassword;
        private Button _resetSubmitButton;
        private Button _resetBackButton;

        // Loading View
        private VisualElement _loadingView;

        // Transfer Password View
        private VisualElement _transferPasswordView;
        private Label _transferTitle;
        private Label _transferWarning;
        private Label _transferUserId;
        private VisualElement _transferPasswordRow;
        private Label _transferPassword;
        private Label _transferIssuedMessage;
        private Label _transferUnavailableMessage;
        private Button _reissueTransferPasswordButton;
        private Button _transferPasswordDoneButton;

        // Message
        private Label _messageLabel;

        protected override void OnDestroy()
        {
            _onCloseClicked.Dispose();
            _onLinkEmailClicked.Dispose();
            _onSubmitLinkClicked.Dispose();
            _onUnlinkClicked.Dispose();
            _onBackToStatusClicked.Dispose();
            _onUserIdLoginClicked.Dispose();
            _onUserIdLoginSubmitted.Dispose();
            _onForgotPasswordClicked.Dispose();
            _onForgotPasswordSubmitted.Dispose();
            _onResetPasswordSubmitted.Dispose();
            _onIssueTransferPasswordClicked.Dispose();
            _onTransferPasswordDoneClicked.Dispose();
            _onReissueTransferPasswordClicked.Dispose();
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
            _statusUserId = _root.Q<Label>("status-user-id");
            _statusUserName = _root.Q<Label>("status-display-name");
            _statusEmailRow = _root.Q<VisualElement>("status-email-row");
            _statusEmail = _root.Q<Label>("status-email");
            _linkEmailButton = _root.Q<Button>("link-email-button");
            _issueTransferPasswordButton = _root.Q<Button>("issue-transfer-password-button");
            _userIdLoginButton = _root.Q<Button>("userid-login-button");
            _unlinkEmailButton = _root.Q<Button>("unlink-email-button");

            // Link Form View
            _linkFormView = _root.Q<VisualElement>("link-form-view");
            _formEmail = _root.Q<TextField>("form-email");
            _formPassword = _root.Q<TextField>("form-password");
            _formConfirmPassword = _root.Q<TextField>("form-confirm-password");
            _submitLinkButton = _root.Q<Button>("submit-link-button");
            _backButton = _root.Q<Button>("back-button");
            _linkForgotPasswordButton = _root.Q<Button>("link-forgot-password-button");

            // UserId Login View
            _userIdLoginView = _root.Q<VisualElement>("userid-login-view");
            _userIdInput = _root.Q<TextField>("userid-input");
            _userIdPassword = _root.Q<TextField>("userid-password");
            _userIdLoginSubmitButton = _root.Q<Button>("userid-login-submit-button");
            _userIdLoginBackButton = _root.Q<Button>("userid-login-back-button");
            _userIdForgotPasswordButton = _root.Q<Button>("userid-forgot-password-button");

            // Forgot Password View
            _forgotPasswordView = _root.Q<VisualElement>("forgot-password-view");
            _forgotEmail = _root.Q<TextField>("forgot-email");
            _forgotSubmitButton = _root.Q<Button>("forgot-submit-button");
            _forgotBackButton = _root.Q<Button>("forgot-back-button");

            // Reset Password View
            _resetPasswordView = _root.Q<VisualElement>("reset-password-view");
            _resetToken = _root.Q<TextField>("reset-token");
            _resetNewPassword = _root.Q<TextField>("reset-new-password");
            _resetSubmitButton = _root.Q<Button>("reset-submit-button");
            _resetBackButton = _root.Q<Button>("reset-back-button");

            // Loading View
            _loadingView = _root.Q<VisualElement>("loading-view");

            // Transfer Password View
            _transferPasswordView = _root.Q<VisualElement>("transfer-password-view");
            _transferTitle = _root.Q<Label>("transfer-title");
            _transferWarning = _root.Q<Label>("transfer-warning");
            _transferUserId = _root.Q<Label>("transfer-user-id");
            _transferPasswordRow = _root.Q<VisualElement>("transfer-password-row");
            _transferPassword = _root.Q<Label>("transfer-password");
            _transferIssuedMessage = _root.Q<Label>("transfer-issued-message");
            _transferUnavailableMessage = _root.Q<Label>("transfer-unavailable-message");
            _reissueTransferPasswordButton = _root.Q<Button>("reissue-transfer-password-button");
            _transferPasswordDoneButton = _root.Q<Button>("transfer-password-done-button");

            // Message
            _messageLabel = _root.Q<Label>("message-label");
        }

        private void SetupEventHandlers()
        {
            _closeButton?.RegisterCallback<ClickEvent>(_ =>
                _onCloseClicked.OnNext(Unit.Default));

            _linkEmailButton?.RegisterCallback<ClickEvent>(_ =>
                _onLinkEmailClicked.OnNext(Unit.Default));

            _issueTransferPasswordButton?.RegisterCallback<ClickEvent>(_ =>
                _onIssueTransferPasswordClicked.OnNext(Unit.Default));

            _userIdLoginButton?.RegisterCallback<ClickEvent>(_ =>
                _onUserIdLoginClicked.OnNext(Unit.Default));

            _unlinkEmailButton?.RegisterCallback<ClickEvent>(_ =>
                _onUnlinkClicked.OnNext(Unit.Default));

            _submitLinkButton?.RegisterCallback<ClickEvent>(_ =>
                _onSubmitLinkClicked.OnNext((
                    _formEmail?.value ?? "",
                    _formPassword?.value ?? "",
                    _formConfirmPassword?.value ?? "")));

            _backButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackToStatusClicked.OnNext(Unit.Default));

            // UserId Login View
            _userIdLoginSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onUserIdLoginSubmitted.OnNext((
                    _userIdInput?.value ?? "",
                    _userIdPassword?.value ?? "")));

            _userIdLoginBackButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackToStatusClicked.OnNext(Unit.Default));

            _userIdForgotPasswordButton?.RegisterCallback<ClickEvent>(_ =>
                _onForgotPasswordClicked.OnNext(Unit.Default));

            // Link Form View - Forgot Password
            _linkForgotPasswordButton?.RegisterCallback<ClickEvent>(_ =>
                _onForgotPasswordClicked.OnNext(Unit.Default));

            // Forgot Password View
            _forgotSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onForgotPasswordSubmitted.OnNext(_forgotEmail?.value ?? ""));

            _forgotBackButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackToStatusClicked.OnNext(Unit.Default));

            // Reset Password View
            _resetSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onResetPasswordSubmitted.OnNext((
                    _resetToken?.value ?? "",
                    _resetNewPassword?.value ?? "")));

            _resetBackButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackToStatusClicked.OnNext(Unit.Default));

            // Transfer Password View
            _transferPasswordDoneButton?.RegisterCallback<ClickEvent>(_ =>
                _onTransferPasswordDoneClicked.OnNext(Unit.Default));

            _reissueTransferPasswordButton?.RegisterCallback<ClickEvent>(_ =>
                _onReissueTransferPasswordClicked.OnNext(Unit.Default));
        }

        /// <summary>
        /// StatusView を表示
        /// </summary>
        public void ShowStatusView(bool isGuest, string userName, string email,
            string formattedUserId = null, bool hasValidSession = false)
        {
            HideAllViews();
            _statusView?.RemoveFromClassList("view-panel--hidden");

            if (_statusAuthType != null)
                _statusAuthType.text = isGuest ? "Guest" : "Email";

            if (_statusUserId != null)
                _statusUserId.text = formattedUserId ?? "-";

            if (_statusUserName != null)
                _statusUserName.text = userName ?? "-";

            if (_statusEmailRow != null)
                _statusEmailRow.style.display = isGuest ? DisplayStyle.None : DisplayStyle.Flex;

            if (_statusEmail != null)
                _statusEmail.text = string.IsNullOrEmpty(email) ? "-" : email;

            if (_linkEmailButton != null)
                _linkEmailButton.style.display = isGuest ? DisplayStyle.Flex : DisplayStyle.None;

            if (_issueTransferPasswordButton != null)
                _issueTransferPasswordButton.style.display = (isGuest && hasValidSession)
                    ? DisplayStyle.Flex : DisplayStyle.None;

            if (_userIdLoginButton != null)
                _userIdLoginButton.style.display = isGuest ? DisplayStyle.Flex : DisplayStyle.None;

            if (_unlinkEmailButton != null)
                _unlinkEmailButton.style.display = isGuest ? DisplayStyle.None : DisplayStyle.Flex;

            HideMessage();
        }

        /// <summary>
        /// LinkFormView を表示（初期遷移用、フィールドをクリア）
        /// </summary>
        public void ShowLinkForm()
        {
            HideAllViews();
            _linkFormView?.RemoveFromClassList("view-panel--hidden");

            if (_formEmail != null)
                _formEmail.value = "";

            if (_formPassword != null)
                _formPassword.value = "";

            if (_formConfirmPassword != null)
                _formConfirmPassword.value = "";

            HideMessage();
        }

        /// <summary>
        /// LinkFormView を表示（入力値を保持したまま再表示）
        /// </summary>
        public void RevealLinkFormView()
        {
            HideAllViews();
            _linkFormView?.RemoveFromClassList("view-panel--hidden");
        }

        /// <summary>
        /// UserIdLoginView を表示
        /// </summary>
        public void ShowUserIdLoginView()
        {
            HideAllViews();
            _userIdLoginView?.RemoveFromClassList("view-panel--hidden");

            if (_userIdInput != null)
                _userIdInput.value = "";

            if (_userIdPassword != null)
                _userIdPassword.value = "";

            HideMessage();
        }

        /// <summary>
        /// ForgotPasswordView を表示
        /// </summary>
        public void ShowForgotPasswordView()
        {
            HideAllViews();
            _forgotPasswordView?.RemoveFromClassList("view-panel--hidden");

            if (_forgotEmail != null)
                _forgotEmail.value = "";

            HideMessage();
        }

        /// <summary>
        /// ResetPasswordView を表示
        /// </summary>
        public void ShowResetPasswordView()
        {
            HideAllViews();
            _resetPasswordView?.RemoveFromClassList("view-panel--hidden");

            if (_resetToken != null)
                _resetToken.value = "";

            if (_resetNewPassword != null)
                _resetNewPassword.value = "";

            HideMessage();
        }

        /// <summary>
        /// ローディング表示
        /// </summary>
        public void ShowLoading()
        {
            HideAllViews();
            _loadingView?.RemoveFromClassList("view-panel--hidden");
            HideMessage();
        }

        /// <summary>
        /// TransferPasswordView を表示（新規発行/再発行後、パスワード表示あり）
        /// </summary>
        public void ShowTransferPasswordViewWithPassword(string userId, string password)
        {
            HideAllViews();
            _transferPasswordView?.RemoveFromClassList("view-panel--hidden");

            // タイトルと警告表示
            if (_transferTitle != null)
                _transferTitle.text = "Transfer Password Issued";

            if (_transferWarning != null)
                _transferWarning.style.display = DisplayStyle.Flex;

            if (_transferUserId != null)
                _transferUserId.text = userId ?? "-";

            // パスワード表示
            if (_transferPasswordRow != null)
                _transferPasswordRow.style.display = DisplayStyle.Flex;

            if (_transferPassword != null)
                _transferPassword.text = password ?? "-";

            // メッセージ非表示
            if (_transferIssuedMessage != null)
                _transferIssuedMessage.style.display = DisplayStyle.None;

            if (_transferUnavailableMessage != null)
                _transferUnavailableMessage.style.display = DisplayStyle.None;

            // 再発行ボタン非表示（発行直後なので不要）
            if (_reissueTransferPasswordButton != null)
                _reissueTransferPasswordButton.style.display = DisplayStyle.None;

            HideMessage();
        }

        /// <summary>
        /// TransferPasswordView を表示（発行済み）
        /// passwordがnullの場合はパスワード表示不可、再発行を促す
        /// </summary>
        public void ShowTransferPasswordViewExisting(string userId, string password)
        {
            HideAllViews();
            _transferPasswordView?.RemoveFromClassList("view-panel--hidden");

            bool hasPassword = !string.IsNullOrEmpty(password);

            // タイトル
            if (_transferTitle != null)
                _transferTitle.text = "Transfer Password";

            // 警告非表示
            if (_transferWarning != null)
                _transferWarning.style.display = DisplayStyle.None;

            if (_transferUserId != null)
                _transferUserId.text = userId ?? "-";

            // パスワード表示（ローカルにある場合のみ）
            if (_transferPasswordRow != null)
                _transferPasswordRow.style.display = hasPassword ? DisplayStyle.Flex : DisplayStyle.None;

            if (_transferPassword != null)
                _transferPassword.text = password ?? "-";

            // 発行済みメッセージ（パスワードがある場合）
            if (_transferIssuedMessage != null)
                _transferIssuedMessage.style.display = hasPassword ? DisplayStyle.Flex : DisplayStyle.None;

            // パスワード不明メッセージ（パスワードがない場合）
            if (_transferUnavailableMessage != null)
                _transferUnavailableMessage.style.display = hasPassword ? DisplayStyle.None : DisplayStyle.Flex;

            // 再発行ボタン表示
            if (_reissueTransferPasswordButton != null)
                _reissueTransferPasswordButton.style.display = DisplayStyle.Flex;

            HideMessage();
        }

        /// <summary>
        /// 全ビューを非表示
        /// </summary>
        private void HideAllViews()
        {
            _statusView?.AddToClassList("view-panel--hidden");
            _linkFormView?.AddToClassList("view-panel--hidden");
            _userIdLoginView?.AddToClassList("view-panel--hidden");
            _forgotPasswordView?.AddToClassList("view-panel--hidden");
            _resetPasswordView?.AddToClassList("view-panel--hidden");
            _transferPasswordView?.AddToClassList("view-panel--hidden");
            _loadingView?.AddToClassList("view-panel--hidden");
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
