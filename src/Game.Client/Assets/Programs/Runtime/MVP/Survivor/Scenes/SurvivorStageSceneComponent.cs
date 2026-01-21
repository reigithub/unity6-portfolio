using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Models;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Services;
using Game.MVP.Survivor.Weapon;
using Game.Shared.Services;
using R3;
using UnityEngine;
using UnityEngine.UIElements;
using VContainer;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorステージシーンのルートコンポーネント
    /// UI Toolkit（UXML/USS）使用、UI Builderで編集可能
    /// HUD表示とゲームプレイUIを管理
    /// </summary>
    public class SurvivorStageSceneComponent : GameSceneComponent
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument _uiDocument;

        [Header("Player")]
        [SerializeField] private SurvivorPlayerController _playerController;

        [Header("Spawners")]
        [SerializeField] private SurvivorEnemySpawner _enemySpawner;

        [Header("Weapon")]
        [SerializeField] private SurvivorWeaponManager _weaponManager;

        [Header("Items")]
        [SerializeField] private SurvivorItemSpawner _itemSpawner;

        [Inject] private IInputService _inputService;

        private readonly Subject<Unit> _onPauseClicked = new();
        private readonly Subject<bool> _onApplicationPause = new();
        private readonly Subject<Unit> _onApplicationQuit = new();

        public Observable<Unit> OnPauseClicked => _onPauseClicked;
        public Observable<bool> OnApplicationPauseObservable => _onApplicationPause;
        public Observable<Unit> OnApplicationQuitObservable => _onApplicationQuit;

        // Game Component References
        public SurvivorPlayerController PlayerController => _playerController;
        public SurvivorEnemySpawner EnemySpawner => _enemySpawner;
        public SurvivorWeaponManager WeaponManager => _weaponManager;
        public SurvivorItemSpawner SurvivorItemSpawner => _itemSpawner;

        // UI Element References
        private VisualElement _root;
        private Label _waveText;
        private Label _timeText;
        private Label _levelText;
        private Label _hpText;
        private Label _killsText;
        private Label _enemiesText;
        private VisualElement _hpBarFill;
        private VisualElement _staminaBarFill;
        private VisualElement _expBarFill;
        private Button _pauseButton;
        private VisualElement _gameOverPanel;
        private VisualElement _victoryPanel;

        // Wave Banner Elements
        private VisualElement _waveBanner;
        private Label _waveBannerText;
        private Label _waveBannerSubtext;
        private CancellationTokenSource _bannerCts;

        // Cached values for bar calculations
        private int _maxHp = 100;
        private int _maxStamina = 100;
        private int _maxExp = 100;
        private float _timeLimit = 0f;
        private int _totalWaves = 1;

        protected override void OnDestroy()
        {
            _bannerCts?.Cancel();
            _bannerCts?.Dispose();
            _onPauseClicked.Dispose();
            _onApplicationPause.Dispose();
            _onApplicationQuit.Dispose();
            base.OnDestroy();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _onApplicationPause.OnNext(pauseStatus);
        }

        private void OnApplicationQuit()
        {
            _onApplicationQuit.OnNext(Unit.Default);
        }

        private void Awake()
        {
            QueryUIElements();
            SetupEventHandlers();
        }

        private void QueryUIElements()
        {
            _root = _uiDocument.rootVisualElement;

            _waveText = _root.Q<Label>("wave-text");
            _timeText = _root.Q<Label>("time-text");
            _levelText = _root.Q<Label>("level-text");
            _hpText = _root.Q<Label>("hp-text");
            _killsText = _root.Q<Label>("kills-text");
            _enemiesText = _root.Q<Label>("enemies-text");
            _hpBarFill = _root.Q<VisualElement>("hp-bar-fill");
            _staminaBarFill = _root.Q<VisualElement>("stamina-bar-fill");
            _expBarFill = _root.Q<VisualElement>("exp-bar-fill");
            _pauseButton = _root.Q<Button>("pause-button");
            _gameOverPanel = _root.Q<VisualElement>("game-over-panel");
            _victoryPanel = _root.Q<VisualElement>("victory-panel");

            // Wave Banner
            _waveBanner = _root.Q<VisualElement>("wave-banner");
            _waveBannerText = _root.Q<Label>("wave-banner-text");
            _waveBannerSubtext = _root.Q<Label>("wave-banner-subtext");
        }

        private void SetupEventHandlers()
        {
            _pauseButton?.RegisterCallback<ClickEvent>(_ =>
                _onPauseClicked.OnNext(Unit.Default));
        }

        public void Initialize(SurvivorStageModel model, int totalWaves)
        {
            // Hide result panels
            _gameOverPanel?.AddToClassList("result-overlay--hidden");
            _victoryPanel?.AddToClassList("result-overlay--hidden");
            _waveBanner?.AddToClassList("wave-banner--hidden");

            // Initial values
            _maxHp = model.MaxHp.Value;
            _maxExp = model.ExperienceToNextLevel.Value;
            _timeLimit = model.TimeLimit;
            _totalWaves = totalWaves;

            UpdateHp(model.CurrentHp.Value, model.MaxHp.Value);
            UpdateExperience(model.Experience.Value, model.ExperienceToNextLevel.Value);
            UpdateLevel(model.Level.Value);
            UpdateKills(model.TotalKills.Value);
            UpdateWave(model.CurrentWave.Value, totalWaves);
            UpdateTime(0f);
            UpdateEnemies(0, 0);
        }

        public void InitializePlayer(SurvivorPlayerLevelMaster levelMaster, Camera mainCamera)
        {
            if (_playerController != null && levelMaster != null)
            {
                _playerController.Initialize(levelMaster);
                _playerController.SetMainCamera(mainCamera.transform);

                // スタミナ初期表示
                UpdateStamina(levelMaster.MaxStamina, levelMaster.MaxStamina);
            }
        }

        /// <summary>
        /// 動的生成されたプレイヤーコントローラーを設定する
        /// </summary>
        public void SetPlayerController(SurvivorPlayerController playerController)
        {
            _playerController = playerController;
        }

        public async UniTask InitializeEnemySpawnerAsync(SurvivorStageWaveManager waveManager)
        {
            if (_enemySpawner != null && _playerController != null)
            {
                _enemySpawner.SetPlayer(_playerController.transform);
                await _enemySpawner.InitializeAsync(waveManager);
            }
        }

        public async UniTask InitializeWeaponManagerAsync(int startingWeaponId, float damageMultiplier = 1f)
        {
            if (_weaponManager != null && _playerController != null)
            {
                await _weaponManager.InitializeAsync(_playerController.transform, startingWeaponId, damageMultiplier);
            }
        }

        public async UniTask InitializeExperienceOrbSpawnerAsync()
        {
            if (_itemSpawner != null)
            {
                await _itemSpawner.InitializeAsync();

                if (_enemySpawner != null)
                {
                    _itemSpawner.ConnectToEnemySpawner(_enemySpawner);
                }
            }
        }

        #region HUD Updates

        public void UpdateHp(int current, int max)
        {
            _maxHp = max;

            if (_hpText != null)
            {
                _hpText.text = $"{current}/{max}";
            }

            if (_hpBarFill != null)
            {
                var percent = max > 0 ? (float)current / max * 100f : 0f;
                _hpBarFill.style.width = Length.Percent(percent);
            }
        }

        public void UpdateStamina(int current, int max)
        {
            _maxStamina = max;

            if (_staminaBarFill != null)
            {
                var percent = max > 0 ? (float)current / max * 100f : 0f;
                _staminaBarFill.style.width = Length.Percent(percent);

                // スタミナが少ない時は色を変える
                if (percent < 30f)
                {
                    _staminaBarFill.AddToClassList("stamina-bar__fill--depleted");
                }
                else
                {
                    _staminaBarFill.RemoveFromClassList("stamina-bar__fill--depleted");
                }
            }
        }

        public void UpdateExperience(int current, int max)
        {
            _maxExp = max;

            if (_expBarFill != null)
            {
                var percent = max > 0 ? (float)current / max * 100f : 0f;
                _expBarFill.style.width = Length.Percent(percent);
            }
        }

        public void UpdateLevel(int level)
        {
            if (_levelText != null)
            {
                _levelText.text = $"Lv.{level}";
            }
        }

        public void UpdateTime(float time)
        {
            if (_timeText != null)
            {
                var minutes = Mathf.FloorToInt(time / 60f);
                var seconds = Mathf.FloorToInt(time % 60f);

                if (_timeLimit > 0)
                {
                    var limitMinutes = Mathf.FloorToInt(_timeLimit / 60f);
                    var limitSeconds = Mathf.FloorToInt(_timeLimit % 60f);
                    _timeText.text = $"{minutes:00}:{seconds:00} / {limitMinutes:00}:{limitSeconds:00}";
                }
                else
                {
                    _timeText.text = $"{minutes:00}:{seconds:00}";
                }
            }
        }

        public void UpdateKills(int kills)
        {
            if (_killsText != null)
            {
                _killsText.text = $"KILLS: {kills}";
            }
        }

        public void UpdateWave(int wave, int totalWaves = -1)
        {
            if (totalWaves < 0) totalWaves = _totalWaves;

            if (_waveText != null)
            {
                _waveText.text = $"WAVE {wave}/{totalWaves}";
            }
        }

        public void UpdateEnemies(int killed, int total)
        {
            if (_enemiesText != null)
            {
                _enemiesText.text = $"{killed} / {total}";
            }
        }

        /// <summary>
        /// Wave開始時のバナー表示
        /// </summary>
        public void ShowWaveBanner(int wave, int totalWaves, int enemyCount)
        {
            if (_waveBanner == null) return;

            // 前回のバナー表示をキャンセル
            _bannerCts?.Cancel();
            _bannerCts?.Dispose();
            _bannerCts = new CancellationTokenSource();

            // テキスト設定
            if (_waveBannerText != null)
            {
                _waveBannerText.text = $"WAVE {wave}";
            }

            if (_waveBannerSubtext != null)
            {
                if (wave == totalWaves)
                {
                    _waveBannerSubtext.text = "FINAL WAVE - Defeat the Boss!";
                }
                else
                {
                    _waveBannerSubtext.text = $"Defeat {enemyCount} enemies!";
                }
            }

            // バナー表示
            _waveBanner.RemoveFromClassList("wave-banner--hidden");
            _waveBanner.RemoveFromClassList("wave-banner--fade-out");

            // 一定時間後にフェードアウト
            HideBannerAfterDelayAsync(_bannerCts.Token).Forget();
        }

        private async UniTaskVoid HideBannerAfterDelayAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(2500, cancellationToken: ct);

                if (_waveBanner != null)
                {
                    _waveBanner.AddToClassList("wave-banner--fade-out");
                }

                await UniTask.Delay(300, cancellationToken: ct);

                if (_waveBanner != null)
                {
                    _waveBanner.AddToClassList("wave-banner--hidden");
                }
            }
            catch (System.OperationCanceledException)
            {
                // キャンセルされた場合は何もしない
            }
        }

        #endregion

        #region Result Panels

        public void ShowGameOver()
        {
            _gameOverPanel?.RemoveFromClassList("result-overlay--hidden");
        }

        public void ShowVictory()
        {
            _victoryPanel?.RemoveFromClassList("result-overlay--hidden");
        }

        #endregion

        public override void SetInteractables(bool interactable)
        {
            _root?.SetEnabled(interactable);
            base.SetInteractables(interactable);
        }
    }
}