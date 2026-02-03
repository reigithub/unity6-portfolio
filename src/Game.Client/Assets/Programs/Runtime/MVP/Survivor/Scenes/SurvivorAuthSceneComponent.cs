using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// 認証画面のView層
    /// UI Toolkit で各フォームパネルの表示切り替えとイベント発行を担当
    /// </summary>
    public class SurvivorAuthSceneComponent : GameSceneComponent
    {
        public enum AuthViewState
        {
            Menu,
            EmailLogin,
            Register,
            ForgotPassword,
            ResetPassword
        }

        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        // Subjects (View → Presenter)
        private readonly Subject<Unit> _onGuestLoginClicked = new();
        private readonly Subject<(string email, string password)> _onEmailLoginSubmitted = new();
        private readonly Subject<(string email, string password, string displayName)> _onRegisterSubmitted = new();
        private readonly Subject<string> _onForgotPasswordSubmitted = new();
        private readonly Subject<(string token, string newPassword)> _onResetPasswordSubmitted = new();
        private readonly Subject<Unit> _onBackToMenuClicked = new();

        // Observables
        public Observable<Unit> OnGuestLoginClicked => _onGuestLoginClicked;
        public Observable<(string email, string password)> OnEmailLoginSubmitted => _onEmailLoginSubmitted;
        public Observable<(string email, string password, string displayName)> OnRegisterSubmitted => _onRegisterSubmitted;
        public Observable<string> OnForgotPasswordSubmitted => _onForgotPasswordSubmitted;
        public Observable<(string token, string newPassword)> OnResetPasswordSubmitted => _onResetPasswordSubmitted;
        public Observable<Unit> OnBackToMenuClicked => _onBackToMenuClicked;

        // UI Elements
        private VisualElement _root;

        // Panels
        private VisualElement _menuPanel;
        private VisualElement _emailLoginPanel;
        private VisualElement _registerPanel;
        private VisualElement _forgotPasswordPanel;
        private VisualElement _resetPasswordPanel;

        // Menu buttons
        private Button _guestButton;
        private Button _emailLoginButton;
        private Button _registerButton;

        // Email login fields
        private TextField _loginEmailField;
        private TextField _loginPasswordField;
        private Button _loginSubmitButton;
        private Button _forgotPasswordButton;
        private Button _loginBackButton;

        // Register fields
        private TextField _registerNameField;
        private TextField _registerEmailField;
        private TextField _registerPasswordField;
        private VisualElement _passwordStrengthBar;
        private Label _passwordStrengthLabel;
        private Button _registerSubmitButton;
        private Button _registerBackButton;

        // Forgot password fields
        private TextField _forgotEmailField;
        private Button _forgotSubmitButton;
        private Button _forgotBackButton;

        // Reset password fields
        private TextField _resetTokenField;
        private TextField _resetPasswordField;
        private Button _resetSubmitButton;
        private Button _resetBackButton;

        // Messages & overlay
        private Label _errorMessage;
        private Label _successMessage;
        private VisualElement _loadingOverlay;

        private AuthViewState _currentState = AuthViewState.Menu;

        protected override void OnDestroy()
        {
            _onGuestLoginClicked.Dispose();
            _onEmailLoginSubmitted.Dispose();
            _onRegisterSubmitted.Dispose();
            _onForgotPasswordSubmitted.Dispose();
            _onResetPasswordSubmitted.Dispose();
            _onBackToMenuClicked.Dispose();
            base.OnDestroy();
        }

        private void Awake()
        {
            QueryUIElements();
            SetupEventHandlers();
            SetViewState(AuthViewState.Menu);
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;

            // Panels
            _menuPanel = _root.Q<VisualElement>("menu-panel");
            _emailLoginPanel = _root.Q<VisualElement>("email-login-panel");
            _registerPanel = _root.Q<VisualElement>("register-panel");
            _forgotPasswordPanel = _root.Q<VisualElement>("forgot-password-panel");
            _resetPasswordPanel = _root.Q<VisualElement>("reset-password-panel");

            // Menu
            _guestButton = _root.Q<Button>("guest-button");
            _emailLoginButton = _root.Q<Button>("email-login-button");
            _registerButton = _root.Q<Button>("register-button");

            // Email login
            _loginEmailField = _root.Q<TextField>("login-email-field");
            _loginPasswordField = _root.Q<TextField>("login-password-field");
            _loginSubmitButton = _root.Q<Button>("login-submit-button");
            _forgotPasswordButton = _root.Q<Button>("forgot-password-button");
            _loginBackButton = _root.Q<Button>("login-back-button");

            // Register
            _registerNameField = _root.Q<TextField>("register-name-field");
            _registerEmailField = _root.Q<TextField>("register-email-field");
            _registerPasswordField = _root.Q<TextField>("register-password-field");
            _passwordStrengthBar = _root.Q<VisualElement>("password-strength-bar");
            _passwordStrengthLabel = _root.Q<Label>("password-strength-label");
            _registerSubmitButton = _root.Q<Button>("register-submit-button");
            _registerBackButton = _root.Q<Button>("register-back-button");

            // Forgot password
            _forgotEmailField = _root.Q<TextField>("forgot-email-field");
            _forgotSubmitButton = _root.Q<Button>("forgot-submit-button");
            _forgotBackButton = _root.Q<Button>("forgot-back-button");

            // Reset password
            _resetTokenField = _root.Q<TextField>("reset-token-field");
            _resetPasswordField = _root.Q<TextField>("reset-password-field");
            _resetSubmitButton = _root.Q<Button>("reset-submit-button");
            _resetBackButton = _root.Q<Button>("reset-back-button");

            // Messages & overlay
            _errorMessage = _root.Q<Label>("error-message");
            _successMessage = _root.Q<Label>("success-message");
            _loadingOverlay = _root.Q<VisualElement>("loading-overlay");
        }

        private void SetupEventHandlers()
        {
            // Menu
            _guestButton?.RegisterCallback<ClickEvent>(_ =>
                _onGuestLoginClicked.OnNext(Unit.Default));

            _emailLoginButton?.RegisterCallback<ClickEvent>(_ =>
                SetViewState(AuthViewState.EmailLogin));

            _registerButton?.RegisterCallback<ClickEvent>(_ =>
                SetViewState(AuthViewState.Register));

            // Email login
            _loginSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onEmailLoginSubmitted.OnNext((_loginEmailField.value, _loginPasswordField.value)));

            _forgotPasswordButton?.RegisterCallback<ClickEvent>(_ =>
                SetViewState(AuthViewState.ForgotPassword));

            _loginBackButton?.RegisterCallback<ClickEvent>(_ =>
            {
                SetViewState(AuthViewState.Menu);
                _onBackToMenuClicked.OnNext(Unit.Default);
            });

            // Register
            _registerSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onRegisterSubmitted.OnNext((
                    _registerEmailField.value,
                    _registerPasswordField.value,
                    _registerNameField.value)));

            _registerBackButton?.RegisterCallback<ClickEvent>(_ =>
            {
                SetViewState(AuthViewState.Menu);
                _onBackToMenuClicked.OnNext(Unit.Default);
            });

            // Register - password strength
            _registerPasswordField?.RegisterValueChangedCallback(evt =>
                UpdatePasswordStrength(evt.newValue));

            // Forgot password
            _forgotSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onForgotPasswordSubmitted.OnNext(_forgotEmailField.value));

            _forgotBackButton?.RegisterCallback<ClickEvent>(_ =>
                SetViewState(AuthViewState.EmailLogin));

            // Reset password
            _resetSubmitButton?.RegisterCallback<ClickEvent>(_ =>
                _onResetPasswordSubmitted.OnNext((_resetTokenField.value, _resetPasswordField.value)));

            _resetBackButton?.RegisterCallback<ClickEvent>(_ =>
                SetViewState(AuthViewState.ForgotPassword));
        }

        /// <summary>
        /// 表示パネルの切り替え
        /// </summary>
        public void SetViewState(AuthViewState state)
        {
            _currentState = state;
            HideMessages();

            _menuPanel?.RemoveFromClassList("form-panel--hidden");
            _emailLoginPanel?.RemoveFromClassList("form-panel--hidden");
            _registerPanel?.RemoveFromClassList("form-panel--hidden");
            _forgotPasswordPanel?.RemoveFromClassList("form-panel--hidden");
            _resetPasswordPanel?.RemoveFromClassList("form-panel--hidden");

            // 全パネルを非表示にしてから、対象のみ表示
            _menuPanel?.AddToClassList("form-panel--hidden");
            _emailLoginPanel?.AddToClassList("form-panel--hidden");
            _registerPanel?.AddToClassList("form-panel--hidden");
            _forgotPasswordPanel?.AddToClassList("form-panel--hidden");
            _resetPasswordPanel?.AddToClassList("form-panel--hidden");

            switch (state)
            {
                case AuthViewState.Menu:
                    _menuPanel?.RemoveFromClassList("form-panel--hidden");
                    break;
                case AuthViewState.EmailLogin:
                    _emailLoginPanel?.RemoveFromClassList("form-panel--hidden");
                    break;
                case AuthViewState.Register:
                    _registerPanel?.RemoveFromClassList("form-panel--hidden");
                    break;
                case AuthViewState.ForgotPassword:
                    _forgotPasswordPanel?.RemoveFromClassList("form-panel--hidden");
                    break;
                case AuthViewState.ResetPassword:
                    _resetPasswordPanel?.RemoveFromClassList("form-panel--hidden");
                    break;
            }
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        public void ShowError(string message)
        {
            HideMessages();
            if (_errorMessage == null) return;
            _errorMessage.text = message;
            _errorMessage.RemoveFromClassList("error-message--hidden");
        }

        /// <summary>
        /// 成功メッセージを表示
        /// </summary>
        public void ShowSuccess(string message)
        {
            HideMessages();
            if (_successMessage == null) return;
            _successMessage.text = message;
            _successMessage.RemoveFromClassList("success-message--hidden");
        }

        /// <summary>
        /// メッセージを非表示
        /// </summary>
        public void HideMessages()
        {
            _errorMessage?.AddToClassList("error-message--hidden");
            _successMessage?.AddToClassList("success-message--hidden");
        }

        /// <summary>
        /// ローディングオーバーレイの表示制御
        /// </summary>
        public void SetLoading(bool isLoading)
        {
            if (isLoading)
            {
                _loadingOverlay?.RemoveFromClassList("loading-overlay--hidden");
            }
            else
            {
                _loadingOverlay?.AddToClassList("loading-overlay--hidden");
            }

            _root?.SetEnabled(!isLoading);
        }

        /// <summary>
        /// パスワード強度インジケーターの更新
        /// </summary>
        private void UpdatePasswordStrength(string password)
        {
            if (_passwordStrengthBar == null || _passwordStrengthLabel == null) return;

            if (string.IsNullOrEmpty(password))
            {
                _passwordStrengthBar.style.width = new StyleLength(new Length(0, LengthUnit.Percent));
                _passwordStrengthLabel.text = "";
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

            _passwordStrengthBar.style.width = new StyleLength(new Length(percentage, LengthUnit.Percent));
            _passwordStrengthBar.style.backgroundColor = new StyleColor(barColor);
            _passwordStrengthLabel.text = label;
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
        }
    }
}
