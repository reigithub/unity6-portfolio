using Game.Client.MasterData;
using Game.MVP.Core.DI;
using Game.Shared.Item;
using Game.Shared;
using Game.Shared.Combat;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
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
    /// SDUnityChanPlayerControllerをベースにしたRigidbody + RaycastCheckerベースの移動制御
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(RaycastChecker))]
    public partial class SurvivorPlayerController : MonoBehaviour, IDamageable, IPlayerMovementHandler
    {
        // Profiler markers
        private static readonly ProfilerMarker s_attractItemsMarker = new("ProfilerMarker.Player.AttractItems");
        private static readonly ProfilerMarker s_safeMovementMarker = new("ProfilerMarker.Player.SafeMovement");

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

        [Header("振り向き補間比率")]
        [SerializeField]
        private float _rotationRatio = 10.0f;

        [Inject] private readonly IGameRootController _gameRootController;
        [Inject] private readonly IInputService _inputService;

        // Components
        private Rigidbody _rigidbody;
        private RaycastChecker _groundedRaycastChecker;
        private CapsuleCollider _capsuleCollider;

        // Sweep-based移動用の定数
        private const float SkinWidth = 0.01f; // 壁との最小距離
        private const float StepHeight = 0.3f; // この高さ以下の障害物は乗り越え可能

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

        #region MonoBehaviour Methods

        private void Awake()
        {
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _groundedRaycastChecker);
            TryGetComponent(out _capsuleCollider);
        }

        private void Update()
        {
            // FusionPlayer がまだバインドされていなければポーリングで取得
            if (_fusionPlayer == null && _runnerService != null)
            {
                if (_runnerService.TryGet<SurvivorFusionPlayer>(out var fp))
                    BindFusionPlayer(fp);
            }

            UpdateItemAttraction();
        }

        private void OnDestroy()
        {
            if (_fusionPlayer != null)
            {
                _fusionPlayer.InputGatherer = null;
                _fusionPlayer.MovementHandler = null;
                _fusionPlayer.InterpolationTarget = null;
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

            // 移動ハンドラ + 補間ターゲットを設定
            fusionPlayer.MovementHandler = this;
            fusionPlayer.InterpolationTarget = transform;

            // InputAuthority プレイヤー: Fusion OnInput 用の入力収集デリゲートを設定
            if (fusionPlayer.HasInputAuthority)
            {
                fusionPlayer.InputGatherer = () => new PlayerNetworkInput
                {
                    Move = _inputService.Player.Move.ReadValue<UnityEngine.Vector2>(),
                    IsSprinting = _inputService.Player.LeftShift.IsPressed(),
                    CameraRotationY = _mainCamera != null ? _mainCamera.eulerAngles.y : 0f
                };
            }

            Debug.Log($"[SurvivorPlayerController] Bound (InputAuth={fusionPlayer.HasInputAuthority})");
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

            // プレイヤースポーンシグナルを発行（カメラフォロー等に使用）
            _spawnedPublisher?.Publish(new SurvivorSignals.Player.Spawned(transform));
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
        public PlayerPhysicsSnapshot ProcessTick(PlayerNetworkInput input, float deltaTime)
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
            }

            return new PlayerPhysicsSnapshot
            {
                Position = transform.position,
                RotationY = transform.eulerAngles.y,
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
            return _groundedRaycastChecker.Check();
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
                        // アイテムに吸引開始を通知（ターゲットと速度を渡す）
                        collectible.StartAttraction(transform, _itemAttractSpeed);
                    }
                }
            }
        }

        #endregion

        #region Movement

        private void ExecuteMovement(PlayerNetworkInput input, float deltaTime)
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

            var desiredMovement = _moveVector * _speed.Value * deltaTime;
            var safeMovement = CalculateSafeMovement(desiredMovement);
            _rigidbody.MovePosition(_rigidbody.position + safeMovement);

            if (isMoveInput)
            {
                _rigidbody.MoveRotation(
                    Quaternion.Slerp(_rigidbody.rotation, _lookRotation, _rotationRatio * deltaTime));
            }
        }

        /// <summary>
        /// CapsuleCastで衝突チェックを行い、安全な移動量を計算
        /// StepHeight以下の障害物は無視して乗り越え可能
        /// </summary>
        private Vector3 CalculateSafeMovement(Vector3 desiredMovement)
        {
            using (s_safeMovementMarker.Auto())
            {
                if (_capsuleCollider == null || desiredMovement.sqrMagnitude < 0.0001f)
                {
                    return desiredMovement;
                }

                var moveDistance = desiredMovement.magnitude;
                var moveDirection = desiredMovement.normalized;

                // CapsuleColliderの上下端点を計算
                // point2をStepHeight分上げることで、低い障害物を無視
                var center = _rigidbody.position + _capsuleCollider.center;
                var halfHeight = Mathf.Max(0f, _capsuleCollider.height * 0.5f - _capsuleCollider.radius);
                var point1 = center + Vector3.up * halfHeight;
                // StepHeightより上の位置から判定開始（低い障害物は無視）
                var point2Bottom = center - Vector3.up * halfHeight;
                var point2 = new Vector3(point2Bottom.x, _rigidbody.position.y + StepHeight + _capsuleCollider.radius, point2Bottom.z);

                // point2がpoint1より上になってしまう場合は補正
                if (point2.y > point1.y)
                {
                    point2 = point1;
                }

                // CapsuleCastで衝突チェック（Enemyレイヤーを除外して構造物のみ判定）
                var obstacleLayerMask = Physics.DefaultRaycastLayers & ~LayerMaskConstants.Enemy;
                if (Physics.CapsuleCast(
                        point1, point2,
                        _capsuleCollider.radius,
                        moveDirection,
                        out var hit,
                        moveDistance + SkinWidth,
                        obstacleLayerMask,
                        QueryTriggerInteraction.Ignore))
                {
                    // 衝突した場合、衝突点の手前までの移動に制限
                    var safeDistance = Mathf.Max(0f, hit.distance - SkinWidth);
                    return moveDirection * safeDistance;
                }

                return desiredMovement;
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

        #region Collision

        private void OnTriggerEnter(Collider other)
        {
            if (_fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;

            // アイテムとの衝突
            if (other.CompareLayer(LayerConstants.Item))
            {
                if (other.TryGetComponent<ICollectible>(out var collectible))
                {
                    collectible.Collect();
                }
            }
        }

        #endregion
    }
}
