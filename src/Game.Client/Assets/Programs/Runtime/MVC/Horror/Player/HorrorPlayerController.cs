using Game.Core.Constants;
using Game.Core.Services;
using Game.Horror.Interaction;
using Game.Horror.SaveData;
using Game.Library.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Enums;
using Game.Shared.Extensions;
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
        [SerializeField] private Camera _mainCamera;

        [SerializeField] private float _walkSpeed = 2.0f;
        [SerializeField] private float _runSpeed = 5.0f;
        [SerializeField] private float _jump = 5.0f;
        [SerializeField] private float _gravity = -20.0f;

        [Header("しゃがみ")]
        [SerializeField] private float _crouchSpeed = 1.2f;
        [SerializeField] private float _crouchHeight = 1.0f;

        [Tooltip("立ち↔しゃがみ補間の応答速度（1-exp(-k・dt) の k）")]
        [SerializeField] private float _crouchTransitionSpeed = 8f;

        [Tooltip("立ち上がり判定の対象レイヤー。プレイヤー自身のレイヤーは含めないこと")]
        [SerializeField] private LayerMask _ceilingLayerMask;

        [Header("回転速度（度/秒）")]
        [SerializeField] private float _lookRotationSpeed = 0.1f;

        [Header("インタラクション")]
        [Tooltip("インタラクト対象を検出する検出器（同一 Prefab 上にアタッチ）")]
        [SerializeField] private InteractionDetector _interactionDetector;

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

        // インタラクト（起動入力フラグ／Hold 実行中の対象と経過時間）
        private bool _interactTriggered;
        private IInteractable _holdTarget;
        private float _holdElapsed;

        // 走り（トグル/ホールド切替）
        private bool _sprintToggle; // オプション値（false=ホールド, true=トグル）
        private bool _isSprinting;  // 走り状態

        // しゃがみ姿勢（移動ステートと直交する姿勢として保持）
        private bool _crouchToggle;   // オプション値（false=ホールド, true=トグル）
        private bool _isCrouching;    // 目標姿勢
        private float _crouchBlend;   // 0=立ち, 1=しゃがみ の実補間値（形状・カメラ高さの単一ソース）
        private float _standHeight;   // 立ち時の CharacterController 高さ（Initialize で実測）

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
        private Vector3 _standCameraBasePosition; // 立ち目線の不変参照点（しゃがみ補間の基準）
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

        private const float CeilingCheckBuffer = 0.15f; // しゃがみ：立ち上がりに必要な頭上余裕（m）

        public void Initialize(HorrorOptionSaveData data)
        {
            TryGetComponent(out _characterController);

            // 立ち姿勢の基準値を実測で保持（prefab 値の変更に追従させ、しゃがみ補間の不変参照点にする）
            _standHeight = _characterController.height;
            _standCameraBasePosition = _mainCamera.transform.localPosition;

            // ヘッドボブの基準（rest）位置と Camera（FOV 反映用）を保持
            _cameraBasePosition = _standCameraBasePosition;

            // オプション設定の反映
            ApplyOptions(data);

            // ステートマシン初期化
            InitializeStateMachine();

            // プレイヤー入力監視
            Observable.Merge(Player.Move.OnPerformedAsObservable()
                    , Player.Look.OnPerformedAsObservable()
                    , Player.Attack.OnPerformedAsObservable()
                    , Player.Interact.OnPerformedAsObservable()
                    , Player.Jump.OnPerformedAsObservable()
                    , Player.Crouch.OnPerformedAsObservable()
                    , Player.Sprint.OnPerformedAsObservable()
                    )
                .Subscribe(_ => ApplicationEvents.HideCursor())
                .AddTo(this);
        }

        public void ApplyOptions(HorrorOptionSaveData data)
        {
            _lookInvertX = data.CameraControlHorizontal ? -1f : 1f;
            _lookInvertY = data.CameraControlVertical ? -1f : 1f;

            _lookSensitivityX = data.CameraSensitivityHorizontal;
            _lookSensitivityY = data.CameraSensitivityVertical;

            _lookAcceleration = data.CameraAcceleration;
            _cameraShake = data.CameraShake;
            if (_mainCamera != null) _mainCamera.fieldOfView = data.CameraFov;

            // OnSaved でランタイム再適用される。カメラ基準位置は触らない（しゃがみ中のリセット防止）
            _sprintToggle = data.SprintToggle;
            _crouchToggle = data.CrouchToggle;
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

            // インタラクト中（身体占有）は移動・他アクションを受け付けない
            var interacting = _stateMachine.IsProcessing() && _stateMachine.IsCurrentState<InteractingState>();
            if (!interacting)
            {
                // しゃがみ入力（モード別）。移動速度が姿勢に依存するため先に確定させる
                UpdateCrouchInput();
                // 走り入力（モード別）。しゃがみ状態が確定した後に判定する
                UpdateSprintInput();
                // インタラクト起動入力：フラグを立てるのみ。実際の起動・遷移は Idle/Moving ステートが行う
                UpdateInteractInput();
            }

            // 移動速度更新（拘束中は 0、しゃがみ中は crouchSpeed 優先、それ以外は _isSprinting で走り/歩き）
            if (interacting)
            {
                _speed = 0f;
            }
            else
            {
                var baseSpeed = _isCrouching ? _crouchSpeed : (_isSprinting ? _runSpeed : _walkSpeed);
                _speed = _moveValue.magnitude * baseSpeed;
            }

            // ジャンプ入力受付（拘束中は不可）
            if (!interacting && Player.Jump.WasPressedThisFrame() && CanJump())
            {
                _jumpTriggered = true;
            }
        }

        private bool CanJump()
        {
            if (!_stateMachine.IsProcessing())
                return false;

            // Idle/Moving状態でのみジャンプ可能（しゃがみ中は不可）
            var canJumpFromState = _stateMachine.IsCurrentState<IdleState>() ||
                                   _stateMachine.IsCurrentState<MovingState>();

            return canJumpFromState && IsGrounded() && !_isCrouching;
        }

        /// <summary>
        /// 立てられた起動入力フラグを消費してインタラクトを開始する。Idle/Moving ステートの Update から呼ばれ、
        /// 単発/トグルはその場で実行し（状態は変えない）、Hold は対象を保持して true を返す（遷移は呼び出し元ステートが行う）。
        /// </summary>
        /// <returns>Hold 開始で InteractingState へ遷移すべきなら true。</returns>
        private bool TryBeginInteraction()
        {
            if (!_interactTriggered)
                return false;

            _interactTriggered = false;

            if (_interactionDetector == null || !_interactionDetector.TryGetActionable(out var target))
                return false;

            if (!target.CanInteract())
                return false;

            if (target.InputType == InteractionInputType.Hold)
            {
                _holdTarget = target;
                return true;
            }

            // 単発 / トグル：状態を変えずその場で実行
            target.Interact();
            return false;
        }

        /// <summary>
        /// Hold 長押しの進捗（0→1）を算出する。<paramref name="holdSeconds"/> が 0 以下なら
        /// ゼロ除算を避けて即時完了（1）とみなす。表示側で Clamp されるため、
        /// elapsed が holdSeconds を超えた最終フレームでは 1 を超える生値を返しうる。
        /// </summary>
        public static float CalculateHoldProgress(float elapsed, float holdSeconds)
            => holdSeconds > 0f ? elapsed / holdSeconds : 1f;

        private bool IsGrounded() => _characterController.isGrounded;
        private bool IsMoving() => _speed > 0f;
        private bool IsWalking() => _speed >= _walkSpeed && _speed < _runSpeed;
        private bool IsRunning() => _speed >= _runSpeed;

        private bool IsMoveInput() => _moveValue.magnitude > PlayerPhysicsConstants.InputThreshold;
        private bool IsLookInput() => _lookValue.magnitude > PlayerPhysicsConstants.InputThreshold;

        /// <summary>
        /// しゃがみ入力をモード別に処理する。空中（非接地）では姿勢を変更しない。
        /// 立ち上がる方向のみ <see cref="CanStandUp"/> で頭上を確認し、塞がっていればしゃがみを維持する。
        /// </summary>
        private void UpdateCrouchInput()
        {
            // 空中ではしゃがみ入力を無視（姿勢は維持）
            if (!IsGrounded()) return;

            if (_crouchToggle)
            {
                // トグル：押した瞬間に反転（立ち上がりは天井チェックを通す）
                if (Player.Crouch.WasPressedThisFrame())
                {
                    if (_isCrouching)
                    {
                        if (CanStandUp()) _isCrouching = false;
                    }
                    else
                    {
                        _isCrouching = true;
                    }
                }
            }
            else
            {
                // ホールド：押下中はしゃがみ、離したら立ち上がり試行
                if (Player.Crouch.IsPressed())
                {
                    _isCrouching = true;
                }
                else if (_isCrouching && CanStandUp())
                {
                    _isCrouching = false;
                }
            }
        }

        /// <summary>
        /// 走り入力をモード別に処理する。しゃがみ中は走れない（トグル状態も解除）。
        /// トグル時は押下で反転し、移動を止めると解除する。
        /// </summary>
        private void UpdateSprintInput()
        {
            // しゃがみ中は走れない（トグル状態も強制解除）
            if (_isCrouching) { _isSprinting = false; return; }

            if (_sprintToggle)
            {
                // トグル：押下で反転。移動入力が無ければ解除（停止で解除）
                if (Player.Sprint.WasPressedThisFrame()) _isSprinting = !_isSprinting;
                if (!IsMoveInput()) _isSprinting = false;
            }
            else
            {
                // ホールド：押下中のみ走る
                _isSprinting = Player.Sprint.IsPressed();
            }
        }

        private void UpdateInteractInput()
        {
            if (Player.Interact.WasPressedThisFrame() && IsGrounded() && !_isCrouching)
                _interactTriggered = true;
        }

        /// <summary>
        /// 立ち上がれるか（頭上の障害物判定）。立ち姿勢のカプセル頭頂までを SphereCast で掃引し、障害物が無ければ true。
        /// 自己衝突は (1)_ceilingMask に自レイヤーを含めない (2)始点を下半球中心に置く (3)半径を skinWidth 分縮める の三重で回避する。
        /// </summary>
        private bool CanStandUp()
        {
            var radius = _characterController.radius;

            // 現在（しゃがみ）のカプセル下端をワールド座標で求める（center はローカル基準）
            var bottomWorld = transform.TransformPoint(_characterController.center) - Vector3.up * (_characterController.height * 0.5f);
            var origin = bottomWorld + Vector3.up * radius; // 下半球の中心（自カプセル内）

            // 下半球中心から、立ち姿勢の上半球中心（下端 + standHeight - radius）までの距離 ＋ 頭上余裕
            var castDistance = _standHeight - 2f * radius + CeilingCheckBuffer;
            if (castDistance <= 0f) return true; // 立ち高さ ≈ しゃがみ高さなら常に立てる

            var castRadius = Mathf.Max(0.01f, radius - _characterController.skinWidth);

            return !Physics.SphereCast(
                origin,
                castRadius,
                Vector3.up,
                out _,
                castDistance,
                _ceilingLayerMask,
                QueryTriggerInteraction.Ignore);
        }

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

            _stateMachine.AddTransition<IdleState, InteractingState>(StateEvent.Interact);
            _stateMachine.AddTransition<MovingState, InteractingState>(StateEvent.Interact);
            _stateMachine.AddTransition<InteractingState, IdleState>(StateEvent.EndInteract);

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
            Interact, // インタラクト開始: Idle/Moving → Interacting
            EndInteract, // インタラクト終了: Interacting → Idle
        }

        private class IdleState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;
                ctx.ApplyRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // インタラクト起動チェック（Hold は Interacting へ遷移）
                if (ctx.TryBeginInteraction())
                {
                    StateMachine.Transition(StateEvent.Interact);
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
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // インタラクト起動チェック（Hold は Interacting へ遷移）
                if (ctx.TryBeginInteraction())
                {
                    StateMachine.Transition(StateEvent.Interact);
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
                ctx.UpdateCrouchPose();
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

        /// <summary>
        /// インタラクト（Hold）実行中の身体占有状態。視点回転のみ許可し水平移動は止める。
        /// ボタン解放・対象喪失・視線外し・実行不可化で中断し、長押し閾値到達で効果を発火する。
        /// </summary>
        private class InteractingState : State<HorrorPlayerController, StateEvent>
        {
            public override void Enter()
            {
                Context._holdElapsed = 0f;
                Context._holdTarget?.SetHoldProgress(0f); // ゲージを初期化（非表示）
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.ApplyRotation(); // 視点回転のみ許可

                var target = ctx._holdTarget;

                // 中断条件：対象喪失 / 視線を外した / ボタン解放 / 実行不可化
                var stillAimed = ctx._interactionDetector != null
                                 && ctx._interactionDetector.TryGetActionable(out var current)
                                 && current == target;
                if (target == null || !stillAimed || !ctx.Player.Interact.IsPressed() || !target.CanInteract())
                {
                    StateMachine.Transition(StateEvent.EndInteract);
                    return;
                }

                ctx._holdElapsed += Time.deltaTime;

                // 進捗ゲージへ反映（HoldSeconds=0 はゼロ除算回避で満充填扱い）
                var holdSeconds = target.HoldSeconds;
                target.SetHoldProgress(CalculateHoldProgress(ctx._holdElapsed, holdSeconds));

                if (ctx._holdElapsed >= holdSeconds)
                {
                    target.Interact();
                    StateMachine.Transition(StateEvent.EndInteract);
                }
            }

            // 水平移動なし＝拘束（重力のみ適用）
            public override void FixedUpdate() => Context.ApplyMovementWithGravity(Vector3.zero);

            public override void Exit()
            {
                Context._holdTarget?.SetHoldProgress(0f); // 中断・完了とも即非表示
                Context._holdTarget = null;
                Context._holdElapsed = 0f;
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

        /// <summary>
        /// しゃがみ姿勢を毎フレーム補間する。CharacterController の height/center とカメラ基準位置（ヘッドボブの rest 位置）を
        /// 補間値 _crouchBlend から導出する。カメラ rest 自体を下げることで <see cref="UpdateHeadBob"/> と自然に合成される
        /// （UpdateHeadBob より前に呼ぶこと）。
        /// </summary>
        private void UpdateCrouchPose()
        {
            // 目標 0/1 へ指数補間（フレームレート非依存）
            var target = _isCrouching ? 1f : 0f;
            var ease = 1f - Mathf.Exp(-_crouchTransitionSpeed * Time.deltaTime);
            _crouchBlend = Mathf.Lerp(_crouchBlend, target, ease);

            var height = Mathf.Lerp(_standHeight, _crouchHeight, _crouchBlend);

            // カプセル下端（= center.y - height/2）を立ち時と同じに固定し、足元を保ったまま頭だけ縮める
            var centerY = (height - _standHeight) * 0.5f;
            _characterController.height = height;
            var center = _characterController.center;
            center.y = centerY;
            _characterController.center = center;

            // カメラ rest を縮んだ分だけ下げる（ヘッドボブはこの rest を基準に揺れる）
            var eyeDrop = _standHeight - height;
            _cameraBasePosition = _standCameraBasePosition - new Vector3(0f, eyeDrop, 0f);
        }

        #endregion
    }
}
