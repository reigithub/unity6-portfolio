using System;
using Fusion;
using Fusion.Addons.Physics;
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
    public class SurvivorFusionPlayer : NetworkBehaviour
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

        private ChangeDetector _changeDetector;
        private SurvivorNetworkWeaponUpgradeOption[] _lastSentWeaponOptions;
        private int _renderLogCount;

        /// <summary>入力収集デリゲート（InputAuthority 側の Controller が設定）</summary>
        public Func<SurvivorPlayerNetworkInput> InputGatherer { get; set; }

        /// <summary>移動処理委譲先（Controller がバインド時に設定）</summary>
        public ISurvivorPlayerMovementHandler MovementHandler { get; set; }

        /// <summary>クライアント側で状態変更を検知するイベント</summary>
        public event Action<SurvivorFusionPlayer> OnStateChanged;

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            DontDestroyOnLoad(gameObject);

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

            Debug.Log($"[SurvivorFusionPlayer] Spawned (InputAuth={HasInputAuthority}, StateAuth={HasStateAuthority}, Injected={_playerLeveledUpPub != null})");
        }

        /// <summary>
        /// NetworkRigidbody3D の InterpolationTarget を設定する。
        /// </summary>
        public void SetInterpolationTarget(Transform target)
        {
            if (TryGetComponent<NetworkRigidbody3D>(out var nrb))
            {
                nrb.InterpolationTarget = target;
                _renderLogCount = 0;
                Debug.Log($"[SurvivorFusionPlayer] InterpolationTarget set to '{target.name}' (NRB3D found)");
            }
            else
            {
                Debug.LogWarning("[SurvivorFusionPlayer] SetInterpolationTarget: NRB3D not found!");
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

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority && !HasInputAuthority) return;

            if (GetInput(out SurvivorPlayerNetworkInput input) && MovementHandler != null)
            {
                var snapshot = MovementHandler.ProcessTick(input, Runner.DeltaTime);

                if (HasStateAuthority)
                {
                    Speed = snapshot.Speed;
                    Health = snapshot.Health;
                    MaxHealth = snapshot.MaxHealth;
                    Stamina = snapshot.Stamina;
                    MaxStamina = snapshot.MaxStamina;
                    IsInvincible = snapshot.IsInvincible;
                }
            }
        }

        public override void Render()
        {
            // 位置/回転の補間は NetworkRigidbody3D が自動処理

            if (_renderLogCount < 5)
            {
                _renderLogCount++;
                var nrb = GetComponent<NetworkRigidbody3D>();
                var interpTarget = nrb != null ? nrb.InterpolationTarget : null;
                var interpPos = interpTarget != null ? interpTarget.position.ToString() : "N/A";
                Debug.Log($"[FP.Render#{_renderLogCount}] auth=I:{HasInputAuthority}/S:{HasStateAuthority}, root={transform.position}, interp={interpPos}, interpName={interpTarget?.name ?? "null"}, rb={GetComponent<Rigidbody>()?.position}");
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

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientSceneReady()
        {
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnClientSceneReady(Object.InputAuthority);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientRequestPause()
        {
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnClientRequestPause();
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientRequestResume()
        {
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnClientRequestResume();
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientWeaponChoice(int weaponId, NetworkBool isNewWeapon)
        {
            if (!ValidateAndClearWeaponChoice(weaponId))
            {
                Debug.LogWarning($"[SurvivorFusionPlayer] Rejected invalid weapon choice: {weaponId}");
                return;
            }
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnClientWeaponChoice(weaponId, isNewWeapon);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientWeaponReplace(int removeWeaponId, int newWeaponId)
        {
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnClientWeaponReplace(removeWeaponId, newWeaponId);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientHitReported(int enemyNetworkId, int weaponId)
        {
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
            {
                gs.OnClientHitReported(enemyNetworkId, weaponId);
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RpcClientPlayerDied()
        {
            if (_runnerService.TryGet<SurvivorFusionGameState>(out var gs))
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
