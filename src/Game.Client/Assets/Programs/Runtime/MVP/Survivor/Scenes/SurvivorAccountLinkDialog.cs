using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using Game.Shared.Services;
using R3;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// アカウントリンクダイアログ（Presenter）
    /// ゲスト→メール連携 / メール→ゲスト解除 を提供
    /// </summary>
    public class SurvivorAccountLinkDialog :
        GameDialogScene<SurvivorAccountLinkDialog, SurvivorAccountLinkDialogComponent, Unit>
    {
        protected override string AssetPathOrAddress => "SurvivorAccountLinkDialog";

        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ISessionService _sessionService;
        [Inject] private readonly IAuthApiService _authApiService;
        [Inject] private readonly IInputService _inputService;

        private bool _hasValidSession;
        private bool _hasTransferPassword;
        private string _currentUserId;

        public static UniTask RunAsync(IGameSceneService sceneService)
        {
            return sceneService.TransitionDialogAsync<SurvivorAccountLinkDialog, SurvivorAccountLinkDialogComponent, Unit>();
        }

        public override async UniTask Startup()
        {
            await base.Startup();

            // セッションの有効性を確認（トークン期限切れ対策）
            SceneComponent.ShowLoading();
            _hasValidSession = await EnsureValidSessionAsync();

            if (_hasValidSession)
            {
                // セッション有効 → サーバーからプロフィール取得して表示
                await RefreshStatusViewAsync();
            }
            else
            {
                // セッションなし → ログインオプションのみ表示
                SceneComponent.ShowStatusView(true, "-", null, "-");
            }

            // イベント購読
            SceneComponent.OnCloseClicked
                .Subscribe(_ => OnClose().Forget())
                .AddTo(Disposables);

            SceneComponent.OnLinkEmailClicked
                .Subscribe(_ => OnLinkEmail())
                .AddTo(Disposables);

            SceneComponent.OnSubmitLinkClicked
                .Subscribe(x => OnSubmitLink(x.email, x.password, x.confirmPassword).Forget())
                .AddTo(Disposables);

            SceneComponent.OnUnlinkClicked
                .Subscribe(_ => OnUnlink().Forget())
                .AddTo(Disposables);

            SceneComponent.OnBackToStatusClicked
                .Subscribe(_ => OnBackToStatus().Forget())
                .AddTo(Disposables);

            SceneComponent.OnUserIdLoginClicked
                .Subscribe(_ => OnUserIdLoginButton())
                .AddTo(Disposables);

            SceneComponent.OnUserIdLoginSubmitted
                .Subscribe(x => OnUserIdLogin(x.userId, x.password).Forget())
                .AddTo(Disposables);

            SceneComponent.OnForgotPasswordClicked
                .Subscribe(_ => OnForgotPasswordButton())
                .AddTo(Disposables);

            SceneComponent.OnForgotPasswordSubmitted
                .Subscribe(email => OnForgotPassword(email).Forget())
                .AddTo(Disposables);

            SceneComponent.OnResetPasswordSubmitted
                .Subscribe(x => OnResetPassword(x.token, x.newPassword).Forget())
                .AddTo(Disposables);

            SceneComponent.OnIssueTransferPasswordClicked
                .Subscribe(_ => OnIssueTransferPassword().Forget())
                .AddTo(Disposables);

            SceneComponent.OnTransferPasswordDoneClicked
                .Subscribe(_ => OnBackToStatus().Forget())
                .AddTo(Disposables);

            SceneComponent.OnReissueTransferPasswordClicked
                .Subscribe(_ => OnReissueTransferPassword().Forget())
                .AddTo(Disposables);

        }

        public override async UniTask Ready()
        {
            // 入力受付フレームをずらす
            await UniTask.Yield();

            // Escapeキーで閉じる
            Observable.EveryValueChanged(_inputService, x => x.UI.Escape.WasPressedThisFrame(), UnityFrameProvider.Update)
                .Subscribe(escape =>
                {
                    if (escape) OnClose().Forget();
                })
                .AddTo(Disposables);
        }

        /// <summary>
        /// セッションの有効性を確認（未認証時はスキップ）
        /// </summary>
        private async UniTask<bool> EnsureValidSessionAsync()
        {
            if (!_sessionService.IsAuthenticated) return false;
            var refreshResult = await _authApiService.RefreshTokenAsync();
            return refreshResult.IsSuccess;
        }

        /// <summary>
        /// サーバーからプロフィールを取得して StatusView を更新
        /// </summary>
        private async UniTask RefreshStatusViewAsync()
        {
            SceneComponent.ShowLoading();

            var profileResult = await _authApiService.GetMyProfileAsync();

            if (profileResult.IsSuccess)
            {
                var profile = profileResult.Data;
                var isGuest = string.IsNullOrEmpty(profile.authType) ||
                              profile.authType.ToLower() == "guest";

                // Transfer password状態を保存
                _hasTransferPassword = profile.hasTransferPassword;
                _currentUserId = profile.userId;

                SceneComponent.ShowStatusView(isGuest, profile.userName, profile.email,
                    _sessionService.FormatUserId(), _hasValidSession);
            }
            else
            {
                // フォールバック: セッション情報のみで表示
                var isGuest = _sessionService.AuthType == null ||
                              _sessionService.AuthType.ToLower() == "guest";
                _hasTransferPassword = false;
                _currentUserId = _sessionService.UserId;

                SceneComponent.ShowStatusView(isGuest, _sessionService.UserName, null,
                    _sessionService.FormatUserId(), _hasValidSession);
            }
        }

        private async UniTaskVoid OnClose()
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(Unit.Default);
        }

        private void OnLinkEmail()
        {
            SceneComponent.ShowLinkForm();
        }

        private async UniTaskVoid OnBackToStatus()
        {
            if (_hasValidSession)
            {
                await RefreshStatusViewAsync();
            }
            else
            {
                SceneComponent.ShowStatusView(true, "-", null, "-");
            }
        }

        private async UniTaskVoid OnSubmitLink(string email, string password, string confirmPassword)
        {
            // Basic client-side validation
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(confirmPassword))
            {
                SceneComponent.ShowError("All fields are required.");
                return;
            }

            if (password != confirmPassword)
            {
                SceneComponent.ShowError("Passwords do not match.");
                return;
            }

            if (password.Length < 8)
            {
                SceneComponent.ShowError("Password must be at least 8 characters.");
                return;
            }

            SceneComponent.ShowLoading();

            // Step 1: 既存メールアカウントへのログインを試行（認証不要）
            var loginResult = await _authApiService.EmailLoginAsync(email, password);
            if (loginResult.IsSuccess)
            {
                _hasValidSession = true;
                await RefreshStatusViewAsync();
                SceneComponent.ShowSuccess("Logged in successfully!");
                return;
            }

            // Step 2: ログイン失敗 → 新規連携を試行
            // 未認証の場合はゲストを作成
            if (!_hasValidSession)
            {
                var guestResult = await _authApiService.GuestLoginAsync();
                if (!guestResult.IsSuccess)
                {
                    SceneComponent.RevealLinkFormView();
                    SceneComponent.ShowError(guestResult.Error?.Message ?? "Failed to connect.");
                    return;
                }
                _hasValidSession = true;
            }

            var linkResult = await _authApiService.LinkEmailAsync(email, password);
            if (linkResult.IsSuccess)
            {
                await RefreshStatusViewAsync();
                SceneComponent.ShowSuccess("Account linked to email successfully!");
            }
            else
            {
                // 入力値を保持したままフォームを再表示
                SceneComponent.RevealLinkFormView();
                SceneComponent.ShowError(linkResult.Error?.Message ?? "Failed to link account.");
            }
        }

        private async UniTaskVoid OnUnlink()
        {
            // Show confirm dialog
            var options = new SurvivorConfirmDialogOptions
            {
                Title = "UNLINK EMAIL",
                Message = "Revert to guest account?\nYour game data will be preserved.",
                ConfirmButtonText = "UNLINK",
                CancelButtonText = "CANCEL"
            };

            var confirmed = await SurvivorConfirmDialog.RunAsync(_sceneService, options);

            if (!confirmed) return;

            SceneComponent.ShowLoading();

            var response = await _authApiService.UnlinkEmailAsync();

            if (response.IsSuccess)
            {
                await RefreshStatusViewAsync();
                SceneComponent.ShowSuccess("Account reverted to guest successfully!");
            }
            else
            {
                await RefreshStatusViewAsync();
                SceneComponent.ShowError(response.Error?.Message ?? "Failed to unlink account.");
            }
        }

        private void OnUserIdLoginButton()
        {
            SceneComponent.ShowUserIdLoginView();
        }

        private void OnForgotPasswordButton()
        {
            SceneComponent.ShowForgotPasswordView();
        }

        private async UniTaskVoid OnForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                SceneComponent.ShowError("Please enter your email address.");
                return;
            }

            SceneComponent.ShowLoading();

            var response = await _authApiService.ForgotPasswordAsync(email);

            if (response.IsSuccess)
            {
                // メール送信成功 → リセットトークン入力画面へ遷移
                SceneComponent.ShowResetPasswordView();
                SceneComponent.ShowSuccess("Reset link sent! Check your email for the token.");
            }
            else
            {
                SceneComponent.ShowForgotPasswordView();
                SceneComponent.ShowError(response.Error?.Message ?? "Failed to send reset email.");
            }
        }

        private async UniTaskVoid OnResetPassword(string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            {
                SceneComponent.ShowError("Please enter the reset token and new password.");
                return;
            }

            if (newPassword.Length < 8)
            {
                SceneComponent.ShowError("Password must be at least 8 characters.");
                return;
            }

            SceneComponent.ShowLoading();

            var response = await _authApiService.ResetPasswordAsync(token, newPassword);

            if (response.IsSuccess)
            {
                await RefreshStatusViewAsync();
                SceneComponent.ShowSuccess("Password reset successfully!");
            }
            else
            {
                SceneComponent.ShowResetPasswordView();
                SceneComponent.ShowError(response.Error?.Message ?? "Failed to reset password.");
            }
        }

        private async UniTaskVoid OnUserIdLogin(string userId, string password)
        {
            // Remove spaces from UserId input (UI displays "0000 0000 0000" format)
            var cleanUserId = userId?.Replace(" ", "") ?? "";

            if (string.IsNullOrWhiteSpace(cleanUserId) || string.IsNullOrWhiteSpace(password))
            {
                SceneComponent.ShowError("Please enter User ID and password.");
                return;
            }

            SceneComponent.ShowLoading();

            var response = await _authApiService.UserIdLoginAsync(cleanUserId, password);

            if (response.IsSuccess)
            {
                _hasValidSession = true;
                await RefreshStatusViewAsync();
                SceneComponent.ShowSuccess("Logged in successfully!");
            }
            else
            {
                SceneComponent.ShowUserIdLoginView();
                SceneComponent.ShowError(response.Error?.Message ?? "Login failed.");
            }
        }

        private async UniTaskVoid OnIssueTransferPassword()
        {
            // 発行済みの場合は表示のみ（新規発行しない）
            if (_hasTransferPassword)
            {
                var formattedUserId = FormatUserId(_currentUserId);
                // ローカルに保存されたパスワードを取得
                var localPassword = _sessionService.GetTransferPassword();
                SceneComponent.ShowTransferPasswordViewExisting(formattedUserId, localPassword);
                return;
            }

            // 未発行の場合は新規発行
            SceneComponent.ShowLoading();

            var response = await _authApiService.IssueTransferPasswordAsync();

            if (response.IsSuccess)
            {
                _hasTransferPassword = true;
                // パスワードをローカルに保存
                await _sessionService.SaveTransferPasswordAsync(response.Data.transferPassword);

                var formattedUserId = FormatUserId(response.Data.userId);
                SceneComponent.ShowTransferPasswordViewWithPassword(formattedUserId, response.Data.transferPassword);
                SceneComponent.ShowSuccess("Transfer password issued! Save it now.");
            }
            else
            {
                await RefreshStatusViewAsync();
                SceneComponent.ShowError(response.Error?.Message ?? "Failed to issue transfer password.");
            }
        }

        private async UniTaskVoid OnReissueTransferPassword()
        {
            // 確認ダイアログを表示
            var options = new SurvivorConfirmDialogOptions
            {
                Title = "REISSUE PASSWORD",
                Message = "Are you sure you want to reissue?\nThe previous password will be invalidated.",
                ConfirmButtonText = "REISSUE",
                CancelButtonText = "CANCEL"
            };

            var confirmed = await SurvivorConfirmDialog.RunAsync(_sceneService, options);

            if (!confirmed)
            {
                // キャンセル → 発行済み表示画面に戻る
                var formattedUserId = FormatUserId(_currentUserId);
                var localPassword = _sessionService.GetTransferPassword();
                SceneComponent.ShowTransferPasswordViewExisting(formattedUserId, localPassword);
                return;
            }

            // 再発行
            SceneComponent.ShowLoading();

            var response = await _authApiService.IssueTransferPasswordAsync();

            if (response.IsSuccess)
            {
                // パスワードをローカルに保存（上書き）
                await _sessionService.SaveTransferPasswordAsync(response.Data.transferPassword);

                var formattedUserId = FormatUserId(response.Data.userId);
                SceneComponent.ShowTransferPasswordViewWithPassword(formattedUserId, response.Data.transferPassword);
                SceneComponent.ShowSuccess("Transfer password reissued! Save it now.");
            }
            else
            {
                await RefreshStatusViewAsync();
                SceneComponent.ShowError(response.Error?.Message ?? "Failed to reissue transfer password.");
            }
        }

        private static string FormatUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId) || userId.Length != 12)
                return userId ?? "-";

            return $"{userId.Substring(0, 4)} {userId.Substring(4, 4)} {userId.Substring(8, 4)}";
        }

    }
}
