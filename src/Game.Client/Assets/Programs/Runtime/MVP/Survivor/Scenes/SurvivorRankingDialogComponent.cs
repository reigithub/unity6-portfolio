using System.Collections.Generic;
using Game.Client.MasterData;
using Game.MVP.Core.Scenes;
using Game.Library.Shared.Dto;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// ランキングダイアログのView Component
    /// UI Toolkit（UXML/USS）使用
    /// </summary>
    public class SurvivorRankingDialogComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<int> _onStageSelected = new();
        private readonly Subject<Unit> _onCloseClicked = new();
        private readonly Subject<Unit> _onRefreshClicked = new();

        public Observable<int> OnStageSelected => _onStageSelected;
        public Observable<Unit> OnCloseClicked => _onCloseClicked;
        public Observable<Unit> OnRefreshClicked => _onRefreshClicked;

        // UI Element References
        private VisualElement _root;
        private DropdownField _stageDropdown;
        private ScrollView _rankingList;
        private VisualElement _myRankContainer;
        private Label _myRankLabel;
        private Label _myNameLabel;
        private Label _myScoreLabel;
        private Label _myTimeLabel;
        private VisualElement _loadingOverlay;
        private Label _errorLabel;
        private Label _emptyLabel;
        private Label _cacheNoticeLabel;
        private Button _closeButton;
        private Button _refreshButton;

        private List<SurvivorStageMaster> _stages = new();

        protected override void OnDestroy()
        {
            _onStageSelected.Dispose();
            _onCloseClicked.Dispose();
            _onRefreshClicked.Dispose();
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

            _stageDropdown = _root.Q<DropdownField>("stage-dropdown");
            _rankingList = _root.Q<ScrollView>("ranking-list");
            _myRankContainer = _root.Q<VisualElement>("my-rank-container");
            _myRankLabel = _root.Q<Label>("my-rank");
            _myNameLabel = _root.Q<Label>("my-name");
            _myScoreLabel = _root.Q<Label>("my-score");
            _myTimeLabel = _root.Q<Label>("my-time");
            _loadingOverlay = _root.Q<VisualElement>("loading-overlay");
            _errorLabel = _root.Q<Label>("error-label");
            _emptyLabel = _root.Q<Label>("empty-label");
            _cacheNoticeLabel = _root.Q<Label>("cache-notice-label");
            _closeButton = _root.Q<Button>("close-button");
            _refreshButton = _root.Q<Button>("refresh-button");
        }

        private void SetupEventHandlers()
        {
            _stageDropdown?.RegisterValueChangedCallback(evt =>
            {
                var selectedIndex = _stageDropdown.index;
                if (selectedIndex >= 0 && selectedIndex < _stages.Count)
                {
                    _onStageSelected.OnNext(_stages[selectedIndex].Id);
                }
            });

            _closeButton?.RegisterCallback<ClickEvent>(_ =>
                _onCloseClicked.OnNext(Unit.Default));

            _refreshButton?.RegisterCallback<ClickEvent>(_ =>
                _onRefreshClicked.OnNext(Unit.Default));
        }

        #region Public Methods

        /// <summary>
        /// ステージ選択肢を設定
        /// </summary>
        public void SetStageOptions(List<SurvivorStageMaster> stages)
        {
            _stages = stages;

            if (_stageDropdown == null) return;

            var choices = new List<string>();
            foreach (var stage in _stages)
            {
                choices.Add(stage.Name);
            }

            _stageDropdown.choices = choices;

            if (choices.Count > 0)
            {
                _stageDropdown.index = 0;
            }
        }

        /// <summary>
        /// ランキングデータを設定
        /// </summary>
        public void SetRankingData(RankingResponse ranking, RankingEntryDto myRank)
        {
            // ランキングリストをクリア
            if (_rankingList != null)
            {
                _rankingList.Clear();

                if (ranking.Entries != null && ranking.Entries.Count > 0)
                {
                    _emptyLabel?.AddToClassList("hidden");

                    foreach (var entry in ranking.Entries)
                    {
                        var item = CreateRankingEntryItem(entry);
                        _rankingList.Add(item);
                    }
                }
                else
                {
                    _emptyLabel?.RemoveFromClassList("hidden");
                }
            }

            // 自分の順位を表示
            if (_myRankContainer != null)
            {
                if (myRank != null)
                {
                    _myRankContainer.RemoveFromClassList("hidden");

                    if (_myRankLabel != null)
                        _myRankLabel.text = $"#{myRank.Rank}";

                    if (_myNameLabel != null)
                        _myNameLabel.text = myRank.UserName;

                    if (_myScoreLabel != null)
                        _myScoreLabel.text = $"{myRank.Score:N0}";

                    if (_myTimeLabel != null)
                    {
                        var minutes = Mathf.FloorToInt(myRank.ClearTime / 60f);
                        var seconds = Mathf.FloorToInt(myRank.ClearTime % 60f);
                        _myTimeLabel.text = $"{minutes:00}:{seconds:00}";
                    }
                }
                else
                {
                    _myRankContainer.AddToClassList("hidden");
                }
            }
        }

        private VisualElement CreateRankingEntryItem(RankingEntryDto entry)
        {
            var item = new VisualElement();
            item.AddToClassList("ranking-entry");

            // 上位3位のスタイル
            if (entry.Rank <= 3)
            {
                item.AddToClassList($"ranking-entry--rank-{entry.Rank}");
            }

            // 順位
            var rankLabel = new Label($"#{entry.Rank}");
            rankLabel.AddToClassList("entry-rank");

            // ユーザー名
            var nameLabel = new Label(entry.UserName);
            nameLabel.AddToClassList("entry-name");

            // スコア
            var scoreLabel = new Label($"{entry.Score:N0}");
            scoreLabel.AddToClassList("entry-score");

            // タイム
            var minutes = Mathf.FloorToInt(entry.ClearTime / 60f);
            var seconds = Mathf.FloorToInt(entry.ClearTime % 60f);
            var timeLabel = new Label($"{minutes:00}:{seconds:00}");
            timeLabel.AddToClassList("entry-time");

            item.Add(rankLabel);
            item.Add(nameLabel);
            item.Add(scoreLabel);
            item.Add(timeLabel);

            return item;
        }

        /// <summary>
        /// ローディング表示を切り替え
        /// </summary>
        public void ShowLoading(bool show)
        {
            if (_loadingOverlay != null)
            {
                if (show)
                {
                    _loadingOverlay.RemoveFromClassList("hidden");
                }
                else
                {
                    _loadingOverlay.AddToClassList("hidden");
                }
            }

            if (_refreshButton != null)
            {
                _refreshButton.SetEnabled(!show);
            }
        }

        /// <summary>
        /// エラーメッセージを表示
        /// </summary>
        public void ShowError(string message)
        {
            if (_errorLabel != null)
            {
                _errorLabel.text = message;
                _errorLabel.RemoveFromClassList("hidden");
            }
        }

        /// <summary>
        /// エラーメッセージをクリア
        /// </summary>
        public void ClearError()
        {
            _errorLabel?.AddToClassList("hidden");
        }

        /// <summary>
        /// キャッシュからのデータ表示通知を表示
        /// </summary>
        /// <param name="message">表示メッセージ</param>
        public void ShowCacheNotice(string message)
        {
            if (_cacheNoticeLabel != null)
            {
                _cacheNoticeLabel.text = message;
                _cacheNoticeLabel.RemoveFromClassList("hidden");
            }
        }

        /// <summary>
        /// キャッシュ通知を非表示
        /// </summary>
        public void HideCacheNotice()
        {
            _cacheNoticeLabel?.AddToClassList("hidden");
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }

        #endregion
    }
}
