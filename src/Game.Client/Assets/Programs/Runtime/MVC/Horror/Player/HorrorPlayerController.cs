using Game.Core.Constants;
using Game.Core.Services;
using Game.Horror.SaveData;
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
        [SerializeField] private Camera _mainCamera;

        [SerializeField] private float _walkSpeed = 2.0f;
        [SerializeField] private float _runSpeed = 5.0f;
        [SerializeField] private float _jump = 5.0f;
        [SerializeField] private float _gravity = -20.0f;

        [Header("回転速度（度/秒）")]
        [SerializeField] private float _lookRotationSpeed = 0.1f;

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

        // カメラ感度設定（per-axis）
        private float _lookSensitivityX = 1f;
        private float _lookSensitivityY = 1f;

        // カメラ加速度設定（入力スムージング）
        private Vector2 _smoothedLookValue;
        private float _lookAcceleration = 1f;

        // カメラ揺れ設定（ヘッドボブ figure-8 ＋ ストライド同期ロール、停止時はアイドルスウェイ）
        private Vector3 _cameraBasePosition;
        private float _bobPhase;
        private float _idlePhase;         // アイドルスウェイの常時位相
        private float _moveBobWeight;     // 0=停止, 1=移動（ease）。cameraShake とは分離
        private float _cameraShake = 1f;

        private const float BobWalkAmplitude = 0.04f;   // 歩き：縦位置振幅（m）
        private const float BobRunAmplitude = 0.06f;    // 走り：縦位置振幅（m）
        private const float BobWalkSpeed = 10f;         // 歩き：位相速度 rad/s（ゆっくり）
        private const float BobRunSpeed = 15f;          // 走り：位相速度 rad/s（少しだけ速い）
        private const float BobHorizontalRatio = 0.5f;  // 横位置/縦位置 比
        private const float BobWalkRoll = 0.1f;         // 歩き：ロール角（度）＝知覚される横揺れ
        private const float BobRunRoll = 0.2f;          // 走り：ロール角（度）
        private const float BobAmplitudeResponse = 10f; // 強度イーズの応答

        private const float IdleSwaySpeed = 1.2f;       // アイドル：位相速度 rad/s（呼吸 ~5秒周期）
        private const float IdleSwayAmplitude = 0.05f;  // アイドル：縦位置振幅（m, ヘッドボブより小）
        private const float IdleSwayRoll = 0.01f;       // アイドル：ロール角（度, 小）

        public void Initialize(HorrorOptionSaveData data)
        {
            TryGetComponent(out _characterController);

            // ヘッドボブの基準（rest）位置と Camera（FOV 反映用）を保持
            _cameraBasePosition = _mainCamera.transform.localPosition;

            // オプション設定の反映
            ApplyOptions(data);

            // ステートマシン初期化
            InitializeStateMachine();
        }

        public void ApplyOptions(HorrorOptionSaveData data)
        {
            _lookInvertX = data.CameraControlHorizontal ? -1f : 1f;
            _lookInvertY = data.CameraControlVertical ? -1f : 1f;

            _lookSensitivityX = data.CameraSensitivityHorizontal;
            _lookSensitivityY = data.CameraSensitivityVertical;

            _lookAcceleration = data.CameraAcceleration;
            _cameraShake = data.CameraShake;
            _mainCamera.fieldOfView = data.CameraFov;
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

            if (Player.enabled)
            {
                // 加速度（入力スムージング）：実効 look を生入力へ追従。応答が高いほど即時、低いほど滑らか。
                var acceleration = Mathf.Max(_lookAcceleration, 0.01f);
                var smoothing = 1f - Mathf.Exp(-acceleration * Time.deltaTime);
                _smoothedLookValue = Vector2.Lerp(_smoothedLookValue, _lookValue, smoothing);
            }
            else
            {
                _smoothedLookValue = Vector2.zero;
            }

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
                ctx.UpdateHeadBob();

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
                ctx.UpdateHeadBob();

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
                ctx.UpdateHeadBob();

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

            // Yaw: Player 本体を Y 軸回転（感度H・反転を適用、入力は加速度スムージング後の値）
            var horizontalInput = _smoothedLookValue.x * _lookSensitivityX * _lookInvertX;
            transform.Rotate(0f, horizontalInput * _lookRotationSpeed, 0f, Space.Self);

            // Pitch: カメラの X 軸 localEulerAngles を更新、クランプ（既定 -y、感度V・反転を適用）
            var verticalInput = -_smoothedLookValue.y * _lookSensitivityY * _lookInvertY;
            _cameraVerticalAngle = Mathf.Clamp(_cameraVerticalAngle + verticalInput * _lookRotationSpeed, -89f, 89f);
            _mainCamera.transform.localEulerAngles = new Vector3(_cameraVerticalAngle, 0f, 0f);
        }

        /// <summary>
        /// カメラ揺れを適用。移動中は figure-8 ヘッドボブ、停止中はアイドルスウェイ（呼吸揺れ）をクロスフェードする。
        /// 全体強度は CameraShake でスケール。ApplyRotation 直後に呼ばれ、pitch を維持しつつ roll を合成する。
        /// </summary>
        private void UpdateHeadBob()
        {
            if (_mainCamera == null) return;

            // 入力ブロック中（ポーズ等）は neutral に戻す（Time.deltaTime=0 凍結による残オフセット防止）
            if (!Player.enabled)
            {
                _mainCamera.transform.localPosition = _cameraBasePosition;
                _mainCamera.transform.localEulerAngles = new Vector3(_cameraVerticalAngle, 0f, 0f);
                _moveBobWeight = 0f;
                return;
            }

            // 接地して移動中のみヘッドボブ。停止でアイドルスウェイへクロスフェード。
            // ケイデンスは _speed 直結にせず歩き/走りで固定（走りは少しだけ速い）。
            var active = IsGrounded() && IsMoving();
            var running = IsRunning();

            var ease = 1f - Mathf.Exp(-BobAmplitudeResponse * Time.deltaTime);
            _moveBobWeight = Mathf.Lerp(_moveBobWeight, active ? 1f : 0f, ease);

            if (active)
                _bobPhase += (running ? BobRunSpeed : BobWalkSpeed) * Time.deltaTime;
            _idlePhase += IdleSwaySpeed * Time.deltaTime; // アイドルは常時進む

            // ヘッドボブ（移動）：縦は位相、横はストライド（半周期）＝figure-8。横揺れの知覚はロールが主成分。
            var moveAmplitude = (running ? BobRunAmplitude : BobWalkAmplitude) * _moveBobWeight;
            var moveRoll = (running ? BobRunRoll : BobWalkRoll) * _moveBobWeight;
            var bobX = Mathf.Sin(_bobPhase * 0.5f) * moveAmplitude * BobHorizontalRatio;
            var bobY = Mathf.Sin(_bobPhase) * moveAmplitude;
            var bobRoll = Mathf.Sin(_bobPhase * 0.5f) * moveRoll;

            // アイドルスウェイ（停止）：別周波数の遅い sin を重ねて有機的に
            var idleWeight = 1f - _moveBobWeight;
            var idleX = Mathf.Sin(_idlePhase * 1.3f) * IdleSwayAmplitude * BobHorizontalRatio * idleWeight;
            var idleY = Mathf.Sin(_idlePhase) * IdleSwayAmplitude * idleWeight;
            var idleRoll = Mathf.Sin(_idlePhase * 0.7f) * IdleSwayRoll * idleWeight;

            // 合算 → 全体強度 CameraShake（0 で完全停止）
            var offset = new Vector3(bobX + idleX, bobY + idleY, 0f) * _cameraShake;
            var roll = (bobRoll + idleRoll) * _cameraShake;

            _mainCamera.transform.localPosition = _cameraBasePosition + offset;
            _mainCamera.transform.localEulerAngles = new Vector3(_cameraVerticalAngle, 0f, roll);
        }

        #endregion
    }
}
