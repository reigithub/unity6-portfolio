using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.MVP.Core.Scenes;
using Game.Shared.Network.Survivor;
using Game.Shared.Realtime.Client;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    public class SurvivorLobbyRoomScene : GamePrefabScene<SurvivorLobbyRoomScene, SurvivorLobbyRoomSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ILobbyClient _lobbyClient;

        protected override string AssetPathOrAddress => "SurvivorLobbyRoomScene";

        private bool _isReady;
        private string _currentLobbyId;

        public override async UniTask Startup()
        {
            await base.Startup();

            // View イベント購読
            SceneComponent.OnReadyClicked
                .Subscribe(_ => OnReady().Forget())
                .AddTo(Disposables);

            SceneComponent.OnSendMessageClicked
                .Subscribe(message => OnSendMessage(message).Forget())
                .AddTo(Disposables);

            SceneComponent.OnLeaveClicked
                .Subscribe(_ => OnLeave().Forget())
                .AddTo(Disposables);

            // LobbyClient イベント購読
            SubscribeLobbyEvents();

            // ロビー情報を取得して表示
            await InitializeLobbyAsync();
        }

        public override async UniTask Terminate()
        {
            UnsubscribeLobbyEvents();
            await base.Terminate();
        }

        private void SubscribeLobbyEvents()
        {
            _lobbyClient.OnPlayerJoined += HandlePlayerJoined;
            _lobbyClient.OnPlayerLeft += HandlePlayerLeft;
            _lobbyClient.OnMessageReceived += HandleMessageReceived;
            _lobbyClient.OnPlayerReadyChanged += HandlePlayerReadyChanged;
            _lobbyClient.OnGameStarting += HandleGameStarting;
            _lobbyClient.OnLobbyClosed += HandleLobbyClosed;
            _lobbyClient.OnDisconnected += HandleDisconnected;
        }

        private void UnsubscribeLobbyEvents()
        {
            _lobbyClient.OnPlayerJoined -= HandlePlayerJoined;
            _lobbyClient.OnPlayerLeft -= HandlePlayerLeft;
            _lobbyClient.OnMessageReceived -= HandleMessageReceived;
            _lobbyClient.OnPlayerReadyChanged -= HandlePlayerReadyChanged;
            _lobbyClient.OnGameStarting -= HandleGameStarting;
            _lobbyClient.OnLobbyClosed -= HandleLobbyClosed;
            _lobbyClient.OnDisconnected -= HandleDisconnected;
        }

        private async UniTask InitializeLobbyAsync()
        {
            try
            {
                _currentLobbyId = _lobbyClient.CurrentLobbyId;
                if (string.IsNullOrEmpty(_currentLobbyId))
                {
                    Debug.LogWarning("[SurvivorLobbyRoomScene] No current lobby ID");
                    SceneComponent.SetLobbyInfo("LOBBY ROOM", 4);
                    return;
                }

                var lobbyInfo = await _lobbyClient.GetLobbyInfoAsync(_currentLobbyId);
                SceneComponent.SetLobbyInfo(lobbyInfo.LobbyName, lobbyInfo.MaxPlayers);

                var playerList = await _lobbyClient.GetLobbyPlayersAsync(_currentLobbyId);
                SceneComponent.InitializePlayers(playerList);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyRoomScene] Failed to initialize lobby: {ex.Message}");
                SceneComponent.SetLobbyInfo("LOBBY ROOM", 4);
            }
        }

        private void HandlePlayerJoined(string userId, string playerName)
        {
            SceneComponent.AddPlayer(userId, playerName);
        }

        private void HandlePlayerLeft(string userId, string playerName)
        {
            SceneComponent.RemovePlayer(userId);
        }

        private void HandleMessageReceived(string userId, string playerName, string message)
        {
            SceneComponent.AddChatMessage(playerName, message);
        }

        private void HandlePlayerReadyChanged(string userId, bool isReady)
        {
            SceneComponent.UpdatePlayerReady(userId, isReady);
        }

        private void HandleGameStarting(string matchId, string serverAddress, int port, string sessionToken)
        {
            OnGameStarting(matchId, serverAddress, port, sessionToken).Forget();
        }

        private void HandleLobbyClosed(string reason)
        {
            OnLobbyClosed(reason).Forget();
        }

        private void HandleDisconnected(string reason)
        {
            OnDisconnectedFromLobby(reason).Forget();
        }

        private async UniTaskVoid OnReady()
        {
            try
            {
                _isReady = !_isReady;
                await _lobbyClient.SetReadyAsync(_isReady);
                SceneComponent.SetReadyButtonState(_isReady);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyRoomScene] Failed to set ready: {ex.Message}");
                _isReady = !_isReady; // revert
            }
        }

        private async UniTaskVoid OnSendMessage(string message)
        {
            try
            {
                await _lobbyClient.SendMessageAsync(message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyRoomScene] Failed to send message: {ex.Message}");
            }
        }

        private async UniTaskVoid OnLeave()
        {
            SceneComponent.SetInteractables(false);

            try
            {
                await _lobbyClient.LeaveLobbyAsync();
                await _lobbyClient.DisconnectAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyRoomScene] Failed to leave lobby: {ex.Message}");
            }

            await _sceneService.TransitionAsync<SurvivorLobbyScene>();
        }

        private async UniTaskVoid OnGameStarting(string matchId, string serverAddress, int port, string sessionToken)
        {
            Debug.Log($"[SurvivorLobbyRoomScene] Game starting! MatchId: {matchId}, Server: {serverAddress}:{port}");
            SceneComponent.SetInteractables(false);
            SceneComponent.ShowNotification("Game starting...");

            // MatchResult にトークンを含めて保存
            SurvivorNetworkMatchConnector.StoreMatchResult(new MatchResult
            {
                MatchId = matchId,
                PlayerIds = System.Array.Empty<string>(),
                ServerAddress = serverAddress,
                ServerPort = port,
                SessionToken = sessionToken,
            });

            await _sceneService.TransitionAsync<SurvivorStageSelectScene>();
        }

        private async UniTaskVoid OnLobbyClosed(string reason)
        {
            Debug.Log($"[SurvivorLobbyRoomScene] Lobby closed: {reason}");
            SceneComponent.ShowNotification($"Lobby closed: {reason}");
            SceneComponent.SetInteractables(false);

            // 少し待ってからロビーリストに戻る
            await UniTask.Delay(TimeSpan.FromSeconds(2));
            await _sceneService.TransitionAsync<SurvivorLobbyScene>();
        }

        private async UniTaskVoid OnDisconnectedFromLobby(string reason)
        {
            Debug.LogWarning($"[SurvivorLobbyRoomScene] Disconnected: {reason}");
            SceneComponent.ShowNotification($"Disconnected: {reason}");
            SceneComponent.SetInteractables(false);

            await UniTask.Delay(TimeSpan.FromSeconds(2));
            await _sceneService.TransitionAsync<SurvivorLobbyScene>();
        }
    }
}
