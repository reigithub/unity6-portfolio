using System;
using System.Collections.Generic;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivor総合リザルト画面のルートコンポーネント
    /// UI Toolkit（UXML/USS）使用、UI Builderで編集可能
    /// スコア、キル数、クリア時間などを表示
    /// </summary>
    public class SurvivorTotalResultSceneComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        [SerializeField] private VisualTreeAsset _stageResultItemTemplate;

        private readonly Subject<Unit> _onRetryClicked = new();
        private readonly Subject<Unit> _onReturnToTitleClicked = new();

        public Observable<Unit> OnRetryClicked => _onRetryClicked;
        public Observable<Unit> OnReturnToTitleClicked => _onReturnToTitleClicked;

        // UI Element References
        private VisualElement _root;
        private Label _resultTitleText;
        private Label _totalScoreText;
        private Label _totalKillsText;
        private Label _totalTimeText;
        private Label _totalHpText;
        private VisualElement _stageResultsContainer;
        private Button _retryButton;
        private Button _returnButton;

        protected override void OnDestroy()
        {
            _onRetryClicked.Dispose();
            _onReturnToTitleClicked.Dispose();
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

            _resultTitleText = _root.Q<Label>("result-title");
            _totalScoreText = _root.Q<Label>("total-score");
            _totalKillsText = _root.Q<Label>("total-kills");
            _totalTimeText = _root.Q<Label>("total-time");
            _totalHpText = _root.Q<Label>("total-hp");
            _stageResultsContainer = _root.Q<VisualElement>("stage-results-container");
            _retryButton = _root.Q<Button>("retry-button");
            _returnButton = _root.Q<Button>("return-button");
        }

        private void SetupEventHandlers()
        {
            _retryButton?.RegisterCallback<ClickEvent>(_ =>
                _onRetryClicked.OnNext(Unit.Default));

            _returnButton?.RegisterCallback<ClickEvent>(_ =>
                _onReturnToTitleClicked.OnNext(Unit.Default));
        }

        /// <summary>
        /// リザルトデータを設定
        /// </summary>
        public void SetResultData(int totalScore, int totalKills, IReadOnlyList<SurvivorStageResultData> stageResults, bool isVictory)
        {
            // ヘッダー設定
            if (_resultTitleText != null)
            {
                _resultTitleText.text = isVictory ? "VICTORY!" : "GAME OVER";
                _resultTitleText.RemoveFromClassList("header__title--victory");
                _resultTitleText.RemoveFromClassList("header__title--gameover");
                _resultTitleText.AddToClassList(isVictory ? "header__title--victory" : "header__title--gameover");
            }

            // 総合スコア
            if (_totalScoreText != null)
                _totalScoreText.text = $"{totalScore:N0}";

            // 総キル数
            if (_totalKillsText != null)
                _totalKillsText.text = $"{totalKills}";

            // 総プレイ時間
            float totalTime = 0f;
            foreach (var result in stageResults)
            {
                totalTime += result.ClearTime;
            }

            var minutes = Mathf.FloorToInt(totalTime / 60f);
            var seconds = Mathf.FloorToInt(totalTime % 60f);
            if (_totalTimeText != null)
                _totalTimeText.text = $"{minutes:00}:{seconds:00}";

            // 最終HP%（最後のステージのHP割合）
            if (_totalHpText != null && stageResults.Count > 0)
            {
                var lastResult = stageResults[stageResults.Count - 1];
                var hpPercent = Mathf.RoundToInt(lastResult.HpRatio * 100f);
                _totalHpText.text = $"{hpPercent}%";
            }

            // 総合星評価（クリアしたステージの最低評価値）
            UpdateTotalStarRating(stageResults);

            // ステージ別結果
            PopulateStageResults(stageResults);
        }

        private void UpdateTotalStarRating(IReadOnlyList<SurvivorStageResultData> stageResults)
        {
            // クリアしたステージの最低星評価を計算
            int minStarRating = 3;
            bool hasVictory = false;

            foreach (var result in stageResults)
            {
                if (result.IsVictory)
                {
                    hasVictory = true;
                    if (result.StarRating < minStarRating)
                    {
                        minStarRating = result.StarRating;
                    }
                }
            }

            // 勝利がない場合は0星
            int totalStarRating = hasVictory ? minStarRating : 0;

            // 総合星評価を表示
            for (int i = 1; i <= 3; i++)
            {
                var star = _root.Q<Label>($"total-star-{i}");
                if (star != null)
                {
                    star.RemoveFromClassList("total-star--empty");
                    star.RemoveFromClassList("total-star--filled");

                    if (i <= totalStarRating)
                    {
                        star.AddToClassList("total-star--filled");
                    }
                    else
                    {
                        star.AddToClassList("total-star--empty");
                    }
                }
            }
        }

        private void PopulateStageResults(IReadOnlyList<SurvivorStageResultData> stageResults)
        {
            if (_stageResultsContainer == null) return;

            // 既存の子要素をクリア
            _stageResultsContainer.Clear();

            // 各ステージの結果を表示
            for (int i = 0; i < stageResults.Count; i++)
            {
                var result = stageResults[i];
                var item = CreateStageResultItem(i + 1, result);
                _stageResultsContainer.Add(item);
            }
        }

        private VisualElement CreateStageResultItem(int stageNumber, SurvivorStageResultData result)
        {
            // テンプレートから生成
            if (_stageResultItemTemplate == null)
                throw new NullReferenceException($"{_stageResultItemTemplate} is null.");

            var templateInstance = _stageResultItemTemplate.Instantiate();
            var item = templateInstance.Q<VisualElement>("stage-result-item");

            // クリア/失敗のスタイルを適用
            item.RemoveFromClassList("stage-result-item--clear");
            item.RemoveFromClassList("stage-result-item--failed");
            item.AddToClassList(result.IsVictory ? "stage-result-item--clear" : "stage-result-item--failed");

            // ステージ名
            var stageName = item.Q<Label>("stage-name");
            if (stageName != null)
                stageName.text = $"Stage {stageNumber}";

            // ステータス
            var statusLabel = item.Q<Label>("stage-status");
            if (statusLabel != null)
            {
                statusLabel.text = result.IsVictory ? "CLEAR" : "FAILED";
                statusLabel.RemoveFromClassList("stage-result-item__status--clear");
                statusLabel.RemoveFromClassList("stage-result-item__status--failed");
                statusLabel.AddToClassList(result.IsVictory ? "stage-result-item__status--clear" : "stage-result-item__status--failed");
            }

            // スコア
            var scoreLabel = item.Q<Label>("stage-score");
            if (scoreLabel != null)
                scoreLabel.text = $"Score: {result.Score:N0}";

            // キル数
            var killsLabel = item.Q<Label>("stage-kills");
            if (killsLabel != null)
                killsLabel.text = $"Kills: {result.Kills}";

            // 時間
            var minutes = Mathf.FloorToInt(result.ClearTime / 60f);
            var seconds = Mathf.FloorToInt(result.ClearTime % 60f);
            var timeLabel = item.Q<Label>("stage-time");
            if (timeLabel != null)
                timeLabel.text = $"Time: {minutes:00}:{seconds:00}";

            // HP%
            var hpLabel = item.Q<Label>("stage-hp");
            if (hpLabel != null)
            {
                var hpPercent = Mathf.RoundToInt(result.HpRatio * 100f);
                hpLabel.text = $"HP: {hpPercent}%";
            }

            // 星評価
            UpdateStarRating(item, result.StarRating);

            return item;
        }

        private void UpdateStarRating(VisualElement item, int starRating)
        {
            for (int i = 1; i <= 3; i++)
            {
                var star = item.Q<Label>($"star-{i}");
                if (star != null)
                {
                    star.RemoveFromClassList("stage-result-item__star--empty");
                    star.RemoveFromClassList("stage-result-item__star--filled");

                    if (i <= starRating)
                    {
                        star.AddToClassList("stage-result-item__star--filled");
                    }
                    else
                    {
                        star.AddToClassList("stage-result-item__star--empty");
                    }
                }
            }
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}