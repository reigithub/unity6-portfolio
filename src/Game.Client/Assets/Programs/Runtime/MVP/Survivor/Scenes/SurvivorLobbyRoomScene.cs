using System;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.Dto;
using Game.Library.Shared.Realtime.Hubs;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.Shared.Network.Survivor;
using Game.Shared.Realtime.Client;
using Game.Shared.Services;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    public class SurvivorLobbyRoomScene : GamePrefabScene<SurvivorLobbyRoomScene, SurvivorLobbyRoomSceneComponent>
    {
        [Inject] private readonly IGameSceneService _sceneService;
        [Inject] private readonly ILobbyClient _lobbyClient;
        [Inject] private readonly ISurvivorSaveService _saveService;
        [Inject] private readonly IAuthSessionService _authSessionService;
        [Inject] private readonly IUnityServerSessionConfig _sessionConfig;

        protected override string AssetPathOrAddress => "SurvivorLobbyRoomScene";

        private bool _isReady;
        private string _currentLobbyId;
        private int _maxPlayers = 4;
        private int _stageId = 1;

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
                _maxPlayers = lobbyInfo.MaxPlayers;
                _stageId = lobbyInfo.StageId;
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

        private void HandleGameStarting(MatchStartInfo info)
        {
            OnGameStarting(info).Forget();
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
            // 楽観的 UI 更新: 先にローカル UI を切替えて RPC 往復遅延（最後の Ready は
            // 全員 Ready 判定 + マッチメイキング処理を含むため数秒かかる）でボタンが
            // NOT READY のまま見える問題を回避する。
            bool nextReady = !_isReady;
            _isReady = nextReady;
            SceneComponent.SetReadyButtonState(_isReady);

            try
            {
                await _lobbyClient.SetReadyAsync(nextReady);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SurvivorLobbyRoomScene] Failed to set ready: {ex.Message}");
                // エラー時はロールバック (UI も元に戻す)
                _isReady = !nextReady;
                SceneComponent.SetReadyButtonState(_isReady);
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

        private async UniTaskVoid OnGameStarting(MatchStartInfo info)
        {
            Debug.Log($"[SurvivorLobbyRoomScene] Game starting! Topology: {info.Topology}, Session: {info.SessionName}");
            SceneComponent.SetInteractables(false);
            SceneComponent.ShowNotification($"Game starting ({info.Topology})...");

            // Topology に応じて ConnectionSource を決定
            ConnectionSource source;
            if (info.Topology == NetworkTopology.PeerToPeer)
            {
                var myUserId = _authSessionService.UserId;
                var isHost = myUserId == info.HostUserId;
                source = isHost ? ConnectionSource.P2PHost : ConnectionSource.P2PClient;
            }
            else
            {
                source = ConnectionSource.Matchmaking;
            }

            // 統一 Configure overload で MatchStartInfo を設定
            _sessionConfig.Configure(source, info, _maxPlayers);

            // セッション開始（stageId はロビー情報から取得）
            var playerId = _saveService.Data.SelectedPlayerId;
            _saveService.StartSession(_stageId, playerId);
            await _saveService.SaveIfDirtyAsync();

            Debug.Log($"[SurvivorLobbyRoomScene] Transitioning to StageConnectScene (topology={info.Topology}, source={source}, session={info.SessionName}, region={info.PhotonRegion})");
            await _sceneService.TransitionAsync<SurvivorStageConnectScene>();
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
