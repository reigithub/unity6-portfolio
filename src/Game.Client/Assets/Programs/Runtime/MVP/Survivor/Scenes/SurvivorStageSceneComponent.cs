using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Models;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Services;
using Game.MVP.Survivor.Weapon;
using R3;
using UnityEngine;
using UnityEngine.UIElements;

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
        private VisualElement _hpBarFill;
        private VisualElement _expBarFill;
        private Button _pauseButton;
        private VisualElement _gameOverPanel;
        private VisualElement _victoryPanel;

        // Cached values for bar calculations
        private int _maxHp = 100;
        private int _maxExp = 100;

        protected override void OnDestroy()
        {
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
            _hpBarFill = _root.Q<VisualElement>("hp-bar-fill");
            _expBarFill = _root.Q<VisualElement>("exp-bar-fill");
            _pauseButton = _root.Q<Button>("pause-button");
            _gameOverPanel = _root.Q<VisualElement>("game-over-panel");
            _victoryPanel = _root.Q<VisualElement>("victory-panel");
        }

        private void SetupEventHandlers()
        {
            _pauseButton?.RegisterCallback<ClickEvent>(_ =>
                _onPauseClicked.OnNext(Unit.Default));
        }

        public void Initialize(SurvivorStageModel model)
        {
            // Hide result panels
            _gameOverPanel?.AddToClassList("result-overlay--hidden");
            _victoryPanel?.AddToClassList("result-overlay--hidden");

            // Initial values
            _maxHp = model.MaxHp.Value;
            _maxExp = model.ExperienceToNextLevel.Value;

            UpdateHp(model.CurrentHp.Value, model.MaxHp.Value);
            UpdateExperience(model.Experience.Value, model.ExperienceToNextLevel.Value);
            UpdateLevel(model.Level.Value);
            UpdateKills(model.TotalKills.Value);
            UpdateWave(model.CurrentWave.Value);
            UpdateTime(0f);
        }

        public void InitializePlayer(SurvivorPlayerMaster playerMaster, Camera mainCamera)
        {
            if (_playerController != null && playerMaster != null)
            {
                _playerController.Initialize(playerMaster);
                _playerController.SetMainCamera(mainCamera.transform);
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
                _timeText.text = $"{minutes:00}:{seconds:00}";
            }
        }

        public void UpdateKills(int kills)
        {
            if (_killsText != null)
            {
                _killsText.text = $"KILLS: {kills}";
            }
        }

        public void UpdateWave(int wave)
        {
            if (_waveText != null)
            {
                _waveText.text = $"WAVE {wave}";
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