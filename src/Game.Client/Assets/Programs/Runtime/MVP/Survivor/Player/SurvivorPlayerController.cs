using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Core.DI;
using Game.MVP.Survivor.Signals;
using Game.Shared.Item;
using Game.Shared;
using Game.Shared.Combat;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Services;
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
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(RaycastChecker))]
    public partial class SurvivorPlayerController : MonoBehaviour, IDamageable
    {
        // Profiler markers
        private static readonly ProfilerMarker s_updateInputMarker = new("ProfilerMarker.Player.UpdateInput");
        private static readonly ProfilerMarker s_attractItemsMarker = new("ProfilerMarker.Player.AttractItems");
        private static readonly ProfilerMarker s_safeMovementMarker = new("ProfilerMarker.Player.SafeMovement");

        // VContainer Injection
        [Inject] private IPublisher<SurvivorSignals.Player.Spawned> _spawnedPublisher;

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
        private Animator _animator;
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
        private Vector2 _moveValue;
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

        // IDamageable
        public bool IsDead => _currentHp.Value <= 0;

        // Events
        private readonly Subject<int> _onDamaged = new();
        private readonly Subject<Unit> _onDeath = new();

        public Observable<int> OnDamaged => _onDamaged;
        public Observable<Unit> OnDeath => _onDeath;

        // State
        private float _invincibilityTimer;

        // アイテム吸引用
        private readonly Collider[] _itemHitBuffer = new Collider[50];
        private const float ItemCheckInterval = 0.1f;
        private float _itemCheckTimer;

        // アニメータハッシュ
        private static readonly int AnimatorHashSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimatorHashDeath = Animator.StringToHash("Death");

        #region MonoBehaviour Methods

        private void Awake()
        {
            TryGetComponent(out _animator);
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _groundedRaycastChecker);
            TryGetComponent(out _capsuleCollider);
        }

        private void Update()
        {
            UpdateInput();
            UpdateItemAttraction();
            _stateMachine?.Update();
        }

        private void FixedUpdate()
        {
            _stateMachine?.FixedUpdate();
        }

        private void OnDestroy()
        {
            _speed.Dispose();
            _currentHp.Dispose();
            _currentStamina.Dispose();
            _isInvincible.Dispose();
            _onDamaged.Dispose();
            _onDeath.Dispose();
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

            // メインカメラを自動取得
            if (_mainCamera == null)
            {
                _mainCamera = _gameRootController.MainCamera.transform;
            }

            // スピードが変わった時だけアニメーターを更新
            _speed
                .DistinctUntilChanged()
                .Subscribe(speed => _animator.SetFloat(AnimatorHashSpeed, speed))
                .AddTo(this);

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

        #region Input

        private void UpdateInput()
        {
            using (s_updateInputMarker.Auto())
            {
                if (_inputService == null)
                    return;

                // 移動入力受付
                _moveValue = _inputService.Player.Move.ReadValue<Vector2>();
                _moveVector = new Vector3(_moveValue.x, 0.0f, _moveValue.y).normalized;

                // ダッシュ入力チェック
                var wantToRun = _inputService.Player.LeftShift.IsPressed() && IsMoveInput();
                var canRun = _currentStamina.Value > 0;

                // 移動速度更新（スタミナがある場合のみダッシュ可能）
                var isRunning = wantToRun && canRun;
                _speed.Value = _moveVector.magnitude * (isRunning ? _runSpeed : _jogSpeed);

                // スタミナ消費・回復（deltaTimeベース）
                UpdateStamina(isRunning);

                // 回転入力受付
                if (IsMoveInput())
                {
                    _lookRotation = Quaternion.LookRotation(_moveVector);
                }
            }
        }

        private void UpdateStamina(bool isRunning)
        {
            if (isRunning)
            {
                // ダッシュ中: スタミナ消費（1秒毎にStaminaDepleteRate分消費）
                _staminaAccumulator -= _staminaDepleteRate * Time.deltaTime;
            }
            else
            {
                // ダッシュしていない: スタミナ回復（1秒毎にStaminaRegenRate分回復）
                _staminaAccumulator += _staminaRegenRate * Time.deltaTime;
            }

            // 蓄積された変化を整数として適用
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

        private bool IsMoveInput()
        {
            return _moveValue.magnitude > 0.1f;
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

        #region Movement (Called from States)

        private void HandleMovement()
        {
            if (_mainCamera)
            {
                if (IsMoveInput())
                {
                    var forward = _mainCamera.forward;
                    var right = _mainCamera.right;
                    forward.y = 0f;
                    right.y = 0f;

                    _moveVector = forward * _moveValue.y + right * _moveValue.x;
                    _lookRotation = Quaternion.LookRotation(_moveVector);
                }
            }

            // Sweep-based移動: 移動前にCapsuleCastで衝突チェック
            var desiredMovement = _moveVector * _speed.Value * Time.fixedDeltaTime;
            var safeMovement = CalculateSafeMovement(desiredMovement);
            _rigidbody.MovePosition(_rigidbody.position + safeMovement);

            if (IsMoveInput())
            {
                _rigidbody.MoveRotation(
                    Quaternion.Slerp(_rigidbody.rotation, _lookRotation, _rotationRatio * Time.fixedDeltaTime));
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