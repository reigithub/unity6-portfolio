using System.Collections.Generic;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorステージ選択シーンのルートコンポーネント
    /// UI Toolkit（UXML/USS）使用、UI Builderで編集可能
    /// </summary>
    public class SurvivorStageSelectSceneComponent : GameSceneComponent
    {
        [Header("UI Documents")]
        [SerializeField] private UIDocument _uiDocument;

        [SerializeField] private VisualTreeAsset _stageItemTemplate;

        private readonly Subject<int> _onStageSelected = new();
        private readonly Subject<Unit> _onBackClicked = new();
        private readonly Subject<Unit> _onOptionsClicked = new();

        public Observable<int> OnStageSelected => _onStageSelected;
        public Observable<Unit> OnBackClicked => _onBackClicked;
        public Observable<Unit> OnOptionsClicked => _onOptionsClicked;

        // UI Element References
        private VisualElement _root;
        private VisualElement _stageListContainer;
        private VisualElement _detailPanel;
        private VisualElement _resumePanel;
        private Label _detailTitle;
        private Label _detailDescription;
        private Label _detailHighScore;
        private Label _detailBestTime;
        private VisualElement _starsContainer;
        private Button _playButton;
        private Button _backButton;
        private Button _optionsButton;
        private Button _resumeButton;
        private Button _newGameButton;
        private Label _resumeInfo;

        private int _selectedStageId;
        private SurvivorStageSession _resumeSession;
        private readonly List<VisualElement> _stageItems = new();

        protected override void OnDestroy()
        {
            _onStageSelected.Dispose();
            _onBackClicked.Dispose();
            _onOptionsClicked.Dispose();
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

            // Main UI
            _stageListContainer = _root.Q<VisualElement>("stage-list-container");
            _detailPanel = _root.Q<VisualElement>("detail-panel");
            _backButton = _root.Q<Button>("back-button");
            _optionsButton = _root.Q<Button>("options-button");
            _playButton = _root.Q<Button>("play-button");

            // Detail Panel
            _detailTitle = _root.Q<Label>("detail-title");
            _detailDescription = _root.Q<Label>("detail-description");
            _detailHighScore = _root.Q<Label>("detail-high-score");
            _detailBestTime = _root.Q<Label>("detail-best-time");
            _starsContainer = _root.Q<VisualElement>("stars-container");

            // Resume Panel
            _resumePanel = _root.Q<VisualElement>("resume-panel");
            _resumeInfo = _root.Q<Label>("resume-info");
            _resumeButton = _root.Q<Button>("resume-button");
            _newGameButton = _root.Q<Button>("new-game-button");
        }

        /// <summary>
        /// イベントハンドラーを設定
        /// </summary>
        private void SetupEventHandlers()
        {
            _backButton?.RegisterCallback<ClickEvent>(_ =>
                _onBackClicked.OnNext(Unit.Default));

            _optionsButton?.RegisterCallback<ClickEvent>(_ =>
                _onOptionsClicked.OnNext(Unit.Default));

            _playButton?.RegisterCallback<ClickEvent>(_ =>
            {
                if (_selectedStageId > 0)
                {
                    _onStageSelected.OnNext(_selectedStageId);
                }
            });

            _resumeButton?.RegisterCallback<ClickEvent>(_ =>
            {
                if (_resumeSession != null)
                {
                    _onStageSelected.OnNext(_resumeSession.StageId);
                }
            });

            _newGameButton?.RegisterCallback<ClickEvent>(_ =>
                HideResumePanel());
        }

        #region Public Methods

        /// <summary>
        /// ステージ一覧を初期化
        /// </summary>
        public void Initialize(List<StageSelectItemData> stages)
        {
            // 既存アイテムをクリア
            _stageListContainer?.Clear();
            _stageItems.Clear();

            if (_stageItemTemplate == null)
            {
                Debug.LogWarning("[StageSelectScene] Stage item template is not set");
                return;
            }

            // ステージアイテムを生成
            foreach (var stageData in stages)
            {
                var itemElement = CreateStageItem(stageData);
                _stageListContainer?.Add(itemElement);
                _stageItems.Add(itemElement);
            }
        }

        /// <summary>
        /// 再開オプションを表示
        /// </summary>
        public void ShowResumeOption(SurvivorStageSession session)
        {
            _resumeSession = session;

            if (session != null && _resumePanel != null)
            {
                if (_resumeInfo != null)
                {
                    var minutes = (int)(session.ElapsedTime / 60);
                    var seconds = (int)(session.ElapsedTime % 60);
                    _resumeInfo.text = $"Wave: {session.CurrentWave}\n" +
                                       $"Time: {minutes:D2}:{seconds:D2}\n" +
                                       $"Score: {session.Score}";
                }

                _resumePanel.RemoveFromClassList("overlay--hidden");
            }
        }

        /// <summary>
        /// UI操作の有効/無効を設定
        /// </summary>
        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);

            base.SetInteractables(interactable);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// ステージアイテムを生成
        /// </summary>
        private VisualElement CreateStageItem(StageSelectItemData data)
        {
            var itemElement = _stageItemTemplate.Instantiate();
            var item = itemElement.Q<VisualElement>("stage-item");

            // ステージ番号
            var numberLabel = item.Q<Label>("stage-number");
            if (numberLabel != null)
            {
                numberLabel.text = $"{data.StageId:D2}";
                if (!data.IsUnlocked)
                {
                    numberLabel.AddToClassList("stage-item__number--locked");
                }
            }

            // ステージ名
            var nameLabel = item.Q<Label>("stage-name");
            if (nameLabel != null)
            {
                nameLabel.text = data.StageName;
            }

            // 難易度
            var difficultyLabel = item.Q<Label>("stage-difficulty");
            if (difficultyLabel != null)
            {
                difficultyLabel.text = $"Difficulty: {new string('★', data.Difficulty)}";
                difficultyLabel.AddToClassList(GetDifficultyClass(data.Difficulty));
            }

            // ステータスバッジ / ロックアイコン
            var statusBadge = item.Q<Label>("status-badge");
            var lockIcon = item.Q<Label>("lock-icon");

            if (data.IsUnlocked)
            {
                lockIcon?.AddToClassList("hidden");

                if (data.IsCleared)
                {
                    statusBadge?.RemoveFromClassList("hidden");
                }
                else
                {
                    statusBadge?.AddToClassList("hidden");
                }

                // クリックイベント
                item.RegisterCallback<ClickEvent>(_ => OnStageItemClicked(data));
            }
            else
            {
                item.AddToClassList("stage-item--locked");
                statusBadge?.AddToClassList("hidden");
                lockIcon?.RemoveFromClassList("hidden");
            }

            return itemElement;
        }

        /// <summary>
        /// 難易度に応じたCSSクラス名を取得
        /// </summary>
        private string GetDifficultyClass(int difficulty)
        {
            return difficulty switch
            {
                1 => "stage-item__difficulty--easy",
                2 => "stage-item__difficulty--normal",
                3 => "stage-item__difficulty--hard",
                4 => "stage-item__difficulty--very-hard",
                _ => "stage-item__difficulty--extreme"
            };
        }

        /// <summary>
        /// ステージアイテムクリック時
        /// </summary>
        private void OnStageItemClicked(StageSelectItemData data)
        {
            _selectedStageId = data.StageId;
            ShowStageDetail(data);
        }

        /// <summary>
        /// ステージ詳細を表示
        /// </summary>
        private void ShowStageDetail(StageSelectItemData data)
        {
            _detailPanel?.RemoveFromClassList("detail-panel--hidden");

            if (_detailTitle != null)
            {
                _detailTitle.text = data.StageName;
            }

            if (_detailDescription != null)
            {
                _detailDescription.text = data.Description ?? "";
            }

            if (_detailHighScore != null)
            {
                _detailHighScore.text = data.IsCleared
                    ? $"{data.HighScore:N0}"
                    : "---";
            }

            if (_detailBestTime != null)
            {
                if (data.IsCleared && data.HasBestClearTime)
                {
                    var minutes = (int)(data.BestClearTime / 60);
                    var seconds = (int)(data.BestClearTime % 60);
                    _detailBestTime.text = $"{minutes:D2}:{seconds:D2}";
                }
                else
                {
                    _detailBestTime.text = "--:--";
                }
            }

            UpdateStarRating(data.StarRating);
        }

        /// <summary>
        /// 星評価を更新
        /// </summary>
        private void UpdateStarRating(int rating)
        {
            if (_starsContainer == null) return;

            var stars = _starsContainer.Query<Label>(className: "star").ToList();
            for (int i = 0; i < stars.Count; i++)
            {
                var star = stars[i];
                star.RemoveFromClassList("star--filled");
                star.RemoveFromClassList("star--empty");

                if (i < rating)
                {
                    star.text = "★";
                    star.AddToClassList("star--filled");
                }
                else
                {
                    star.text = "☆";
                    star.AddToClassList("star--empty");
                }
            }
        }

        /// <summary>
        /// 再開パネルを非表示
        /// </summary>
        private void HideResumePanel()
        {
            _resumePanel?.AddToClassList("overlay--hidden");
        }

        #endregion
    }
}