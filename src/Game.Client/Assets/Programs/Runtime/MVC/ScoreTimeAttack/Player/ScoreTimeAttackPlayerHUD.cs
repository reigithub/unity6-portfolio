using DG.Tweening;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Client.MasterData;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.ScoreTimeAttack.Player
{
    /// <summary>
    /// 簡易的なプレイヤーHUD
    /// MessagePipeを通じてイベントを受信する
    /// </summary>
    public class ScoreTimeAttackPlayerHUD : MonoBehaviour
    {
        [SerializeField] private CanvasGroup _uiCanvasGroup;

        [SerializeField] private Slider _hpGauge;

        [SerializeField] private TextMeshProUGUI _currentHp;
        [SerializeField] private TextMeshProUGUI _maxHp;

        [SerializeField] private Slider _staminaGauge;

        [SerializeField] private TextMeshProUGUI _currentStamina;
        [SerializeField] private TextMeshProUGUI _maxStamina;

        private MessagePipeService _messagePipeService;
        private MessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private readonly ReactiveProperty<int> _currentHpValue = new();
        private float _maxHpValue = 100;

        private readonly ReactiveProperty<float> _currentStaminaValue = new();
        private float _maxStaminaValue = 100f;
        private float _staminaDepleteRate = 10f;
        private float _staminaRegenRate = 5f;
        private bool _isRunning;

        private void Awake()
        {
            _uiCanvasGroup.alpha = 0f;
        }

        public void Initialize(ScoreTimeAttackPlayerMaster playerMaster)
        {
            // UI更新のサブスクリプション
            _currentHpValue.DistinctUntilChanged()
                .Subscribe(x =>
                {
                    _currentHp.text = x.ToString();
                    _hpGauge.value = x / _maxHpValue;
                }).AddTo(this);

            _currentStaminaValue.DistinctUntilChanged()
                .Subscribe(x =>
                {
                    _currentStamina.text = x.ToString("0");
                    _staminaGauge.value = x / _maxStaminaValue;
                    // スタミナ変更をPublish
                    MessagePipeService.Publish(MessageKey.Player.StaminaChanged, x);
                }).AddTo(this);

            // マスターデータから初期値設定
            _maxHpValue = playerMaster.MaxHp;
            _maxStaminaValue = playerMaster.MaxStamina;
            _staminaDepleteRate = playerMaster.StaminaDepleteRate;
            _staminaRegenRate = playerMaster.StaminaRegenRate;

            _currentHpValue.Value = playerMaster.MaxHp;
            _maxHp.text = playerMaster.MaxHp.ToString();

            _currentStaminaValue.Value = playerMaster.MaxStamina;
            _maxStamina.text = playerMaster.MaxStamina.ToString();

            // MessagePipeイベントの購読
            SubscribeToMessagePipe();
        }

        private void SubscribeToMessagePipe()
        {
            // HUDフェードイン
            MessagePipeService.Subscribe(MessageKey.Player.HudFadeIn, DoFadeIn)
                .AddTo(this);

            // HUDフェードアウト
            MessagePipeService.Subscribe(MessageKey.Player.HudFadeOut, DoFadeOut)
                .AddTo(this);

            // HP変更
            MessagePipeService.Subscribe<int>(MessageKey.Player.HpChanged, hp => _currentHpValue.Value = hp)
                .AddTo(this);

            // 走行状態変更
            MessagePipeService.Subscribe<bool>(MessageKey.Player.Running, isRunning => _isRunning = isRunning)
                .AddTo(this);
        }

        private void Update()
        {
            if (_isRunning)
            {
                var nextStamina = _currentStaminaValue.Value - _staminaDepleteRate * Time.deltaTime;
                _currentStaminaValue.Value = Mathf.Clamp(nextStamina, 0f, _maxStaminaValue);
            }
            else
            {
                var nextStamina = _currentStaminaValue.Value + _staminaRegenRate * Time.deltaTime;
                _currentStaminaValue.Value = Mathf.Clamp(nextStamina, 0f, _maxStaminaValue);
            }
        }

        private void DoFadeIn()
        {
            _uiCanvasGroup.DOFade(1f, 0.25f);
        }

        private void DoFadeOut()
        {
            _uiCanvasGroup.DOFade(0f, 0.25f);
        }
    }
}