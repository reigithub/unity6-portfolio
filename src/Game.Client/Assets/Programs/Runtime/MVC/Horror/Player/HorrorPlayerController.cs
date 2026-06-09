using Game.Core.Constants;
using Game.Core.Services;
using Game.Horror.SaveData;
using Game.Library.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Input;
using R3;
using UnityEngine;

namespace Game.Horror.Player
{
    /// <summary>
    /// Horror 用プレイヤーコントローラー（CharacterController ベース）
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class HorrorPlayerController : MonoBehaviour
    {
        [SerializeField] private Transform _mainCamera;

        [SerializeField] private float _walkSpeed = 2.0f;
        [SerializeField] private float _runSpeed = 5.0f;
        [SerializeField] private float _jump = 5.0f;
        [SerializeField] private float _gravity = -20.0f;

        [Header("回転速度（度/秒）")]
        [SerializeField] private float _lookRotationSpeed = 0.1f;

        [Header("マウス感度")]
        [SerializeField] private float _lookSensitivity = 1f;

        private InputSystemService _inputService;
        private InputSystemService InputService => _inputService ??= GameServiceManager.Get<InputSystemService>();

        private ProjectDefaultInputSystem.PlayerActions Player => InputService.Player;

        private CharacterController _characterController;

        // ステートマシーン
        private StateMachine<HorrorPlayerController, StateEvent> _stateMachine;

        // 入力関連
        private Vector2 _moveValue;
        private Vector2 _lookValue;
        private float _speed;
        private bool _jumpTriggered;

        // 垂直速度（重力 + ジャンプ）
        private float _verticalVelocity;

        // カメラピッチ角度
        private float _cameraVerticalAngle;

        // カメラ操作反転設定
        private float _lookInvertX = 1f;
        private float _lookInvertY = 1f;

        public void Initialize(HorrorOptionSaveData data)
        {
            TryGetComponent(out _characterController);

            // オプション設定の反映
            ApplyOptions(data);

            // ステートマシン初期化
            InitializeStateMachine();
        }

        public void ApplyOptions(HorrorOptionSaveData data)
        {
            _lookInvertX = data.CameraControlHorizontal ? -1f : 1f;
            _lookInvertY = data.CameraControlVertical ? -1f : 1f;
        }

        #region MonoBehaviour Methods

        protected void OnEnable() => InputService.EnablePlayer();

        protected void OnDisable() => InputService.DisablePlayer();

        protected void Update()
        {
            UpdateInput();
            _stateMachine?.Update();
        }

        protected void FixedUpdate()
        {
            _stateMachine?.FixedUpdate();
        }

        #endregion

        #region Input

        private void UpdateInput()
        {
            // 移動入力受付
            _moveValue = Player.Move.ReadValue<Vector2>();

            // 視点入力受付
            _lookValue = Player.Look.ReadValue<Vector2>();

            // 移動速度更新（LeftShift で走り、それ以外は歩き）
            _speed = _moveValue.magnitude * (Player.LeftShift.IsPressed() ? _runSpeed : _walkSpeed);

            // ジャンプ入力受付
            if (Player.Jump.WasPressedThisFrame() && CanJump())
            {
                _jumpTriggered = true;
            }

            if (IsMoveInput() || IsLookInput()) ApplicationEvents.HideCursor();
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
        private bool IsLookInput() => _lookValue.magnitude > PlayerPhysicsConstants.InputThreshold;

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
                ctx.ApplyRotation();

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
                Context.ApplyMovementWithGravity(Vector3.zero);
            }
        }

        private class MovingState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;
                ctx.ApplyRotation();

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
                ctx.ApplyMovementWithGravity(ctx.ComputeHorizontalVelocity());
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
                ctx.ApplyRotation();

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
                ctx.ApplyMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        #endregion

        #region Movement

        /// <summary>
        /// Player 本体の向き（Yaw 適用済 transform）を基準に水平速度を計算
        /// アナログ入力強度を保持するため normalize しない
        /// </summary>
        private Vector3 ComputeHorizontalVelocity()
        {
            if (!IsMoveInput()) return Vector3.zero;

            var forward = transform.forward;
            var right = transform.right;
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
        private void ApplyMovementWithGravity(Vector3 horizontalVelocity)
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

        /// <summary>
        /// 視点回転を適用
        /// Yaw: Player 本体を Y 軸回転（カメラは子なので自動追従）
        /// Pitch: カメラ Transform の X 軸を localEulerAngles で回転、±89° クランプ
        /// </summary>
        private void ApplyRotation()
        {
            if (_mainCamera == null) return;

            var sensitivity = _lookSensitivity * _lookRotationSpeed;

            // Yaw: Player 本体を Y 軸回転（反転 ON で符号反転）
            transform.Rotate(0f, _lookValue.x * sensitivity * _lookInvertX, 0f, Space.Self);

            // Pitch: カメラの X 軸 localEulerAngles を更新、クランプ（既定 -y、反転 ON で符号反転）
            var verticalInput = -_lookValue.y * _lookInvertY;
            _cameraVerticalAngle = Mathf.Clamp(_cameraVerticalAngle + verticalInput * sensitivity, -89f, 89f);
            _mainCamera.localEulerAngles = new Vector3(_cameraVerticalAngle, 0f, 0f);
        }

        #endregion
    }
}
