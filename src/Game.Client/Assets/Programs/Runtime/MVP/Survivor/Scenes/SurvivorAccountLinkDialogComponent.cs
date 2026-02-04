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
        private readonly Subject<(string email, string password, string userName)> _onSubmitLinkClicked = new();
        private readonly Subject<Unit> _onUnlinkClicked = new();
        private readonly Subject<Unit> _onBackToStatusClicked = new();
        private readonly Subject<Unit> _onEmailLoginClicked = new();
        private readonly Subject<(string email, string password)> _onEmailLoginSubmitted = new();
        private readonly Subject<Unit> _onForgotPasswordClicked = new();
        private readonly Subject<string> _onForgotPasswordSubmitted = new();
        private readonly Subject<(string token, string newPassword)> _onResetPasswordSubmitted = new();

        public Observable<Unit> OnCloseClicked => _onCloseClicked;
        public Observable<Unit> OnLinkEmailClicked => _onLinkEmailClicked;
        public Observable<(string email, string password, string userName)> OnSubmitLinkClicked => _onSubmitLinkClicked;
        public Observable<Unit> OnUnlinkClicked => _onUnlinkClicked;
        public Observable<Unit> OnBackToStatusClicked => _onBackToStatusClicked;
        public Observable<Unit> OnEmailLoginClicked => _onEmailLoginClicked;
        public Observable<(string email, string password)> OnEmailLoginSubmitted => _onEmailLoginSubmitted;
        public Observable<Unit> OnForgotPasswordClicked => _onForgotPasswordClicked;
        public Observable<string> OnForgotPasswordSubmitted => _onForgotPasswordSubmitted;
        public Observable<(string token, string newPassword)> OnResetPasswordSubmitted => _onResetPasswordSubmitted;

        // UI Element References
        private VisualElement _root;
        private Button _closeButton;

        // Status View
        private VisualElement _statusView;
        private Label _statusAuthType;
        private Label _statusUserName;
        private VisualElement _statusEmailRow;
        private Label _statusEmail;
        private Button _linkEmailButton;
        private Button _emailLoginButton;
        private Button _unlinkEmailButton;

        // Link Form View
        private VisualElement _linkFormView;
        private TextField _formEmail;
        private TextField _formPassword;
        private TextField _formUserName;
        private Button _submitLinkButton;
        private Button _backButton;

        // Email Login View
        private VisualElement _emailLoginView;
        private TextField _loginEmail;
        private TextField _loginPassword;
        private Button _loginSubmitButton;
        private Button _forgotPasswordButton;
        private Button _loginBackButton;

        // Forgot Password View
        private VisualElement _forgotPasswordView;
        private TextField _forgotEmail;
        private Button _forgotSubmitButton;
        private Button _forgotBackButton;

        // Reset Password View
        private VisualElement _resetPasswordView;
        private TextField _resetToken;
        private TextField _resetNewPassword;
        private VisualElement _resetPasswordStrengthBar;
        private Label _resetPasswordStrengthLabel;
        private Button _resetSubmitButton;
        private Button _resetBackButton;

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
            _onEmailLoginClicked.Dispose();
            _onEmailLoginSubmitted.Dispose();
            _onForgotPasswordClicked.Dispose();
            _onForgotPasswordSubmitted.Dispose();
            _onResetPasswordSubmitted.Dispose();
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
            _statusUserName = _root.Q<Label>("status-display-name");
            _statusEmailRow = _root.Q<VisualElement>("status-email-row");
            _statusEmail = _root.Q<Label>("status-email");
            _linkEmailButton = _root.Q<Button>("link-email-button");
            _emailLoginButton = _root.Q<Button>("email-login-button");
            _unlinkEmailButton = _root.Q<Button>("unlink-email-button");

            // Link Form View
            _linkFormView = _root.Q<VisualElement>("link-form-view");
            _formEmail = _root.Q<TextField>("form-email");
            _formPassword = _root.Q<TextField>("form-password");
            _formUserName = _root.Q<TextField>("form-display-name");
            _submitLinkButton = _root.Q<Button>("submit-link-button");
            _backButton = _root.Q<Button>("back-button");

            // Email Login View
            _emailLoginView = _root.Q<VisualElement>("email-login-view");
            _loginEmail = _root.Q<TextField>("login-email");
            _loginPassword = _root.Q<TextField>("login-password");
            _loginSubmitButton = _root.Q<Button>("login-submit-button");
            _forgotPasswordButton = _root.Q<Button>("forgot-password-button");
            _loginBackButton = _root.Q<Button>("login-back-button");

            // Forgot Password View
            _forgotPasswordView = _root.Q<VisualElement>("forgot-password-view");
            _forgotEmail = _root.Q<TextField>("forgot-email");
            _forgotSubmitButton = _root.Q<Button>("forgot-submit-button");
            _forgotBackButton = _root.Q<Button>("forgot-back-button");

            // Reset Password View
            _resetPasswordView = _root.Q<VisualElement>("reset-password-view");
            _resetToken = _root.Q<TextField>("reset-token");
            _resetNewPassword = _root.Q<TextField>("reset-new-password");
            _resetPasswordStrengthBar = _root.Q<VisualElement>("reset-password-strength-bar");
            _resetPasswordStrengthLabel = _root.Q<Label>("reset-password-strength-label");
            _resetSubmitButton = _root.Q<Button>("reset-submit-button");
            _resetBackButton = _root.Q<Button>("reset-back-button");

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

            _emailLoginButton?.RegisterCallback<ClickEvent>(_ =>
                _onEmailLoginClicked.OnNext(Unit.Default));

            _unlinkEmailButton?.RegisterCallback<ClickEvent>(_ =>
                _onUnlinkClicked.OnNext(Unit.Default));

            _submitLinkButton?.RegisterCallback<ClickEvent>(_ =>
                _onSubmitLinkClicked.OnNext((
                    _formEmail?.value ?? "",
                    _formPassword?.value ?? "",
                    _formUserName?.value ?? "")));

            _backButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackToStatusClicked.OnNext(Unit.Default));

            // Email Login View
            _loginSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onEmailLoginSubmitted.OnNext((
                    _loginEmail?.value ?? "",
                    _loginPassword?.value ?? "")));

            _forgotPasswordButton?.RegisterCallback<ClickEvent>(_ =>
                _onForgotPasswordClicked.OnNext(Unit.Default));

            _loginBackButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackToStatusClicked.OnNext(Unit.Default));

            // Forgot Password View
            _forgotSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onForgotPasswordSubmitted.OnNext(_forgotEmail?.value ?? ""));

            _forgotBackButton?.RegisterCallback<ClickEvent>(_ =>
                _onEmailLoginClicked.OnNext(Unit.Default));

            // Reset Password View
            _resetNewPassword?.RegisterValueChangedCallback(evt =>
                UpdatePasswordStrength(evt.newValue));

            _resetSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onResetPasswordSubmitted.OnNext((
                    _resetToken?.value ?? "",
                    _resetNewPassword?.value ?? "")));

            _resetBackButton?.RegisterCallback<ClickEvent>(_ =>
                _onForgotPasswordClicked.OnNext(Unit.Default));
        }

        /// <summary>
        /// StatusView を表示
        /// </summary>
        public void ShowStatusView(bool isGuest, string userName, string email)
        {
            HideAllViews();
            _statusView?.RemoveFromClassList("view-panel--hidden");

            if (_statusAuthType != null)
                _statusAuthType.text = isGuest ? "Guest" : "Email";

            if (_statusUserName != null)
                _statusUserName.text = userName ?? "-";

            if (_statusEmailRow != null)
                _statusEmailRow.style.display = isGuest ? DisplayStyle.None : DisplayStyle.Flex;

            if (_statusEmail != null)
                _statusEmail.text = string.IsNullOrEmpty(email) ? "-" : email;

            if (_linkEmailButton != null)
                _linkEmailButton.style.display = isGuest ? DisplayStyle.Flex : DisplayStyle.None;

            if (_emailLoginButton != null)
                _emailLoginButton.style.display = isGuest ? DisplayStyle.Flex : DisplayStyle.None;

            if (_unlinkEmailButton != null)
                _unlinkEmailButton.style.display = isGuest ? DisplayStyle.None : DisplayStyle.Flex;

            HideMessage();
        }

        /// <summary>
        /// LinkFormView を表示
        /// </summary>
        public void ShowLinkForm(string currentUserName)
        {
            HideAllViews();
            _linkFormView?.RemoveFromClassList("view-panel--hidden");

            if (_formUserName != null)
                _formUserName.value = currentUserName ?? "";

            if (_formEmail != null)
                _formEmail.value = "";

            if (_formPassword != null)
                _formPassword.value = "";

            HideMessage();
        }

        /// <summary>
        /// EmailLoginView を表示
        /// </summary>
        public void ShowEmailLoginView()
        {
            HideAllViews();
            _emailLoginView?.RemoveFromClassList("view-panel--hidden");

            if (_loginEmail != null)
                _loginEmail.value = "";

            if (_loginPassword != null)
                _loginPassword.value = "";

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
        /// 全ビューを非表示
        /// </summary>
        private void HideAllViews()
        {
            _statusView?.AddToClassList("view-panel--hidden");
            _linkFormView?.AddToClassList("view-panel--hidden");
            _emailLoginView?.AddToClassList("view-panel--hidden");
            _forgotPasswordView?.AddToClassList("view-panel--hidden");
            _resetPasswordView?.AddToClassList("view-panel--hidden");
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

        /// <summary>
        /// パスワード強度インジケーターの更新
        /// </summary>
        private void UpdatePasswordStrength(string password)
        {
            if (_resetPasswordStrengthBar == null || _resetPasswordStrengthLabel == null) return;

            if (string.IsNullOrEmpty(password))
            {
                _resetPasswordStrengthBar.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
                _resetPasswordStrengthLabel.text = "";
                return;
            }

            int score = 0;
            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[A-Z]")) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[0-9]")) score++;
            if (System.Text.RegularExpressions.Regex.IsMatch(password, @"[^a-zA-Z0-9]")) score++;

            float percentage = score / 5f * 100f;
            string label;
            Color barColor;

            switch (score)
            {
                case 0:
                case 1:
                    label = "Weak";
                    barColor = new Color(0.7f, 0.23f, 0.23f);
                    break;
                case 2:
                case 3:
                    label = "Fair";
                    barColor = new Color(0.85f, 0.65f, 0.13f);
                    break;
                default:
                    label = "Strong";
                    barColor = new Color(0.2f, 0.7f, 0.3f);
                    break;
            }

            _resetPasswordStrengthBar.style.width = new StyleLength(new Length(percentage, LengthUnit.Percent));
            _resetPasswordStrengthBar.style.backgroundColor = new StyleColor(barColor);
            _resetPasswordStrengthLabel.text = label;
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
