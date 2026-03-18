using Fusion.Addons.KCC;
using Game.Client.MasterData;
using Game.MVP.Core.DI;
using Game.MVP.Survivor.Item;
using Game.Shared.Item;
using Game.Shared;
using Game.Shared.Combat;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using R3;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Survivorプレイヤーコントローラー
    /// KCC（Advanced Kinematic Character Controller）ベースの移動制御
    /// </summary>
    public partial class SurvivorPlayerController : MonoBehaviour, IDamageable, ISurvivorPlayerMovementHandler
    {
        // Profiler markers
        private static readonly ProfilerMarker s_attractItemsMarker = new("ProfilerMarker.Player.AttractItems");

        // VContainer Injection
        [Inject] private IPublisher<SurvivorSignals.Player.Spawned> _spawnedPublisher;
        [Inject] private IFusionRunnerService _runnerService;

        private readonly Subject<SurvivorSignals.Player.DamageReceived> _onDamageReceived = new();
        private readonly Subject<SurvivorSignals.Player.Died> _onDied = new();
        public Observable<SurvivorSignals.Player.DamageReceived> OnDamageReceived => _onDamageReceived;
        public Observable<SurvivorSignals.Player.Died> OnDied => _onDied;

        [Header("ジョギング速度")]
        [SerializeField]
        private float _jogSpeed = 5.0f;

        [Header("走る速度")]
        [SerializeField]
        private float _runSpeed = 8.0f;

        [Header("振り向き速度 (degrees/sec)")]
        [SerializeField]
        private float _rotationSpeed = 600f;

        [Inject] private readonly IGameRootController _gameRootController;
        [Inject] private readonly IInputService _inputService;

        // Components
        private KCC _kcc;

        // マスターデータから設定される値
        private int _maxHp = 100;
        private int _maxStamina = 100;
        private int _staminaDepleteRate = 10;
        private int _staminaRegenRate = 5;
        private float _staminaAccumulator = 0f; // スタミナ変化の端数を蓄積
        private float _pickupRange = 2f;
        private float _invincibilityDuration = 0.5f;
        private float _itemAttractDistance = 5f;
        private float _itemAttractSpeed = 10f;
        private float _itemCollectDistance = 1f;

        // 入力関連
        private Transform _mainCamera;
        private Vector3 _moveVector;
        private readonly ReactiveProperty<float> _speed = new();
        private Quaternion _lookRotation = Quaternion.identity;

        // Reactive Properties
        private readonly ReactiveProperty<int> _currentHp = new();
        private readonly ReactiveProperty<int> _currentStamina = new();
        private readonly ReactiveProperty<bool> _isInvincible = new();

        public ReadOnlyReactiveProperty<int> CurrentHp => _currentHp;
        public ReadOnlyReactiveProperty<int> CurrentStamina => _currentStamina;
        public ReadOnlyReactiveProperty<bool> IsInvincible => _isInvincible;
        public int MaxHp => _maxHp;
        public int MaxStamina => _maxStamina;
        public float PickupRange => _pickupRange;
        public float ItemAttractDistance => _itemAttractDistance;
        public float ItemAttractSpeed => _itemAttractSpeed;
        public float ItemCollectDistance => _itemCollectDistance;
        public ReadOnlyReactiveProperty<float> Speed => _speed;

        // IDamageable
        public bool IsDead => _currentHp.Value <= 0;

        // State
        private float _invincibilityTimer;

        // アイテム吸引用
        private readonly Collider[] _itemHitBuffer = new Collider[50];
        private const float ItemCheckInterval = 0.1f;
        private float _itemCheckTimer;

        // ネットワーク同期用
        private SurvivorFusionPlayer _fusionPlayer;
        public SurvivorFusionPlayer FusionPlayer => _fusionPlayer;
        private float _networkDeltaTime;

        // 入力蓄積（ExpertMovement パターン: フレームレート差による入力の不均一を補正）
        private Vector2 _accumulatedMoveDirection;
        private float _accumulatedMoveDirectionSize;
        private float _lastCameraRotationY;

        #region MonoBehaviour Methods

        private void Awake()
        {
            TryGetComponent(out _kcc);
        }

        private void Update()
        {
            AccumulateRenderInput();
            UpdateItemAttraction();
        }

        private void OnDestroy()
        {
            if (_fusionPlayer != null)
            {
                _fusionPlayer.InputGatherer = null;
                _fusionPlayer.MovementHandler = null;
                _fusionPlayer = null;
            }

            _speed.Dispose();
            _currentHp.Dispose();
            _currentStamina.Dispose();
            _isInvincible.Dispose();
            _onDamageReceived.Dispose();
            _onDied.Dispose();
        }

        #endregion

        #region Network

        /// <summary>
        /// Fusion 2: SurvivorFusionPlayer をバインド
        /// </summary>
        public void BindFusionPlayer(SurvivorFusionPlayer fusionPlayer)
        {
            _fusionPlayer = fusionPlayer;

            // 移動ハンドラを設定
            fusionPlayer.MovementHandler = this;

            // InputAuthority プレイヤー: Fusion OnInput 用の入力収集デリゲートを設定
            // 蓄積された移動入力を時間加重平均で返す（ExpertMovement パターン）
            if (fusionPlayer.HasInputAuthority)
            {
                fusionPlayer.InputGatherer = () =>
                {
                    var input = new SurvivorPlayerNetworkInput
                    {
                        Move = _accumulatedMoveDirectionSize > 0f
                            ? _accumulatedMoveDirection / _accumulatedMoveDirectionSize
                            : Vector2.zero,
                        IsSprinting = _inputService.Player.LeftShift.IsPressed(),
                        CameraRotationY = _lastCameraRotationY
                    };

                    // 蓄積リセット
                    _accumulatedMoveDirection = Vector2.zero;
                    _accumulatedMoveDirectionSize = 0f;

                    return input;
                };
            }

        }

        #endregion

        #region Initialize

        /// <summary>
        /// マスターデータから初期化
        /// </summary>
        public void Initialize(SurvivorPlayerLevelMaster levelMaster)
        {
            _maxHp = levelMaster.MaxHp;
            _maxStamina = levelMaster.MaxStamina;
            _staminaDepleteRate = levelMaster.StaminaDepleteRate;
            _staminaRegenRate = levelMaster.StaminaRegenRate;
            _jogSpeed = levelMaster.MoveSpeed.ToUnit();
            _runSpeed = levelMaster.RunSpeed.ToUnit();
            _pickupRange = levelMaster.PickupRange.ToUnit();
            _invincibilityDuration = levelMaster.InvincibilityDuration.ToSeconds();
            _itemAttractDistance = levelMaster.ItemAttractDistance.ToUnit();
            _itemAttractSpeed = levelMaster.ItemAttractSpeed.ToUnit();
            _itemCollectDistance = levelMaster.ItemCollectDistance.ToUnit();

            _currentHp.Value = _maxHp;
            _currentStamina.Value = _maxStamina;
            _isInvincible.Value = false;
            _invincibilityTimer = 0f;

            // メインカメラを自動取得（サーバーでは _gameRootController が null）
            if (_mainCamera == null)
            {
                _mainCamera = _gameRootController?.MainCamera?.transform;
            }

            // ステートマシン初期化
            InitializeStateMachine();

            // プレイヤースポーンシグナルは SurvivorPlayerStart.LoadPlayerAsync から発行
            // （InterpolationTarget をカメラ追従先にするため）
        }

        /// <summary>
        /// レベルアップ時にステータスを更新
        /// </summary>
        public void UpdateLevelStats(SurvivorPlayerLevelMaster levelMaster)
        {
            var previousMaxHp = _maxHp;
            var previousMaxStamina = _maxStamina;

            _maxHp = levelMaster.MaxHp;
            _maxStamina = levelMaster.MaxStamina;
            _staminaDepleteRate = levelMaster.StaminaDepleteRate;
            _staminaRegenRate = levelMaster.StaminaRegenRate;
            _jogSpeed = levelMaster.MoveSpeed.ToUnit();
            _runSpeed = levelMaster.RunSpeed.ToUnit();
            _pickupRange = levelMaster.PickupRange.ToUnit();
            _invincibilityDuration = levelMaster.InvincibilityDuration.ToSeconds();
            _itemAttractDistance = levelMaster.ItemAttractDistance.ToUnit();
            _itemAttractSpeed = levelMaster.ItemAttractSpeed.ToUnit();
            _itemCollectDistance = levelMaster.ItemCollectDistance.ToUnit();

            // レベルアップ時のHP増加（差分を回復）
            if (_maxHp > previousMaxHp)
            {
                var hpIncrease = _maxHp - previousMaxHp;
                _currentHp.Value = Mathf.Min(_currentHp.Value + hpIncrease, _maxHp);
            }

            // レベルアップ時のスタミナ増加（差分を回復）
            if (_maxStamina > previousMaxStamina)
            {
                var staminaIncrease = _maxStamina - previousMaxStamina;
                _currentStamina.Value = Mathf.Min(_currentStamina.Value + staminaIncrease, _maxStamina);
            }
        }

        /// <summary>
        /// カメラ参照を設定（カメラ相対移動用）
        /// </summary>
        public void SetMainCamera(Transform mainCamera)
        {
            _mainCamera = mainCamera;
        }

        #endregion

        #region Input / ProcessTick

        /// <summary>
        /// IPlayerMovementHandler: Fusion tick ごとに FusionPlayer から呼ばれる。
        /// </summary>
        public SurvivorPlayerPhysicsSnapshot ProcessTick(SurvivorPlayerNetworkInput input, float deltaTime)
        {
            _networkDeltaTime = deltaTime;

            var moveValue = input.Move;
            var isMoveInput = moveValue.magnitude > 0.1f;
            var wantToRun = input.IsSprinting && isMoveInput;
            var isRunning = wantToRun && _currentStamina.Value > 0;
            _speed.Value = (isMoveInput ? 1f : 0f) * (isRunning ? _runSpeed : _jogSpeed);

            UpdateStamina(isRunning, deltaTime);

            _stateMachine?.Update();

            if (!IsDead)
            {
                ExecuteMovement(input, deltaTime);
                CollectItemsFromKCCHits();
            }

            return new SurvivorPlayerPhysicsSnapshot
            {
                Speed = _speed.Value,
                Health = _currentHp.Value,
                MaxHealth = _maxHp,
                Stamina = _currentStamina.Value,
                MaxStamina = _maxStamina,
                IsInvincible = _isInvincible.Value
            };
        }

        private void UpdateStamina(bool isRunning, float deltaTime)
        {
            if (isRunning)
            {
                _staminaAccumulator -= _staminaDepleteRate * deltaTime;
            }
            else
            {
                _staminaAccumulator += _staminaRegenRate * deltaTime;
            }

            if (_staminaAccumulator >= 1f)
            {
                var regenAmount = Mathf.FloorToInt(_staminaAccumulator);
                _staminaAccumulator -= regenAmount;
                _currentStamina.Value = Mathf.Min(_maxStamina, _currentStamina.Value + regenAmount);
            }
            else if (_staminaAccumulator <= -1f)
            {
                var depleteAmount = Mathf.FloorToInt(-_staminaAccumulator);
                _staminaAccumulator += depleteAmount;
                _currentStamina.Value = Mathf.Max(0, _currentStamina.Value - depleteAmount);
            }
        }

        public bool IsMoving()
        {
            return _speed.Value > 0f;
        }

        public bool IsGrounded()
        {
            return _kcc != null && _kcc.FixedData.IsGrounded;
        }

        #endregion

        #region Item Attraction

        /// <summary>
        /// 範囲内のアイテムを検知して吸引を開始する
        /// </summary>
        private void UpdateItemAttraction()
        {
            using (s_attractItemsMarker.Auto())
            {
                if (_fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;
                _itemCheckTimer -= Time.deltaTime;
                if (_itemCheckTimer > 0f) return;
                _itemCheckTimer = ItemCheckInterval;

                // ItemレイヤーのみをOverlapSphereで検索
                int hitCount = Physics.OverlapSphereNonAlloc(
                    transform.position,
                    _itemAttractDistance,
                    _itemHitBuffer,
                    LayerMaskConstants.Item
                );

                for (int i = 0; i < hitCount; i++)
                {
                    if (_itemHitBuffer[i].TryGetComponent<ICollectible>(out var collectible) && !collectible.IsCollected)
                    {
                        // 吸引開始（収集は KCCData.Hits または ItemProxyCollectible.Update の到達判定で行う）
                        collectible.StartAttraction(transform, _itemAttractSpeed);

                        // プロキシアイテムに収集距離を伝達（毎フレームの到達判定用）
                        if (_itemHitBuffer[i].TryGetComponent<ItemProxyCollectible>(out var proxy))
                        {
                            proxy.CollectDistance = _itemCollectDistance;
                        }
                    }
                }
            }
        }

        #endregion

        #region Input Accumulation

        /// <summary>
        /// 毎 Update で呼び出し、移動入力を時間加重で蓄積する。
        /// Fusion の OnInput で蓄積値の平均を返すことで、フレームレート差による入力の不均一を補正。
        /// </summary>
        private void AccumulateRenderInput()
        {
            if (_fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;

            var move = _inputService.Player.Move.ReadValue<Vector2>();
            var dt = Time.unscaledDeltaTime;

            _accumulatedMoveDirection += move * dt;
            _accumulatedMoveDirectionSize += dt;
            _lastCameraRotationY = _mainCamera != null ? _mainCamera.eulerAngles.y : 0f;
        }

        /// <summary>
        /// Render フレームの入力予測処理。現在の入力を KCC に設定してレンダー予測を可能にする。
        /// SurvivorFusionPlayer.Render() から ISurvivorPlayerMovementHandler 経由で呼ばれる。
        /// </summary>
        public void ProcessRenderInput(KCC kcc)
        {
            if (_fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;

            var move = _inputService.Player.Move.ReadValue<Vector2>();
            var isMoveInput = move.magnitude > 0.1f;
            var wantToRun = _inputService.Player.LeftShift.IsPressed() && isMoveInput;
            var isRunning = wantToRun && _currentStamina.Value > 0;
            var speed = (isMoveInput ? 1f : 0f) * (isRunning ? _runSpeed : _jogSpeed);

            var cameraRotY = _mainCamera != null ? _mainCamera.eulerAngles.y : 0f;
            var cameraRot = Quaternion.Euler(0f, cameraRotY, 0f);
            var moveVector = isMoveInput
                ? (cameraRot * Vector3.forward * move.y + cameraRot * Vector3.right * move.x).normalized
                : Vector3.zero;

            kcc.SetInputDirection(moveVector);
            kcc.SetSpeed(speed);

            if (isMoveInput)
            {
                var targetYaw = Quaternion.LookRotation(moveVector).eulerAngles.y;
                var deltaYaw = Mathf.DeltaAngle(kcc.RenderData.LookYaw, targetYaw);
                var maxDelta = _rotationSpeed * Time.deltaTime;
                var clampedDelta = Mathf.Clamp(deltaYaw, -maxDelta, maxDelta);
                kcc.AddLookRotation(0f, clampedDelta);
            }
        }

        #endregion

        #region Movement

        private void ExecuteMovement(SurvivorPlayerNetworkInput input, float deltaTime)
        {
            var moveValue = input.Move;
            var isMoveInput = moveValue.magnitude > 0.1f;

            if (isMoveInput)
            {
                var cameraRot = Quaternion.Euler(0f, input.CameraRotationY, 0f);
                var forward = cameraRot * Vector3.forward;
                var right = cameraRot * Vector3.right;
                _moveVector = (forward * moveValue.y + right * moveValue.x).normalized;
                _lookRotation = Quaternion.LookRotation(_moveVector);
            }
            else
            {
                _moveVector = Vector3.zero;
            }

            // KCC に移動方向と速度を設定
            _kcc.SetInputDirection(_moveVector);
            _kcc.SetSpeed(_speed.Value);

            // 回転: KCC.AddLookRotation で最大速度制限付き差分回転
            // KCC がティック間の補間/予測を内部処理する
            if (isMoveInput)
            {
                var targetYaw = _lookRotation.eulerAngles.y;
                var deltaYaw = Mathf.DeltaAngle(_kcc.FixedData.LookYaw, targetYaw);
                var maxDelta = _rotationSpeed * deltaTime;
                var clampedDelta = Mathf.Clamp(deltaYaw, -maxDelta, maxDelta);
                _kcc.AddLookRotation(0f, clampedDelta);
            }
        }

        #endregion

        #region Damage / Heal

        public void TakeDamage(int damage)
        {
            TakeDamageWithStateMachine(damage);
        }

        public void Heal(int amount)
        {
            _currentHp.Value = Mathf.Min(_maxHp, _currentHp.Value + amount);
        }

        /// <summary>
        /// 現在HPを設定（モデルとの同期用）
        /// </summary>
        public void SetCurrentHp(int value)
        {
            _currentHp.Value = Mathf.Clamp(value, 0, _maxHp);
        }

        #endregion

        #region KCC Item Collection

        private static int _kccHitCollectLogCount;

        /// <summary>
        /// KCCData.Hits からアイテム収集。ProcessTick の ExecuteMovement 後に呼ぶ。
        /// KCC カプセル内のアイテムを直接収集する（OnTriggerEnter 非使用）。
        /// </summary>
        private void CollectItemsFromKCCHits()
        {
            if (_kcc == null || _fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;

            foreach (var hit in _kcc.FixedData.Hits.All)
            {
                if (hit.Collider != null
                    && hit.Collider.gameObject.layer == LayerConstants.Item
                    && hit.Collider.TryGetComponent<ICollectible>(out var collectible)
                    && !collectible.IsCollected)
                {
                    if (_kccHitCollectLogCount < 10)
                    {
                        _kccHitCollectLogCount++;
                        Debug.Log($"[KCCHits.Collect#{_kccHitCollectLogCount}] item={hit.Collider.name}");
                    }

                    collectible.Collect();
                }
            }
        }

        #endregion
    }
}
