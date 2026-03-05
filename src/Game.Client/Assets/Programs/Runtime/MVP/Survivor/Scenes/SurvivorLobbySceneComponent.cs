using System;
using Game.Library.Shared.Dto;
using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    public class SurvivorLobbySceneComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<(string lobbyName, int maxPlayers, int stageId)> _onCreateClicked = new();
        private readonly Subject<string> _onJoinClicked = new();
        private readonly Subject<Unit> _onRefreshClicked = new();
        private readonly Subject<(int stageId, int matchSize)> _onQuickMatchClicked = new();
        private readonly Subject<Unit> _onCancelMatchmakingClicked = new();
        private readonly Subject<Unit> _onBackClicked = new();

        public Observable<(string lobbyName, int maxPlayers, int stageId)> OnCreateClicked => _onCreateClicked;
        public Observable<string> OnJoinClicked => _onJoinClicked;
        public Observable<Unit> OnRefreshClicked => _onRefreshClicked;
        public Observable<(int stageId, int matchSize)> OnQuickMatchClicked => _onQuickMatchClicked;
        public Observable<Unit> OnCancelMatchmakingClicked => _onCancelMatchmakingClicked;
        public Observable<Unit> OnBackClicked => _onBackClicked;

        // UI Element References
        private VisualElement _root;
        private Button _backButton;
        private Button _refreshButton;
        private Button _createButton;
        private Button _quickMatchButton;
        private Button _cancelMatchmakingButton;
        private TextField _lobbyNameInput;
        private SliderInt _maxPlayersSlider;
        private SliderInt _stageIdSlider;
        private SliderInt _quickMatchStageSlider;
        private SliderInt _matchSizeSlider;
        private ScrollView _lobbyList;
        private Label _lobbyListEmpty;
        private VisualElement _matchmakingStatus;
        private Label _matchmakingLabel;
        private Label _queueCountLabel;
        private Label _errorLabel;

        protected override void OnDestroy()
        {
            _onCreateClicked.Dispose();
            _onJoinClicked.Dispose();
            _onRefreshClicked.Dispose();
            _onQuickMatchClicked.Dispose();
            _onCancelMatchmakingClicked.Dispose();
            _onBackClicked.Dispose();
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

            _backButton = _root.Q<Button>("back-button");
            _refreshButton = _root.Q<Button>("refresh-button");
            _createButton = _root.Q<Button>("create-button");
            _quickMatchButton = _root.Q<Button>("quick-match-button");
            _cancelMatchmakingButton = _root.Q<Button>("cancel-matchmaking-button");
            _lobbyNameInput = _root.Q<TextField>("lobby-name-input");
            _maxPlayersSlider = _root.Q<SliderInt>("max-players-slider");
            _stageIdSlider = _root.Q<SliderInt>("stage-id-slider");
            _quickMatchStageSlider = _root.Q<SliderInt>("quick-match-stage-slider");
            _matchSizeSlider = _root.Q<SliderInt>("match-size-slider");
            _lobbyList = _root.Q<ScrollView>("lobby-list");
            _lobbyListEmpty = _root.Q<Label>("lobby-list-empty");
            _matchmakingStatus = _root.Q<VisualElement>("matchmaking-status");
            _matchmakingLabel = _root.Q<Label>("matchmaking-label");
            _queueCountLabel = _root.Q<Label>("queue-count-label");
            _errorLabel = _root.Q<Label>("error-label");
        }

        private void SetupEventHandlers()
        {
            _backButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackClicked.OnNext(Unit.Default));

            _refreshButton?.RegisterCallback<ClickEvent>(_ =>
                _onRefreshClicked.OnNext(Unit.Default));

            _createButton?.RegisterCallback<ClickEvent>(_ =>
            {
                var lobbyName = _lobbyNameInput?.value ?? "My Lobby";
                var maxPlayers = _maxPlayersSlider?.value ?? 4;
                var stageId = _stageIdSlider?.value ?? 1;
                _onCreateClicked.OnNext((lobbyName, maxPlayers, stageId));
            });

            _quickMatchButton?.RegisterCallback<ClickEvent>(_ =>
            {
                var stageId = _quickMatchStageSlider?.value ?? 0;
                var matchSize = _matchSizeSlider?.value ?? 2;
                _onQuickMatchClicked.OnNext((stageId, matchSize));
            });

            _cancelMatchmakingButton?.RegisterCallback<ClickEvent>(_ =>
                _onCancelMatchmakingClicked.OnNext(Unit.Default));
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
        }

        public void UpdateLobbyList(LobbyInfo[] lobbies)
        {
            _lobbyList.Clear();

            if (lobbies == null || lobbies.Length == 0)
            {
                if (_lobbyListEmpty != null)
                {
                    _lobbyListEmpty.text = "No lobbies found. Create one or try Quick Match!";
                    _lobbyListEmpty.style.display = DisplayStyle.Flex;
                }
                return;
            }

            if (_lobbyListEmpty != null)
            {
                _lobbyListEmpty.style.display = DisplayStyle.None;
            }

            foreach (var lobby in lobbies)
            {
                var item = CreateLobbyItem(lobby);
                _lobbyList.Add(item);
            }
        }

        private VisualElement CreateLobbyItem(LobbyInfo lobby)
        {
            var container = new VisualElement();
            container.AddToClassList("lobby-item");

            var info = new VisualElement();
            info.AddToClassList("lobby-item__info");

            var nameLabel = new Label(lobby.LobbyName);
            nameLabel.AddToClassList("lobby-item__name");

            var detailsLabel = new Label($"{lobby.GameMode} | {lobby.CurrentPlayers}/{lobby.MaxPlayers} players");
            detailsLabel.AddToClassList("lobby-item__details");

            info.Add(nameLabel);
            info.Add(detailsLabel);

            var joinButton = new Button();
            joinButton.text = "JOIN";
            joinButton.AddToClassList("lobby-item__join-button");
            joinButton.RegisterCallback<ClickEvent>(_ =>
                _onJoinClicked.OnNext(lobby.LobbyId));

            if (lobby.CurrentPlayers >= lobby.MaxPlayers)
            {
                joinButton.SetEnabled(false);
                joinButton.text = "FULL";
            }

            container.Add(info);
            container.Add(joinButton);

            return container;
        }

        public void ShowMatchmaking(bool isSearching)
        {
            if (_matchmakingStatus != null)
            {
                if (isSearching)
                {
                    _matchmakingStatus.RemoveFromClassList("matchmaking-status--hidden");
                }
                else
                {
                    _matchmakingStatus.AddToClassList("matchmaking-status--hidden");
                }
            }

            if (_quickMatchButton != null)
            {
                _quickMatchButton.style.display = isSearching ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public void UpdateQueueCount(int count)
        {
            if (_queueCountLabel != null)
            {
                _queueCountLabel.text = $"Players in queue: {count}";
            }
        }

        public void ShowError(string message)
        {
            if (_errorLabel != null)
            {
                _errorLabel.text = message;
                _errorLabel.style.display = DisplayStyle.Flex;
            }
        }

        public void ClearError()
        {
            if (_errorLabel != null)
            {
                _errorLabel.style.display = DisplayStyle.None;
            }
        }
    }
}
