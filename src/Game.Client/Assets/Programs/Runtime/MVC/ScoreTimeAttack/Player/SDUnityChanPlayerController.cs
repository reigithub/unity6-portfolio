using System.Linq;
using Cysharp.Threading.Tasks;
using Game.Shared;
using Game.Core.MessagePipe;
using Game.Core.Services;
using Game.Library.Shared.Enums;
using Game.Library.Shared.MasterData.MemoryTables;
using Game.Shared.Input;
using R3;
using R3.Triggers;
using UnityChan;
using UnityEngine;

namespace Game.ScoreTimeAttack.Player
{
    /// <summary>
    /// SD-Unityちゃん用のプレイヤーコントローラー
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(RaycastChecker))] // 着地判定に使用
    public class SDUnityChanPlayerController : MonoBehaviour
    {
        [Header("歩く速度")]
        [SerializeField]
        private float _walkSpeed = 2.0f;

        [Header("ジョギング速度")]
        [SerializeField]
        private float _jogSpeed = 5.0f;

        [Header("走る速度")]
        [SerializeField]
        private float _runSpeed = 8.0f;

        [Header("振り向き補間比率")]
        [SerializeField]
        private float _rotationRatio = 10.0f;

        [Header("ジャンプ力")]
        [SerializeField]
        private float _jump = 5.0f;

        private AudioService _audioService;
        private AudioService AudioService => _audioService ??= GameServiceManager.Get<AudioService>();

        private MessagePipeService _messagePipeService;
        private MessagePipeService MessagePipeService => _messagePipeService ??= GameServiceManager.Get<MessagePipeService>();

        private ProjectDefaultInputSystem _inputSystem;
        private ProjectDefaultInputSystem.PlayerActions _player;

        private Animator _animator;
        private Rigidbody _rigidbody;
        private RaycastChecker _groundedRaycastChecker;
        private CapsuleCollider _capsuleCollider;

        // Sweep-based移動用の定数
        private const float SkinWidth = 0.01f; // 壁との最小距離
        private const float StepHeight = 0.3f; // この高さ以下の障害物は乗り越え可能

        // ステートマシーン
        private StateMachine<SDUnityChanPlayerController, StateEvent> _stateMachine;

        // 入力関連
        private Transform _mainCamera;
        private Vector2 _moveValue;
        private Vector3 _moveVector;
        private readonly ReactiveProperty<float> _speed = new();
        private Quaternion _lookRotation = Quaternion.identity;
        private bool _jumpTriggered;

        // アニメーター状態フラグ
        private bool _isJumpingAnim;
        private bool _isDamagedAnim;
        private bool _isDownAnim;
        private bool _isGettingUpComplete;

        // アニメータハッシュ
        private readonly int _animatorHashJump = Animator.StringToHash("Jump");
        private readonly int _animatorHashSpeed = Animator.StringToHash("Speed");
        private readonly int _animatorHashDamaged = Animator.StringToHash("Damaged");

        public void Initialize(ScoreTimeAttackPlayerMaster playerMaster)
        {
            _walkSpeed = playerMaster.WalkSpeed;
            _jogSpeed = playerMaster.JogSpeed;
            _runSpeed = playerMaster.RunSpeed;
            _jump = playerMaster.Jump;

            TryGetComponent(out _animator);
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _groundedRaycastChecker);
            TryGetComponent(out _capsuleCollider);

            // ステートマシン初期化
            InitializeStateMachine();

            // アニメーター状態の監視
            var triggers = _animator.GetBehaviours<ObservableStateMachineTrigger>();
            triggers.Select(x => x.OnStateEnterAsObservable())
                .Merge()
                .Subscribe(info => OnAnimatorStateEnter(info.StateInfo))
                .AddTo(this);
            triggers.Select(x => x.OnStateExitAsObservable())
                .Merge()
                .Subscribe(info => OnAnimatorStateExit(info.StateInfo))
                .AddTo(this);

            // スピードが変わった時だけアニメーターを更新
            _speed
                .DistinctUntilChanged()
                .Subscribe(speed => _animator.SetFloat(_animatorHashSpeed, speed))
                .AddTo(this);
            // 走り始めた時のボイス再生 & Running状態をPublish
            _speed
                .DistinctUntilChangedBy(_ => IsRunning())
                .Subscribe(_ =>
                {
                    var isRunning = IsRunning();
                    if (isRunning) AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerRun).Forget();
                    MessagePipeService.Publish(MessageKey.Player.Running, isRunning);
                })
                .AddTo(this);

            // スタミナ変更の購読
            MessagePipeService.Subscribe<float>(MessageKey.Player.StaminaChanged, stamina => SetRunInput(stamina > 0f))
                .AddTo(this);
        }

        public void SetMainCamera(Transform mainCamera)
        {
            _mainCamera = mainCamera;
        }

        #region MonoBehaviour Methods

        private void Awake()
        {
            _inputSystem = new ProjectDefaultInputSystem();
            _player = _inputSystem.Player;
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

        private void OnDestroy()
        {
            _inputSystem.Dispose();
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

        #endregion

        #region Input

        private void UpdateInput()
        {
            // 移動入力受付
            _moveValue = _player.Move.ReadValue<Vector2>();
            _moveVector = new Vector3(_moveValue.x, 0.0f, _moveValue.y).normalized;

            // 移動速度更新
            _speed.Value = _moveVector.magnitude * (_player.LeftShift.IsPressed() ? _runSpeed : _jogSpeed);

            // 回転入力受付
            if (IsMoveInput())
            {
                _lookRotation = Quaternion.LookRotation(_moveVector);
            }

            // ジャンプ入力受付
            if (_player.Jump.WasPressedThisFrame() && CanJump())
            {
                _jumpTriggered = true;
            }
        }

        private bool CanJump()
        {
            if (!_stateMachine.IsProcessing())
                return false;

            // Idle/Moving状態でのみジャンプ可能
            var canJumpFromState = _stateMachine.IsCurrentState<IdleState>() ||
                                   _stateMachine.IsCurrentState<MovingState>();

            return canJumpFromState && IsGrounded();
        }

        private bool IsGrounded()
        {
            return _groundedRaycastChecker.Check();
        }

        public bool IsMoving()
        {
            return _speed.Value > 0f;
        }

        public bool IsWalking()
        {
            return _speed.Value >= _walkSpeed && _speed.Value < _jogSpeed;
        }

        public bool IsJogging()
        {
            return _speed.Value >= _jogSpeed && _speed.Value < _runSpeed;
        }

        public bool IsRunning()
        {
            return _speed.Value >= _runSpeed;
        }

        private bool IsMoveInput()
        {
            return _moveValue.magnitude > 0.1f;
        }

        public void SetRunInput(bool canRun)
        {
            if (canRun)
                _player.LeftShift.Enable();
            else
                _player.LeftShift.Disable();
        }

        #endregion

        #region Collider

        private void OnTriggerEnter(Collider other)
        {
            MessagePipeService.Publish(MessageKey.Player.OnTriggerEnter, other);
        }

        private void OnCollisionEnter(Collision other)
        {
            MessagePipeService.Publish(MessageKey.Player.OnCollisionEnter, other);

            if (other.gameObject.CompareTag("Enemy"))
            {
                _animator.SetTrigger(_animatorHashDamaged);
            }
        }

        #endregion

        #region Movement

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

        #region StateMachine

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<SDUnityChanPlayerController, StateEvent>(this);

            // 状態遷移テーブルの構築
            _stateMachine.AddTransition<IdleState, MovingState>(StateEvent.Move);

            _stateMachine.AddTransition<MovingState, IdleState>(StateEvent.Stop);

            _stateMachine.AddTransition<IdleState, JumpingState>(StateEvent.Jump);
            _stateMachine.AddTransition<MovingState, JumpingState>(StateEvent.Jump);

            _stateMachine.AddTransition<JumpingState, IdleState>(StateEvent.Land);

            _stateMachine.AddTransition<IdleState, DamagedState>(StateEvent.Damage);
            _stateMachine.AddTransition<MovingState, DamagedState>(StateEvent.Damage);
            _stateMachine.AddTransition<JumpingState, DamagedState>(StateEvent.Damage);

            _stateMachine.AddTransition<DamagedState, DownState>(StateEvent.Down);
            _stateMachine.AddTransition<MovingState, DownState>(StateEvent.Down);

            _stateMachine.AddTransition<DamagedState, IdleState>(StateEvent.Recover);
            _stateMachine.AddTransition<DownState, IdleState>(StateEvent.GetUp);

            _stateMachine.AddTransition<IdleState>(StateEvent.Idle);

            // 初期ステート
            _stateMachine.SetInitState<IdleState>();
        }

        /// <summary>
        /// 状態遷移イベントKey
        /// </summary>
        private enum StateEvent
        {
            Idle,    // 待機状態: Idle
            Move,    // 移動開始: Idle → Moving<
            Stop,    // 移動停止: Moving → Idle
            Jump,    // ジャンプ: Idle/Moving → Jumping
            Land,    // 着地: Jumping → Idle
            Damage,  // ダメージ: Idle/Moving/Jumping → Damaged
            Down,    // ダウン: Damaged/Moving → Down
            Recover, // ダメージ回復: Damaged → Idle
            GetUp,   // 起き上がり: Down → Idle
        }

        private class IdleState : State<SDUnityChanPlayerController, StateEvent>
        {
            public override void Update()
            {
                // ダメージ状態への遷移チェック
                var ctx = Context;
                if (ctx._isDamagedAnim)
                {
                    StateMachine.Transition(StateEvent.Damage);
                    return;
                }

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // 移動入力チェック
                if (ctx.IsMoveInput())
                {
                    StateMachine.Transition(StateEvent.Move);
                }
            }
        }

        private class MovingState : State<SDUnityChanPlayerController, StateEvent>
        {
            public override void Update()
            {
                // ダメージ状態への遷移チェック
                var ctx = Context;
                if (ctx._isDamagedAnim)
                {
                    StateMachine.Transition(StateEvent.Damage);
                    return;
                }

                // ダウン状態への遷移チェック
                if (ctx._isDownAnim)
                {
                    StateMachine.Transition(StateEvent.Down);
                    return;
                }

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // 移動入力がなくなったらIdleへ
                if (!ctx.IsMoveInput())
                {
                    StateMachine.Transition(StateEvent.Stop);
                }
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                if (ctx._mainCamera)
                {
                    if (ctx.IsMoveInput())
                    {
                        var forward = ctx._mainCamera.forward;
                        var right = ctx._mainCamera.right;
                        forward.y = 0f;
                        right.y = 0f;

                        ctx._moveVector = forward * ctx._moveValue.y + right * ctx._moveValue.x;
                        ctx._lookRotation = Quaternion.LookRotation(ctx._moveVector);
                    }
                }

                // Sweep-based移動: 移動前にCapsuleCastで衝突チェック
                var desiredMovement = ctx._moveVector * ctx._speed.Value * Time.fixedDeltaTime;
                var safeMovement = ctx.CalculateSafeMovement(desiredMovement);
                ctx._rigidbody.MovePosition(ctx._rigidbody.position + safeMovement);

                if (ctx.IsMoveInput())
                {
                    ctx._rigidbody.MoveRotation(
                        Quaternion.Slerp(ctx._rigidbody.rotation, ctx._lookRotation, ctx._rotationRatio * Time.fixedDeltaTime));
                }
            }
        }

        private class JumpingState : State<SDUnityChanPlayerController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx._animator.SetTrigger(ctx._animatorHashJump);
                ctx.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerJump).Forget();

                ctx._rigidbody.linearVelocity = new Vector3(
                    ctx._rigidbody.linearVelocity.x,
                    ctx._jump,
                    ctx._rigidbody.linearVelocity.z);

                ctx._jumpTriggered = false;
            }

            public override void Update()
            {
                // ダメージ状態への遷移チェック
                var ctx = Context;
                if (ctx._isDamagedAnim)
                {
                    StateMachine.Transition(StateEvent.Damage);
                    return;
                }

                // 着地チェック（ジャンプアニメーション終了）
                if (!ctx._isJumpingAnim)
                {
                    StateMachine.Transition(StateEvent.Land);
                }
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                if (ctx._mainCamera && ctx.IsMoveInput())
                {
                    var forward = ctx._mainCamera.forward;
                    var right = ctx._mainCamera.right;
                    forward.y = 0f;
                    right.y = 0f;

                    ctx._moveVector = forward * ctx._moveValue.y + right * ctx._moveValue.x;
                    ctx._lookRotation = Quaternion.LookRotation(ctx._moveVector);
                }

                // Sweep-based移動: 移動前にCapsuleCastで衝突チェック
                var desiredMovement = ctx._moveVector * ctx._speed.Value * Time.fixedDeltaTime;
                var safeMovement = ctx.CalculateSafeMovement(desiredMovement);
                ctx._rigidbody.MovePosition(ctx._rigidbody.position + safeMovement);

                if (ctx.IsMoveInput())
                {
                    ctx._rigidbody.MoveRotation(
                        Quaternion.Slerp(ctx._rigidbody.rotation, ctx._lookRotation, ctx._rotationRatio * Time.fixedDeltaTime));
                }
            }
        }

        private class DamagedState : State<SDUnityChanPlayerController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx._jumpTriggered = false;
                ctx.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDamaged).Forget();
            }

            public override void Update()
            {
                // ダウン状態への遷移チェック
                var ctx = Context;
                if (ctx._isDownAnim)
                {
                    StateMachine.Transition(StateEvent.Down);
                    return;
                }

                // ダメージアニメーション終了チェック
                if (!ctx._isDamagedAnim)
                {
                    StateMachine.Transition(StateEvent.Recover);
                }
            }
        }

        private class DownState : State<SDUnityChanPlayerController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx._jumpTriggered = false;
                ctx._isGettingUpComplete = false;
                ctx.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDown).Forget();
            }

            public override void Update()
            {
                // 起き上がり完了チェック
                var ctx = Context;
                if (ctx._isGettingUpComplete)
                {
                    ctx._isGettingUpComplete = false;
                    ctx.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerGetUp).Forget();
                    StateMachine.Transition(StateEvent.GetUp);
                }
            }
        }

        #endregion

        #region AnimatorState

        private void OnAnimatorStateEnter(AnimatorStateInfo stateInfo)
        {
            // アニメーター状態をフラグに反映（State内で遷移判断に使用）
            if (stateInfo.IsName("Base Layer.LocomotionState.JumpState.Jumping"))
            {
                _isJumpingAnim = true;
            }
            else if (stateInfo.IsName("Base Layer.Damaged"))
            {
                _isDamagedAnim = true;
            }
            else if (stateInfo.IsName("Base Layer.GoDown"))
            {
                _isDownAnim = true;
            }
        }

        private void OnAnimatorStateExit(AnimatorStateInfo stateInfo)
        {
            // アニメーター状態をフラグに反映（State内で遷移判断に使用）
            if (stateInfo.IsName("Base Layer.LocomotionState.JumpState.Jumping"))
            {
                _isJumpingAnim = false;
            }
            else if (stateInfo.IsName("Base Layer.Damaged"))
            {
                _isDamagedAnim = false;
            }
            else if (stateInfo.IsName("Base Layer.DownToUp"))
            {
                _isDownAnim = false;
                _isGettingUpComplete = true;
            }
        }

        #endregion
    }
}