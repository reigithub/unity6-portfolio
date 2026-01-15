using Cysharp.Threading.Tasks;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Core.Scenes;
using Game.MVP.Survivor.Enemy;
using Game.MVP.Survivor.Item;
using Game.MVP.Survivor.Models;
using Game.MVP.Survivor.Player;
using Game.MVP.Survivor.Services;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.MVP.Survivor.Scenes
{
    /// <summary>
    /// Survivorステージシーンのルートコンポーネント
    /// HUD表示とゲームプレイUIを管理
    /// </summary>
    public class SurvivorStageSceneComponent : GameSceneComponent
    {
        [Header("HUD Elements")]
        [SerializeField] private Slider _hpSlider;

        [SerializeField] private TextMeshProUGUI _hpText;
        [SerializeField] private Slider _expSlider;
        [SerializeField] private TextMeshProUGUI _levelText;
        [SerializeField] private TextMeshProUGUI _timeText;
        [SerializeField] private TextMeshProUGUI _killsText;
        [SerializeField] private TextMeshProUGUI _waveText;

        [Header("Buttons")]
        [SerializeField] private Button _pauseButton;

        [Header("Game Over / Victory")]
        [SerializeField] private GameObject _gameOverPanel;

        [SerializeField] private GameObject _victoryPanel;

        [Header("Player")]
        [SerializeField] private SurvivorPlayerController _playerController;

        [Header("Spawners")]
        [SerializeField] private SurvivorEnemySpawner _enemySpawner;

        [Header("Weapon")]
        [SerializeField] private Weapon.WeaponManager _weaponManager;

        [Header("Items")]
        [SerializeField] private ExperienceOrbSpawner _experienceOrbSpawner;

        private readonly Subject<Unit> _onPauseClicked = new();
        private readonly Subject<bool> _onApplicationPause = new();
        private readonly Subject<Unit> _onApplicationQuit = new();

        public Observable<Unit> OnPauseClicked => _onPauseClicked;

        /// <summary>
        /// アプリケーション中断時（バックグラウンド移行時）
        /// </summary>
        public Observable<bool> OnApplicationPauseObservable => _onApplicationPause;

        /// <summary>
        /// アプリケーション終了時
        /// </summary>
        public Observable<Unit> OnApplicationQuitObservable => _onApplicationQuit;

        /// <summary>
        /// プレイヤーコントローラー参照
        /// </summary>
        public SurvivorPlayerController PlayerController => _playerController;

        /// <summary>
        /// 敵スポーナー参照
        /// </summary>
        public SurvivorEnemySpawner EnemySpawner => _enemySpawner;

        /// <summary>
        /// 武器マネージャー参照
        /// </summary>
        public Weapon.WeaponManager WeaponManager => _weaponManager;

        /// <summary>
        /// 経験値オーブスポーナー参照
        /// </summary>
        public ExperienceOrbSpawner ExperienceOrbSpawner => _experienceOrbSpawner;

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

        protected void Awake()
        {
            SetupButtons();
        }

        private void SetupButtons()
        {
            if (_pauseButton != null)
            {
                _pauseButton.OnClickAsObservable()
                    .Subscribe(_ => _onPauseClicked.OnNext(Unit.Default))
                    .AddTo(Disposables);
            }
        }

        public void Initialize(SurvivorStageModel model)
        {
            // 初期表示
            if (_gameOverPanel != null) _gameOverPanel.SetActive(false);
            if (_victoryPanel != null) _victoryPanel.SetActive(false);

            // 初期値設定
            UpdateHP(model.CurrentHp.Value, model.MaxHp.Value);
            UpdateExperience(model.Experience.Value, model.ExperienceToNextLevel.Value);
            UpdateLevel(model.Level.Value);
            UpdateKills(model.TotalKills.Value);
            UpdateWave(model.CurrentWave.Value);
            UpdateTime(0f);
        }

        /// <summary>
        /// プレイヤーをマスターデータで初期化
        /// </summary>
        public void InitializePlayer(SurvivorPlayerMaster playerMaster)
        {
            if (_playerController != null && playerMaster != null)
            {
                _playerController.Initialize(playerMaster);
            }
        }

        /// <summary>
        /// 敵スポーナーを初期化
        /// </summary>
        public async UniTask InitializeEnemySpawnerAsync(SurvivorStageWaveManager waveManager)
        {
            if (_enemySpawner != null && _playerController != null)
            {
                _enemySpawner.SetPlayer(_playerController.transform);
                await _enemySpawner.InitializeAsync(waveManager);
            }
        }

        /// <summary>
        /// 武器マネージャーを初期化
        /// </summary>
        public async UniTask InitializeWeaponManagerAsync(int startingWeaponId, float damageMultiplier = 1f)
        {
            if (_weaponManager != null && _playerController != null)
            {
                await _weaponManager.InitializeAsync(_playerController.transform, startingWeaponId, damageMultiplier);
            }
        }

        /// <summary>
        /// 経験値オーブスポーナーを初期化
        /// </summary>
        public async UniTask InitializeExperienceOrbSpawnerAsync()
        {
            if (_experienceOrbSpawner != null)
            {
                await _experienceOrbSpawner.InitializeAsync();

                // 敵スポーナーに接続
                if (_enemySpawner != null)
                {
                    _experienceOrbSpawner.ConnectToEnemySpawner(_enemySpawner);
                }
            }
        }

        public void UpdateHP(int current, int max)
        {
            if (_hpSlider != null)
            {
                _hpSlider.maxValue = max;
                _hpSlider.value = current;
            }

            if (_hpText != null)
            {
                _hpText.text = $"{current}/{max}";
            }
        }

        public void UpdateExperience(int current, int max)
        {
            if (_expSlider != null)
            {
                _expSlider.maxValue = max;
                _expSlider.value = current;
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
                _killsText.text = $"Kills: {kills}";
            }
        }

        public void UpdateWave(int wave)
        {
            if (_waveText != null)
            {
                _waveText.text = $"Wave {wave}";
            }
        }

        public void ShowGameOver()
        {
            if (_gameOverPanel != null)
            {
                _gameOverPanel.SetActive(true);
            }
        }

        public void ShowVictory()
        {
            if (_victoryPanel != null)
            {
                _victoryPanel.SetActive(true);
            }
        }
    }
}