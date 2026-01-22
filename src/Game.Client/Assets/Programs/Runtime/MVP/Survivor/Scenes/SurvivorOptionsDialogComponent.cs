using System.Collections.Generic;
using Game.MVP.Core.Scenes;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// オプション画面のステージデータ
    /// </summary>
    public class OptionsStageData
    {
        public int StageId { get; set; }
        public string StageName { get; set; }
        public bool HasRecord { get; set; }
        public int HighScore { get; set; }
        public int StarRating { get; set; }
    }

    /// <summary>
    /// オプション画面のオーディオデータ
    /// </summary>
    public class OptionsAudioData
    {
        public int MasterVolume { get; set; }
        public int BgmVolume { get; set; }
        public int VoiceVolume { get; set; }
        public int SeVolume { get; set; }
    }

    /// <summary>
    /// カスタムボリュームスライダーのデータ
    /// </summary>
    internal class VolumeSliderData
    {
        public string Category { get; set; }
        public VisualElement Container { get; set; }
        public VisualElement Fill { get; set; }
        public VisualElement Dragger { get; set; }
        public Label ValueLabel { get; set; }
        public int CurrentValue { get; set; }
        public bool IsDragging { get; set; }
    }

    /// <summary>
    /// オプションダイアログのView Component
    /// </summary>
    public class SurvivorOptionsDialogComponent : GameSceneComponent
    {
        private const int MaxVolume = 10;
        private const int DraggerSize = 56;

        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        private readonly Subject<Unit> _onCloseClicked = new();
        private readonly Subject<int> _onDeleteStageClicked = new();
        private readonly Subject<(string category, int value)> _onVolumeChanged = new();

        public Observable<Unit> OnCloseClicked => _onCloseClicked;
        public Observable<int> OnDeleteStageClicked => _onDeleteStageClicked;
        public Observable<(string category, int value)> OnVolumeChanged => _onVolumeChanged;

        // UI Element References
        private VisualElement _root;
        private Button _closeButton;
        private Button _tabSaveData;
        private Button _tabAudio;
        private VisualElement _contentSaveData;
        private VisualElement _contentAudio;
        private VisualElement _audioSettings;
        private ScrollView _stageList;
        private Label _noRecordsLabel;

        // Volume sliders
        private readonly List<VolumeSliderData> _volumeSliders = new();
        private readonly List<VisualElement> _stageItems = new();

        protected override void OnDestroy()
        {
            _onCloseClicked.Dispose();
            _onDeleteStageClicked.Dispose();
            _onVolumeChanged.Dispose();
            base.OnDestroy();
        }

        private void Awake()
        {
            QueryUIElements();
            SetupEventHandlers();
            SetupVolumeSliders();
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;

            _closeButton = _root.Q<Button>("close-button");
            _tabSaveData = _root.Q<Button>("tab-save-data");
            _tabAudio = _root.Q<Button>("tab-audio");
            _contentSaveData = _root.Q<VisualElement>("content-save-data");
            _contentAudio = _root.Q<VisualElement>("content-audio");
            _audioSettings = _root.Q<VisualElement>(className: "audio-settings");
            _stageList = _root.Q<ScrollView>("stage-list");
            _noRecordsLabel = _root.Q<Label>("no-records-label");
        }

        private void SetupEventHandlers()
        {
            _closeButton?.RegisterCallback<ClickEvent>(_ =>
                _onCloseClicked.OnNext(Unit.Default));

            _tabSaveData?.RegisterCallback<ClickEvent>(_ => SwitchToTab("save-data"));
            _tabAudio?.RegisterCallback<ClickEvent>(_ => SwitchToTab("audio"));
        }

        private void SetupVolumeSliders()
        {
            SetupVolumeSlider("master");
            SetupVolumeSlider("bgm");
            SetupVolumeSlider("voice");
            SetupVolumeSlider("se");
        }

        private void SetupVolumeSlider(string category)
        {
            var container = _root.Q<VisualElement>($"{category}-slider");
            var fill = _root.Q<VisualElement>($"{category}-fill");
            var dragger = _root.Q<VisualElement>($"{category}-dragger");
            var valueLabel = _root.Q<Label>($"{category}-value");

            if (container == null || fill == null || dragger == null) return;

            var sliderData = new VolumeSliderData
            {
                Category = category,
                Container = container,
                Fill = fill,
                Dragger = dragger,
                ValueLabel = valueLabel,
                CurrentValue = 0,
                IsDragging = false
            };

            _volumeSliders.Add(sliderData);

            // Click on track to set value
            container.RegisterCallback<PointerDownEvent>(evt =>
            {
                sliderData.IsDragging = true;
                container.CapturePointer(evt.pointerId);
                UpdateSliderFromPointer(sliderData, evt.localPosition.x);
            });

            container.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (sliderData.IsDragging)
                {
                    UpdateSliderFromPointer(sliderData, evt.localPosition.x);
                }
            });

            container.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (sliderData.IsDragging)
                {
                    sliderData.IsDragging = false;
                    container.ReleasePointer(evt.pointerId);
                }
            });

            container.RegisterCallback<PointerCaptureOutEvent>(_ =>
            {
                sliderData.IsDragging = false;
            });
        }

        private void UpdateSliderFromPointer(VolumeSliderData sliderData, float localX)
        {
            var containerWidth = sliderData.Container.resolvedStyle.width;
            if (containerWidth <= 0) return;

            // Calculate value from position (0-10)
            var ratio = Mathf.Clamp01(localX / containerWidth);
            var newValue = Mathf.RoundToInt(ratio * MaxVolume);

            if (newValue != sliderData.CurrentValue)
            {
                sliderData.CurrentValue = newValue;
                UpdateSliderVisuals(sliderData);
                _onVolumeChanged.OnNext((sliderData.Category, newValue));
            }
        }

        private void UpdateSliderVisuals(VolumeSliderData sliderData)
        {
            var containerWidth = sliderData.Container.resolvedStyle.width;
            if (containerWidth <= 0) return;

            var ratio = (float)sliderData.CurrentValue / MaxVolume;
            var position = ratio * containerWidth;

            // Update fill width
            sliderData.Fill.style.width = new Length(ratio * 100f, LengthUnit.Percent);

            // Update dragger position
            sliderData.Dragger.style.left = position;

            // Update value label
            if (sliderData.ValueLabel != null)
            {
                sliderData.ValueLabel.text = sliderData.CurrentValue.ToString();
            }
        }

        private void SwitchToTab(string tabId)
        {
            // Update tab button states
            _tabSaveData?.RemoveFromClassList("tab-button--active");
            _tabAudio?.RemoveFromClassList("tab-button--active");

            // Update content visibility
            _contentSaveData?.RemoveFromClassList("tab-content--active");
            _contentAudio?.RemoveFromClassList("tab-content--active");

            switch (tabId)
            {
                case "save-data":
                    _tabSaveData?.AddToClassList("tab-button--active");
                    _contentSaveData?.AddToClassList("tab-content--active");
                    break;
                case "audio":
                    _tabAudio?.AddToClassList("tab-button--active");
                    _contentAudio?.AddToClassList("tab-content--active");
                    // Update slider visuals when tab is shown (to get correct container width)
                    _root.schedule.Execute(() =>
                    {
                        foreach (var slider in _volumeSliders)
                        {
                            UpdateSliderVisuals(slider);
                        }
                    });
                    break;
            }
        }

        #region Public Methods

        /// <summary>
        /// ステージ一覧を初期化
        /// </summary>
        public void InitializeStageList(List<OptionsStageData> stages)
        {
            _stageList?.Clear();
            _stageItems.Clear();

            var hasRecords = false;

            foreach (var stage in stages)
            {
                if (stage.HasRecord)
                {
                    hasRecords = true;
                }

                var item = CreateStageItem(stage);
                _stageList?.Add(item);
                _stageItems.Add(item);
            }

            // Show/hide no records label
            if (_noRecordsLabel != null)
            {
                _noRecordsLabel.style.display = hasRecords ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        /// <summary>
        /// ステージ記録を更新（削除後など）
        /// </summary>
        public void UpdateStageRecord(int stageId, bool hasRecord, int highScore, int starRating)
        {
            foreach (var item in _stageItems)
            {
                if (item.userData is int id && id == stageId)
                {
                    var recordLabel = item.Q<Label>(className: "record-label");
                    var deleteButton = item.Q<Button>(className: "delete-button");

                    if (recordLabel != null)
                    {
                        recordLabel.text = hasRecord
                            ? $"Score: {highScore:N0} | {new string('\u2605', starRating)}"
                            : "No record";
                    }

                    if (deleteButton != null)
                    {
                        deleteButton.SetEnabled(hasRecord);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// オーディオ設定を初期化
        /// </summary>
        public void InitializeAudioSettings(OptionsAudioData data)
        {
            SetSliderValue("master", data.MasterVolume);
            SetSliderValue("bgm", data.BgmVolume);
            SetSliderValue("voice", data.VoiceVolume);
            SetSliderValue("se", data.SeVolume);

            // Schedule visual update after layout is calculated, then show
            _root.schedule.Execute(() =>
            {
                foreach (var slider in _volumeSliders)
                {
                    UpdateSliderVisuals(slider);
                }
                // Show audio settings after initialization
                _audioSettings?.AddToClassList("audio-settings--ready");
            });
        }

        private void SetSliderValue(string category, int value)
        {
            var slider = _volumeSliders.Find(s => s.Category == category);
            if (slider != null)
            {
                slider.CurrentValue = Mathf.Clamp(value, 0, MaxVolume);
                if (slider.ValueLabel != null)
                {
                    slider.ValueLabel.text = slider.CurrentValue.ToString();
                }
            }
        }

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }

        #endregion

        #region Private Methods

        private VisualElement CreateStageItem(OptionsStageData data)
        {
            var item = new VisualElement();
            item.AddToClassList("stage-item");
            item.userData = data.StageId;

            // Stage info container
            var infoContainer = new VisualElement();
            infoContainer.AddToClassList("stage-info");

            var numberLabel = new Label($"{data.StageId:D2}");
            numberLabel.AddToClassList("stage-number");

            var nameLabel = new Label(data.StageName);
            nameLabel.AddToClassList("stage-name");

            infoContainer.Add(numberLabel);
            infoContainer.Add(nameLabel);

            // Record info
            var recordContainer = new VisualElement();
            recordContainer.AddToClassList("stage-record");

            var recordLabel = new Label(data.HasRecord
                ? $"Score: {data.HighScore:N0} | {new string('\u2605', data.StarRating)}"
                : "No record");
            recordLabel.AddToClassList("record-label");

            var deleteButton = new Button { text = "DELETE" };
            deleteButton.AddToClassList("delete-button");
            deleteButton.SetEnabled(data.HasRecord);
            deleteButton.RegisterCallback<ClickEvent>(_ =>
                _onDeleteStageClicked.OnNext(data.StageId));

            recordContainer.Add(recordLabel);
            recordContainer.Add(deleteButton);

            item.Add(infoContainer);
            item.Add(recordContainer);

            return item;
        }

        #endregion
    }
}
