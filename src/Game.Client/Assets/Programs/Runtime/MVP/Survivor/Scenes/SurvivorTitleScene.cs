using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Enums;
using Game.MVP.Core.Scenes;
using Game.Shared.Bootstrap;
using Game.Shared.Realtime.Client;
using Game.Shared.Services;
using Game.Shared.Network.Survivor;
using Game.Shared.Services.Network;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorタイトルシーン（Presenter）
    /// MVPパターンでViewとの仲介を行う
    /// </summary>
    public class SurvivorTitleScene : GamePrefabScene<SurvivorTitleScene, SurvivorTitleSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly IAudioService _audioService;
        [Inject] private readonly IAuthSessionService _authSessionService;
        [Inject] private readonly IAuthApiService _authApiService;
        [Inject] private readonly INetworkService _networkService;
        [Inject] private readonly ILobbyClient _lobbyClient;
        [Inject] private readonly IUnityServerSessionConfig _sessionConfig;

        protected override string AssetPathOrAddress => "SurvivorTitleScene";

        public override async UniTask Startup()
        {
            await base.Startup();

            // Viewのイベントを購読
            SceneComponent.OnStartGameClicked
                .Subscribe(_ => OnStartGame())
                .AddTo(Disposables);

            SceneComponent.OnSinglePlayerClicked
                .Subscribe(_ => OnSinglePlayer().Forget())
                .AddTo(Disposables);

            SceneComponent.OnMultiplayerClicked
                .Subscribe(_ => OnMultiplayer().Forget())
                .AddTo(Disposables);

            SceneComponent.OnPlayModeBackClicked
                .Subscribe(_ => OnPlayModeBack())
                .AddTo(Disposables);

            SceneComponent.OnReturnClicked
                .Subscribe(_ => OnReturn().Forget())
                .AddTo(Disposables);

            SceneComponent.OnQuitClicked
                .Subscribe(_ => OnQuit().Forget())
                .AddTo(Disposables);

            SceneComponent.OnOptionsClicked
                .Subscribe(_ => OnOptions().Forget())
                .AddTo(Disposables);

            SceneComponent.OnDataLinkClicked
                .Subscribe(_ => OnDataLink().Forget())
                .AddTo(Disposables);

            // 接続状態監視を設定
            _networkService.OnConnectivityChanged
                .Subscribe(connected => SceneComponent.SetConnectionIndicator(connected))
                .AddTo(Disposables);

            // 初期接続状態を設定
            SceneComponent.SetConnectionIndicator(_networkService.IsConnected);
        }

        public override async UniTask Ready()
        {
            SceneComponent.PlayAnimation();
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameReady);
        }

        private void OnStartGame()
        {
            SceneComponent.ShowPlayModeMenu();
        }

        private async UniTaskVoid OnSinglePlayer()
        {
            SceneComponent.SetInteractables(false);
            SceneComponent.ClearError();

            // セッション有効性を確認し、必要に応じて再認証
            if (!await EnsureValidSessionAsync())
            {
                SceneComponent.SetInteractables(true);
                return;
            }

            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart);
            _sessionConfig.UpdateConfigure(playerCount: 1);
            await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
        }

        private async UniTaskVoid OnMultiplayer()
        {
            SceneComponent.SetInteractables(false);
            SceneComponent.ClearError();

            // セッション有効性を確認し、必要に応じて再認証
            if (!await EnsureValidSessionAsync())
            {
                SceneComponent.SetInteractables(true);
                return;
            }

            // 参加済みロビーがあれば直接ロビールームへ
            if (await TryAutoRejoinAsync())
            {
                return;
            }

            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameStart);
            await _sceneService.TransitionAsync<SurvivorLobbyScene>();
        }

        /// <summary>
        /// 参加済みロビーがあれば Hub 再接続してロビールームへ直接遷移する
        /// </summary>
        private async UniTask<bool> TryAutoRejoinAsync()
        {
            try
            {
                var lobby = await _lobbyClient.GetMyLobbyAsync();
                if (lobby == null) return false;

                Debug.Log($"[SurvivorTitleScene] Rejoining lobby: {lobby.LobbyId}");
                var playerName = _authSessionService.UserName ?? "Player";
                await _lobbyClient.ConnectToLobbyAsync(lobby.LobbyId, playerName);
                await _sceneService.TransitionAsync<SurvivorLobbyRoomScene>();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorTitleScene] Auto-rejoin failed: {ex.Message}");
                return false;
            }
        }

        private void OnPlayModeBack()
        {
            SceneComponent.ClearError();
            SceneComponent.ShowMainMenu();
        }

        /// <summary>
        /// セッションの有効性を確認し、必要に応じてトークンリフレッシュまたは再ログインを行う
        /// </summary>
        /// <returns>有効なセッションが確立できた場合はtrue</returns>
        private async UniTask<bool> EnsureValidSessionAsync()
        {
            // オフライン時はローカルセッションで続行可能
            if (!_networkService.IsConnected)
            {
                UnityEngine.Debug.Log("[SurvivorTitleScene] Offline mode - using local session");
                return true;
            }

            // 未認証の場合はゲストログイン
            if (!_authSessionService.IsAuthenticated)
            {
                var loginResult = await _authApiService.GuestLoginAsync();
                if (!loginResult.IsSuccess)
                {
                    SceneComponent.ShowError(loginResult.Error?.Message ?? NetworkErrorLocalizer.GetOfflineMessage());
                    return false;
                }
                return true;
            }

            // 認証済みの場合はトークンリフレッシュを試行
            var refreshResult = await _authApiService.RefreshTokenAsync();
            if (refreshResult.IsSuccess)
            {
                return true;
            }

            // リフレッシュ失敗（トークン期限切れ等）の場合、ゲストログインで再認証
            // サーバーはデバイスフィンガープリントで既存ユーザーを識別して返す
            UnityEngine.Debug.Log("[SurvivorTitleScene] Token refresh failed, attempting guest login for session recovery");
            var recoveryResult = await _authApiService.GuestLoginAsync();
            if (!recoveryResult.IsSuccess)
            {
                SceneComponent.ShowError(recoveryResult.Error?.Message ?? NetworkErrorLocalizer.GetOfflineMessage());
                return false;
            }
            return true;
        }

        private async UniTaskVoid OnReturn()
        {
            SceneComponent.SetInteractables(false);
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameQuit);
            await ApplicationEvents.RequestReturnToTitleAsync();
        }

        private async UniTaskVoid OnQuit()
        {
            SceneComponent.SetInteractables(false);
            await _audioService.PlayRandomOneAsync(AudioPlayTag.GameQuit);
            ApplicationEvents.RequestShutdown();
        }

        private async UniTaskVoid OnOptions()
        {
            SceneComponent.SetInteractables(false);
            await SurvivorOptionsDialog.RunAsync(_sceneService);
            SceneComponent.SetInteractables(true);
        }

        private async UniTaskVoid OnDataLink()
        {
            SceneComponent.SetInteractables(false);
            SceneComponent.ClearError();

            // セッション有効性を確認し、必要に応じて再認証
            if (!await EnsureValidSessionAsync())
            {
                SceneComponent.SetInteractables(true);
                return;
            }

            await SurvivorAccountLinkDialog.RunAsync(_sceneService);
            SceneComponent.SetInteractables(true);
        }
    }
}
