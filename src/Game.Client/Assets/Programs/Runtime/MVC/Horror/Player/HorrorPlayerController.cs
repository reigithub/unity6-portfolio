using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Constants;
using Game.Horror.Equipment;
using Game.Horror.Interaction;
using Game.Horror.SaveData;
using Game.Horror.Services.Interfaces;
using Game.Horror.Signals;
using Game.Library.Shared;
using Game.Shared.Bootstrap;
using Game.Shared.Combat;
using Game.Shared.Constants;
using Game.Shared.Enums;
using Game.Shared.Extensions;
using Game.Shared.Input;
using Game.Shared.Scriptable.Database.Tables;
using Game.Shared.Services;
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
        [SerializeField] private int _playerId = 1;

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
        [SerializeField] private LayerMask _ceilingMask;

        [Header("回転速度（度/秒）")]
        [SerializeField] private float _lookRotationSpeed = 0.1f;

        [Header("エイム")]
        [Tooltip("エイム構え補間の応答速度（1-exp(-k・dt) の k）")]
        [SerializeField] private float _aimTransitionSpeed = 8f;

        [Tooltip("エイム中のカメラ回転速度倍率")]
        [SerializeField] private float _aimRotationMultiplier = 0.4f;

        [Tooltip("エイム中にカメラ揺れをゼロへ減衰させる秒数（解除時の復帰も同じ秒数）")]
        [SerializeField] private float _aimShakeFadeSeconds = 1f;

        [Header("インタラクション")]
        [Tooltip("インタラクト対象を検出する検出器（同一 Prefab 上にアタッチ）")]
        [SerializeField] private InteractionDetector _interactionDetector;

        [Header("攻撃（ハンドガン）")]
        [Tooltip("射撃 Raycast の対象レイヤー。敵＋遮蔽（壁）を含めること")]
        [SerializeField] private LayerMask _hitMask;

        [Tooltip("発砲カメラリコイルが収まるまでの秒数（減衰オフセット型・照準は元へ戻る）。発砲時に武器マスター値で上書きされる")]
        [SerializeField] private float _recoilRecoverSeconds = 0.25f;

        [Header("装備（武器モデル表示）")]
        [Tooltip("装備中武器の一人称モデルを表示するビュー（Camera/WeaponRoot にアタッチ）")]
        [SerializeField] private HorrorWeaponView _weaponView;

        [Tooltip("装備切替時のショートカット HUD（OverlayCanvas/Equipments にアタッチ）")]
        [SerializeField] private HorrorEquipmentsView _equipmentsView;

        [Tooltip("エイム連動レティクル（OverlayCanvas/Reticle にアタッチ）")]
        [SerializeField] private HorrorReticleView _reticleView;

        [Tooltip("残弾 HUD（OverlayCanvas/Ammo にアタッチ）")]
        [SerializeField] private HorrorAmmoView _ammoView;

        private bool _initialized;
        private IInputSystemService _inputService;
        private ProjectDefaultInputSystem.PlayerActions Player => _inputService.Player;

        private CharacterController _characterController;

        // ステートマシーン
        private StateMachine<HorrorPlayerController, StateEvent> _stateMachine;

        // 入力関連
        private Vector2 _moveValue;
        private Vector2 _lookValue;
        private float _speed;
        private bool _jumpTriggered;

        // インタラクト（起動入力フラグ／実行中の対象。経過時間は InteractingState ローカル）
        private bool _interactTriggered;
        private IInteractable _interactTarget;

        // 攻撃（ハンドガン）：マスター値・銃声発行サービス・起動入力フラグ（硬直経過は AttackingState ローカル）
        private HorrorWeaponMaster _weaponMaster;
        private IMessagePipeService _messagePipeService;
        private bool _attackTriggered;

        // 発砲カメラリコイル（減衰オフセット型）。強度・回復秒は発砲時点のマスター値をキャプチャし、表示 pitch にのみ合成する（照準の真値 _cameraVerticalAngle は変えない）
        private float _recoilPitchAmount;
        private float _recoilWeight;

        // 装備（ショートカット呼び出し）：セーブサービス（装備状態とショートカットを一元管理）・DB参照・起動入力フラグ・遷移時キャッシュ（硬直経過は EquippingState ローカル）
        private IHorrorEquipmentService _equipmentService;
        private IScriptableDatabaseService _dbService;
        private bool _equipTriggered;
        private int _equipSlotIndex;
        private InventorySlotType _pendingEquipType;
        private int _pendingEquipId;
        private HorrorWeaponMaster _pendingWeaponMaster;

        // リロード：インベントリセーブサービス（予備弾の所持数参照）・SE 再生サービス・起動入力フラグ（硬直経過は ReloadingState ローカル）
        private IHorrorInventoryService _inventoryService;
        private IAudioService _audioService;
        private bool _reloadTriggered;

        /// <summary>
        /// 弾切れ発砲時に自動でリロードを開始するか（将来オプション設定から反映する拡張点。既定 false）。
        /// </summary>
        public bool AutoReloadOnEmpty { get; set; }

        // 走り（トグル/ホールド切替）
        private bool _sprintToggle; // オプション値（false=ホールド, true=トグル）
        private bool _isSprinting;  // 走り状態

        // しゃがみ姿勢（移動ステートと直交する姿勢として保持）
        private bool _crouchToggle;   // オプション値（false=ホールド, true=トグル）
        private bool _isCrouching;    // 目標姿勢
        private float _crouchBlend;   // 0=立ち, 1=しゃがみ の実補間値（形状・カメラ高さの単一ソース）
        private float _standHeight;   // 立ち時の CharacterController 高さ（Initialize で実測）
        private const float CeilingCheckBuffer = 0.15f; // しゃがみ：立ち上がりに必要な頭上余裕（m）

        // エイム姿勢（移動ステートと直交する姿勢として保持）
        private bool _isAiming;          // HOLD 判定（二値: ダメージ・スプレッド・揺れ減衰の目標方向に使用）
        private float _aimBlend;         // 0=通常, 1=構え の実補間値（FOV・回転倍率・武器構え位置の単一ソース）
        private float _aimShakeWeight = 1f; // 1=通常揺れ, 0=無揺れ（エイムで線形減衰）
        private float _baseFov;          // オプション由来の基準 FOV（エイムズームの基準）

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

        // ヘッドボブ/アイドルスウェイ調整値（HorrorPlayerMaster から Initialize で上書き。初期値はマスター欠落時のフォールバック）
        private float _bobWalkAmplitude = 0.04f;   // 歩き：縦位置振幅（m）
        private float _bobRunAmplitude = 0.06f;    // 走り：縦位置振幅（m）
        private float _bobWalkSpeed = 10f;         // 歩き：位相速度 rad/s（ゆっくり）
        private float _bobRunSpeed = 15f;          // 走り：位相速度 rad/s（少しだけ速い）
        private float _bobHorizontalRatio = 0.5f;  // 横位置/縦位置 比
        private float _bobWalkRoll = 0.05f;         // 歩き：ロール角（度）＝知覚される横揺れ
        private float _bobRunRoll = 0.1f;          // 走り：ロール角（度）
        private float _bobAmplitudeResponse = 10f; // 強度イーズの応答

        private float _idleSwaySpeed = 1.2f;       // アイドル：位相速度 rad/s（呼吸 ~5秒周期）
        private float _idleSwayAmplitude = 0.05f;  // アイドル：縦位置振幅（m, ヘッドボブより小）
        private float _idleSwayRoll = 0.01f;       // アイドル：ロール角（度, 小）

        // 足音調整値（HorrorPlayerMaster から Initialize で上書き。初期値はマスター欠落時のフォールバック）
        private float _footstepStride = 1.25f;       // 1歩とみなす移動距離（m）
        private float _footstepWalkLoudness = 0.5f;  // 歩き足音の Loudness（HearingRadius 30 × 0.5 = 15m 到達）
        private float _footstepRunLoudness = 1f;     // 走り足音の Loudness（30m 到達）
        private string _footstepSeAssetName = "";    // 足音 SE アセット名（空=再生しない）

        // 足音の歩幅積算（m）。しゃがみ・非接地・入力ブロック中はリセット
        private float _footstepAccumulatedDistance;

        public void Initialize(HorrorOptionSaveData data)
        {
            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _inputService.EnablePlayer();

            _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
            _audioService = GameServiceManager.Resolve<IAudioService>();

            // Database はプレイヤー生成時点でロード済み
            _dbService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            _equipmentService = GameServiceManager.Resolve<IHorrorEquipmentService>();
            _inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();

            ApplyPlayerMaster();

            _equipmentsView.Initialize();

            // 装備状態をセーブデータから復元。未装備なら _weaponMaster は null のまま（TryAttack の既存 null ガードで攻撃不可）
            if (_equipmentService.TryGetEquipped(out var slotType, out var id)
                && slotType == InventorySlotType.Weapon
                && _dbService.Database.HorrorWeaponMasterTable.TryFindById(id, out var weaponMaster))
            {
                _weaponMaster = weaponMaster;
            }

            // 武器モデル表示ビューの初期化：装備中なら即座に表示し、ショートカット登録武器のモデルは事前ロードしておく
            _weaponView.Initialize();
            if (_weaponMaster != null) _weaponView.ShowImmediate(_weaponMaster);

            _weaponView.PreloadAsync(ResolveEquippableMasters()).Forget();

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
                    , Player.Interact.OnPerformedAsObservable()
                    , Player.Jump.OnPerformedAsObservable()
                    , Player.Crouch.OnPerformedAsObservable()
                    , Player.Sprint.OnPerformedAsObservable()
                    , Player.Equip.OnPerformedAsObservable()
                    , Player.Fire.OnPerformedAsObservable()
                    , Player.Aim.OnPerformedAsObservable()
                    , Player.Reload.OnPerformedAsObservable()
                    )
                .Subscribe(_ => ApplicationEvents.HideCursor())
                .AddTo(this);

            _initialized = true;
        }

        private void ApplyPlayerMaster()
        {
            // プレイヤー調整値をマスターデータで上書き（SerializeField/既定値はマスター欠落時のフォールバック）
            if (_dbService.Database.HorrorPlayerMasterTable.TryFindById(_playerId, out var playerMaster))
            {
                _walkSpeed = playerMaster.WalkSpeed;
                _runSpeed = playerMaster.RunSpeed;
                _jump = playerMaster.Jump;
                _gravity = playerMaster.Gravity;
                _crouchSpeed = playerMaster.CrouchSpeed;
                _crouchHeight = playerMaster.CrouchHeight;
                _crouchTransitionSpeed = playerMaster.CrouchTransitionSpeed;
                _lookRotationSpeed = playerMaster.LookRotationSpeed;
                _aimTransitionSpeed = playerMaster.AimTransitionSpeed;
                _aimRotationMultiplier = playerMaster.AimRotationMultiplier;
                _aimShakeFadeSeconds = playerMaster.AimShakeFadeSeconds;
                _bobWalkAmplitude = playerMaster.BobWalkAmplitude;
                _bobRunAmplitude = playerMaster.BobRunAmplitude;
                _bobWalkSpeed = playerMaster.BobWalkSpeed;
                _bobRunSpeed = playerMaster.BobRunSpeed;
                _bobHorizontalRatio = playerMaster.BobHorizontalRatio;
                _bobWalkRoll = playerMaster.BobWalkRoll;
                _bobRunRoll = playerMaster.BobRunRoll;
                _bobAmplitudeResponse = playerMaster.BobAmplitudeResponse;
                _idleSwaySpeed = playerMaster.IdleSwaySpeed;
                _idleSwayAmplitude = playerMaster.IdleSwayAmplitude;
                _idleSwayRoll = playerMaster.IdleSwayRoll;
                _footstepStride = playerMaster.FootstepStride;
                _footstepWalkLoudness = playerMaster.FootstepWalkLoudness;
                _footstepRunLoudness = playerMaster.FootstepRunLoudness;
                _footstepSeAssetName = playerMaster.FootstepSeAssetName;
            }
            else
            {
                Debug.LogError($"HorrorPlayerMaster が見つかりません Id={_playerId}。Inspector/既定値で継続します。", this);
            }
        }

        public void ApplyOptions(HorrorOptionSaveData data)
        {
            _lookInvertX = data.CameraControlHorizontal ? -1f : 1f;
            _lookInvertY = data.CameraControlVertical ? -1f : 1f;

            _lookSensitivityX = data.CameraSensitivityHorizontal;
            _lookSensitivityY = data.CameraSensitivityVertical;

            _lookAcceleration = data.CameraAcceleration;
            _cameraShake = data.CameraShake;
            _baseFov = data.CameraFov;
            ApplyFov();

            // OnSaved でランタイム再適用される。カメラ基準位置は触らない（しゃがみ中のリセット防止）
            _sprintToggle = data.SprintToggle;
            _crouchToggle = data.CrouchToggle;
        }

        /// <summary>
        /// プレイヤーを指定位置・向きへ即時移動する。有効な CharacterController への transform 書き換えは
        /// 次の Move で内部衝突状態と矛盾しうるため、一時的に無効化してから反映し、物理へ即時同期する。
        /// _characterController を参照するため Initialize 後に呼ぶこと。
        /// </summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            var wasEnabled = _characterController.enabled;
            _characterController.enabled = false;

            transform.SetPositionAndRotation(position, rotation);

            _characterController.enabled = wasEnabled;
            Physics.SyncTransforms();
        }

        #region MonoBehaviour Methods

        protected void OnDestroy()
        {
            if (_initialized) _inputService.DisablePlayer();
        }

        protected void Update()
        {
            if (!_initialized) return;
            UpdateInput();
            _stateMachine?.Update();
        }

        protected void FixedUpdate()
        {
            if (!_initialized) return;
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

            // 身体占有（インタラクト）中は移動・他アクションを受け付けない
            var restrained = _stateMachine.IsProcessing() && _stateMachine.IsCurrentState<InteractingState>();

            // リロード硬直中は攻撃・インタラクト・リロード再入力を受け付けない（完了後の遅延発火も禁止するため、フラグ自体を立てない）
            var reloading = _stateMachine.IsProcessing() && _stateMachine.IsCurrentState<ReloadingState>();

            if (!restrained)
            {
                // しゃがみ入力（モード別）。移動速度が姿勢に依存するため先に確定させる
                UpdateCrouchInput();
                // エイム入力（HOLD）。走りがエイム状態を参照するため先に確定させる
                UpdateAimInput();
                // 走り入力（モード別）。しゃがみ状態が確定した後に判定する
                UpdateSprintInput();
                // インタラクト起動入力：フラグを立てるのみ。実際の起動・遷移は Idle/Moving ステートが行う
                if (!reloading) UpdateInteractInput();
            }
            else
            {
                _isAiming = false;
            }

            // 移動速度更新（拘束中は 0、しゃがみ中は crouchSpeed 優先、それ以外は _isSprinting で走り/歩き）
            if (restrained)
            {
                _speed = 0f;
            }
            else
            {
                var baseSpeed = _isCrouching ? _crouchSpeed : (_isSprinting ? _runSpeed : _walkSpeed);
                _speed = _moveValue.magnitude * baseSpeed;
            }

            // ジャンプ入力受付（拘束中は不可）
            if (!restrained && Player.Jump.WasPressedThisFrame() && CanJump())
            {
                _jumpTriggered = true;
            }

            // 攻撃（射撃）起動入力：フラグを立てるのみ。実際の起動・遷移は Idle/Moving ステートが行う
            if (!restrained && !reloading && Player.Fire.WasPressedThisFrame() && IsGrounded())
            {
                _attackTriggered = true;
            }

            // 装備切替起動入力：方向からスロット index を解決してフラグを立てるのみ。実際の起動・遷移は Idle/Moving ステートが行う
            if (!restrained && Player.Equip.WasPressedThisFrame() && IsGrounded())
            {
                var index = ResolveEquipSlotIndex(Player.Equip.ReadValue<Vector2>());
                if (index >= 0)
                {
                    _equipTriggered = true;
                    _equipSlotIndex = index;
                }
            }

            // リロード起動入力：フラグを立てるのみ。実際の起動・遷移は Idle/Moving ステートが行う
            if (!restrained && !reloading && Player.Reload.WasPressedThisFrame() && IsGrounded())
            {
                _reloadTriggered = true;
            }
        }

        private bool CanJump()
        {
            if (!_stateMachine.IsProcessing())
                return false;

            // Idle/Moving状態でのみジャンプ可能（しゃがみ中は不可）
            var canJumpFromState = _stateMachine.IsCurrentState<IdleState>() ||
                                   _stateMachine.IsCurrentState<MovingState>();

            return canJumpFromState && IsGrounded() && !_isCrouching && !_isAiming;
        }

        /// <summary>
        /// 立てられた起動入力フラグを消費し、インタラクト対象があれば保持して遷移要否を返す。
        /// Idle/Moving ステートの Update から呼ばれ、実際の実行（可否判定・効果・拒否メッセージ）は
        /// 入力タイプを問わず InteractingState 内で一括処理する。
        /// </summary>
        /// <returns>対象を保持し InteractingState へ遷移すべきなら true。</returns>
        private bool TryInteraction()
        {
            if (!_interactTriggered)
                return false;

            _interactTriggered = false;

            if (_interactionDetector == null || !_interactionDetector.TryGetTarget(out var target))
                return false;

            // 可否・InputType を問わず、対象があればインタラクトステートで一括処理する
            _interactTarget = target;
            return true;
        }

        /// <summary>
        /// 立てられた射撃起動フラグを消費し、武器マスターがあれば AttackingState へ遷移すべきと返す。
        /// Idle/Moving ステートの Update から呼ばれ、実際の発砲は AttackingState.Enter が行う。
        /// 弾切れ時は空撃ち（硬直なし）として処理し遷移しない。
        /// </summary>
        /// <returns>AttackingState へ遷移すべきなら true。</returns>
        private bool TryAttack()
        {
            if (!_attackTriggered)
                return false;

            _attackTriggered = false;

            if (_weaponMaster == null)
                return false;

            // 弾切れは空撃ち（ステート遷移なし＝硬直なし）。AmmoItemId=0 の武器は弾薬概念なし（無限）
            if (_weaponMaster.AmmoItemId > 0
                && _equipmentService.GetMagazineCount(_weaponMaster.Id, _weaponMaster.MagazineSize) <= 0)
            {
                HandleDryFire();
                return false;
            }

            return true;
        }

        /// <summary>
        /// 空撃ちを処理する。SE（マスターにアセット名がある場合のみ）と HUD の表示キックを行い、
        /// 自動リロードが有効ならリロード起動フラグを立てる（同フレームの TryReload が消費する）。
        /// </summary>
        private void HandleDryFire()
        {
            if (!string.IsNullOrEmpty(_weaponMaster.DryFireSeAssetName))
                _audioService.PlaySoundEffectOneShotAsync(_weaponMaster.DryFireSeAssetName, destroyCancellationToken).Forget();

            _ammoView?.Notify();

            if (AutoReloadOnEmpty) _reloadTriggered = true;
        }

        /// <summary>
        /// 立てられた装備切替起動フラグを消費し、ショートカット登録・現在装備・所持を検証して
        /// EquippingState へ遷移すべきかを判定する。Idle/Moving ステートの Update から呼ばれ、
        /// 実際の装備反映（<see cref="IHorrorEquipmentService.TryEquip"/>）は EquippingState.Enter が行う。
        /// </summary>
        /// <returns>EquippingState へ遷移すべきなら true。</returns>
        private bool TryEquip()
        {
            if (!_equipTriggered)
                return false;

            _equipTriggered = false;

            // 空スロット（未登録）は無操作
            if (!_equipmentService.TryGetSlot(_equipSlotIndex, out var slot))
                return false;

            // 現在装備と同一スロットの再指定は無操作（要件1）
            if (_equipmentService.TryGetEquipped(out var currentType, out var currentId)
                && currentType == slot.SlotType && currentId == slot.Id)
                return false;

            // Weapon 限定・所持検証。不成立なら硬直を発生させない
            if (!_equipmentService.CanEquip(slot.SlotType, slot.Id))
                return false;

            if (!_dbService.Database.HorrorWeaponMasterTable.TryFindById(slot.Id, out var weaponMaster))
                return false;

            _pendingEquipType = slot.SlotType;
            _pendingEquipId = slot.Id;
            _pendingWeaponMaster = weaponMaster;
            return true;
        }

        /// <summary>
        /// 立てられたリロード起動フラグを消費し、弾薬を使う武器で弾倉に空きがあり予備弾を所持している場合に
        /// ReloadingState へ遷移すべきと返す。Idle/Moving ステートの Update から呼ばれ、
        /// 実際の装填（弾倉回復・予備消費）は ReloadingState が硬直消化後に行う。予備 0 は無反応（拒否演出なし）。
        /// </summary>
        /// <returns>ReloadingState へ遷移すべきなら true。</returns>
        private bool TryReload()
        {
            if (!_reloadTriggered)
                return false;

            _reloadTriggered = false;

            if (_weaponMaster == null || _weaponMaster.AmmoItemId <= 0 || _weaponMaster.MagazineSize <= 0)
                return false;

            if (_equipmentService.GetMagazineCount(_weaponMaster.Id, _weaponMaster.MagazineSize) >= _weaponMaster.MagazineSize)
                return false;

            if (_inventoryService.GetCount(InventorySlotType.Item, _weaponMaster.AmmoItemId) <= 0)
                return false;

            return true;
        }

        /// <summary>
        /// ショートカット4スロット＋現在装備中の Weapon をマスター解決し、武器モデルの事前ロード対象として列挙する。
        /// 同一 Id は重複排除する。
        /// </summary>
        private List<HorrorWeaponMaster> ResolveEquippableMasters()
        {
            var masters = new List<HorrorWeaponMaster>();
            var seenIds = new HashSet<int>();

            for (var i = 0; i < HorrorEquipmentConstants.MaxEquipmentSlotCount; i++)
            {
                if (_equipmentService.TryGetSlot(i, out var slot)
                    && slot.SlotType == InventorySlotType.Weapon
                    && seenIds.Add(slot.Id)
                    && _dbService.Database.HorrorWeaponMasterTable.TryFindById(slot.Id, out var slotMaster))
                {
                    masters.Add(slotMaster);
                }
            }

            if (_equipmentService.TryGetEquipped(out var equippedType, out var equippedId)
                && equippedType == InventorySlotType.Weapon
                && seenIds.Add(equippedId)
                && _dbService.Database.HorrorWeaponMasterTable.TryFindById(equippedId, out var equippedMaster))
            {
                masters.Add(equippedMaster);
            }

            return masters;
        }

        /// <summary>
        /// Hold 長押しの進捗（0→1）を算出する。<paramref name="holdSeconds"/> が 0 以下なら
        /// ゼロ除算を避けて即時完了（1）とみなす。表示側で Clamp されるため、
        /// elapsed が holdSeconds を超えた最終フレームでは 1 を超える生値を返しうる。
        /// </summary>
        internal static float CalculateHoldProgress(float elapsed, float holdSeconds)
            => holdSeconds > 0f ? elapsed / holdSeconds : 1f;

        /// <summary>
        /// D-Pad / 2DVector composite の方向入力からショートカットスロット index (0-3) を解決する。
        /// 閾値 0.5 を両軸とも超える（斜め）入力は判定不能として -1 を返す。
        /// スロット並びは 1=左(0) / 2=上(1) / 3=右(2) / 4=下(3)。
        /// </summary>
        internal static int ResolveEquipSlotIndex(Vector2 value)
        {
            const float threshold = 0.5f;
            var xExceeds = Mathf.Abs(value.x) > threshold;
            var yExceeds = Mathf.Abs(value.y) > threshold;

            if (xExceeds && yExceeds) return -1; // 斜め入力は無視

            if (xExceeds) return value.x < 0f ? 0 : 2; // left / right
            if (yExceeds) return value.y > 0f ? 1 : 3; // up / down

            return -1;
        }

        /// <summary>
        /// 拡散角の範囲内でランダムに逸れた射撃方向を算出する（FPS Microgame の
        /// GetShotDirectionWithinSpread と同式）。<paramref name="spreadAngle"/> 0 で forward のまま。
        /// </summary>
        internal static Vector3 CalculateShotDirection(Vector3 forward, Vector3 randomUnit, float spreadAngle)
            => Vector3.Slerp(forward, randomUnit, spreadAngle / 180f);

        /// <summary>
        /// リコイルオフセット（跳ね上げ＝pitch 減算）を合成した表示用 pitch を算出する（±89° クランプ込み）。
        /// 照準の真値には加算しないため、減衰が終われば照準は発砲前の位置へ戻る。
        /// </summary>
        internal static float CalculateRecoiledPitch(float pitch, float recoilPitch, float recoilWeight)
            => Mathf.Clamp(pitch - recoilPitch * recoilWeight, -89f, 89f);

        /// <summary>
        /// エイム状態を加味した射撃ダメージを算出する。エイム中は倍率を掛けて四捨五入する。
        /// </summary>
        internal static int CalculateAimedDamage(int baseDamage, bool isAiming, float aimDamageMultiplier)
            => isAiming ? Mathf.RoundToInt(baseDamage * aimDamageMultiplier) : baseDamage;

        /// <summary>
        /// リロードの装填数（=予備弾の消費数）を算出する。弾倉の不足分と予備所持数の小さい方。満タン・予備 0 は 0。
        /// </summary>
        internal static int CalculateReloadAmount(int magazine, int magazineSize, int reserve)
            => Mathf.Max(0, Mathf.Min(magazineSize - magazine, reserve));

        /// <summary>
        /// 足音の歩幅積算を1ステップ進め、1歩分（stride）到達の発火判定と次の積算値を算出する。
        /// stride 到達時は超過分のみ持ち越す（1 物理フレームで複数歩分を移動しても発火は1回に
        /// 集約し、剰余は [0, stride) に収まる）。stride が 0 以下なら無限発火を避けて発火しない。
        /// </summary>
        internal static (bool Fired, float Next) StepFootstep(float accumulated, float movedDistance, float stride)
        {
            if (stride <= 0f) return (false, 0f);

            var total = accumulated + movedDistance;
            return (total >= stride, Mathf.Repeat(total, stride));
        }

        /// <summary>
        /// 足音の Loudness を決定する。走り中は走り値、歩き（エイム歩行含む）は歩き値。
        /// しゃがみ中の無音は UpdateFootstep の積算ガード（Publish と SE を両方止める唯一の地点）で保証する。
        /// </summary>
        internal static float CalculateFootstepLoudness(bool isRunning, float walkLoudness, float runLoudness)
            => isRunning ? runLoudness : walkLoudness;

        private bool IsGrounded() => _characterController.isGrounded;
        private bool IsMoving() => _speed > 0f;
        private bool IsWalking() => _speed >= _walkSpeed && _speed < _runSpeed;
        private bool IsRunning() => _speed >= _runSpeed;

        private bool IsMoveInput() => _moveValue.magnitude > InputConstants.InputThreshold;
        private bool IsLookInput() => _lookValue.magnitude > InputConstants.InputThreshold;

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
        /// エイム入力（HOLD）を処理する。武器未装備では構えられない。
        /// 装備切替の硬直中は強制解除する（HOLD 継続なら硬直明けに自動で再エイムされる）。
        /// </summary>
        private void UpdateAimInput()
        {
            _isAiming = Player.Aim.IsPressed()
                        && _weaponMaster != null
                        && !(_stateMachine.IsProcessing() && _stateMachine.IsCurrentState<EquippingState>());
        }

        /// <summary>
        /// 走り入力をモード別に処理する。しゃがみ中は走れない（トグル状態も解除）。
        /// トグル時は押下で反転し、移動を止めると解除する。
        /// </summary>
        private void UpdateSprintInput()
        {
            // しゃがみ中は走れない（トグル状態も強制解除）
            if (_isCrouching) { _isSprinting = false; return; }

            // エイム中は走れない（トグル状態も強制解除）
            if (_isAiming) { _isSprinting = false; return; }

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
                _ceilingMask,
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

            _stateMachine.AddTransition<IdleState, AttackingState>(StateEvent.Attack);
            _stateMachine.AddTransition<MovingState, AttackingState>(StateEvent.Attack);
            _stateMachine.AddTransition<AttackingState, IdleState>(StateEvent.EndAttack);

            _stateMachine.AddTransition<IdleState, EquippingState>(StateEvent.Equip);
            _stateMachine.AddTransition<MovingState, EquippingState>(StateEvent.Equip);
            _stateMachine.AddTransition<EquippingState, IdleState>(StateEvent.EndEquip);

            _stateMachine.AddTransition<IdleState, ReloadingState>(StateEvent.Reload);
            _stateMachine.AddTransition<MovingState, ReloadingState>(StateEvent.Reload);
            _stateMachine.AddTransition<ReloadingState, IdleState>(StateEvent.EndReload);

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
            Attack, // 攻撃開始: Idle/Moving → Attacking
            EndAttack, // 攻撃終了（発射間隔経過）: Attacking → Idle
            Equip, // 装備切替開始: Idle/Moving → Equipping
            EndEquip, // 装備切替終了（EquipDuration経過）: Equipping → Idle
            Reload, // リロード開始: Idle/Moving → Reloading
            EndReload, // リロード終了（ReloadDuration経過）: Reloading → Idle
        }

        private class IdleState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // インタラクト起動チェック
                if (ctx.TryInteraction())
                {
                    StateMachine.Transition(StateEvent.Interact);
                    return;
                }

                // 攻撃（射撃）起動チェック
                if (ctx.TryAttack())
                {
                    StateMachine.Transition(StateEvent.Attack);
                    return;
                }

                // 装備切替起動チェック
                if (ctx.TryEquip())
                {
                    StateMachine.Transition(StateEvent.Equip);
                    return;
                }

                // リロード起動チェック
                if (ctx.TryReload())
                {
                    StateMachine.Transition(StateEvent.Reload);
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
                Context.UpdateMovementWithGravity(Vector3.zero);
            }
        }

        private class MovingState : State<HorrorPlayerController, StateEvent>
        {
            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

                // ジャンプ入力チェック
                if (ctx._jumpTriggered && ctx.IsGrounded())
                {
                    StateMachine.Transition(StateEvent.Jump);
                    return;
                }

                // インタラクト起動チェック
                if (ctx.TryInteraction())
                {
                    StateMachine.Transition(StateEvent.Interact);
                    return;
                }

                // 攻撃（射撃）起動チェック
                if (ctx.TryAttack())
                {
                    StateMachine.Transition(StateEvent.Attack);
                    return;
                }

                // 装備切替起動チェック
                if (ctx.TryEquip())
                {
                    StateMachine.Transition(StateEvent.Equip);
                    return;
                }

                // リロード起動チェック
                if (ctx.TryReload())
                {
                    StateMachine.Transition(StateEvent.Reload);
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
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
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
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

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
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        /// <summary>
        /// インタラクト実行中の身体占有状態。視点回転とエイム解除の補間のみ許可し水平移動は止める。
        /// 入力タイプを問わず、拒否メッセージ／単発・トグル／長押しを 1 本の非同期シーケンスで処理する。
        /// </summary>
        private class InteractingState : State<HorrorPlayerController, StateEvent>
        {
            private bool _completed;

            public override void Enter()
            {
                _completed = false;
                RunAsync(Context._interactTarget).Forget();
            }

            public override void Update()
            {
                Context.UpdateRotation(); // 拘束中は視点回転とエイム解除の補間のみ許可
                Context.UpdateAimPose();
                if (_completed) StateMachine.Transition(StateEvent.EndInteract);
            }

            // 水平移動なし＝拘束（重力のみ適用）
            public override void FixedUpdate() => Context.UpdateMovementWithGravity(Vector3.zero);

            public override void Exit()
            {
                var ctx = Context;
                ctx._interactTarget?.SetHoldProgress(0f); // 中断・完了とも即非表示
                ctx._interactTarget = null;
                _completed = false;
            }

            // 1 回のインタラクトを開始～効果発火まで逐次処理する。
            // 拒否（メッセージ）／単発・トグル（即時）／長押し（進捗）を 1 本のフローで扱う。
            private async UniTask RunAsync(IInteractable target)
            {
                if (!target.CanInteract())
                {
                    await target.TryShowRejectionMessage();
                }
                else if (target.InputType == InteractionInputType.Hold)
                {
                    await RunHoldAsync(target);
                }
                else
                {
                    target.Interact();
                }

                _completed = true;
            }

            private async UniTask RunHoldAsync(IInteractable target)
            {
                var ctx = Context;
                var elapsed = 0f;
                target.SetHoldProgress(0f);

                while (true)
                {
                    // 中断条件：対象喪失 / 視線を外した / ボタン解放 / 実行不可化
                    var stillAimed = ctx._interactionDetector != null
                                     && ctx._interactionDetector.TryGetTarget(out var current)
                                     && current == target;
                    if (!stillAimed || !ctx.Player.Interact.IsPressed() || !target.CanInteract())
                        return;

                    elapsed += Time.deltaTime;
                    target.SetHoldProgress(CalculateHoldProgress(elapsed, target.HoldSeconds));

                    if (elapsed >= target.HoldSeconds)
                    {
                        target.Interact();
                        return;
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }
        }

        /// <summary>
        /// 射撃実行中の状態。Enter で 1 発発砲し、FireInterval（武器マスター）の間は移動・視点を許可しつつ
        /// 次弾の発射を待たせる（発射レート制限）。間隔を消化したら Idle へ戻る。
        /// </summary>
        private class AttackingState : State<HorrorPlayerController, StateEvent>
        {
            private float _elapsed;

            public override void Enter()
            {
                // インスタンスはキャッシュ再利用されるため経過時間を必ずリセット
                _elapsed = 0f;
                Context.Fire();
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();
                ctx.UpdateAimPose();

                _elapsed += Time.deltaTime;
                if (_elapsed >= ctx.GetFireInterval())
                    StateMachine.Transition(StateEvent.EndAttack);
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        /// <summary>
        /// 装備切替実行中の状態。Enter で装備をセーブデータへ反映し、EquipDuration（武器マスター）の間は
        /// 移動・視点を許可しつつ硬直として滞在する。滞在秒を消化したら Idle へ戻る。
        /// </summary>
        private class EquippingState : State<HorrorPlayerController, StateEvent>
        {
            private float _elapsed;

            public override void Enter()
            {
                // インスタンスはキャッシュ再利用されるため経過時間を必ずリセット
                _elapsed = 0f;

                var ctx = Context;
                if (ctx._equipmentService.TryEquip(ctx._pendingEquipType, ctx._pendingEquipId))
                {
                    ctx._weaponMaster = ctx._pendingWeaponMaster;
                    ctx._weaponView.BeginSwitch(ctx._pendingWeaponMaster);
                    ctx._equipmentsView.Show(ctx._pendingEquipType, ctx._pendingEquipId);
                    Debug.Log($"{ctx._weaponMaster.Name}");
                }
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();

                _elapsed += Time.deltaTime;
                ctx._weaponView.TickSwitch(_elapsed, ctx._pendingWeaponMaster.EquipDuration);
                ctx.UpdateAimPose(); // TickSwitch の後に呼ぶ（下げ量更新 → 位置反映の順序）
                if (_elapsed >= ctx._pendingWeaponMaster.EquipDuration)
                    StateMachine.Transition(StateEvent.EndEquip);
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }
        }

        /// <summary>
        /// リロード実行中の状態。ReloadDuration（武器マスター）の間、移動・視点・エイムを許可しつつ硬直として滞在し、
        /// 武器を傾ける演出を進める。滞在秒を消化した時点で装填（弾倉回復・予備消費）を適用して Idle へ戻る。
        /// 攻撃・ジャンプ・インタラクトの起動は入力側・遷移構造で禁止される。
        /// </summary>
        private class ReloadingState : State<HorrorPlayerController, StateEvent>
        {
            private float _elapsed;
            private float _duration;
            private bool _applied;

            public override void Enter()
            {
                var ctx = Context;
                // インスタンスはキャッシュ再利用されるため経過時間・適用済みフラグを必ずリセット
                _elapsed = 0f;
                _applied = false;
                _duration = ctx._weaponMaster.ReloadDuration;
                if (ctx._ammoView != null)
                    ctx._ammoView.Notify();
            }

            public override void Update()
            {
                var ctx = Context;
                ctx.UpdateRotation();
                ctx.UpdateCrouchPose();
                ctx.UpdateHeadBob();

                _elapsed += Time.deltaTime;
                ctx._weaponView.TickReload(_elapsed, _duration);
                ctx.UpdateAimPose(); // TickReload の後に呼ぶ（傾き量更新 → 反映の順序）

                if (!_applied && _elapsed >= _duration)
                {
                    _applied = true; // フレーム落ち・将来の中断遷移追加に対する二重適用防止
                    ctx.ApplyReload();
                    StateMachine.Transition(StateEvent.EndReload);
                }
            }

            public override void FixedUpdate()
            {
                var ctx = Context;
                ctx.UpdateMovementWithGravity(ctx.ComputeHorizontalVelocity());
            }

            public override void Exit()
            {
                // 中断・完了とも傾き演出を確実に解除する
                Context._weaponView.ResetReload();
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
        private void UpdateMovementWithGravity(Vector3 horizontalVelocity)
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
            var positionBeforeMove = transform.position;
            _characterController.Move(motion * Time.fixedDeltaTime);
            UpdateFootstep(positionBeforeMove);
        }

        /// <summary>
        /// 足音の歩幅積算を実移動距離（Move 前後の水平位置差分）で進め、1歩ごとに騒音発行＋SE再生する。
        /// しゃがみ・非接地・入力ブロック（ポーズ）中は積算をリセットして無音を保証する
        /// （しゃがみ→立ちの瞬間に持ち越し積算で即発火させないためのリセット）。
        /// 実移動ベースのため、壁押し付け（視覚上静止）や Teleport では発火しない。
        /// </summary>
        private void UpdateFootstep(Vector3 positionBeforeMove)
        {
            if (_isCrouching || !IsGrounded() || !Player.enabled)
            {
                _footstepAccumulatedDistance = 0f;
                return;
            }

            var delta = transform.position - positionBeforeMove;
            delta.y = 0f;

            var (fired, next) = StepFootstep(_footstepAccumulatedDistance, delta.magnitude, _footstepStride);
            if (fired)
                EmitFootstep();

            _footstepAccumulatedDistance = next;
        }

        /// <summary>
        /// 足音の副作用（騒音 Publish と SE 再生）。Loudness 0 以下は発行しない・SE 名が空なら再生しない
        /// （発砲ノイズ・発砲 SE と同じイディオム）。
        /// </summary>
        private void EmitFootstep()
        {
            var loudness = CalculateFootstepLoudness(IsRunning(), _footstepWalkLoudness, _footstepRunLoudness);
            if (loudness > 0f)
                _messagePipeService?.Publish(new HorrorSignals.Noise.Occurred(transform.position, loudness, NoiseType.Footstep));

            if (!string.IsNullOrEmpty(_footstepSeAssetName))
                _audioService.PlaySoundEffectOneShotAsync(_footstepSeAssetName, destroyCancellationToken).Forget();
        }

        // カメラ localEulerAngles へ書き込む際は常にこの表示用 pitch を使う（リコイル合成の単一点）
        private float GetDisplayPitch() => CalculateRecoiledPitch(_cameraVerticalAngle, _recoilPitchAmount, _recoilWeight);

        /// <summary>
        /// 視点回転を適用
        /// Yaw: Player 本体を Y 軸回転（カメラは子なので自動追従）
        /// Pitch: カメラ Transform の X 軸を localEulerAngles で回転、±89° クランプ
        /// </summary>
        private void UpdateRotation()
        {
            if (_mainCamera == null) return;

            // エイム中はカメラ回転を減速する（精密な狙いを支援）
            var aimMultiplier = Mathf.Lerp(1f, _aimRotationMultiplier, _aimBlend);

            // Yaw: Player 本体を Y 軸回転（感度H・反転を適用、入力は加速度スムージング後の値）
            var horizontalInput = _smoothedLookValue.x * _lookSensitivityX * _lookInvertX;
            transform.Rotate(0f, horizontalInput * _lookRotationSpeed * aimMultiplier, 0f, Space.Self);

            // Pitch: カメラの X 軸 localEulerAngles を更新、クランプ（既定 -y、感度V・反転を適用）
            var verticalInput = -_smoothedLookValue.y * _lookSensitivityY * _lookInvertY;
            _cameraVerticalAngle = Mathf.Clamp(_cameraVerticalAngle + verticalInput * _lookRotationSpeed * aimMultiplier, -89f, 89f);

            // 発砲リコイルの減衰（全ステートの Update から毎フレーム呼ばれるためここで駆動する）
            _recoilWeight = Mathf.MoveTowards(_recoilWeight, 0f, Time.deltaTime / Mathf.Max(_recoilRecoverSeconds, 0.0001f));

            _mainCamera.transform.localEulerAngles = new Vector3(GetDisplayPitch(), 0f, 0f);
        }

        /// <summary>
        /// カメラ揺れを適用。移動中は figure-8 ヘッドボブ、停止中はアイドルスウェイ（呼吸揺れ）をクロスフェードする。
        /// 全体強度は CameraShake でスケール。ApplyRotation 直後に呼ばれ、表示用 pitch（リコイル込み）を維持しつつ roll を合成する。
        /// </summary>
        private void UpdateHeadBob()
        {
            if (_mainCamera == null) return;

            // 入力ブロック中（ポーズ等）は neutral に戻す（Time.deltaTime=0 凍結による残オフセット防止）
            if (!Player.enabled)
            {
                _mainCamera.transform.localPosition = _cameraBasePosition;
                _mainCamera.transform.localEulerAngles = new Vector3(GetDisplayPitch(), 0f, 0f);
                _moveBobWeight = 0f;
                return;
            }

            // 接地して移動中のみヘッドボブ。停止でアイドルスウェイへクロスフェード。
            // ケイデンスは _speed 直結にせず歩き/走りで固定（走りは少しだけ速い）。
            var active = IsGrounded() && IsMoving();
            var running = IsRunning();

            var ease = 1f - Mathf.Exp(-_bobAmplitudeResponse * Time.deltaTime);
            _moveBobWeight = Mathf.Lerp(_moveBobWeight, active ? 1f : 0f, ease);

            if (active)
                _bobPhase += (running ? _bobRunSpeed : _bobWalkSpeed) * Time.deltaTime;
            _idlePhase += _idleSwaySpeed * Time.deltaTime; // アイドルは常時進む

            // ヘッドボブ（移動）：縦は位相、横はストライド（半周期）＝figure-8。横揺れの知覚はロールが主成分。
            var moveAmplitude = (running ? _bobRunAmplitude : _bobWalkAmplitude) * _moveBobWeight;
            var moveRoll = (running ? _bobRunRoll : _bobWalkRoll) * _moveBobWeight;
            var bobX = Mathf.Sin(_bobPhase * 0.5f) * moveAmplitude * _bobHorizontalRatio;
            var bobY = Mathf.Sin(_bobPhase) * moveAmplitude;
            var bobRoll = Mathf.Sin(_bobPhase * 0.5f) * moveRoll;

            // アイドルスウェイ（停止）：別周波数の遅い sin を重ねて有機的に
            var idleWeight = 1f - _moveBobWeight;
            var idleX = Mathf.Sin(_idlePhase * 1.3f) * _idleSwayAmplitude * _bobHorizontalRatio * idleWeight;
            var idleY = Mathf.Sin(_idlePhase) * _idleSwayAmplitude * idleWeight;
            var idleRoll = Mathf.Sin(_idlePhase * 0.7f) * _idleSwayRoll * idleWeight;

            // 合算 → 全体強度 CameraShake × エイム減衰（エイム中は _aimShakeWeight が 0 へ減衰）
            var offset = new Vector3(bobX + idleX, bobY + idleY, 0f) * _cameraShake * _aimShakeWeight;
            var roll = (bobRoll + idleRoll) * _cameraShake * _aimShakeWeight;

            _mainCamera.transform.localPosition = _cameraBasePosition + offset;
            _mainCamera.transform.localEulerAngles = new Vector3(GetDisplayPitch(), 0f, roll);
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

        /// <summary>
        /// エイム姿勢を毎フレーム補間する。FOV・揺れ減衰・武器構え位置・レティクル・残弾 HUD を _aimBlend / _aimShakeWeight から導出する。
        /// 各ステートの Update から呼ばれる（インタラクト拘束中も解除補間・FOV 復帰が必要なため
        /// InteractingState を含む全ステートが呼ぶ）。装備切替中は TickSwitch の後に呼ぶこと（下げ量更新 → 位置反映の順序）。
        /// </summary>
        private void UpdateAimPose()
        {
            if (_mainCamera == null || _weaponView == null) return;

            // 目標 0/1 へ指数補間（フレームレート非依存）
            var target = _isAiming ? 1f : 0f;
            var ease = 1f - Mathf.Exp(-_aimTransitionSpeed * Time.deltaTime);
            _aimBlend = Mathf.Lerp(_aimBlend, target, ease);

            ApplyFov();

            // カメラ揺れの重みを線形に減衰/復帰（_aimShakeFadeSeconds でゼロ/1 に到達）
            _aimShakeWeight = Mathf.MoveTowards(_aimShakeWeight, _isAiming ? 0f : 1f, Time.deltaTime / _aimShakeFadeSeconds);

            _weaponView.UpdatePose(_aimBlend);

            if (_reticleView != null)
                _reticleView.UpdatePose(_isAiming);

            UpdateAmmoHud();
        }

        /// <summary>
        /// 残弾 HUD を毎フレーム駆動する。表示内容（弾倉/予備・所持数のみ・非表示）と最新値をプル型で渡し、
        /// 値の変更検出と表示演出は View 側が担う。エイム中・リロード中は表示を維持する。
        /// </summary>
        private void UpdateAmmoHud()
        {
            if (_ammoView == null) return;

            var mode = HorrorAmmoView.ResolveViewMode(_weaponMaster != null, _weaponMaster?.AmmoItemId ?? 0);
            var keepVisible = _isAiming
                || (_stateMachine != null && _stateMachine.IsProcessing() && _stateMachine.IsCurrentState<ReloadingState>());

            var magazine = 0;
            var magazineSize = 0;
            var reserve = 0;
            switch (mode)
            {
                case HorrorAmmoViewMode.MagazineAndReserve:
                    magazineSize = _weaponMaster.MagazineSize;
                    magazine = _equipmentService.GetMagazineCount(_weaponMaster.Id, magazineSize);
                    reserve = _inventoryService.GetCount(InventorySlotType.Item, _weaponMaster.AmmoItemId);
                    break;
                case HorrorAmmoViewMode.CountOnly:
                    magazine = _inventoryService.GetCount(InventorySlotType.Weapon, _weaponMaster.Id); // 武器アイテム自体の所持数（例: Smoke）
                    break;
            }

            _ammoView.UpdatePose(mode, keepVisible, magazine, magazineSize, reserve);
        }

        /// <summary>
        /// カメラ FOV を基準 FOV とエイムズームの合成で適用する（唯一の FOV 書き込み点）。
        /// オプションのランタイム再適用がエイム中のズームを上書きしないよう、常に同一式で導出する。
        /// </summary>
        private void ApplyFov()
        {
            if (_mainCamera == null) return;
            var zoomRatio = _weaponMaster?.AimZoomRatio ?? 1f;
            _mainCamera.fieldOfView = Mathf.Lerp(_baseFov, _baseFov * zoomRatio, _aimBlend);
        }

        #endregion

        #region Combat

        /// <summary>
        /// カメラ中心からヒットスキャン（Raycast 即着弾）で射撃する。命中すれば IDamageable にダメージ、
        /// 発砲音 HorrorSignals.Noise.Occurred（Gunshot・射手位置）と着弾音（Object・命中点/外れたら射程端。誘引用）を発行する。
        /// あわせて武器キック・マズルフラッシュ・カメラリコイル・射撃音の発砲演出を発火する。
        /// </summary>
        private void Fire()
        {
            if (_mainCamera == null || _weaponMaster == null) return;

            var origin = _mainCamera.transform.position;
            var direction = _mainCamera.transform.forward;

            // 非エイム（腰だめ）射撃はわずかにランダム拡散する（エイム中はカメラ中心へ正確に飛ぶ）
            if (!_isAiming)
            {
                direction = CalculateShotDirection(direction, Random.insideUnitSphere, _weaponMaster.SpreadAngle);
            }

            IDamageable target = null;
            var impactPosition = origin + direction * _weaponMaster.Range;

            if (Physics.Raycast(origin, direction, out var hit, _weaponMaster.Range, _hitMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<IDamageable>();
                impactPosition = hit.point;
            }

            var damage = CalculateAimedDamage(_weaponMaster.Damage, _isAiming, _weaponMaster.AimDamageMultiplier);

            // 弾倉消費（AmmoItemId=0 の武器は弾薬概念なし・無限）
            if (_weaponMaster.AmmoItemId > 0)
            {
                var magazine = _equipmentService.GetMagazineCount(_weaponMaster.Id, _weaponMaster.MagazineSize);
                _equipmentService.SetMagazineCount(_weaponMaster.Id, magazine - 1);
                if (_ammoView != null) _ammoView.Notify();
            }

            // 命中対象があればダメージを与える。
            // ポップアップはダメージが実際に適用された時のみ。致死打で TakeDamage 後は IsDead=true になるため事前判定
            var damageApplied = target != null && !target.IsDead;
            target?.TakeDamage(damage);
            if (damageApplied) _messagePipeService?.Publish(new HorrorSignals.Combat.Damaged(hit.point, damage));

            // 騒音: 着弾音（着弾点・誘引用）→ 発砲音（射手位置）の順で発行する。
            // OnNoise の LastHeardPosition は後着優先のため、両方聞こえた敵には発砲音（射手位置）が優先される
            if (_weaponMaster.ImpactNoiseLoudness > 0f)
                _messagePipeService?.Publish(new HorrorSignals.Noise.Occurred(impactPosition, _weaponMaster.ImpactNoiseLoudness, NoiseType.Object));
            if (_weaponMaster.NoiseLoudness > 0f)
                _messagePipeService?.Publish(new HorrorSignals.Noise.Occurred(origin, _weaponMaster.NoiseLoudness, NoiseType.Gunshot));

            if (_reticleView != null) _reticleView.NotifyFired();

            // 発砲演出：武器ビュー（マズルフラッシュ＋キック）・カメラリコイル・射撃音
            if (_weaponView != null) _weaponView.NotifyFired();
            _recoilPitchAmount = _weaponMaster.RecoilCameraPitch;
            _recoilRecoverSeconds = _weaponMaster.RecoilRecoverSeconds;
            _recoilWeight = 1f;

            if (!string.IsNullOrEmpty(_weaponMaster.FireSeAssetName))
                _audioService.PlaySoundEffectOneShotAsync(_weaponMaster.FireSeAssetName, destroyCancellationToken).Forget();

            Debug.Log($"Weapon Fire: name->{_weaponMaster.Name} , damage->{damage}");
        }


        /// <summary>次弾までの発射間隔（AttackingState 滞在秒）。武器未設定なら 0。</summary>
        private float GetFireInterval() => _weaponMaster?.FireInterval ?? 0f;

        /// <summary>
        /// 装填を適用する。完了時点の弾倉・予備から装填数を再計算し、予備の消費に成功した場合のみ弾倉へ反映する
        /// （予備だけ減る・弾倉だけ増える不整合を防ぐ順序）。
        /// </summary>
        private void ApplyReload()
        {
            if (_weaponMaster == null || _weaponMaster.AmmoItemId <= 0) return;

            var magazineSize = _weaponMaster.MagazineSize;
            var magazine = _equipmentService.GetMagazineCount(_weaponMaster.Id, magazineSize);
            var reserve = _inventoryService.GetCount(InventorySlotType.Item, _weaponMaster.AmmoItemId);
            var amount = CalculateReloadAmount(magazine, magazineSize, reserve);

            if (amount <= 0) return;
            if (!_inventoryService.TryConsume(InventorySlotType.Item, _weaponMaster.AmmoItemId, amount)) return;

            _equipmentService.SetMagazineCount(_weaponMaster.Id, magazine + amount);
            _ammoView?.Notify();
        }

        #endregion
    }
}
