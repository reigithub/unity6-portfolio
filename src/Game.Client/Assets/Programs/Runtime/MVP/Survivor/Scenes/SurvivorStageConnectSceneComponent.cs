using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// SurvivorStageConnectScene の View コンポーネント。
    /// ステータス表示 + エラー表示 + Retry/Cancel ボタン。
    /// </summary>
    public class SurvivorStageConnectSceneComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<Unit> _onRetryClicked = new();
        private readonly Subject<Unit> _onCancelClicked = new();

        public Observable<Unit> OnRetryClicked => _onRetryClicked;
        public Observable<Unit> OnCancelClicked => _onCancelClicked;

        private VisualElement _root;
        private Label _statusLabel;
        private VisualElement _errorContainer;
        private Label _errorLabel;
        private Button _retryButton;
        private Button _cancelButton;

        private void Awake()
        {
            QueryUIElements();
            SetupEventHandlers();
        }

        protected override void OnDestroy()
        {
            _onRetryClicked.Dispose();
            _onCancelClicked.Dispose();
            base.OnDestroy();
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;

            _statusLabel = _root.Q<Label>("status-label");
            _errorContainer = _root.Q<VisualElement>("error-container");
            _errorLabel = _root.Q<Label>("error-label");
            _retryButton = _root.Q<Button>("retry-button");
            _cancelButton = _root.Q<Button>("cancel-button");
        }

        private void SetupEventHandlers()
        {
            _retryButton?.RegisterCallback<ClickEvent>(_ =>
                _onRetryClicked.OnNext(Unit.Default));

            _cancelButton?.RegisterCallback<ClickEvent>(_ =>
                _onCancelClicked.OnNext(Unit.Default));
        }

        public void SetStatus(string message)
        {
            if (_statusLabel != null)
                _statusLabel.text = message;

            if (_errorContainer != null)
                _errorContainer.style.display = DisplayStyle.None;
        }

        public void ShowError(string message)
        {
            if (_statusLabel != null)
                _statusLabel.style.display = DisplayStyle.None;

            if (_errorContainer != null)
                _errorContainer.style.display = DisplayStyle.Flex;

            if (_errorLabel != null)
                _errorLabel.text = message;
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
        }
    }
}
