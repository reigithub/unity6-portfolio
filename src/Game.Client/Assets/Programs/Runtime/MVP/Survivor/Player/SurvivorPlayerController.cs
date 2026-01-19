using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Signals;
using Game.Shared;
using Game.Shared.Input;
using MessagePipe;
using R3;
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
    public partial class SurvivorPlayerController : MonoBehaviour
    {
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

        // InputSystem
        private ProjectDefaultInputSystem _inputSystem;
        private ProjectDefaultInputSystem.PlayerActions _player;

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
        private float _pickupRange = 2f;
        private float _invincibilityDuration = 0.5f;

        // 入力関連
        private Transform _mainCamera;
        private Vector2 _moveValue;
        private Vector3 _moveVector;
        private readonly ReactiveProperty<float> _speed = new();
        private Quaternion _lookRotation = Quaternion.identity;

        // Reactive Properties
        private readonly ReactiveProperty<int> _currentHp = new();
        private readonly ReactiveProperty<bool> _isInvincible = new();

        public ReadOnlyReactiveProperty<int> CurrentHp => _currentHp;
        public ReadOnlyReactiveProperty<bool> IsInvincible => _isInvincible;
        public int MaxHp => _maxHp;
        public float PickupRange => _pickupRange;

        // Events
        private readonly Subject<int> _onDamaged = new();
        private readonly Subject<Unit> _onDeath = new();

        public Observable<int> OnDamaged => _onDamaged;
        public Observable<Unit> OnDeath => _onDeath;

        // State
        private float _invincibilityTimer;

        // アニメータハッシュ
        private static readonly int AnimatorHashSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimatorHashDeath = Animator.StringToHash("Death");

        #region MonoBehaviour Methods

        private void Awake()
        {
            _inputSystem = new ProjectDefaultInputSystem();
            _player = _inputSystem.Player;

            TryGetComponent(out _animator);
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _groundedRaycastChecker);
            TryGetComponent(out _capsuleCollider);
        }

        private void OnEnable()
        {
            _inputSystem.Enable();
            _player.Enable();
        }

        private void OnDisable()
        {
            _inputSystem.Disable();
            _player.Disable();
        }

        private void Update()
        {
            UpdateInput();
            _stateMachine?.Update();
        }

        private void FixedUpdate()
        {
            _stateMachine?.FixedUpdate();
        }

        private void OnDestroy()
        {
            _inputSystem?.Dispose();
            _speed.Dispose();
            _currentHp.Dispose();
            _isInvincible.Dispose();
            _onDamaged.Dispose();
            _onDeath.Dispose();
        }

        #endregion

        #region Initialize

        /// <summary>
        /// マスターデータから初期化\
        /// </summary>
        public void Initialize(SurvivorPlayerMaster master)
        {
            _maxHp = master.MaxHp;
            _jogSpeed = master.MoveSpeed;
            _runSpeed = master.MoveSpeed * 1.5f; // ダッシュは1.5倍速
            _pickupRange = master.PickupRange;

            _currentHp.Value = _maxHp;
            _isInvincible.Value = false;
            _invincibilityTimer = 0f;

            // メインカメラを自動取得
            if (_mainCamera == null && Camera.main != null)
            {
                _mainCamera = Camera.main.transform;
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
            // 移動入力受付
            _moveValue = _player.Move.ReadValue<Vector2>();
            _moveVector = new Vector3(_moveValue.x, 0.0f, _moveValue.y).normalized;

            // 移動速度更新（ダッシュ対応）
            _speed.Value = _moveVector.magnitude * (_player.LeftShift.IsPressed() ? _runSpeed : _jogSpeed);

            // 回転入力受付
            if (IsMoveInput())
            {
                _lookRotation = Quaternion.LookRotation(_moveVector);
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

            // CapsuleCastで衝突チェック
            if (Physics.CapsuleCast(
                point1, point2,
                _capsuleCollider.radius,
                moveDirection,
                out var hit,
                moveDistance + SkinWidth,
                Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
            {
                // 衝突した場合、衝突点の手前までの移動に制限
                var safeDistance = Mathf.Max(0f, hit.distance - SkinWidth);
                return moveDirection * safeDistance;
            }

            return desiredMovement;
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

        #endregion

        #region Collision

        private void OnTriggerEnter(Collider other)
        {
            // 敵との衝突判定
            if (other.CompareTag("Enemy"))
            {
                var enemy = other.GetComponent<Enemy.SurvivorEnemyController>();
                if (enemy != null)
                {
                    TakeDamage(enemy.AttackDamage);
                }
            }

            // 経験値オーブとの衝突
            if (other.CompareTag("ExperienceOrb"))
            {
                var orb = other.GetComponent<Item.SurvivorExperienceOrb>();
                if (orb != null)
                {
                    orb.Collect();
                }
            }
        }

        #endregion
    }
}