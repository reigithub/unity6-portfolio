using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorポーズダイアログのルートコンポーネント
    /// UI Toolkit（UXML/USS）使用、UI Builderで編集可能
    /// </summary>
    public class SurvivorPauseDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<SurvivorPauseResult> _onResultSelected = new();
        public Observable<SurvivorPauseResult> OnResultSelected => _onResultSelected;

        // UI Element References
        private VisualElement _root;
        private Button _resumeButton;
        private Button _retryButton;
        private Button _quitButton;

        protected override void OnDestroy()
        {
            _onResultSelected.Dispose();
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

            _resumeButton = _root.Q<Button>("resume-button");
            _retryButton = _root.Q<Button>("retry-button");
            _quitButton = _root.Q<Button>("quit-button");
        }

        private void SetupEventHandlers()
        {
            _resumeButton?.RegisterCallback<ClickEvent>(_ =>
                _onResultSelected.OnNext(SurvivorPauseResult.Resume));

            _retryButton?.RegisterCallback<ClickEvent>(_ =>
                _onResultSelected.OnNext(SurvivorPauseResult.Retry));

            _quitButton?.RegisterCallback<ClickEvent>(_ =>
                _onResultSelected.OnNext(SurvivorPauseResult.Quit));
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}
