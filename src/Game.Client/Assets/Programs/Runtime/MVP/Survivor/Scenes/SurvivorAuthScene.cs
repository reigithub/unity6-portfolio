using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.MVP.Core.Scenes;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// 認証画面シーン（Presenter）
    /// ゲストログイン・メールログイン・メール登録・パスワードリセットを管理
    /// </summary>
    public class SurvivorAuthScene : GamePrefabScene<SurvivorAuthScene, SurvivorAuthSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IAuthApiService _authApiService;
        [Inject] private readonly ISessionService _sessionService;
        [Inject] private readonly IAudioService _audioService;

        protected override string AssetPathOrAddress => "SurvivorAuthScene";

        public override async UniTask Startup()
        {
            await base.Startup();

            // View のイベントを購読
            SceneComponent.OnGuestLoginClicked
                .Subscribe(_ => OnGuestLogin().Forget())
                .AddTo(Disposables);

            SceneComponent.OnEmailLoginSubmitted
                .Subscribe(data => OnEmailLogin(data.email, data.password).Forget())
                .AddTo(Disposables);

            SceneComponent.OnRegisterSubmitted
                .Subscribe(data => OnRegister(data.email, data.password, data.displayName).Forget())
                .AddTo(Disposables);

            SceneComponent.OnForgotPasswordSubmitted
                .Subscribe(email => OnForgotPassword(email).Forget())
                .AddTo(Disposables);

            SceneComponent.OnResetPasswordSubmitted
                .Subscribe(data => OnResetPassword(data.token, data.newPassword).Forget())
                .AddTo(Disposables);
        }

        private async UniTaskVoid OnGuestLogin()
        {
            SceneComponent.SetLoading(true);

            var response = await _authApiService.GuestLoginAsync();
            if (response.IsSuccess)
            {
                await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart);
                await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
            }
            else
            {
                SceneComponent.SetLoading(false);
                SceneComponent.ShowError(response.Error?.Message ?? "Guest login failed.");
            }
        }

        private async UniTaskVoid OnEmailLogin(string email, string password)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            {
                SceneComponent.ShowError("Please enter email and password.");
                return;
            }

            SceneComponent.SetLoading(true);

            var response = await _authApiService.EmailLoginAsync(email, password);
            if (response.IsSuccess)
            {
                await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart);
                await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
            }
            else
            {
                SceneComponent.SetLoading(false);
                SceneComponent.ShowError(response.Error?.Message ?? "Login failed.");
            }
        }

        private async UniTaskVoid OnRegister(string email, string password, string displayName)
        {
            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(displayName))
            {
                SceneComponent.ShowError("Please fill in all fields.");
                return;
            }

            if (password.Length < 8)
            {
                SceneComponent.ShowError("Password must be at least 8 characters.");
                return;
            }

            SceneComponent.SetLoading(true);

            var response = await _authApiService.EmailRegisterAsync(email, password, displayName);
            if (response.IsSuccess)
            {
                await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart);
                await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
            }
            else
            {
                SceneComponent.SetLoading(false);
                SceneComponent.ShowError(response.Error?.Message ?? "Registration failed.");
            }
        }

        private async UniTaskVoid OnForgotPassword(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                SceneComponent.ShowError("Please enter your email.");
                return;
            }

            SceneComponent.SetLoading(true);

            var response = await _authApiService.ForgotPasswordAsync(email);
            SceneComponent.SetLoading(false);

            if (response.IsSuccess)
            {
                SceneComponent.ShowSuccess(response.Data?.message ?? "Reset token sent to your email.");
                SceneComponent.SetViewState(SurvivorAuthSceneComponent.AuthViewState.ResetPassword);
            }
            else
            {
                SceneComponent.ShowError(response.Error?.Message ?? "Failed to send reset token.");
            }
        }

        private async UniTaskVoid OnResetPassword(string token, string newPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            {
                SceneComponent.ShowError("Please enter token and new password.");
                return;
            }

            if (newPassword.Length < 8)
            {
                SceneComponent.ShowError("Password must be at least 8 characters.");
                return;
            }

            SceneComponent.SetLoading(true);

            var response = await _authApiService.ResetPasswordAsync(token, newPassword);
            SceneComponent.SetLoading(false);

            if (response.IsSuccess)
            {
                SceneComponent.ShowSuccess("Password reset successfully. Please login.");
                SceneComponent.SetViewState(SurvivorAuthSceneComponent.AuthViewState.EmailLogin);
            }
            else
            {
                SceneComponent.ShowError(response.Error?.Message ?? "Password reset failed.");
            }
        }
    }
}
