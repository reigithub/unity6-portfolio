using System.Collections.Generic;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivor総合リザルト画面のルートコンポーネント
    /// スコア、キル数、クリア時間などを表示
    /// </summary>
    public class SurvivorTotalResultSceneComponent : GameSceneComponent
    {
        [Header("Result Header")]
        [SerializeField] private TextMeshProUGUI _resultTitleText;

        [SerializeField] private GameObject _victoryIcon;
        [SerializeField] private GameObject _gameOverIcon;

        [Header("Total Stats")]
        [SerializeField] private TextMeshProUGUI _totalScoreText;

        [SerializeField] private TextMeshProUGUI _totalKillsText;
        [SerializeField] private TextMeshProUGUI _totalTimeText;

        [Header("Stage Results")]
        [SerializeField] private Transform _stageResultContainer;

        [SerializeField] private GameObject _stageResultItemPrefab;

        [Header("Buttons")]
        [SerializeField] private Button _retryButton;

        [SerializeField] private Button _returnToTitleButton;

        private readonly Subject<Unit> _onRetryClicked = new();
        private readonly Subject<Unit> _onReturnToTitleClicked = new();

        public Observable<Unit> OnRetryClicked => _onRetryClicked;
        public Observable<Unit> OnReturnToTitleClicked => _onReturnToTitleClicked;

        protected override void OnDestroy()
        {
            _onRetryClicked.Dispose();
            _onReturnToTitleClicked.Dispose();
            base.OnDestroy();
        }

        protected void Awake()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (_retryButton != null)
            {
                _retryButton.OnClickAsObservable()
                    .Subscribe(_ => _onRetryClicked.OnNext(Unit.Default))
                    .AddTo(Disposables);
            }

            if (_returnToTitleButton != null)
            {
                _returnToTitleButton.OnClickAsObservable()
                    .Subscribe(_ => _onReturnToTitleClicked.OnNext(Unit.Default))
                    .AddTo(Disposables);
            }
        }

        /// <summary>
        /// リザルトデータを設定
        /// </summary>
        public void SetResultData(int totalScore, int totalKills, IReadOnlyList<StageResultData> stageResults, bool isVictory)
        {
            // ヘッダー設定
            if (_resultTitleText != null)
            {
                _resultTitleText.text = isVictory ? "VICTORY!" : "GAME OVER";
            }

            if (_victoryIcon != null) _victoryIcon.SetActive(isVictory);
            if (_gameOverIcon != null) _gameOverIcon.SetActive(!isVictory);

            // 総合スコア
            if (_totalScoreText != null)
            {
                _totalScoreText.text = $"{totalScore:N0}";
            }

            // 総キル数
            if (_totalKillsText != null)
            {
                _totalKillsText.text = $"{totalKills}";
            }

            // 総プレイ時間
            float totalTime = 0f;
            foreach (var result in stageResults)
            {
                totalTime += result.ClearTime;
            }

            if (_totalTimeText != null)
            {
                var minutes = Mathf.FloorToInt(totalTime / 60f);
                var seconds = Mathf.FloorToInt(totalTime % 60f);
                _totalTimeText.text = $"{minutes:00}:{seconds:00}";
            }

            // ステージ別結果
            PopulateStageResults(stageResults);
        }

        private void PopulateStageResults(IReadOnlyList<StageResultData> stageResults)
        {
            if (_stageResultContainer == null || _stageResultItemPrefab == null) return;

            // 既存の子要素をクリア
            foreach (Transform child in _stageResultContainer)
            {
                Destroy(child.gameObject);
            }

            // 各ステージの結果を表示
            for (int i = 0; i < stageResults.Count; i++)
            {
                var result = stageResults[i];
                var item = Instantiate(_stageResultItemPrefab, _stageResultContainer);

                var itemText = item.GetComponentInChildren<TextMeshProUGUI>();
                if (itemText != null)
                {
                    var minutes = Mathf.FloorToInt(result.ClearTime / 60f);
                    var seconds = Mathf.FloorToInt(result.ClearTime % 60f);
                    var status = result.IsVictory ? "Clear" : "Failed";

                    itemText.text = $"Stage {i + 1}: {status} - Score: {result.Score:N0} - Kills: {result.Kills} - Time: {minutes:00}:{seconds:00}";
                }
            }
        }
    }
}