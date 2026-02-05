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
        private readonly Subject<Unit> _onReturnClicked = new();
        private readonly Subject<Unit> _onQuitClicked = new();
        private readonly Subject<Unit> _onOptionsClicked = new();
        private readonly Subject<Unit> _onDataLinkClicked = new();

        public Observable<Unit> OnStartGameClicked => _onStartGameClicked;
        public Observable<Unit> OnReturnClicked => _onReturnClicked;
        public Observable<Unit> OnQuitClicked => _onQuitClicked;
        public Observable<Unit> OnOptionsClicked => _onOptionsClicked;
        public Observable<Unit> OnDataLinkClicked => _onDataLinkClicked;

        // UI Element References
        private VisualElement _root;
        private Button _startButton;
        private Button _returnButton;
        private Button _quitButton;
        private Button _optionsButton;
        private Button _dataLinkButton;

        protected override void OnDestroy()
        {
            _onStartGameClicked.Dispose();
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

            _startButton = _root.Q<Button>("start-button");
            _returnButton = _root.Q<Button>("return-button");
            _quitButton = _root.Q<Button>("quit-button");
            _optionsButton = _root.Q<Button>("options-button");
            _dataLinkButton = _root.Q<Button>("data-link-button");
        }

        /// <summary>
        /// イベントハンドラーを設定
        /// </summary>
        private void SetupEventHandlers()
        {
            _startButton?.RegisterCallback<ClickEvent>(_ =>
                _onStartGameClicked.OnNext(Unit.Default));

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
    }
}