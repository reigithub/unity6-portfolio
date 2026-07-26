using Cysharp.Threading.Tasks;
using Game.Core.Services;
using Game.Horror.Dialogs;
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
    public partial class HorrorPlayerController : MonoBehaviour, IDamageable
    {
        [SerializeField] private Camera _mainCamera;

        [Header("しゃがみ")]
        [Tooltip("立ち上がり判定の対象レイヤー。プレイヤー自身のレイヤーは含めないこと")]
        [SerializeField] private LayerMask _ceilingMask;

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

        [Tooltip("HP ゲージ HUD（OverlayCanvas/Hp にアタッチ）")]
        [SerializeField] private HorrorHealthView _healthView;

        private bool _initialized;
        private IInputSystemService _inputService;
        private ProjectInputActions.PlayerActions Player => _inputService.Player;

        private IAudioService _audioService;
        private IMessagePipeService _messagePipeService;
        private IScriptableDatabaseService _dbService;
        private IHorrorEquipmentService _equipmentService;
        private IHorrorInventoryService _inventoryService;
        private IHorrorPlayerService _playerService;

        /// <summary>操作中プレイヤーの解決済みマスター（真実源＝プレイヤーサービス。null = 解決失敗）。</summary>
        private HorrorPlayerMaster PlayerMaster => _playerService.PlayerMaster;

        /// <summary>装備中武器の解決済みマスター（真実源＝装備サービス。null = 未装備）。</summary>
        private HorrorWeaponMaster EquippedWeaponMaster => _equipmentService.EquippedWeaponMaster;

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

        // 攻撃（ハンドガン）：起動入力フラグ（硬直経過は AttackingState ローカル）
        private bool _attackTriggered;

        // 発砲カメラリコイル（減衰オフセット型）。強度・回復秒は発砲時点のマスター値をキャプチャし、表示 pitch にのみ合成する（照準の真値 _cameraVerticalAngle は変えない）
        private float _recoilPitchAmount;
        private float _recoilWeight;

        // 装備（ショートカット呼び出し）：セーブサービス（装備状態とショートカットを一元管理）・DB参照・起動入力フラグ・遷移時キャッシュ（硬直経過は EquippingState ローカル）
        private bool _equipTriggered;
        private int _equipSlotIndex;
        private ObjectCategory _pendingEquipType;
        private int _pendingEquipId;
        private HorrorWeaponMaster _pendingWeaponMaster;

        // 装備（インベントリ Equip 予約呼び出し）：閉じたダイアログから (category, id) 直値で要求される。ショートカットの _equipTriggered とは別経路
        private bool _equipRequested;
        private ObjectCategory _requestedEquipCategory;
        private int _requestedEquipId;

        // アイテム使用（インベントリ Use 予約呼び出し）：閉じたダイアログから (category, id) 直値で要求される。遷移時キャッシュ（適用経過は UsingItemState ローカル）
        private bool _useItemRequested;
        private ObjectCategory _requestedUseCategory;
        private int _requestedUseId;
        private HorrorItemMaster _pendingUseItemMaster;

        // リロード：SE 再生サービス・起動入力フラグ（硬直経過は ReloadingState ローカル）
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
        private float _headBobPhase;
        private float _idlePhase;         // アイドルスウェイの常時位相
        private float _moveHeadBobWeight; // 0=停止, 1=移動（ease）。cameraShake とは分離
        private float _cameraShake = 1f;

        // 足音の歩幅積算（m）。しゃがみ・非接地・入力ブロック中はリセット
        private float _footstepAccumulatedDistance;

        private float _lastDamageTime = float.NegativeInfinity;  // 最終被弾時刻（Time.time）。負の無限大=未被弾

        // 死亡から GameOverDialog 表示までの演出ディレイ（ms）。被弾フラッシュ・SE を見せてから遷移する
        private const int GameOverDelayMilliseconds = 1200;

        public void Initialize(HorrorOptionSaveData data)
        {
            _playerService = GameServiceManager.Resolve<IHorrorPlayerService>();
            if (PlayerMaster == null) return;

            _inputService = GameServiceManager.Resolve<IInputSystemService>();
            _inputService.EnablePlayer(forceEnable: true);

            _messagePipeService = GameServiceManager.Resolve<IMessagePipeService>();
            _audioService = GameServiceManager.Resolve<IAudioService>();

            // Database はプレイヤー生成時点でロード済み
            _dbService = GameServiceManager.Resolve<IScriptableDatabaseService>();
            _equipmentService = GameServiceManager.Resolve<IHorrorEquipmentService>();
            _inventoryService = GameServiceManager.Resolve<IHorrorInventoryService>();

            // 残 HP をセーブデータから復元（0 以下=旧セーブ・新規データは Max へ正規化し、結果を書き戻す）
            ApplyHealth(NormalizeLoadedHealth(_playerService.CurrentHealth, _playerService.MaxHealth));

            // 武器モデルの復元に先立ち、セーブの装備記録からマスターを確定する
            _equipmentService.ResolveEquippedWeaponMaster();

            // 装備ショートカットビュー初期化
            _equipmentsView.Initialize();

            // 武器モデル表示ビューの初期化：装備中なら即座に表示し、ショートカット登録武器のモデルは事前ロードしておく
            // （マスターは上で確定済み。未装備なら null＝TryAttack の null ガードで攻撃不可）
            _weaponView.Initialize();
            var equippedWeapon = EquippedWeaponMaster;
            if (equippedWeapon != null) _weaponView.ShowImmediate(equippedWeapon);

            _weaponView.PreloadAsync(_equipmentService.GetEquippableWeaponMasters()).Forget();

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
            Observable.Merge(Player.Move.OnPerformedAsObservable(), Player.Look.OnPerformedAsObservable())
                .Subscribe(_ => ApplicationEvents.HideCursor())
                .AddTo(this);

            _initialized = true;
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

        /// <summary>
        /// インベントリの Equip アクションから装備を要求する。フラグと対象を記録するのみで、
        /// 検証・消費は Idle/Moving ステートの Update（<see cref="TryEquip"/>）が次フレーム以降に行う。
        /// </summary>
        /// <param name="category">装備対象のカテゴリ。</param>
        /// <param name="id">装備対象の ID。</param>
        public void RequestEquip(ObjectCategory category, int id)
        {
            _equipRequested = true;
            _requestedEquipCategory = category;
            _requestedEquipId = id;
        }

        /// <summary>
        /// インベントリの Use アクションからアイテム使用を要求する。フラグと対象を記録するのみで、
        /// 検証・消費は Idle/Moving ステートの Update（<see cref="TryUseItem"/>）が次フレーム以降に行う。
        /// </summary>
        /// <param name="category">使用対象のカテゴリ。</param>
        /// <param name="id">使用対象の ID。</param>
        public void RequestUseItem(ObjectCategory category, int id)
        {
            _useItemRequested = true;
            _requestedUseCategory = category;
            _requestedUseId = id;
        }

        #region IDamageable

        /// <summary>死亡フラグ（体力が 0 以下）</summary>
        public bool IsDead => _playerService.CurrentHealth <= 0;

        /// <summary>
        /// 残 HP をサービス（セーブデータ）へ書き込み、HUD 値反映を一括で行う（HP 書き込みの単一点）。
        /// 表示キック（Notify）は行わない（必要な呼び出し側のみが行う）。
        /// </summary>
        private void ApplyHealth(int newHealth)
        {
            _playerService.SetCurrentHealth(newHealth);
            if (_healthView != null)
                _healthView.UpdateHealth(newHealth, _playerService.MaxHealth);
        }

        /// <summary>
        /// ダメージを受ける。死亡中・無敵時間中は無視する。
        /// HUD 反映・被弾シグナル・SE は致死判定より先に行い、致死打でもゲージ 0 と演出を発火させる。
        /// 残 HP はセーブデータへ同期し、実書き込みはセーブポイントに委ねる。
        /// </summary>
        public void TakeDamage(int damage)
        {
            if (IsDead) return;
            if (IsInvincible(Time.time, _lastDamageTime, PlayerMaster.InvincibleSeconds)) return;

            _lastDamageTime = Time.time;
            ApplyHealth(CalculateDamagedHealth(_playerService.CurrentHealth, damage));
            if (_healthView != null)
                _healthView.Notify();

            _messagePipeService?.Publish(new HorrorSignals.Player.Damaged(damage, _playerService.CurrentHealth, _playerService.MaxHealth));

            if (!string.IsNullOrEmpty(PlayerMaster.DamageSeAssetName))
                _audioService.PlaySoundEffectOneShotAsync(PlayerMaster.DamageSeAssetName, destroyCancellationToken).Forget();

            if (IsDead)
            {
                // エネミー知覚の断絶用。DeadState 遷移（GameOver シーケンス起動）より先に
                // 同期配信で全エネミーへ死亡を通知する
                _messagePipeService?.Publish(new HorrorSignals.Player.Died(transform.position));

                if (_stateMachine != null && _stateMachine.IsProcessing())
                    _stateMachine.Transition(StateEvent.Dead);
            }
        }

        /// <summary>
        /// 死亡演出ディレイの後にゲームオーバーダイアログを起動する。
        /// ディレイ中のポーズ/インベントリ起動は BlockInputActions で防ぐ（表示後はダイアログ自身が同アクションをブロック）。
        /// シーン遷移によるプレイヤー破棄で起動前ならキャンセルされる（destroyCancellationToken）。
        /// </summary>
        private async UniTask RunGameOverAsync()
        {
            await UniTask.Delay(GameOverDelayMilliseconds, DelayType.UnscaledDeltaTime, cancellationToken: destroyCancellationToken);
            await HorrorGameOverDialog.RunAsync();
        }

        #endregion

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

            if (Player.Look.enabled)
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

            // 身体占有（インタラクト）中・死亡後は移動・他アクションを受け付けない
            var restrained = IsActiveState<InteractingState>() || IsActiveState<DeadState>();

            // リロード・アイテム使用の硬直中は攻撃・インタラクト・リロード再入力を受け付けない（完了後の遅延発火も禁止するため、フラグ自体を立てない）
            var actionLocked = IsActiveState<ReloadingState>() || IsActiveState<UsingItemState>();

            if (!restrained)
            {
                // しゃがみ入力（モード別）。移動速度が姿勢に依存するため先に確定させる
                UpdateCrouchInput();
                // エイム入力（HOLD）。走りがエイム状態を参照するため先に確定させる
                UpdateAimInput();
                // 走り入力（モード別）。しゃがみ状態が確定した後に判定する
                UpdateSprintInput();
                // インタラクト起動入力：フラグを立てるのみ。実際の起動・遷移は Idle/Moving ステートが行う
                if (!actionLocked) UpdateInteractInput();
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
                var baseSpeed = _isCrouching ? PlayerMaster.CrouchSpeed : (_isSprinting ? PlayerMaster.RunSpeed : PlayerMaster.WalkSpeed);
                _speed = _moveValue.magnitude * baseSpeed;
            }

            // ジャンプ入力受付（拘束中は不可）
            if (!restrained && Player.Jump.WasPressedThisFrame() && CanJump())
            {
                _jumpTriggered = true;
            }

            // 攻撃（射撃）起動入力：フラグを立てるのみ。実際の起動・遷移は Idle/Moving ステートが行う
            if (!restrained && !actionLocked && Player.Fire.WasPressedThisFrame() && IsGrounded())
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
            if (!restrained && !actionLocked && Player.Reload.WasPressedThisFrame() && IsGrounded())
            {
                _reloadTriggered = true;
            }
        }

        private bool CanJump()
        {
            // Idle/Moving状態でのみジャンプ可能（しゃがみ中は不可）
            var canJumpFromState = IsActiveState<IdleState>() || IsActiveState<MovingState>();

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

            var weapon = EquippedWeaponMaster;
            if (weapon == null)
                return false;

            // 弾切れは空撃ち（ステート遷移なし＝硬直なし）。AmmoItemId=0 の武器は弾薬概念なし（無限）
            if (weapon.AmmoItemId > 0
                && _equipmentService.GetMagazineCount(weapon.Id, weapon.MagazineSize) <= 0)
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
            var weapon = EquippedWeaponMaster;
            if (!string.IsNullOrEmpty(weapon.DryFireSeAssetName))
                _audioService.PlaySoundEffectOneShotAsync(weapon.DryFireSeAssetName, destroyCancellationToken).Forget();

            NotifyHudViews();

            if (AutoReloadOnEmpty) _reloadTriggered = true;
        }

        /// <summary>
        /// 立てられた装備切替起動フラグを消費し、共通検証（<see cref="TryPrepareEquip"/>）を経て
        /// EquippingState へ遷移すべきかを判定する。Idle/Moving ステートの Update から呼ばれ、
        /// 実際の装備反映（<see cref="IHorrorEquipmentService.TryEquip"/>）は EquippingState.Enter が行う。
        /// インベントリからの直接指定（<see cref="RequestEquip"/>）を優先消費し、ショートカット起動フラグは
        /// 同フレームの競合防止のため合わせて破棄する。
        /// </summary>
        /// <returns>EquippingState へ遷移すべきなら true。</returns>
        private bool TryEquip()
        {
            if (_equipRequested)
            {
                _equipRequested = false;
                _equipTriggered = false;
                return TryPrepareEquip(_requestedEquipCategory, _requestedEquipId);
            }

            if (!_equipTriggered)
                return false;

            _equipTriggered = false;

            // 空スロット（未登録）は無操作
            if (!_equipmentService.TryGetSlot(_equipSlotIndex, out var slot))
                return false;

            return TryPrepareEquip(slot.ObjectCategory, slot.Id);
        }

        /// <summary>
        /// 指定した装備対象について現在装備・所持を検証し、成立すれば EquippingState 遷移用のキャッシュへ
        /// ステージングする。<see cref="TryEquip"/> の各起動経路（インベントリ直接指定／ショートカット）に
        /// 共通の検証ロジック。
        /// </summary>
        /// <param name="category">装備対象のカテゴリ。</param>
        /// <param name="id">装備対象の ID。</param>
        /// <returns>検証に成功し EquippingState へ遷移すべきなら true。</returns>
        private bool TryPrepareEquip(ObjectCategory category, int id)
        {
            // 現在装備と同一スロットの再指定は無操作（要件1）
            if (_equipmentService.TryGetEquipped(out var currentType, out var currentId)
                && currentType == category && currentId == id)
                return false;

            // Weapon 限定・所持検証。不成立なら硬直を発生させない
            if (!_equipmentService.CanEquip(category, id))
                return false;

            if (!_dbService.Database.HorrorWeaponMasterTable.TryFindById(id, out var weaponMaster))
                return false;

            _pendingEquipType = category;
            _pendingEquipId = id;
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

            var weapon = EquippedWeaponMaster;
            if (weapon == null || weapon.AmmoItemId <= 0 || weapon.MagazineSize <= 0)
                return false;

            if (_equipmentService.GetMagazineCount(weapon.Id, weapon.MagazineSize) >= weapon.MagazineSize)
                return false;

            if (_inventoryService.GetCount(ObjectCategory.Item, weapon.AmmoItemId) <= 0)
                return false;

            return true;
        }

        /// <summary>
        /// 立てられたアイテム使用予約を消費し、マスタ解決・使用効果・所持数・残 HP を検証して
        /// UsingItemState へ遷移すべきかを判定する。Idle/Moving ステートの Update から呼ばれ、
        /// 実際の消費・回復適用は UsingItemState が行う。発火時点で HP 満タンなら消費せず破棄する
        /// （回復完了後に発火する再予約など、ダイアログ側の満タン無反応をすり抜けた予約への再検証）。
        /// </summary>
        /// <returns>UsingItemState へ遷移すべきなら true。</returns>
        private bool TryUseItem()
        {
            if (!_useItemRequested)
                return false;

            _useItemRequested = false;

            // マスタは id のみで引くため、Item 以外のカテゴリ要求を誤解決しないよう先に弾く
            if (_requestedUseCategory != ObjectCategory.Item)
                return false;

            if (!_dbService.Database.HorrorItemMasterTable.TryFindById(_requestedUseId, out var itemMaster))
                return false;

            if (!itemMaster.HasEffect)
                return false;

            if (_inventoryService.GetCount(_requestedUseCategory, _requestedUseId) <= 0)
                return false;

            // 満タン判定はサービスの共有述語で行う
            if (_playerService.IsHealthFull)
                return false;

            _pendingUseItemMaster = itemMaster;
            return true;
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

        /// <summary>
        /// 被弾後の無敵時間中かを判定する（Time.time 基準）。未被弾（lastDamageTime = 負の無限大）は
        /// 常に false。境界（経過 == invincibleSeconds）は無敵終了とみなし、invincibleSeconds 0 以下は無敵なし。
        /// </summary>
        internal static bool IsInvincible(float time, float lastDamageTime, float invincibleSeconds)
            => time - lastDamageTime < invincibleSeconds;

        /// <summary>
        /// 被弾後の残 HP を算出する（0 未満に落ちない）。
        /// </summary>
        internal static int CalculateDamagedHealth(int current, int damage)
            => Mathf.Max(0, current - damage);

        /// <summary>
        /// 回復後の残 HP を算出する（最大値を超えない）。
        /// </summary>
        internal static int CalculateHealedHealth(int current, int amount, int max)
            => Mathf.Min(max, current + amount);

        /// <summary>
        /// アイテム使用の経過時間に応じた適用済み回復総量を算出する。毎フレームの加算ではなく
        /// 経過比率からの再計算のため丸め誤差が蓄積せず、duration 経過時点で必ず effect 全量に到達する。
        /// duration が 0 以下ならゼロ除算を避けて即全量とみなす。
        /// </summary>
        internal static int CalculateAppliedHeal(int effect, float elapsed, float duration)
            => duration > 0f ? Mathf.RoundToInt(effect * Mathf.Clamp01(elapsed / duration)) : effect;

        /// <summary>
        /// セーブデータからロードした残 HP を正規化する。0 以下（旧セーブの既定値・新規データ・不正値）は
        /// 最大値へ、最大値超（マスター変更後の旧セーブ）は最大値へクランプする。
        /// </summary>
        internal static int NormalizeLoadedHealth(int saved, int max)
            => saved <= 0 ? max : Mathf.Min(saved, max);

        /// <summary>ステートマシンが動作中かつ現在ステートが指定型かを判定する（ステート種別チェックの単一イディオム）。</summary>
        private bool IsActiveState<TState>() where TState : State<HorrorPlayerController, StateEvent>
            => _stateMachine != null && _stateMachine.IsProcessing() && _stateMachine.IsCurrentState<TState>();

        private bool IsGrounded() => _characterController.isGrounded;
        private bool IsMoving() => _speed > 0f;
        private bool IsWalking() => _speed >= PlayerMaster.WalkSpeed && _speed < PlayerMaster.RunSpeed;
        private bool IsRunning() => _speed >= PlayerMaster.RunSpeed;

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
        /// 装備切替・アイテム使用（両手が塞がる）の硬直中は強制解除する（HOLD 継続なら硬直明けに自動で再エイムされる）。
        /// </summary>
        private void UpdateAimInput()
        {
            _isAiming = Player.Aim.IsPressed()
                        && EquippedWeaponMaster != null
                        && !IsActiveState<EquippingState>()
                        && !IsActiveState<UsingItemState>();
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
                _verticalVelocity += PlayerMaster.Gravity * Time.fixedDeltaTime;
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
            if (_isCrouching || !IsGrounded() || !Player.Move.enabled)
            {
                _footstepAccumulatedDistance = 0f;
                return;
            }

            var delta = transform.position - positionBeforeMove;
            delta.y = 0f;

            var (fired, next) = StepFootstep(_footstepAccumulatedDistance, delta.magnitude, PlayerMaster.FootstepStride);
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
            var loudness = CalculateFootstepLoudness(IsRunning(), PlayerMaster.FootstepWalkLoudness, PlayerMaster.FootstepRunLoudness);
            if (loudness > 0f)
                _messagePipeService?.Publish(new HorrorSignals.Noise.Occurred(transform.position, loudness, NoiseType.Footstep));

            if (!string.IsNullOrEmpty(PlayerMaster.FootstepSeAssetName))
                _audioService.PlaySoundEffectOneShotAsync(PlayerMaster.FootstepSeAssetName, destroyCancellationToken).Forget();
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
            var aimMultiplier = Mathf.Lerp(1f, PlayerMaster.AimRotationMultiplier, _aimBlend);

            // Yaw: Player 本体を Y 軸回転（感度H・反転を適用、入力は加速度スムージング後の値）
            var horizontalInput = _smoothedLookValue.x * _lookSensitivityX * _lookInvertX;
            transform.Rotate(0f, horizontalInput * PlayerMaster.LookRotationSpeed * aimMultiplier, 0f, Space.Self);

            // Pitch: カメラの X 軸 localEulerAngles を更新、クランプ（既定 -y、感度V・反転を適用）
            var verticalInput = -_smoothedLookValue.y * _lookSensitivityY * _lookInvertY;
            _cameraVerticalAngle = Mathf.Clamp(_cameraVerticalAngle + verticalInput * PlayerMaster.LookRotationSpeed * aimMultiplier, -89f, 89f);

            // 発砲リコイルの減衰（全ステートの Update から毎フレーム呼ばれるためここで駆動する）
            _recoilWeight = Mathf.MoveTowards(_recoilWeight, 0f, Time.deltaTime / Mathf.Max(_recoilRecoverSeconds, 0.0001f));

            _mainCamera.transform.localEulerAngles = new Vector3(GetDisplayPitch(), 0f, 0f);
        }

        /// <summary>
        /// カメラ揺れを適用。移動中は figure-8 ヘッドボブ、停止中はアイドルスウェイ（呼吸揺れ）をクロスフェードする。
        /// 全体強度は CameraShake でスケール。UpdateRotation・UpdateCrouchPose の後に呼ばれ、表示用 pitch（リコイル込み）を維持しつつ roll を合成する。
        /// </summary>
        private void UpdateHeadBob()
        {
            if (_mainCamera == null) return;

            // 入力ブロック中（ポーズ等）は neutral に戻す（Time.deltaTime=0 凍結による残オフセット防止）
            if (!Player.Move.enabled)
            {
                _mainCamera.transform.localPosition = _cameraBasePosition;
                _mainCamera.transform.localEulerAngles = new Vector3(GetDisplayPitch(), 0f, 0f);
                _moveHeadBobWeight = 0f;
                return;
            }

            // 接地して移動中のみヘッドボブ。停止でアイドルスウェイへクロスフェード。
            // ケイデンスは _speed 直結にせず歩き/走りで固定（走りは少しだけ速い）。
            var active = IsGrounded() && IsMoving();
            var running = IsRunning();

            var ease = 1f - Mathf.Exp(-PlayerMaster.HeadBobAmplitudeResponse * Time.deltaTime);
            _moveHeadBobWeight = Mathf.Lerp(_moveHeadBobWeight, active ? 1f : 0f, ease);

            if (active)
                _headBobPhase += (running ? PlayerMaster.HeadBobRunSpeed : PlayerMaster.HeadBobWalkSpeed) * Time.deltaTime;
            _idlePhase += PlayerMaster.IdleSwaySpeed * Time.deltaTime; // アイドルは常時進む

            // ヘッドボブ（移動）：縦は位相、横はストライド（半周期）＝figure-8。横揺れの知覚はロールが主成分。
            var moveAmplitude = (running ? PlayerMaster.HeadBobRunAmplitude : PlayerMaster.HeadBobWalkAmplitude) * _moveHeadBobWeight;
            var moveRoll = (running ? PlayerMaster.HeadBobRunRoll : PlayerMaster.HeadBobWalkRoll) * _moveHeadBobWeight;
            var headBobX = Mathf.Sin(_headBobPhase * 0.5f) * moveAmplitude * PlayerMaster.HeadBobHorizontalRatio;
            var headBobY = Mathf.Sin(_headBobPhase) * moveAmplitude;
            var headBobRoll = Mathf.Sin(_headBobPhase * 0.5f) * moveRoll;

            // アイドルスウェイ（停止）：別周波数の遅い sin を重ねて有機的に
            var idleWeight = 1f - _moveHeadBobWeight;
            var idleX = Mathf.Sin(_idlePhase * 1.3f) * PlayerMaster.IdleSwayAmplitude * PlayerMaster.HeadBobHorizontalRatio * idleWeight;
            var idleY = Mathf.Sin(_idlePhase) * PlayerMaster.IdleSwayAmplitude * idleWeight;
            var idleRoll = Mathf.Sin(_idlePhase * 0.7f) * PlayerMaster.IdleSwayRoll * idleWeight;

            // 合算 → 全体強度 CameraShake × エイム減衰（エイム中は _aimShakeWeight が 0 へ減衰）
            var offset = new Vector3(headBobX + idleX, headBobY + idleY, 0f) * _cameraShake * _aimShakeWeight;
            var roll = (headBobRoll + idleRoll) * _cameraShake * _aimShakeWeight;

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
            var ease = 1f - Mathf.Exp(-PlayerMaster.CrouchTransitionSpeed * Time.deltaTime);
            _crouchBlend = Mathf.Lerp(_crouchBlend, target, ease);

            var height = Mathf.Lerp(_standHeight, PlayerMaster.CrouchHeight, _crouchBlend);

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
        /// エイム姿勢を毎フレーム補間する。FOV・揺れ減衰・武器構え位置・レティクル・残弾/HP HUD を _aimBlend / _aimShakeWeight から導出する。
        /// 各ステートの Update から呼ばれる（インタラクト拘束中も解除補間・FOV 復帰が必要なため
        /// InteractingState を含む全ステートが呼ぶ）。装備切替中は TickSwitch の後に呼ぶこと（下げ量更新 → 位置反映の順序）。
        /// </summary>
        private void UpdateAimPose()
        {
            if (_mainCamera == null || _weaponView == null) return;

            // 目標 0/1 へ指数補間（フレームレート非依存）
            var target = _isAiming ? 1f : 0f;
            var ease = 1f - Mathf.Exp(-PlayerMaster.AimTransitionSpeed * Time.deltaTime);
            _aimBlend = Mathf.Lerp(_aimBlend, target, ease);

            ApplyFov();

            // カメラ揺れの重みを線形に減衰/復帰（AimShakeFadeSeconds でゼロ/1 に到達）
            _aimShakeWeight = Mathf.MoveTowards(_aimShakeWeight, _isAiming ? 0f : 1f, Time.deltaTime / PlayerMaster.AimShakeFadeSeconds);

            _weaponView.UpdatePose(_aimBlend);

            if (_reticleView != null)
                _reticleView.UpdatePose(_isAiming);

            // エイム中・リロード中は両 HUD の表示を維持する（keepVisible の唯一の計算点）。
            // アイテム使用（回復）中は HP 側のみ維持し、残弾 HUD は巻き込まない
            var keepVisible = _isAiming || IsActiveState<ReloadingState>();
            var usingItem = IsActiveState<UsingItemState>();
            UpdateAmmoView(keepVisible);
            UpdateHealthView(keepVisible || usingItem);
        }

        /// <summary>
        /// 残弾 HUD を毎フレーム駆動する。表示内容（弾倉/予備・所持数のみ・非表示）と最新値をプル型で渡し、
        /// 値の変更検出と表示演出は View 側が担う。
        /// </summary>
        private void UpdateAmmoView(bool keepVisible)
        {
            if (_ammoView == null) return;

            var weapon = EquippedWeaponMaster;
            var mode = HorrorAmmoView.ResolveViewMode(weapon != null, weapon?.AmmoItemId ?? 0);

            var magazine = 0;
            var magazineSize = 0;
            var reserve = 0;
            switch (mode)
            {
                case HorrorAmmoViewMode.MagazineAndReserve:
                    magazineSize = weapon.MagazineSize;
                    magazine = _equipmentService.GetMagazineCount(weapon.Id, magazineSize);
                    reserve = _inventoryService.GetCount(ObjectCategory.Item, weapon.AmmoItemId);
                    break;
                case HorrorAmmoViewMode.CountOnly:
                    magazine = _inventoryService.GetCount(ObjectCategory.Weapon, weapon.Id); // 武器アイテム自体の所持数（例: Smoke）
                    break;
            }

            _ammoView.UpdatePose(mode, keepVisible, magazine, magazineSize, reserve);
        }

        /// <summary>
        /// HP HUD を毎フレーム駆動する。フェード演出は View 側が担う。
        /// 値の反映は Initialize / TakeDamage のプッシュ駆動（UpdateHealth）で行い、ここでは表示状態のみ更新する。
        /// </summary>
        private void UpdateHealthView(bool keepVisible)
        {
            if (_healthView == null) return;
            _healthView.UpdatePose(keepVisible);
        }

        /// <summary>
        /// 武器アクション（発砲・空撃ち・リロード）起点の HUD 表示キック。残弾と HP を常に対でキックする
        /// （被弾時の HP 単独キックは TakeDamage 側で行う意図的な非対称）。
        /// </summary>
        private void NotifyHudViews()
        {
            if (_ammoView != null) _ammoView.Notify();
            if (_healthView != null) _healthView.Notify();
        }

        /// <summary>
        /// カメラ FOV を基準 FOV とエイムズームの合成で適用する（唯一の FOV 書き込み点）。
        /// オプションのランタイム再適用がエイム中のズームを上書きしないよう、常に同一式で導出する。
        /// </summary>
        private void ApplyFov()
        {
            if (_mainCamera == null) return;
            var zoomRatio = EquippedWeaponMaster?.AimZoomRatio ?? 1f;
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
            var weapon = EquippedWeaponMaster;
            if (_mainCamera == null || weapon == null) return;

            var origin = _mainCamera.transform.position;
            var direction = _mainCamera.transform.forward;

            // 非エイム（腰だめ）射撃はわずかにランダム拡散する（エイム中はカメラ中心へ正確に飛ぶ）
            if (!_isAiming)
            {
                direction = CalculateShotDirection(direction, Random.insideUnitSphere, weapon.SpreadAngle);
            }

            IDamageable target = null;
            var impactPosition = origin + direction * weapon.Range;

            if (Physics.Raycast(origin, direction, out var hit, weapon.Range, _hitMask, QueryTriggerInteraction.Ignore))
            {
                target = hit.collider.GetComponentInParent<IDamageable>();
                impactPosition = hit.point;
            }

            var damage = CalculateAimedDamage(weapon.Damage, _isAiming, weapon.AimDamageMultiplier);

            // 弾倉消費（AmmoItemId=0 の武器は弾薬概念なし・無限）
            if (weapon.AmmoItemId > 0)
            {
                var magazine = _equipmentService.GetMagazineCount(weapon.Id, weapon.MagazineSize);
                _equipmentService.SetMagazineCount(weapon.Id, magazine - 1);
                NotifyHudViews();
            }

            // 命中対象があればダメージを与える。
            // ポップアップはダメージが実際に適用された時のみ。致死打で TakeDamage 後は IsDead=true になるため事前判定
            var damageApplied = target != null && !target.IsDead;
            target?.TakeDamage(damage);
            if (damageApplied) _messagePipeService?.Publish(new HorrorSignals.Combat.Damaged(hit.point, damage));

            // 騒音: 着弾音（着弾点・誘引用）→ 発砲音（射手位置）の順で発行する。この順序は変更不可:
            // 敵の注意対象位置は同フレームでは後着優先のため、両方聞こえた敵の注意対象は発砲音（射手位置）で確定する
            if (weapon.ImpactNoiseLoudness > 0f)
                _messagePipeService?.Publish(new HorrorSignals.Noise.Occurred(impactPosition, weapon.ImpactNoiseLoudness, NoiseType.Object));
            if (weapon.NoiseLoudness > 0f)
                _messagePipeService?.Publish(new HorrorSignals.Noise.Occurred(origin, weapon.NoiseLoudness, NoiseType.Gunshot));

            if (_reticleView != null) _reticleView.NotifyFired();

            // 発砲演出：武器ビュー（マズルフラッシュ＋キック）・カメラリコイル・射撃音
            if (_weaponView != null) _weaponView.NotifyFired();
            _recoilPitchAmount = weapon.RecoilCameraPitch;
            _recoilRecoverSeconds = weapon.RecoilRecoverSeconds;
            _recoilWeight = 1f;

            if (!string.IsNullOrEmpty(weapon.FireSeAssetName))
                _audioService.PlaySoundEffectOneShotAsync(weapon.FireSeAssetName, destroyCancellationToken).Forget();

            Debug.Log($"Weapon Fire: name->{weapon.Name} , damage->{damage}");
        }


        /// <summary>次弾までの発射間隔（AttackingState 滞在秒）。武器未設定なら 0。</summary>
        private float GetFireInterval() => EquippedWeaponMaster?.FireInterval ?? 0f;

        /// <summary>
        /// 装填を適用する。完了時点の弾倉・予備から装填数を再計算し、予備の消費に成功した場合のみ弾倉へ反映する
        /// （予備だけ減る・弾倉だけ増える不整合を防ぐ順序）。
        /// </summary>
        private void ApplyReload()
        {
            var weapon = EquippedWeaponMaster;
            if (weapon == null || weapon.AmmoItemId <= 0) return;

            var magazineSize = weapon.MagazineSize;
            var magazine = _equipmentService.GetMagazineCount(weapon.Id, magazineSize);
            var reserve = _inventoryService.GetCount(ObjectCategory.Item, weapon.AmmoItemId);
            var amount = CalculateReloadAmount(magazine, magazineSize, reserve);

            if (amount <= 0) return;
            if (!_inventoryService.TryConsume(ObjectCategory.Item, weapon.AmmoItemId, amount)) return;

            _equipmentService.SetMagazineCount(weapon.Id, magazine + amount);
            NotifyHudViews();
        }

        #endregion
    }
}
