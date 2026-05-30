using Game.Core.Constants;
using Game.Core.Services;
using Game.Library.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Input;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// Horror 用プレイヤーコントローラー（CharacterController ベース）
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HorrorPlayerController : MonoBehaviour
    {
        [SerializeField]
        private Transform _mainCamera;

        [Header("歩く速度")]
        [SerializeField]
        private float _walkSpeed = 2.0f;

        [Header("走る速度")]
        [SerializeField]
        private float _runSpeed = 5.0f;

        [Header("ジャンプ力")]
        [SerializeField]
        private float _jump = 5.0f;

        [Header("重力加速度")]
        [SerializeField]
        private float _gravity = -20.0f;

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private ProjectDefaultInputSystem.PlayerActions Player => InputService.Player;

        private CharacterController _characterController;

        // ステートマシーン
        private StateMachine<HorrorPlayerController, StateEvent> _stateMachine;

        // 入力関連
        private Vector2 _moveValue;
        private float _speed;
        private bool _jumpTriggered;

        // 垂直速度（重力 + ジャンプ）
        private float _verticalVelocity;

        public void Initialize()
        {
            TryGetComponent(out _characterController);

            // ステートマシン初期化
            InitializeStateMachine();
        }

        #region MonoBehaviour Methods

        protected void OnEnable() => InputService.EnablePlayer();

        protected void OnDisable() => InputService.DisablePlayer();

        protected void Update()
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
            _moveValue = Player.Move.ReadValue<Vector2>();

            // 移動速度更新（LeftShift で走り、それ以外は歩き）
            _speed = _moveValue.magnitude * (Player.LeftShift.IsPressed() ? _runSpeed : _walkSpeed);

            // ジャンプ入力受付
            if (Player.Jump.WasPressedThisFrame() && CanJump())
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

        private bool IsGrounded() => _characterController.isGrounded;

        public bool IsMoving() => _speed > 0f;

        public bool IsWalking() => _speed >= _walkSpeed && _speed < _runSpeed;

        public bool IsRunning() => _speed >= _runSpeed;

        private bool IsMoveInput() => _moveValue.magnitude > PlayerPhysicsConstants.InputThreshold;

        #endregion

        #region StateMachine

        private void InitializeStateMachine()
        {
            _stateMachine = new StateMachine<HorrorPlayerController, StateEvent>(this);

            // 状態遷移テーブルの構築
            _stateMachine.AddTransition<IdleState, MovingState>(StateEvent.Move);
            _stateMachine.AddTransition<MovingState, IdleState>(StateEvent.Stop);

            _stateMachine.AddTransition<IdleState, JumpingState>(StateEvent.Jump);
            _stateMachine.AddTransition<MovingState, JumpingState>(StateEvent.Jump);

            _stateMachine.AddTransition<JumpingState, IdleState>(StateEvent.Land);

            _stateMachine.AddTransition<IdleState>(StateEvent.Idle);

            // 初期ステート
            _stateMachine.SetInitState<IdleState>();
        }

        /// <summary>
        /// 状態遷移イベントKey
        /// </summary>
        private enum StateEvent
        {
            Idle, // 待機状態: Idle
            Move, // 移動開始: Idle → Moving
            Stop, // 移動停止: Moving → Idle
            Jump, // ジャンプ: Idle/Moving → Jumping
            Land, // 着地: Jumping → Idle
        }

        private class IdleState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;

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

            public override void FixedUpdate()
            {
                // 静止中も重力を適用
                Context.ApplyGravityAndMove(Vector3.zero);
            }
        }

        private class MovingState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;

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
                ctx.ApplyGravityAndMove(ctx.ComputeHorizontalVelocity());
            }
        }

        private class JumpingState : State<HorrorPlayerController, StateEvent>
        {
            public override void Enter()
            {
                var ctx = Context;
                ctx._verticalVelocity = ctx._jump;
                ctx._jumpTriggered = false;
            }

            public override void Update()
            {
                var ctx = Context;

                // 上昇終了 + 接地で着地判定
                if (ctx._verticalVelocity <= 0f && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Land);
                }
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                // 空中でも水平移動を許可
                ctx.ApplyGravityAndMove(ctx.ComputeHorizontalVelocity());
            }
        }

        #endregion

        #region Movement

        /// <summary>
        /// カメラの向きを基準に水平速度を計算
        /// _moveVector を非正規化のまま更新してアナログ入力の強度を保持する
        /// </summary>
        private Vector3 ComputeHorizontalVelocity()
        {
            if (_mainCamera == null) return Vector3.zero;
            if (!IsMoveInput()) return Vector3.zero;

            var forward = _mainCamera.forward;
            var right = _mainCamera.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            var moveVector = forward * _moveValue.y + right * _moveValue.x;
            return moveVector * _speed;
        }

        /// <summary>
        /// 重力を適用して CharacterController で移動
        /// </summary>
        private void ApplyGravityAndMove(Vector3 horizontalVelocity)
        {
            if (IsGrounded() && _verticalVelocity < 0f)
            {
                // 接地中は微小な下向き速度を保持（接地判定の安定化）
                _verticalVelocity = -2f;
            }
            else
            {
                _verticalVelocity += _gravity * Time.fixedDeltaTime;
            }

            var motion = horizontalVelocity + Vector3.up * _verticalVelocity;
            _characterController.Move(motion * Time.fixedDeltaTime);
        }

        #endregion
    }
}
