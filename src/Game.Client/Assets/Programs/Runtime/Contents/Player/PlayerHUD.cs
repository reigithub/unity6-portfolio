using DG.Tweening;
using Game.Core.MasterData.MemoryTables;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Contents.Player
{
    /// <summary>
    /// 簡易的なプレイヤーHUD
    /// </summary>
    public class PlayerHUD : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _uiCanvasGroup;

        [SerializeField] private Slider _hpGauge;

        [SerializeField] private TextMeshProUGUI _currentHp;
        [SerializeField] private TextMeshProUGUI _maxHp;

        [SerializeField] private Slider _staminaGauge;

        [SerializeField] private TextMeshProUGUI _currentStamina;
        [SerializeField] private TextMeshProUGUI _maxStamina;

        public readonly ReactiveProperty<int> CurrentHp = new();
        public float _maxHpValue = 100;

        public readonly ReactiveProperty<float> CurrentStamina = new();
        public float _maxStaminaValue = 100f;
        public float _staminaDepleteRate = 10f;
        public float _staminaRegenRate = 5f;
        public bool _isRunning;

        private void Awake()
        {
            _uiCanvasGroup.alpha = 0f;
        }

        public void Initialize(PlayerMaster playerMaster)
        {
            CurrentHp.DistinctUntilChanged()
                .Subscribe(x =>
                {
                    _currentHp.text = x.ToString();
                    _hpGauge.value = x / _maxHpValue;
                }).AddTo(this);
            CurrentStamina.DistinctUntilChanged()
                .Subscribe(x =>
                {
                    _currentStamina.text = x.ToString("0");
                    _staminaGauge.value = x / _maxStaminaValue;
                }).AddTo(this);

            _maxHpValue = playerMaster.MaxHp;
            _maxStaminaValue = playerMaster.MaxStamina;
            _staminaDepleteRate = playerMaster.StaminaDepleteRate;
            _staminaRegenRate = playerMaster.StaminaRegenRate;

            CurrentHp.Value = playerMaster.MaxHp;
            _maxHp.text = playerMaster.MaxHp.ToString();

            CurrentStamina.Value = playerMaster.MaxStamina;
            _maxStamina.text = playerMaster.MaxStamina.ToString();
        }

        private　void Update()
        {
            if (_isRunning)
            {
                var nextStamina = CurrentStamina.Value - _staminaDepleteRate * Time.deltaTime;
                CurrentStamina.Value = Mathf.Clamp(nextStamina, 0f, _maxStaminaValue);
            }
            else
            {
                var nextStamina = CurrentStamina.Value + _staminaRegenRate * Time.deltaTime;
                CurrentStamina.Value = Mathf.Clamp(nextStamina, 0f, _maxStaminaValue);
            }
        }

        public void SetRunInput(bool isRunning)
        {
            _isRunning = isRunning;
        }

        public void DoFadeIn()
        {
            _uiCanvasGroup.DOFade(1f, 0.25f);
        }

        public void DoFadeOut()
        {
            _uiCanvasGroup.DOFade(0f, 0.25f);
        }
    }
}