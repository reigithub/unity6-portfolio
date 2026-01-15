using System;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.MVP.Survivor.Signals;
using MessagePipe;
using R3;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Survivorプレイヤーコントローラー
    /// マスターデータから初期化される上から見下ろし型の移動制御（StateMachine使用）
    /// </summary>
    public partial class SurvivorPlayerController : MonoBehaviour
    {
        // VContainer Injection
        [Inject] private IPublisher<SurvivorSignals.Player.Spawned> _spawnedPublisher;

        [Header("Movement")]
        [SerializeField] private float _rotationSpeed = 10f;

        [Header("Components")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Animator _animator;

        // マスターデータから設定される値
        private int _maxHp = 100;
        private float _moveSpeed = 5f;
        private float _pickupRange = 2f;
        private float _invincibilityDuration = 0.5f;

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
        private Vector3 _moveDirection;
        private float _invincibilityTimer;

        // Animator hashes
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsMovingHash = Animator.StringToHash("IsMoving");

        private void Awake()
        {
            if (_characterController == null)
            {
                _characterController = GetComponent<CharacterController>();
            }

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        /// <summary>
        /// マスターデータから初期化
        /// </summary>
        public void Initialize(SurvivorPlayerMaster master)
        {
            _maxHp = master.MaxHp;
            _moveSpeed = master.MoveSpeed;
            _pickupRange = master.PickupRange;

            _currentHp.Value = _maxHp;
            _isInvincible.Value = false;
            _invincibilityTimer = 0f;

            InitializeStateMachine();

            // プレイヤースポーンシグナルを発行（カメラフォロー等に使用）
            _spawnedPublisher?.Publish(new SurvivorSignals.Player.Spawned(transform));
        }

        private void Update()
        {
            _stateMachine?.Update();
        }

        private void HandleInput()
        {
            // WASD or Arrow keys input
            float horizontal = Input.GetAxisRaw("Horizontal");
            float vertical = Input.GetAxisRaw("Vertical");

            _moveDirection = new Vector3(horizontal, 0f, vertical).normalized;
        }

        private void HandleMovement()
        {
            if (_characterController == null) return;

            // Move
            if (_moveDirection.magnitude > 0.1f)
            {
                Vector3 velocity = _moveDirection * _moveSpeed;

                // Apply gravity
                if (!_characterController.isGrounded)
                {
                    velocity.y = -9.81f;
                }

                _characterController.Move(velocity * Time.deltaTime);

                // Rotate towards movement direction
                Quaternion targetRotation = Quaternion.LookRotation(_moveDirection);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _rotationSpeed * Time.deltaTime);
            }

            // Update animator
            if (_animator != null)
            {
                float speed = _moveDirection.magnitude;
                _animator.SetFloat(SpeedHash, speed);
                _animator.SetBool(IsMovingHash, speed > 0.1f);
            }
        }

        public void TakeDamage(int damage)
        {
            TakeDamageWithStateMachine(damage);
        }

        public void Heal(int amount)
        {
            _currentHp.Value = Mathf.Min(_maxHp, _currentHp.Value + amount);
        }

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
                var orb = other.GetComponent<Item.ExperienceOrb>();
                if (orb != null)
                {
                    orb.Collect();
                }
            }
        }

        private void OnDestroy()
        {
            _currentHp.Dispose();
            _isInvincible.Dispose();
            _onDamaged.Dispose();
            _onDeath.Dispose();
        }
    }
}
