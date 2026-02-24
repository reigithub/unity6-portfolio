using Cysharp.Threading.Tasks;
using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorタイトルシーンのルートコンポーネント
    /// UI Toolkit（UXML/USS）使用、UI Builderで編集可能
    /// </summary>
    public class SurvivorTitleSceneComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        [SerializeField] private Animator _animator;

        private readonly Subject<Unit> _onStartGameClicked = new();
        private readonly Subject<Unit> _onSinglePlayerClicked = new();
        private readonly Subject<Unit> _onMultiplayerClicked = new();
        private readonly Subject<Unit> _onPlayModeBackClicked = new();
        private readonly Subject<Unit> _onReturnClicked = new();
        private readonly Subject<Unit> _onQuitClicked = new();
        private readonly Subject<Unit> _onOptionsClicked = new();
        private readonly Subject<Unit> _onDataLinkClicked = new();

        public Observable<Unit> OnStartGameClicked => _onStartGameClicked;
        public Observable<Unit> OnSinglePlayerClicked => _onSinglePlayerClicked;
        public Observable<Unit> OnMultiplayerClicked => _onMultiplayerClicked;
        public Observable<Unit> OnPlayModeBackClicked => _onPlayModeBackClicked;
        public Observable<Unit> OnReturnClicked => _onReturnClicked;
        public Observable<Unit> OnQuitClicked => _onQuitClicked;
        public Observable<Unit> OnOptionsClicked => _onOptionsClicked;
        public Observable<Unit> OnDataLinkClicked => _onDataLinkClicked;

        // UI Element References
        private VisualElement _root;
        private VisualElement _mainMenu;
        private VisualElement _playModeMenu;
        private Button _startButton;
        private Button _singlePlayerButton;
        private Button _multiplayerButton;
        private Button _playModeBackButton;
        private Button _returnButton;
        private Button _quitButton;
        private Button _optionsButton;
        private Button _dataLinkButton;
        private VisualElement _connectionIndicator;
        private Label _connectionStatusLabel;
        private Label _errorLabel;

        protected override void OnDestroy()
        {
            _onStartGameClicked.Dispose();
            _onSinglePlayerClicked.Dispose();
            _onMultiplayerClicked.Dispose();
            _onPlayModeBackClicked.Dispose();
            _onReturnClicked.Dispose();
            _onQuitClicked.Dispose();
            _onOptionsClicked.Dispose();
            _onDataLinkClicked.Dispose();
            base.OnDestroy();
        }

        private void Awake()
        {
            QueryUIElements();
            SetupEventHandlers();
        }

        /// <summary>
        /// UXMLからUI要素を取得
        /// </summary>
        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;

            _mainMenu = _root.Q<VisualElement>("main-menu");
            _playModeMenu = _root.Q<VisualElement>("play-mode-menu");
            _startButton = _root.Q<Button>("start-button");
            _singlePlayerButton = _root.Q<Button>("single-player-button");
            _multiplayerButton = _root.Q<Button>("multiplayer-button");
            _playModeBackButton = _root.Q<Button>("play-mode-back-button");
            _returnButton = _root.Q<Button>("return-button");
            _quitButton = _root.Q<Button>("quit-button");
            _optionsButton = _root.Q<Button>("options-button");
            _dataLinkButton = _root.Q<Button>("data-link-button");
            _connectionIndicator = _root.Q<VisualElement>("connection-indicator");
            _connectionStatusLabel = _root.Q<Label>("connection-status-label");
            _errorLabel = _root.Q<Label>("error-label");
        }

        /// <summary>
        /// イベントハンドラーを設定
        /// </summary>
        private void SetupEventHandlers()
        {
            _startButton?.RegisterCallback<ClickEvent>(_ =>
                _onStartGameClicked.OnNext(Unit.Default));

            _singlePlayerButton?.RegisterCallback<ClickEvent>(_ =>
                _onSinglePlayerClicked.OnNext(Unit.Default));

            _multiplayerButton?.RegisterCallback<ClickEvent>(_ =>
                _onMultiplayerClicked.OnNext(Unit.Default));

            _playModeBackButton?.RegisterCallback<ClickEvent>(_ =>
                _onPlayModeBackClicked.OnNext(Unit.Default));

            _returnButton?.RegisterCallback<ClickEvent>(_ =>
                _onReturnClicked.OnNext(Unit.Default));

            _quitButton?.RegisterCallback<ClickEvent>(_ =>
                _onQuitClicked.OnNext(Unit.Default));

            _optionsButton?.RegisterCallback<ClickEvent>(_ =>
                _onOptionsClicked.OnNext(Unit.Default));

            _dataLinkButton?.RegisterCallback<ClickEvent>(_ =>
                _onDataLinkClicked.OnNext(Unit.Default));
        }

        public void PlayAnimation()
        {
            _animator.Play("Salute");
        }

        /// <summary>
        /// UI操作の有効/無効を設定
        /// </summary>
        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
        }

        /// <summary>
        /// プレイモード選択メニューを表示
        /// </summary>
        public void ShowPlayModeMenu()
        {
            _mainMenu?.AddToClassList("menu--hidden");
            _playModeMenu?.RemoveFromClassList("menu--hidden");
        }

        /// <summary>
        /// メインメニューに戻る
        /// </summary>
        public void ShowMainMenu()
        {
            _playModeMenu?.AddToClassList("menu--hidden");
            _mainMenu?.RemoveFromClassList("menu--hidden");
        }

        /// <summary>
        /// 接続状態インジケーターを設定
        /// </summary>
        /// <param name="isConnected">接続状態</param>
        public void SetConnectionIndicator(bool isConnected)
        {
            if (_connectionIndicator != null)
            {
                _connectionIndicator.RemoveFromClassList("connection-indicator--online");
                _connectionIndicator.RemoveFromClassList("connection-indicator--offline");
                _connectionIndicator.AddToClassList(isConnected ? "connection-indicator--online" : "connection-indicator--offline");
            }

            if (_connectionStatusLabel != null)
            {
                _connectionStatusLabel.text = isConnected ? "オンライン" : "オフライン";
            }

            // オフライン時はエラーメッセージをクリア（接続復帰時）
            if (isConnected)
            {
                ClearError();
            }
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        /// <param name="message">エラーメッセージ</param>
        public void ShowError(string message)
        {
            if (_errorLabel != null)
            {
                _errorLabel.text = message;
                _errorLabel.style.display = DisplayStyle.Flex;
            }
        }

        /// <summary>
        /// エラーメッセージをクリア
        /// </summary>
        public void ClearError()
        {
            if (_errorLabel != null)
            {
                _errorLabel.style.display = DisplayStyle.None;
            }
        }
    }
}
