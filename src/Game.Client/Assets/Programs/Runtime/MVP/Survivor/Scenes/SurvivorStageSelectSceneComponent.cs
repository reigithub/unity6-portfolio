using System.Collections.Generic;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.SaveData;
using Game.MVP.Survivor.UI;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorステージ選択シーンのルートコンポーネント
    /// </summary>
    public class SurvivorStageSelectSceneComponent : GameSceneComponent
    {
        [Header("UI References")]
        [SerializeField] private Transform _stageItemContainer;

        [SerializeField] private GameObject _stageItemPrefab;
        [SerializeField] private Button _backButton;

        [Header("Resume Panel")]
        [SerializeField] private GameObject _resumePanel;

        [SerializeField] private TextMeshProUGUI _resumeInfoText;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _newGameButton;

        [Header("Stage Detail Panel")]
        [SerializeField] private GameObject _detailPanel;

        [SerializeField] private TextMeshProUGUI _stageNameText;
        [SerializeField] private TextMeshProUGUI _stageDescriptionText;
        [SerializeField] private TextMeshProUGUI _highScoreText;
        [SerializeField] private TextMeshProUGUI _bestTimeText;
        [SerializeField] private GameObject[] _starIcons;
        [SerializeField] private Button _playButton;

        private readonly Subject<int> _onStageSelected = new();
        private readonly Subject<Unit> _onBackClicked = new();
        private readonly List<StageSelectItemView> _stageItems = new();

        private int _selectedStageId;
        private StageSession _resumeSession;

        public Observable<int> OnStageSelected => _onStageSelected;
        public Observable<Unit> OnBackClicked => _onBackClicked;

        protected override void OnDestroy()
        {
            _onStageSelected.Dispose();
            _onBackClicked.Dispose();
            base.OnDestroy();
        }

        protected void Awake()
        {
            SetupButtons();

            if (_resumePanel != null)
            {
                _resumePanel.SetActive(false);
            }

            if (_detailPanel != null)
            {
                _detailPanel.SetActive(false);
            }
        }

        private void SetupButtons()
        {
            if (_backButton != null)
            {
                _backButton.OnClickAsObservable()
                    .Subscribe(_ => _onBackClicked.OnNext(Unit.Default))
                    .AddTo(Disposables);
            }

            if (_playButton != null)
            {
                _playButton.OnClickAsObservable()
                    .Subscribe(_ =>
                    {
                        if (_selectedStageId > 0)
                        {
                            _onStageSelected.OnNext(_selectedStageId);
                        }
                    })
                    .AddTo(Disposables);
            }

            if (_resumeButton != null)
            {
                _resumeButton.OnClickAsObservable()
                    .Subscribe(_ =>
                    {
                        if (_resumeSession != null)
                        {
                            _onStageSelected.OnNext(_resumeSession.StageId);
                        }
                    })
                    .AddTo(Disposables);
            }

            if (_newGameButton != null)
            {
                _newGameButton.OnClickAsObservable()
                    .Subscribe(_ =>
                    {
                        if (_resumePanel != null)
                        {
                            _resumePanel.SetActive(false);
                        }
                    })
                    .AddTo(Disposables);
            }
        }

        public void Initialize(List<StageSelectItemData> stages)
        {
            // 既存アイテムをクリア
            foreach (var item in _stageItems)
            {
                if (item != null && item.gameObject != null)
                {
                    Destroy(item.gameObject);
                }
            }

            _stageItems.Clear();

            if (_stageItemPrefab == null || _stageItemContainer == null)
            {
                Debug.LogWarning("[StageSelectScene] Stage item prefab or container is not set");
                return;
            }

            // ステージアイテムを生成
            foreach (var stageData in stages)
            {
                var itemObj = Instantiate(_stageItemPrefab, _stageItemContainer);
                var itemView = itemObj.GetComponent<StageSelectItemView>();

                if (itemView != null)
                {
                    itemView.Setup(stageData);
                    itemView.OnClicked
                        .Subscribe(_ => OnStageItemClicked(stageData))
                        .AddTo(Disposables);

                    _stageItems.Add(itemView);
                }
            }
        }

        public void ShowResumeOption(StageSession session)
        {
            _resumeSession = session;

            if (_resumePanel != null && session != null)
            {
                _resumePanel.SetActive(true);

                if (_resumeInfoText != null)
                {
                    var minutes = (int)(session.ElapsedTime / 60);
                    var seconds = (int)(session.ElapsedTime % 60);
                    _resumeInfoText.text = $"中断データあり\n" +
                                           $"Wave: {session.CurrentWave}\n" +
                                           $"Time: {minutes:D2}:{seconds:D2}\n" +
                                           $"Score: {session.Score}";
                }
            }
        }

        private void OnStageItemClicked(StageSelectItemData stageData)
        {
            if (!stageData.IsUnlocked)
            {
                // ロック中は詳細表示しない
                return;
            }

            _selectedStageId = stageData.StageId;
            ShowStageDetail(stageData);
        }

        private void ShowStageDetail(StageSelectItemData stageData)
        {
            if (_detailPanel == null) return;

            _detailPanel.SetActive(true);

            if (_stageNameText != null)
            {
                _stageNameText.text = stageData.StageName;
            }

            if (_stageDescriptionText != null)
            {
                _stageDescriptionText.text = stageData.Description ?? "";
            }

            if (_highScoreText != null)
            {
                _highScoreText.text = stageData.IsCleared
                    ? $"High Score: {stageData.HighScore}"
                    : "High Score: ---";
            }

            if (_bestTimeText != null)
            {
                if (stageData.IsCleared && stageData.BestClearTime < float.MaxValue)
                {
                    var minutes = (int)(stageData.BestClearTime / 60);
                    var seconds = (int)(stageData.BestClearTime % 60);
                    _bestTimeText.text = $"Best Time: {minutes:D2}:{seconds:D2}";
                }
                else
                {
                    _bestTimeText.text = "Best Time: --:--";
                }
            }

            // 星アイコン表示
            if (_starIcons != null)
            {
                for (int i = 0; i < _starIcons.Length; i++)
                {
                    if (_starIcons[i] != null)
                    {
                        _starIcons[i].SetActive(i < stageData.StarRating);
                    }
                }
            }
        }
    }
}