using System;
using System.Collections.Generic;
using Fusion;
using Fusion.Addons.FSM;
using Fusion.Addons.KCC;
using Game.Shared.Network.Fusion;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// Fusion 2 プレイヤー NetworkBehaviour。
    /// 別 NetworkObject として Spawn され、[Networked] プロパティでプレイヤー状態を自動同期。
    /// InputAuthority 側は IFusionRunnerService に Register し、SurvivorPlayerController がポーリングでバインド。
    /// </summary>
    public class SurvivorFusionPlayer : NetworkBehaviour, IStateMachineOwner
    {
        [Inject] private IFusionRunnerService _runnerService;
        [Inject] private IPublisher<SurvivorSignals.Player.LeveledUp> _playerLeveledUpPub;

        // --- Networked State (Server/Host → Client 自動同期) ---
        [Networked] public int Health { get; set; }
        [Networked] public int MaxHealth { get; set; }
        [Networked] public int Stamina { get; set; }
        [Networked] public int MaxStamina { get; set; }
        [Networked] public float Speed { get; set; }
        [Networked] public NetworkBool IsInvincible { get; set; }
        [Networked] public float StaminaAccumulator { get; set; }
        [Networked] public float InvincibilityTimer { get; set; }
        [Networked] public float LookYaw { get; set; }

        private ChangeDetector _changeDetector;
        private SurvivorNetworkWeaponUpgradeOption[] _lastSentWeaponOptions;
        private KCC _kcc;
        private SurvivorFusionGameState _gameState;

        /// <summary>入力収集デリゲート（InputAuthority 側の Controller が設定）</summary>
        public Func<SurvivorPlayerNetworkInput> InputGatherer { get; set; }

        /// <summary>移動処理委譲先（Controller がバインド時に設定）</summary>
        public ISurvivorPlayerMovementHandler MovementHandler { get; set; }

        /// <summary>クライアント側で状態変更を検知するイベント</summary>
        public event Action<SurvivorFusionPlayer> OnStateChanged;

        /// <summary>
        /// Fusion FSM 初期化（Awake）。
        /// StateMachineController.DynamicWordCount が Spawned() より前に呼ばれるため、
        /// FSM オブジェクトを Awake で作成して CollectStateMachines 時に WordCount を返せるようにする。
        /// </summary>
        private void Awake()
        {
            if (_normalState == null) TryGetComponent(out _normalState);
            if (_invincibleState == null) TryGetComponent(out _invincibleState);
            if (_deadState == null) TryGetComponent(out _deadState);

            if (_normalState != null && _invincibleState != null && _deadState != null)
            {
                _playerFsm = new StateMachine<StateBehaviour>("PlayerState", _normalState, _invincibleState, _deadState);
                _normalState.Initialize(this, _invincibleState, _deadState);
                _invincibleState.Initialize(this, _normalState, _deadState);
                _deadState.Initialize(this);
            }
            else
            {
                Debug.LogWarning("[SurvivorFusionPlayer] Fusion FSM states not found on GameObject");
            }
        }

        // --- Fusion FSM ---
        [SerializeField] private SurvivorPlayerNormalState _normalState;
        [SerializeField] private SurvivorPlayerInvincibleState _invincibleState;
        [SerializeField] private SurvivorPlayerDeadState _deadState;
        private StateMachine<StateBehaviour> _playerFsm;

        // --- ダメージ受付 ---
        private bool _hasPendingDamage;
        private int _pendingDamageAmount;

        /// <summary>ダメージ受付（ステートの OnFixedUpdate で消費される）</summary>
        public bool HasPendingDamage => _hasPendingDamage;

        /// <summary>無敵持続時間（マスターデータから SurvivorPlayerController が設定）</summary>
        public float InvincibilityDuration { get; set; }

        // マスターデータから SurvivorPlayerController.Initialize で設定
        public int StaminaDepleteRate { get; set; }
        public int StaminaRegenRate { get; set; }
        public float JogSpeed { get; set; }
        public float RunSpeed { get; set; }

        public void RequestDamage(int damage)
        {
            _hasPendingDamage = true;
            _pendingDamageAmount += damage;
        }

        /// <summary>
        /// サーバー側: ダメージ適用後に RPC 経由で全クライアントに通知。
        /// SurvivorFusionGameState.NotifyPlayerDamaged → MessagePipe で UI 更新。
        /// </summary>
        public void NotifyDamaged(int damage)
        {
            if (!HasStateAuthority) return;
            if (_runnerService == null)
            {
                Debug.LogWarning($"[SurvivorFusionPlayer] NotifyDamaged: _runnerService is NULL");
                return;
            }
            if (TryGetGameState(out var gs))
            {
                gs.NotifyPlayerDamaged(damage, Health);
            }
            else
            {
                Debug.LogWarning($"[SurvivorFusionPlayer] NotifyDamaged: SurvivorFusionGameState not found");
            }
        }

        public int ConsumePendingDamage()
        {
            _hasPendingDamage = false;
            var amount = _pendingDamageAmount;
            _pendingDamageAmount = 0;
            return amount;
        }

        public void CollectStateMachines(List<IStateMachine> stateMachines)
        {
            if (_playerFsm != null)
                stateMachines.Add(_playerFsm);
        }

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            TryGetComponent(out _kcc);
            _runnerService?.TryGet(out _gameState);

            // Fusion FSM: Awake で作成済み → Spawned で初期ステート設定
            if (_playerFsm != null)
            {
                _playerFsm.ForceActivateState(_normalState.StateId);
            }

            // KCC の手動更新を有効化（入力設定→KCC更新→カメラ更新の順序を保証）
            if (_kcc != null)
            {
                _kcc.SetManualUpdate(true);
            }

            if (HasInputAuthority || HasStateAuthority)
            {
                _runnerService?.Register(this);
            }

            if (HasInputAuthority)
            {
                if (Runner.TryGetComponent<SurvivorFusionRunner>(out var fusionRunner))
                {
                    fusionRunner.InputProvider = () => InputGatherer?.Invoke() ?? default;
                }
            }

            if (TryGetComponent<ISurvivorPlayerMovementHandler>(out var handler))
            {
                handler.BindFusionPlayer(this);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _runnerService?.Unregister(this);

            if (HasInputAuthority && runner.TryGetComponent<SurvivorFusionRunner>(out var fusionRunner))
            {
                fusionRunner.InputProvider = null;
            }
        }

        private bool _inputReceived;
        private int _ticksSinceLastInput;
        private bool _wasPaused;

        /// <summary>
        /// 入力タイムアウト閾値（Tick数）。
        /// Fusion 2 のデフォルト TickRate 60Hz で 30tick = 約500ms。
        /// 1-2tickの一時的な入力途絶では前回入力を維持し、
        /// 長期途絶（切断等）のみゼロ入力にリセットする。
        /// </summary>
        private const int InputTimeoutTicks = 30;

        /// <summary>
        /// [Networked] IsPaused の変化に応じて KCC.SetActive を切り替える。
        /// KCC.SetActive(false) で物理・コリジョン・移動入力が完全停止する（KCC 推奨 API）。
        /// </summary>
        private void SyncPauseState()
        {
            if (_gameState == null) return;

            bool isPaused = _gameState.IsPaused;
            if (isPaused != _wasPaused)
            {
                _wasPaused = isPaused;
                _kcc.SetActive(!isPaused);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority && !HasInputAuthority) return;

            // ポーズ状態の同期 → KCC.SetActive で物理・移動を完全停止/再開
            SyncPauseState();

            if (GetInput(out SurvivorPlayerNetworkInput input))
            {
                _inputReceived = true;
                _ticksSinceLastInput = 0;

                // 1. スタミナ計算（[Networked] を直接更新）
                if (HasStateAuthority)
                {
                    UpdateStamina(input, Runner.DeltaTime);
                }

                // 2. 速度計算
                var moveValue = input.Move;
                var isMoveInput = moveValue.magnitude > 0.1f;
                var wantToRun = input.IsSprinting && isMoveInput;
                var isRunning = wantToRun && Stamina > 0;
                if (HasStateAuthority)
                {
                    Speed = (isMoveInput ? 1f : 0f) * (isRunning ? RunSpeed : JogSpeed);
                }

                // 3. Fusion FSM が自動で OnFixedUpdate 実行（ダメージ/無敵/死亡）

                // 4. 移動（生存中のみ）
                if (Health > 0 && MovementHandler != null)
                {
                    MovementHandler.ProcessTick(input, Runner.DeltaTime);
                }
            }
            else
            {
                _ticksSinceLastInput++;

                // 1-2tickの一時的な入力途絶では KCC の既存入力を維持し、クライアント予測との乖離を防ぐ
                var isBeforeFirstInput = !_inputReceived;
                var isInputTimeout = _inputReceived && _ticksSinceLastInput > InputTimeoutTicks;

                if ((isBeforeFirstInput || isInputTimeout) && _kcc != null)
                {
                    _kcc.SetInputDirection(Vector3.zero);

                    if (isInputTimeout && HasStateAuthority)
                    {
                        Speed = 0f;
                    }
                }
            }

            // 入力設定完了後に KCC を手動更新
            if (_kcc != null)
            {
                _kcc.ManualFixedUpdate();

                // KCC の LookYaw をネットワーク同期（リモートクライアントの回転表示用）
                if (HasStateAuthority)
                {
                    LookYaw = _kcc.FixedData.LookYaw;
                }
            }
        }

        /// <summary>
        /// スタミナ計算。[Networked] Stamina / StaminaAccumulator を直接更新。
        /// </summary>
        private void UpdateStamina(SurvivorPlayerNetworkInput input, float deltaTime)
        {
            var isMoveInput = input.Move.magnitude > 0.1f;
            var isRunning = input.IsSprinting && isMoveInput && Stamina > 0;

            float accumulator = StaminaAccumulator;

            if (isRunning)
            {
                accumulator -= StaminaDepleteRate * deltaTime;
            }
            else
            {
                accumulator += StaminaRegenRate * deltaTime;
            }

            if (accumulator >= 1f)
            {
                var regenAmount = Mathf.FloorToInt(accumulator);
                accumulator -= regenAmount;
                Stamina = Mathf.Min(MaxStamina, Stamina + regenAmount);
            }
            else if (accumulator <= -1f)
            {
                var depleteAmount = Mathf.FloorToInt(-accumulator);
                accumulator += depleteAmount;
                Stamina = Mathf.Max(0, Stamina - depleteAmount);
            }

            StaminaAccumulator = accumulator;
        }

        public override void Render()
        {
            // InputAuthority: Render 時の入力予測（ManualRenderUpdate の前に入力を設定）
            // KCC.SetActive(false) 時は ManualRenderUpdate 内で自動的にスキップされる
            if (HasInputAuthority && MovementHandler != null && _kcc != null)
            {
                MovementHandler.ProcessRenderInput(_kcc);
            }

            // KCC のレンダー更新（補間/予測シミュレーション）
            if (_kcc != null)
            {
                _kcc.ManualRenderUpdate();
            }

            if (_changeDetector == null) return;

            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(Health):
                    case nameof(Stamina):
                    case nameof(Speed):
                    case nameof(IsInvincible):
                        OnStateChanged?.Invoke(this);
                        break;
                }
            }
        }

        // =====================================================================
        //  Client→Server RPC（InputAuthority のみ送信可能）
        // =====================================================================

        private bool TryGetGameState(out SurvivorFusionGameState gs)
        {
            return _runnerService.TryGet(out gs);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientSceneReady()
        {
            if (TryGetGameState(out var gs))
                gs.OnClientSceneReady(Object.InputAuthority);
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientRequestPause()
        {
            if (TryGetGameState(out var gs))
                gs.OnClientRequestPause();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientRequestResume()
        {
            if (TryGetGameState(out var gs))
                gs.OnClientRequestResume();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientWeaponChoice(int weaponId, NetworkBool isNewWeapon)
        {
            if (!ValidateAndClearWeaponChoice(weaponId))
            {
                Debug.LogWarning($"[SurvivorFusionPlayer] Rejected invalid weapon choice: {weaponId}");
                return;
            }
            if (TryGetGameState(out var gs))
            {
                gs.OnClientWeaponChoice(weaponId, isNewWeapon);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientWeaponReplace(int removeWeaponId, int newWeaponId)
        {
            if (TryGetGameState(out var gs))
            {
                gs.OnClientWeaponReplace(removeWeaponId, newWeaponId);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientHitReported(int enemyNetworkId, int weaponId)
        {
            if (TryGetGameState(out var gs))
            {
                gs.OnClientHitReported(enemyNetworkId, weaponId);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientItemCollected(int itemId)
        {
            if (TryGetGameState(out var gs))
            {
                gs.OnClientItemCollected(itemId);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientPlayerDied()
        {
            if (TryGetGameState(out var gs))
            {
                gs.NotifyPlayerDied();
                gs.OnPlayerDied("");
            }
        }

        // =====================================================================
        //  Server→Client: レベルアップ通知
        // =====================================================================

        /// <summary>
        /// サーバー側: 武器選択肢をキャッシュし、対象クライアントに RPC で通知する。
        /// </summary>
        public void NotifyPlayerLevelUp(int level, int experience,
            int experienceToNextLevel, SurvivorNetworkWeaponUpgradeOption[] options)
        {
            _lastSentWeaponOptions = options;
            RpcNotifyPlayerLevelUp(level, experience, experienceToNextLevel, options);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
        public void RpcNotifyPlayerLevelUp(int level, int experience,
            int experienceToNextLevel, SurvivorNetworkWeaponUpgradeOption[] options)
        {
            _playerLeveledUpPub?.Publish(
                new SurvivorSignals.Player.LeveledUp(
                    "", level, experience, experienceToNextLevel, options));
        }

        /// <summary>
        /// サーバー側: クライアントからの武器選択が送信済み選択肢に含まれるか検証する。
        /// 検証成功時にキャッシュをクリアする。
        /// </summary>
        public bool ValidateAndClearWeaponChoice(int weaponId)
        {
            if (_lastSentWeaponOptions == null) return true;
            foreach (var opt in _lastSentWeaponOptions)
            {
                if (opt.WeaponId == weaponId)
                {
                    _lastSentWeaponOptions = null;
                    return true;
                }
            }
            return false;
        }

    }
}
