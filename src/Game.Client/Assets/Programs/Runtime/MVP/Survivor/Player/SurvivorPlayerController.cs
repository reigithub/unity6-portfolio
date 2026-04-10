using Cysharp.Threading.Tasks;
using Fusion.Addons.KCC;
using Game.Client.MasterData;
using Game.MVP.Core.DI;
using Game.MVP.Survivor.Item;
using Game.Shared.Item;
using Game.Shared;
using Game.Shared.Combat;
using Game.Shared.Constants;
using Game.Shared.Extensions;
using Game.Shared.Network.Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Services;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using R3;
using Unity.Profiling;
using UnityEngine;
using VContainer;

namespace Game.MVP.Survivor.Player
{
    /// <summary>
    /// Survivorプレイヤーコントローラー
    /// KCC（Advanced Kinematic Character Controller）ベースの移動制御。
    /// ゲームロジック（HP/スタミナ/ステート）は SurvivorFusionPlayer + Fusion FSM が管理。
    /// このクラスは移動実行、入力蓄積、アイテム吸引、UI データ提供（ReactiveProperty）を担当。
    /// </summary>
    public partial class SurvivorPlayerController : MonoBehaviour, IDamageable, ISurvivorPlayerMovementHandler
    {
        // Profiler markers
        private static readonly ProfilerMarker s_attractItemsMarker = new("ProfilerMarker.Player.AttractItems");

        // VContainer Injection
        [Inject] private IPublisher<SurvivorSignals.Player.Spawned> _spawnedPublisher;
        [Inject] private IFusionRunnerService _runnerService;

        [Header("振り向き速度 (degrees/sec)")]
        [SerializeField]
        private float _rotationSpeed = 600f;

        [SerializeField] private GameObject _visual;

        [Inject] private readonly IGameRootController _gameRootController;
        [Inject] private readonly IInputService _inputService;

        // Components
        private KCC _kcc;

        // マスターデータから設定される値（移動/アイテム関連のみ。HP/スタミナは FusionPlayer 管理）
        private float _jogSpeed = 5.0f;
        private float _runSpeed = 8.0f;
        private float _pickupRange = 2f;
        private float _itemAttractDistance = 5f;
        private float _itemAttractSpeed = 10f;
        private float _itemCollectDistance = 1f;

        // 入力関連
        private Transform _mainCamera;
        private Vector3 _moveVector;
        private Quaternion _lookRotation = Quaternion.identity;

        // Reactive Properties（[Networked] からのミラー。UI が購読）
        private readonly ReactiveProperty<float> _speed = new();
        private readonly ReactiveProperty<int> _currentHp = new();
        private readonly ReactiveProperty<int> _currentStamina = new();
        private readonly ReactiveProperty<bool> _isInvincible = new();

        public ReadOnlyReactiveProperty<int> CurrentHp => _currentHp;
        public ReadOnlyReactiveProperty<int> CurrentStamina => _currentStamina;
        public ReadOnlyReactiveProperty<bool> IsInvincible => _isInvincible;
        public int MaxHp => _fusionPlayer != null ? _fusionPlayer.MaxHealth : 0;
        public int MaxStamina => _fusionPlayer != null ? _fusionPlayer.MaxStamina : 0;
        public float PickupRange => _pickupRange;
        public float ItemAttractDistance => _itemAttractDistance;
        public float ItemAttractSpeed => _itemAttractSpeed;
        public float ItemCollectDistance => _itemCollectDistance;
        public ReadOnlyReactiveProperty<float> Speed => _speed;

        // IDamageable
        public bool IsDead => _currentHp.Value <= 0;

        // アイテム吸引用
        private readonly Collider[] _itemHitBuffer = new Collider[50];
        private const float ItemCheckInterval = 0.1f;
        private float _itemCheckTimer;

        // ネットワーク同期用
        private SurvivorFusionPlayer _fusionPlayer;
        public SurvivorFusionPlayer FusionPlayer => _fusionPlayer;
        private SurvivorFusionGameState _gameState;

        // 入力蓄積（ExpertMovement パターン: フレームレート差による入力の不均一を補正）
        private Vector2 _accumulatedMoveDirection;
        private float _accumulatedMoveDirectionSize;
        private float _lastCameraRotationY;

        #region MonoBehaviour Methods

        private void Awake()
        {
            TryGetComponent(out _kcc);
        }

        private void Update()
        {
            AccumulateRenderInput();
            UpdateItemAttraction();
        }

        private void OnDestroy()
        {
            if (_fusionPlayer != null)
            {
                _fusionPlayer.OnStateChanged -= SyncFromNetworkedState;
                _fusionPlayer.InputGatherer = null;
                _fusionPlayer.MovementHandler = null;
                _fusionPlayer = null;
            }

            _speed.Dispose();
            _currentHp.Dispose();
            _currentStamina.Dispose();
            _isInvincible.Dispose();
        }

        #endregion

        #region Network

        /// <summary>
        /// Fusion 2: SurvivorFusionPlayer をバインド
        /// </summary>
        public void BindFusionPlayer(SurvivorFusionPlayer fusionPlayer)
        {
            _fusionPlayer = fusionPlayer;

            // 移動ハンドラを設定
            fusionPlayer.MovementHandler = this;

            // [Networked] → ReactiveProperty ミラーリング
            fusionPlayer.OnStateChanged += SyncFromNetworkedState;

            // InputAuthority プレイヤー: Fusion OnInput 用の入力収集デリゲートを設定
            // 蓄積された移動入力を時間加重平均で返す（ExpertMovement パターン）
            if (fusionPlayer.HasInputAuthority)
            {
                fusionPlayer.InputGatherer = () =>
                {
                    var input = new SurvivorPlayerNetworkInput
                    {
                        Move = _accumulatedMoveDirectionSize > 0f
                            ? _accumulatedMoveDirection / _accumulatedMoveDirectionSize
                            : Vector2.zero,
                        IsSprinting = _inputService.Player.LeftShift.IsPressed(),
                        CameraRotationY = _lastCameraRotationY
                    };

                    // 蓄積リセット
                    _accumulatedMoveDirection = Vector2.zero;
                    _accumulatedMoveDirectionSize = 0f;

                    return input;
                };
            }
        }

        #endregion

        #region Initialize

        private void ApplyMovementParams(SurvivorPlayerLevelMaster levelMaster)
        {
            _jogSpeed = levelMaster.MoveSpeed.ToUnit();
            _runSpeed = levelMaster.RunSpeed.ToUnit();
            _pickupRange = levelMaster.PickupRange.ToUnit();
            _itemAttractDistance = levelMaster.ItemAttractDistance.ToUnit();
            _itemAttractSpeed = levelMaster.ItemAttractSpeed.ToUnit();
            _itemCollectDistance = levelMaster.ItemCollectDistance.ToUnit();
        }

        /// <summary>
        /// マスターデータから初期化（互換性維持用オーバーロード）
        /// </summary>
        public void Initialize(SurvivorPlayerLevelMaster levelMaster)
            => Initialize(levelMaster, null);

        /// <summary>
        /// マスターデータから初期化。スポーン位置が指定された場合は KCC を設定する
        /// </summary>
        /// <param name="levelMaster">プレイヤーレベルマスターデータ</param>
        /// <param name="spawnPosition">スポーン位置（null の場合は KCC 設定をスキップ）</param>
        public void Initialize(SurvivorPlayerLevelMaster levelMaster, Vector3? spawnPosition)
        {
            if (spawnPosition.HasValue)
                ConfigureKCC(spawnPosition.Value);

            _runnerService.TryGet(out _gameState);

            ApplyMovementParams(levelMaster);

            // ゲームロジック関連（FusionPlayer が [Networked] で管理）
            if (_fusionPlayer != null)
            {
                _fusionPlayer.Health = levelMaster.MaxHp;
                _fusionPlayer.MaxHealth = levelMaster.MaxHp;
                _fusionPlayer.Stamina = levelMaster.MaxStamina;
                _fusionPlayer.MaxStamina = levelMaster.MaxStamina;
                _fusionPlayer.StaminaDepleteRate = levelMaster.StaminaDepleteRate;
                _fusionPlayer.StaminaRegenRate = levelMaster.StaminaRegenRate;
                _fusionPlayer.JogSpeed = _jogSpeed;
                _fusionPlayer.RunSpeed = _runSpeed;
                _fusionPlayer.InvincibilityDuration = levelMaster.InvincibilityDuration.ToSeconds();
                _fusionPlayer.IsInvincible = false;
                _fusionPlayer.StaminaAccumulator = 0f;
                _fusionPlayer.InvincibilityTimer = 0f;
            }
            else
            {
                Debug.LogWarning("[SurvivorPlayerController] Initialize: _fusionPlayer is NULL, [Networked] values not set");
            }

            // ReactiveProperty 初期値（UI 用ミラー）
            _currentHp.Value = levelMaster.MaxHp;
            _currentStamina.Value = levelMaster.MaxStamina;
            _isInvincible.Value = false;

            // メインカメラを自動取得（サーバーでは _gameRootController が null）
            if (_mainCamera == null)
            {
                _mainCamera = _gameRootController?.MainCamera?.transform;
            }
        }

        /// <summary>
        /// レベルアップ時にステータスを更新
        /// </summary>
        public void UpdateLevelStats(SurvivorPlayerLevelMaster levelMaster)
        {
            ApplyMovementParams(levelMaster);

            // FusionPlayer のレートとステータスを更新
            if (_fusionPlayer != null)
            {
                var previousMaxHp = _fusionPlayer.MaxHealth;
                var previousMaxStamina = _fusionPlayer.MaxStamina;

                _fusionPlayer.StaminaDepleteRate = levelMaster.StaminaDepleteRate;
                _fusionPlayer.StaminaRegenRate = levelMaster.StaminaRegenRate;
                _fusionPlayer.JogSpeed = _jogSpeed;
                _fusionPlayer.RunSpeed = _runSpeed;
                _fusionPlayer.InvincibilityDuration = levelMaster.InvincibilityDuration.ToSeconds();
                _fusionPlayer.MaxHealth = levelMaster.MaxHp;
                _fusionPlayer.MaxStamina = levelMaster.MaxStamina;

                // レベルアップ時のHP増加（差分を回復）
                if (levelMaster.MaxHp > previousMaxHp)
                {
                    var hpIncrease = levelMaster.MaxHp - previousMaxHp;
                    _fusionPlayer.Health = Mathf.Min(_fusionPlayer.Health + hpIncrease, levelMaster.MaxHp);
                }

                // レベルアップ時のスタミナ増加（差分を回復）
                if (levelMaster.MaxStamina > previousMaxStamina)
                {
                    var staminaIncrease = levelMaster.MaxStamina - previousMaxStamina;
                    _fusionPlayer.Stamina = Mathf.Min(_fusionPlayer.Stamina + staminaIncrease, levelMaster.MaxStamina);
                }
            }
        }

        /// <summary>
        /// カメラ参照を設定（カメラ相対移動用）
        /// </summary>
        public void SetMainCamera(Transform mainCamera)
        {
            _mainCamera = mainCamera;
        }

        /// <summary>
        /// KCC のスポーン位置と設定を適用する。
        /// Awake で初期化済みだが念のため null チェックを行う。
        /// </summary>
        /// <param name="spawnPosition">スポーン位置</param>
        private void ConfigureKCC(Vector3 spawnPosition)
        {
            if (_kcc == null) TryGetComponent(out _kcc);
            if (_kcc == null) return;

            _kcc.SetPosition(spawnPosition);
            _kcc.Settings.CollisionLayerMask = Physics.DefaultRaycastLayers & ~LayerMaskConstants.Enemy;
            _kcc.Settings.InputAuthorityBehavior = EKCCAuthorityBehavior.PredictFixed_InterpolateRender;
            _kcc.Settings.StateAuthorityBehavior = EKCCAuthorityBehavior.PredictFixed_InterpolateRender;
            _kcc.Settings.AntiJitterDistance = new Vector2(0.025f, 0.01f);
            _kcc.Settings.PredictionCorrectionSpeed = 15f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[SurvivorPlayerController] KCC configured: pos={spawnPosition}, scene={gameObject.scene.name}");
#endif
        }

        /// <summary>
        /// Visual の非同期初期化。Presenter に初期化を委譲し、完了後に Visual を有効化する。
        /// サーバー側では Visual を有効化せず、Spawned シグナルのみ発行する。
        /// </summary>
        /// <param name="playerMaster">プレイヤーマスターデータ（アセット名取得用）</param>
        /// <param name="resolver">VContainer リゾルバー（Presenter への Inject 用）</param>
        public async UniTask InitializeVisualAsync(SurvivorPlayerMaster playerMaster, IObjectResolver resolver)
        {
            if (!UnityPlaymodeHelper.IsServer() && _visual != null)
            {
                if (_visual.TryGetComponent<SurvivorPlayerPresenter>(out var presenter))
                    await presenter.InitializeAsync(playerMaster.AssetName, resolver, this);

                // DI 注入 + Animator 設定完了後に Visual を有効化（OnEnable で購読開始）
                _visual.SetActive(true);
            }

            // カメラフォロー用シグナル発行（KCC が RenderData で滑らかに補間するためルート transform）
            _spawnedPublisher?.Publish(new SurvivorSignals.Player.Spawned(transform));
        }

        #endregion

        #region Input / ProcessTick

        /// <summary>
        /// ISurvivorPlayerMovementHandler: Fusion tick ごとに FusionPlayer から呼ばれる。
        /// 移動実行とアイテム収集のみ。HP/スタミナ/ステートは FusionPlayer + Fusion FSM が管理。
        /// </summary>
        public void ProcessTick(SurvivorPlayerNetworkInput input, float deltaTime)
        {
            ExecuteMovement(input, deltaTime);
            CollectItemsFromKCCHits();
        }

        public bool IsMoving()
        {
            return _fusionPlayer != null && _fusionPlayer.Speed > 0f;
        }

        public bool IsGrounded()
        {
            return _kcc != null && _kcc.FixedData.IsGrounded;
        }

        #endregion

        #region Item Attraction

        /// <summary>
        /// 範囲内のアイテムを検知して吸引を開始する
        /// </summary>
        private void UpdateItemAttraction()
        {
            using (s_attractItemsMarker.Auto())
            {
                if (_fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;
                if (_gameState != null && _gameState.IsEffectivelyPaused) return;
                _itemCheckTimer -= _runnerService.GetDeltaTime();
                if (_itemCheckTimer > 0f) return;
                _itemCheckTimer = ItemCheckInterval;

                // ItemレイヤーのみをPhysicsSceneで検索
                var physicsScene = _runnerService.GetPhysicsSceneOrDefault();
                int hitCount = physicsScene.OverlapSphere(
                    transform.position,
                    _itemAttractDistance,
                    _itemHitBuffer,
                    LayerMaskConstants.Item,
                    QueryTriggerInteraction.Collide
                );

                for (int i = 0; i < hitCount; i++)
                {
                    if (_itemHitBuffer[i].TryGetComponent<ICollectible>(out var collectible) && !collectible.IsCollected)
                    {
                        var itemPos = _itemHitBuffer[i].transform.position;
                        var distance = Vector3.Distance(transform.position, itemPos);

                        if (distance <= _itemCollectDistance)
                        {
                            // 収集距離以内 → 即座に収集
                            collectible.Collect();
                        }
                        else
                        {
                            // 吸引開始（収集は KCCData.Hits または次回の距離チェックで行う）
                            collectible.StartAttraction(transform, _itemAttractSpeed);
                        }
                    }
                }
            }
        }

        #endregion

        #region Input Accumulation

        /// <summary>
        /// 毎 Update で呼び出し、移動入力を時間加重で蓄積する。
        /// Fusion の OnInput で蓄積値の平均を返すことで、フレームレート差による入力の不均一を補正。
        /// </summary>
        private void AccumulateRenderInput()
        {
            if (_fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;

            var move = _inputService.Player.Move.ReadValue<Vector2>();
            var dt = Time.unscaledDeltaTime;

            _accumulatedMoveDirection += move * dt;
            _accumulatedMoveDirectionSize += dt;
            _lastCameraRotationY = _mainCamera != null ? _mainCamera.eulerAngles.y : 0f;
        }

        /// <summary>
        /// Render フレームの入力予測処理。現在の入力を KCC に設定してレンダー予測を可能にする。
        /// SurvivorFusionPlayer.Render() から ISurvivorPlayerMovementHandler 経由で呼ばれる。
        /// </summary>
        public void ProcessRenderInput(KCC kcc)
        {
            if (_fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;

            var move = _inputService.Player.Move.ReadValue<Vector2>();
            var isMoveInput = move.magnitude > 0.1f;
            var wantToRun = _inputService.Player.LeftShift.IsPressed() && isMoveInput;
            var isRunning = wantToRun && _fusionPlayer.Stamina > 0;
            var speed = (isMoveInput ? 1f : 0f) * (isRunning ? _runSpeed : _jogSpeed);

            var cameraRotY = _mainCamera != null ? _mainCamera.eulerAngles.y : 0f;
            var cameraRot = Quaternion.Euler(0f, cameraRotY, 0f);
            var moveVector = isMoveInput
                ? (cameraRot * Vector3.forward * move.y + cameraRot * Vector3.right * move.x).normalized
                : Vector3.zero;

            kcc.SetInputDirection(moveVector);
            kcc.SetSpeed(speed);

            if (isMoveInput)
            {
                var targetYaw = Quaternion.LookRotation(moveVector).eulerAngles.y;
                var deltaYaw = Mathf.DeltaAngle(kcc.RenderData.LookYaw, targetYaw);
                var maxDelta = _rotationSpeed * Time.deltaTime;
                var clampedDelta = Mathf.Clamp(deltaYaw, -maxDelta, maxDelta);
                kcc.AddLookRotation(0f, clampedDelta);
            }
        }

        #endregion

        #region Movement

        private void ExecuteMovement(SurvivorPlayerNetworkInput input, float deltaTime)
        {
            var moveValue = input.Move;
            var isMoveInput = moveValue.magnitude > 0.1f;

            if (isMoveInput)
            {
                var cameraRot = Quaternion.Euler(0f, input.CameraRotationY, 0f);
                var forward = cameraRot * Vector3.forward;
                var right = cameraRot * Vector3.right;
                _moveVector = (forward * moveValue.y + right * moveValue.x).normalized;
                _lookRotation = Quaternion.LookRotation(_moveVector);
            }
            else
            {
                _moveVector = Vector3.zero;
            }

            // KCC に移動方向と速度を設定（Speed は FusionPlayer が [Networked] で管理）
            _kcc.SetInputDirection(_moveVector);
            _kcc.SetSpeed(_fusionPlayer != null ? _fusionPlayer.Speed : 0f);

            // 回転: KCC.AddLookRotation で最大速度制限付き差分回転
            // KCC がティック間の補間/予測を内部処理する
            if (isMoveInput)
            {
                var targetYaw = _lookRotation.eulerAngles.y;
                var deltaYaw = Mathf.DeltaAngle(_kcc.FixedData.LookYaw, targetYaw);
                var maxDelta = _rotationSpeed * deltaTime;
                var clampedDelta = Mathf.Clamp(deltaYaw, -maxDelta, maxDelta);
                _kcc.AddLookRotation(0f, clampedDelta);
            }
        }

        #endregion

        #region Damage / Heal

        /// <summary>
        /// IDamageable: ダメージを FusionPlayer の Fusion FSM に委譲。
        /// NormalState.OnFixedUpdate で消費され、HP 減算→無敵/死亡遷移が行われる。
        /// </summary>
        public void TakeDamage(int damage)
        {
            _fusionPlayer?.RequestDamage(damage);
        }

        public void Heal(int amount)
        {
            if (_fusionPlayer != null)
            {
                _fusionPlayer.Health = Mathf.Min(_fusionPlayer.MaxHealth, _fusionPlayer.Health + amount);
            }
        }

        /// <summary>
        /// 現在HPを設定（モデルとの同期用）
        /// </summary>
        public void SetCurrentHp(int value)
        {
            if (_fusionPlayer != null)
            {
                _fusionPlayer.Health = Mathf.Clamp(value, 0, _fusionPlayer.MaxHealth);
            }
        }

        #endregion

        #region Networked State Mirror

        /// <summary>
        /// [Networked] → ReactiveProperty ミラーリング。
        /// SurvivorFusionPlayer.Render() の ChangeDetector から呼ばれる。
        /// UI が購読する ReactiveProperty を [Networked] 値で更新し、ダメージ/死亡シグナルを発行。
        /// </summary>
        /// <summary>
        /// [Networked] → ReactiveProperty ミラーリング。
        /// SurvivorFusionPlayer.Render() の ChangeDetector から呼ばれる。
        /// UI 用の ReactiveProperty を [Networked] 値で更新する。
        /// ダメージ/死亡シグナルは MessagePipe RPC 経由（NotifyPlayerDamaged）で発行されるため、
        /// ここでは発火しない（予測/再シミュレーションによる Health 変動で誤発火を防ぐ）。
        /// </summary>
        private void SyncFromNetworkedState(SurvivorFusionPlayer fp)
        {
            _currentHp.Value = fp.Health;
            _currentStamina.Value = fp.Stamina;
            _isInvincible.Value = fp.IsInvincible;
            _speed.Value = fp.Speed;
        }

        #endregion

        #region KCC Item Collection


        /// <summary>
        /// KCCData.Hits からアイテム収集。ProcessTick の ExecuteMovement 後に呼ぶ。
        /// KCC カプセル内のアイテムを直接収集する（OnTriggerEnter 非使用）。
        /// </summary>
        private void CollectItemsFromKCCHits()
        {
            if (_kcc == null || _fusionPlayer == null || !_fusionPlayer.HasInputAuthority) return;

            foreach (var hit in _kcc.FixedData.Hits.All)
            {
                if (hit.Collider != null
                    && hit.Collider.gameObject.layer == LayerConstants.Item
                    && hit.Collider.TryGetComponent<ICollectible>(out var collectible)
                    && !collectible.IsCollected)
                {
                    collectible.Collect();
                }
            }
        }

        #endregion
    }
}
