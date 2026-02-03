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

        public static UniTask RunAsync(IGameSceneService sceneService)
        {
            return sceneService.TransitionDialogAsync<SurvivorAccountLinkDialog, SurvivorAccountLinkDialogComponent, Unit>();
        }

        public override async UniTask Startup()
        {
            await base.Startup();

            // セッションの有効性を確認（トークン期限切れ対策）
            SceneComponent.ShowLoading();
            bool sessionValid = await EnsureValidSessionAsync();
            if (!sessionValid)
            {
                SceneComponent.ShowStatusView(true, _sessionService.DisplayName ?? "-", null);
                SceneComponent.ShowError("Session expired. Please restart the game and log in again.");
                // Close のみ受け付ける
                SceneComponent.OnCloseClicked
                    .Subscribe(_ => OnClose().Forget())
                    .AddTo(Disposables);
                return;
            }

            // 初期表示（サーバーからプロフィール取得）
            await RefreshStatusViewAsync();

            // イベント購読
            SceneComponent.OnCloseClicked
                .Subscribe(_ => OnClose().Forget())
                .AddTo(Disposables);

            SceneComponent.OnLinkEmailClicked
                .Subscribe(_ => OnLinkEmail())
                .AddTo(Disposables);

            SceneComponent.OnSubmitLinkClicked
                .Subscribe(x => OnSubmitLink(x.email, x.password, x.displayName).Forget())
                .AddTo(Disposables);

            SceneComponent.OnUnlinkClicked
                .Subscribe(_ => OnUnlink().Forget())
                .AddTo(Disposables);

            SceneComponent.OnBackToStatusClicked
                .Subscribe(_ => OnBackToStatus().Forget())
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
        /// セッションの有効性を確認し、期限切れの場合はリフレッシュまたは再ログインを試みる
        /// </summary>
        private async UniTask<bool> EnsureValidSessionAsync()
        {
            // 1. トークンリフレッシュを試みる
            var refreshResult = await _authApiService.RefreshTokenAsync();
            if (refreshResult.IsSuccess)
            {
                return true;
            }

            // 2. リフレッシュ失敗（401）→ ゲストなら再ログインを試みる
            var authType = _sessionService.AuthType?.ToLower();
            if (authType == "guest" || string.IsNullOrEmpty(authType))
            {
                var guestResult = await _authApiService.GuestLoginAsync();
                return guestResult.IsSuccess;
            }

            return false;
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
                SceneComponent.ShowStatusView(isGuest, profile.displayName, profile.email);
            }
            else
            {
                // フォールバック: セッション情報のみで表示
                var isGuest = _sessionService.AuthType == null ||
                              _sessionService.AuthType.ToLower() == "guest";
                SceneComponent.ShowStatusView(isGuest, _sessionService.DisplayName, null);
            }
        }

        private async UniTaskVoid OnClose()
        {
            SceneComponent.SetInteractables(false);
            TrySetResult(Unit.Default);
        }

        private void OnLinkEmail()
        {
            SceneComponent.ShowLinkForm(_sessionService.DisplayName);
        }

        private async UniTaskVoid OnBackToStatus()
        {
            await RefreshStatusViewAsync();
        }

        private async UniTaskVoid OnSubmitLink(string email, string password, string displayName)
        {
            // Basic client-side validation
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(displayName))
            {
                SceneComponent.ShowError("All fields are required.");
                return;
            }

            if (password.Length < 8)
            {
                SceneComponent.ShowError("Password must be at least 8 characters.");
                return;
            }

            SceneComponent.ShowLoading();

            var response = await _authApiService.LinkEmailAsync(email, password, displayName);

            if (response.IsSuccess)
            {
                await RefreshStatusViewAsync();
                SceneComponent.ShowSuccess("Account linked to email successfully!");
            }
            else
            {
                // Return to form on error
                SceneComponent.ShowLinkForm(displayName);
                SceneComponent.ShowError(response.Error?.Message ?? "Failed to link account.");
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
    }
}
