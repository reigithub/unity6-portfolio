using System.Linq;
using Game.Core;
using Game.Core.Enums;
using Game.Core.Extensions;
using Game.Core.MasterData.MemoryTables;
using Game.Core.MessagePipe;
using Game.Core.Services;
using R3;
using R3.Triggers;
using UnityChan;
using UnityEngine;

namespace Game.Contents.Player
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

        private GameServiceReference<AudioService> _audioService;
        private AudioService AudioService => _audioService.Reference;

        private GameServiceReference<MessageBrokerService> _messageBrokerService;
        private GlobalMessageBroker GlobalMessageBroker => _messageBrokerService.Reference.GlobalMessageBroker;

        private SDUnityChanInputSystem _inputSystem;
        private SDUnityChanInputSystem.PlayerActions _player;

        private Animator _animator;
        private Rigidbody _rigidbody;
        private RaycastChecker _groundedRaycastChecker;

        // ステートマシーン
        private StateMachine<SDUnityChanPlayerController> _stateMachine;

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

        public void Initialize(PlayerMaster playerMaster)
        {
            _walkSpeed = playerMaster.WalkSpeed;
            _jogSpeed = playerMaster.JogSpeed;
            _runSpeed = playerMaster.RunSpeed;
            _jump = playerMaster.Jump;

            TryGetComponent(out _animator);
            TryGetComponent(out _rigidbody);
            TryGetComponent(out _groundedRaycastChecker);

            // ステートマシン初期化
            _stateMachine = new StateMachine<SDUnityChanPlayerController>(this);
            _stateMachine.SetInitState<IdleState>();

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

            // 走り始めた時のボイス再生
            _speed
                .DistinctUntilChangedBy(x => IsRunning())
                .Subscribe(_ =>
                {
                    if (IsRunning()) AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerRun).Forget();
                })
                .AddTo(this);
        }

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

        public void SetMainCamera(Transform mainCamera)
        {
            _mainCamera = mainCamera;
        }

        private void Awake()
        {
            _inputSystem = new SDUnityChanInputSystem();
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
            _stateMachine.Update();
        }

        private void FixedUpdate()
        {
            _stateMachine.FixedUpdate();
        }

        private void UpdateInput()
        {
            // 移動入力受付
            _moveValue = _player.Move.ReadValue<Vector2>();
            _moveVector = new Vector3(_moveValue.x, 0.0f, _moveValue.y).normalized;

            // 移動速度更新
            var speed = _moveVector.magnitude * (_player.LeftShift.IsPressed() ? _runSpeed : _jogSpeed);
            _speed.Value = speed;
            _animator.SetFloat(_animatorHashSpeed, speed);

            // 回転入力受付
            if (_moveValue.magnitude > 0.1f)
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

        public void SetRunInput(bool canRun)
        {
            if (canRun)
                _player.LeftShift.Enable();
            else
                _player.LeftShift.Disable();
        }

        private void OnTriggerEnter(Collider other)
        {
            GlobalMessageBroker.GetPublisher<int, Collider>().Publish(MessageKey.Player.OnTriggerEnter, other);
        }

        private void OnCollisionEnter(Collision other)
        {
            GlobalMessageBroker.GetPublisher<int, Collision>().Publish(MessageKey.Player.OnCollisionEnter, other);

            if (other.gameObject.CompareTag("Enemy"))
            {
                _animator.SetTrigger(_animatorHashDamaged);
            }
        }

        #region Player States

        private class IdleState : State<SDUnityChanPlayerController>
        {
            public override void Update()
            {
                var controller = Context;

                // ダメージ状態への遷移チェック
                if (controller._isDamagedAnim)
                {
                    StateMachine.TransitionTo<DamagedState>();
                    return;
                }

                // ジャンプ入力チェック
                if (controller._jumpTriggered && controller.IsGrounded())
                {
                    StateMachine.TransitionTo<JumpingState>();
                    return;
                }

                // 移動入力チェック
                if (controller._moveValue.magnitude > 0.1f)
                {
                    StateMachine.TransitionTo<MovingState>();
                }
            }
        }

        private class MovingState : State<SDUnityChanPlayerController>
        {
            public override void Update()
            {
                var controller = Context;

                // ダメージ状態への遷移チェック
                if (controller._isDamagedAnim)
                {
                    StateMachine.TransitionTo<DamagedState>();
                    return;
                }

                if (controller._isDownAnim)
                {
                    StateMachine.TransitionTo<DownState>();
                    return;
                }

                // ジャンプ入力チェック
                if (controller._jumpTriggered && controller.IsGrounded())
                {
                    StateMachine.TransitionTo<JumpingState>();
                    return;
                }

                // 移動入力がなくなったらIdleへ
                if (controller._moveValue.magnitude <= 0.1f)
                {
                    StateMachine.TransitionTo<IdleState>();
                }
            }

            public override void FixedUpdate()
            {
                var controller = Context;

                if (controller._mainCamera)
                {
                    if (controller._moveValue.magnitude > 0.1f)
                    {
                        var forward = controller._mainCamera.forward;
                        var right = controller._mainCamera.right;
                        forward.y = 0f;
                        right.y = 0f;

                        controller._moveVector = forward * controller._moveValue.y + right * controller._moveValue.x;
                        controller._lookRotation = Quaternion.LookRotation(controller._moveVector);
                    }
                }

                controller._rigidbody.MovePosition(controller._rigidbody.position + controller._moveVector * controller._speed.Value * Time.fixedDeltaTime);

                if (controller._moveValue.magnitude > 0.1f)
                {
                    controller._rigidbody.MoveRotation(
                        Quaternion.Slerp(controller._rigidbody.rotation, controller._lookRotation, controller._rotationRatio * Time.fixedDeltaTime));
                }
            }
        }

        private class JumpingState : State<SDUnityChanPlayerController>
        {
            public override void Enter()
            {
                var controller = Context;

                controller._animator.SetTrigger(controller._animatorHashJump);
                controller.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerJump).Forget();

                controller._rigidbody.linearVelocity = new Vector3(
                    controller._rigidbody.linearVelocity.x,
                    controller._jump,
                    controller._rigidbody.linearVelocity.z);

                controller._jumpTriggered = false;
            }

            public override void Update()
            {
                var controller = Context;

                // ダメージ状態への遷移チェック
                if (controller._isDamagedAnim)
                {
                    StateMachine.TransitionTo<DamagedState>();
                    return;
                }

                // 着地チェック（ジャンプアニメーション終了）
                if (!controller._isJumpingAnim)
                {
                    StateMachine.TransitionTo<IdleState>();
                }
            }

            public override void FixedUpdate()
            {
                var controller = Context;

                if (controller._mainCamera && controller._moveValue.magnitude > 0.1f)
                {
                    var forward = controller._mainCamera.forward;
                    var right = controller._mainCamera.right;
                    forward.y = 0f;
                    right.y = 0f;

                    controller._moveVector = forward * controller._moveValue.y + right * controller._moveValue.x;
                    controller._lookRotation = Quaternion.LookRotation(controller._moveVector);
                }

                controller._rigidbody.MovePosition(
                    controller._rigidbody.position + controller._moveVector * controller._speed.Value * Time.fixedDeltaTime);

                if (controller._moveValue.magnitude > 0.1f)
                {
                    controller._rigidbody.MoveRotation(
                        Quaternion.Slerp(controller._rigidbody.rotation, controller._lookRotation, controller._rotationRatio * Time.fixedDeltaTime));
                }
            }
        }

        private class DamagedState : State<SDUnityChanPlayerController>
        {
            public override void Enter()
            {
                var controller = Context;
                controller._jumpTriggered = false;
                controller.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDamaged).Forget();
            }

            public override void Update()
            {
                var controller = Context;

                // ダウン状態への遷移チェック
                if (controller._isDownAnim)
                {
                    StateMachine.TransitionTo<DownState>();
                    return;
                }

                // ダメージアニメーション終了チェック
                if (!controller._isDamagedAnim)
                {
                    StateMachine.TransitionTo<IdleState>();
                }
            }
        }

        private class DownState : State<SDUnityChanPlayerController>
        {
            public override void Enter()
            {
                var controller = Context;
                controller._jumpTriggered = false;
                controller._isGettingUpComplete = false;
                controller.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerDown).Forget();
            }

            public override void Update()
            {
                var controller = Context;

                // 起き上がり完了チェック
                if (controller._isGettingUpComplete)
                {
                    controller._isGettingUpComplete = false;
                    controller.AudioService.PlayRandomOneAsync(AudioCategory.Voice, AudioPlayTag.PlayerGetUp).Forget();
                    StateMachine.TransitionTo<IdleState>();
                }
            }
        }

        #endregion
    }
}