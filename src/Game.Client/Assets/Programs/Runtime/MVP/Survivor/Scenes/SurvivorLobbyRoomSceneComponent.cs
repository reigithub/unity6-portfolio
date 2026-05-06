using System.Collections.Generic;
using Game.Library.Shared.Dto;
using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    public class SurvivorLobbyRoomSceneComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<Unit> _onReadyClicked = new();
        private readonly Subject<string> _onSendMessageClicked = new();
        private readonly Subject<Unit> _onLeaveClicked = new();

        public Observable<Unit> OnReadyClicked => _onReadyClicked;
        public Observable<string> OnSendMessageClicked => _onSendMessageClicked;
        public Observable<Unit> OnLeaveClicked => _onLeaveClicked;

        // UI Element References
        private VisualElement _root;
        private Button _leaveButton;
        private Button _readyButton;
        private Button _sendButton;
        private Label _lobbyNameLabel;
        private Label _playerCountLabel;
        private ScrollView _playerList;
        private ScrollView _chatMessages;
        private TextField _chatInput;
        private Label _notificationLabel;

        // Player tracking
        private readonly Dictionary<string, VisualElement> _playerElements = new();
        private int _currentPlayerCount;
        private int _maxPlayers;

        protected override void OnDestroy()
        {
            _onReadyClicked.Dispose();
            _onSendMessageClicked.Dispose();
            _onLeaveClicked.Dispose();
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

            _leaveButton = _root.Q<Button>("leave-button");
            _readyButton = _root.Q<Button>("ready-button");
            _sendButton = _root.Q<Button>("send-button");
            _lobbyNameLabel = _root.Q<Label>("lobby-name-label");
            _playerCountLabel = _root.Q<Label>("player-count-label");
            _playerList = _root.Q<ScrollView>("player-list");
            _chatMessages = _root.Q<ScrollView>("chat-messages");
            _chatInput = _root.Q<TextField>("chat-input");
            _notificationLabel = _root.Q<Label>("notification-label");
        }

        private void SetupEventHandlers()
        {
            _leaveButton?.RegisterCallback<ClickEvent>(_ =>
                _onLeaveClicked.OnNext(Unit.Default));

            _readyButton?.RegisterCallback<ClickEvent>(_ =>
                _onReadyClicked.OnNext(Unit.Default));

            _sendButton?.RegisterCallback<ClickEvent>(_ =>
            {
                var message = _chatInput?.value;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    _onSendMessageClicked.OnNext(message);
                    if (_chatInput != null)
                    {
                        _chatInput.value = string.Empty;
                    }
                }
            });

            _chatInput?.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    var message = _chatInput.value;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        _onSendMessageClicked.OnNext(message);
                        _chatInput.value = string.Empty;
                    }
                }
            });
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
        }

        public void SetLobbyInfo(string lobbyName, int maxPlayers)
        {
            if (_lobbyNameLabel != null)
            {
                _lobbyNameLabel.text = lobbyName;
            }
            _maxPlayers = maxPlayers;
            UpdatePlayerCountLabel();
        }

        public void InitializePlayers(LobbyPlayerInfo[] players)
        {
            _playerList?.Clear();
            _playerElements.Clear();
            _currentPlayerCount = 0;

            if (players == null) return;

            foreach (var player in players)
            {
                AddPlayerInternal(player.UserId, player.PlayerName, player.IsReady, player.IsHost);
            }
        }

        public void AddPlayer(string userId, string playerName)
        {
            if (_playerElements.ContainsKey(userId)) return;
            AddPlayerInternal(userId, playerName, false, false);
            AddSystemMessage($"{playerName} joined the lobby.");
        }

        public void RemovePlayer(string userId)
        {
            if (!_playerElements.TryGetValue(userId, out var element)) return;

            var nameLabel = element.Q<Label>(className: "player-item__name");
            var playerName = nameLabel?.text ?? userId;

            _playerList?.Remove(element);
            _playerElements.Remove(userId);
            _currentPlayerCount--;
            UpdatePlayerCountLabel();
            AddSystemMessage($"{playerName} left the lobby.");
        }

        public void UpdatePlayerReady(string userId, bool isReady)
        {
            if (!_playerElements.TryGetValue(userId, out var element)) return;

            var statusLabel = element.Q<Label>(className: "player-item__status");
            if (statusLabel != null)
            {
                statusLabel.text = isReady ? "READY" : "NOT READY";
                statusLabel.RemoveFromClassList("player-item__status--ready");
                statusLabel.RemoveFromClassList("player-item__status--not-ready");
                statusLabel.AddToClassList(isReady ? "player-item__status--ready" : "player-item__status--not-ready");
            }

            if (isReady)
            {
                element.AddToClassList("player-item--ready");
            }
            else
            {
                element.RemoveFromClassList("player-item--ready");
            }
        }

        public void AddChatMessage(string playerName, string message)
        {
            var container = new VisualElement();
            container.AddToClassList("chat-message");

            var sender = new Label(playerName);
            sender.AddToClassList("chat-message__sender");

            var text = new Label(message);
            text.AddToClassList("chat-message__text");

            container.Add(sender);
            container.Add(text);

            _chatMessages?.Add(container);
            ScrollToBottom();
        }

        public void SetReadyButtonState(bool isReady)
        {
            if (_readyButton == null) return;

            if (isReady)
            {
                _readyButton.text = "NOT READY";
                _readyButton.AddToClassList("ready-button--active");
            }
            else
            {
                _readyButton.text = "READY";
                _readyButton.RemoveFromClassList("ready-button--active");
            }
        }

        public void ShowNotification(string message)
        {
            if (_notificationLabel != null)
            {
                _notificationLabel.text = message;
                _notificationLabel.style.display = DisplayStyle.Flex;
            }
        }

        private void AddPlayerInternal(string userId, string playerName, bool isReady, bool isHost)
        {
            var container = new VisualElement();
            container.AddToClassList("player-item");
            if (isReady)
            {
                container.AddToClassList("player-item--ready");
            }

            var nameLabel = new Label(playerName);
            nameLabel.AddToClassList("player-item__name");
            container.Add(nameLabel);

            if (isHost)
            {
                var hostBadge = new Label("HOST");
                hostBadge.AddToClassList("player-item__host-badge");
                container.Add(hostBadge);
            }

            var statusLabel = new Label(isReady ? "READY" : "NOT READY");
            statusLabel.AddToClassList("player-item__status");
            statusLabel.AddToClassList(isReady ? "player-item__status--ready" : "player-item__status--not-ready");
            container.Add(statusLabel);

            _playerList?.Add(container);
            _playerElements[userId] = container;
            _currentPlayerCount++;
            UpdatePlayerCountLabel();
        }

        private void AddSystemMessage(string message)
        {
            var container = new VisualElement();
            container.AddToClassList("chat-message");
            container.AddToClassList("chat-message--system");

            var text = new Label(message);
            text.AddToClassList("chat-message__text");

            container.Add(text);
            _chatMessages?.Add(container);
            ScrollToBottom();
        }

        private void UpdatePlayerCountLabel()
        {
            if (_playerCountLabel != null)
            {
                _playerCountLabel.text = $"{_currentPlayerCount}/{_maxPlayers}";
            }
        }

        private void ScrollToBottom()
        {
            // Schedule scroll to bottom after layout update
            _chatMessages?.schedule.Execute(() =>
            {
                _chatMessages.scrollOffset = new Vector2(0, float.MaxValue);
            });
        }
    }
}
