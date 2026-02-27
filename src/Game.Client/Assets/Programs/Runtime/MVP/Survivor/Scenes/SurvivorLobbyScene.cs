using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.MVP.Core.Scenes;
using Game.Shared.Netcode.Client;
using Game.Shared.Realtime.Client;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    public class SurvivorLobbyScene : GamePrefabScene<SurvivorLobbyScene, SurvivorLobbySceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ILobbyClient _lobbyClient;
        [Inject] private readonly IMatchmakingClient _matchmakingClient;
        [Inject] private readonly IAuthSessionService _authSessionService;

        protected override string AssetPathOrAddress => "SurvivorLobbyScene";

        private IDisposable _matchFoundSubscription;
        private IDisposable _queueStatusSubscription;
        private IDisposable _matchmakingCancelledSubscription;

        public override async UniTask Startup()
        {
            await base.Startup();

            // View イベント購読
            SceneComponent.OnCreateClicked
                .Subscribe(args => OnCreate(args.lobbyName, args.maxPlayers).Forget())
                .AddTo(Disposables);

            SceneComponent.OnJoinClicked
                .Subscribe(lobbyId => OnJoin(lobbyId).Forget())
                .AddTo(Disposables);

            SceneComponent.OnRefreshClicked
                .Subscribe(_ => RefreshLobbiesAsync().Forget())
                .AddTo(Disposables);

            SceneComponent.OnQuickMatchClicked
                .Subscribe(_ => OnQuickMatch().Forget())
                .AddTo(Disposables);

            SceneComponent.OnCancelMatchmakingClicked
                .Subscribe(_ => OnCancelMatchmaking().Forget())
                .AddTo(Disposables);

            SceneComponent.OnBackClicked
                .Subscribe(_ => OnBack().Forget())
                .AddTo(Disposables);

            // MatchmakingClient イベント購読
            SubscribeMatchmakingEvents();

            // 初回ロビーリスト取得
            await RefreshLobbiesAsync();
        }

        public override async UniTask Terminate()
        {
            UnsubscribeMatchmakingEvents();
            await base.Terminate();
        }

        private void SubscribeMatchmakingEvents()
        {
            _matchmakingClient.OnMatchFound += HandleMatchFound;
            _matchmakingClient.OnQueueStatusUpdated += HandleQueueStatusUpdated;
            _matchmakingClient.OnMatchmakingCancelled += HandleMatchmakingCancelled;
        }

        private void UnsubscribeMatchmakingEvents()
        {
            _matchmakingClient.OnMatchFound -= HandleMatchFound;
            _matchmakingClient.OnQueueStatusUpdated -= HandleQueueStatusUpdated;
            _matchmakingClient.OnMatchmakingCancelled -= HandleMatchmakingCancelled;
        }

        private void HandleMatchFound(MatchResult result)
        {
            OnMatchFound(result).Forget();
        }

        private void HandleQueueStatusUpdated(int count)
        {
            SceneComponent.UpdateQueueCount(count);
        }

        private void HandleMatchmakingCancelled(string reason)
        {
            Debug.Log($"[SurvivorLobbyScene] Matchmaking cancelled: {reason}");
            SceneComponent.ShowMatchmaking(false);
            SceneComponent.SetInteractables(true);
        }

        private async UniTask RefreshLobbiesAsync()
        {
            try
            {
                SceneComponent.ClearError();
                var lobbies = await _lobbyClient.SearchLobbiesAsync("survival", 20);
                SceneComponent.UpdateLobbyList(lobbies);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyScene] Failed to refresh lobbies: {ex.Message}");
                SceneComponent.ShowError("Failed to load lobby list.");
                SceneComponent.UpdateLobbyList(null);
            }
        }

        private async UniTaskVoid OnCreate(string lobbyName, int maxPlayers)
        {
            SceneComponent.SetInteractables(false);
            SceneComponent.ClearError();

            try
            {
                var playerName = _authSessionService.UserName ?? "Player";
                var request = new CreateLobbyRequest
                {
                    LobbyName = lobbyName,
                    GameMode = "survival",
                    MaxPlayers = maxPlayers,
                    IsPublic = true,
                    PlayerName = playerName
                };

                var response = await _lobbyClient.CreateLobbyAsync(request);
                if (!response.Success)
                {
                    SceneComponent.ShowError(response.ErrorMessage);
                    SceneComponent.SetInteractables(true);
                    return;
                }

                // Hub 接続してロビールームへ遷移
                await _lobbyClient.ConnectToLobbyAsync(response.LobbyId, playerName);
                await _sceneService.TransitionAsync<SurvivorLobbyRoomScene>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyScene] Failed to create lobby: {ex.Message}");
                SceneComponent.ShowError("Failed to create lobby.");
                SceneComponent.SetInteractables(true);
            }
        }

        private async UniTaskVoid OnJoin(string lobbyId)
        {
            SceneComponent.SetInteractables(false);
            SceneComponent.ClearError();

            try
            {
                var playerName = _authSessionService.UserName ?? "Player";

                // Unary で参加
                await _lobbyClient.JoinLobbyAsync(lobbyId, playerName);

                // Hub 接続してロビールームへ遷移
                await _lobbyClient.ConnectToLobbyAsync(lobbyId, playerName);
                await _sceneService.TransitionAsync<SurvivorLobbyRoomScene>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyScene] Failed to join lobby: {ex.Message}");
                SceneComponent.ShowError("Failed to join lobby.");
                SceneComponent.SetInteractables(true);
            }
        }

        private async UniTaskVoid OnQuickMatch()
        {
            SceneComponent.SetInteractables(false);
            SceneComponent.ClearError();

            try
            {
                SceneComponent.ShowMatchmaking(true);
                SceneComponent.SetInteractables(true);

                var response = await _matchmakingClient.StartMatchmakingAsync("survival");
                if (!response.Success)
                {
                    SceneComponent.ShowMatchmaking(false);
                    SceneComponent.ShowError(response.ErrorMessage);
                    return;
                }

                SceneComponent.UpdateQueueCount(response.PlayersInQueue);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyScene] Failed to start matchmaking: {ex.Message}");
                SceneComponent.ShowMatchmaking(false);
                SceneComponent.ShowError("Failed to start matchmaking.");
            }
        }

        private async UniTaskVoid OnMatchFound(MatchResult result)
        {
            Debug.Log($"[SurvivorLobbyScene] Match found: {result.MatchId}");
            NetworkSurvivorMatchConnector.StoreMatchResult(result);
            SceneComponent.SetInteractables(false);

            try
            {
                await _matchmakingClient.DisconnectAsync();
                await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyScene] Failed to transition after match: {ex.Message}");
                SceneComponent.ShowError("Failed to join match.");
                SceneComponent.SetInteractables(true);
            }
        }

        private async UniTaskVoid OnCancelMatchmaking()
        {
            try
            {
                await _matchmakingClient.CancelMatchmakingAsync();
                SceneComponent.ShowMatchmaking(false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyScene] Failed to cancel matchmaking: {ex.Message}");
            }
        }

        private async UniTaskVoid OnBack()
        {
            SceneComponent.SetInteractables(false);

            try
            {
                if (_matchmakingClient.IsSearching)
                {
                    await _matchmakingClient.CancelMatchmakingAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyScene] Failed to cancel matchmaking on back: {ex.Message}");
            }

            await _sceneService.TransitionAsync<SurvivorTitleScene>();
        }
    }
}
