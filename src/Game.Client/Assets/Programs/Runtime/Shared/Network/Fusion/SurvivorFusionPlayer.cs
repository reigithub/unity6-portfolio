using System;
using Fusion;
using Game.Shared.Network.Survivor;
using Game.Shared.Signals.Survivor;
using MessagePipe;
using UnityEngine;
using VContainer;

namespace Game.Shared.Network.Fusion
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
        [Networked] public Vector3 NetworkPosition { get; set; }
        [Networked] public float NetworkRotationY { get; set; }

        private ChangeDetector _changeDetector;
        private SurvivorNetworkWeaponUpgradeOption[] _lastSentWeaponOptions;

        /// <summary>入力収集デリゲート（InputAuthority 側の Controller が設定）</summary>
        public Func<PlayerNetworkInput> InputGatherer { get; set; }

        /// <summary>クライアント側で状態変更を検知するイベント</summary>
        public event Action<SurvivorFusionPlayer> OnStateChanged;

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            DontDestroyOnLoad(gameObject);

            if (HasInputAuthority)
            {
                _runnerService?.Register(this);

                if (Runner.TryGetComponent<SurvivorFusionRunner>(out var fusionRunner))
                {
                    fusionRunner.InputProvider = () => InputGatherer?.Invoke() ?? default;
                }
            }

            Debug.Log($"[SurvivorFusionPlayer] Spawned (InputAuth={HasInputAuthority}, StateAuth={HasStateAuthority}, Injected={_playerLeveledUpPub != null})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            _runnerService?.Unregister(this);

            if (HasInputAuthority && runner.TryGetComponent<SurvivorFusionRunner>(out var fusionRunner))
            {
                fusionRunner.InputProvider = null;
            }
        }

        public override void Render()
        {
            if (_changeDetector == null) return;

            foreach (var change in _changeDetector.DetectChanges(this))
            {
                switch (change)
                {
                    case nameof(Health):
                    case nameof(Stamina):
                    case nameof(Speed):
                    case nameof(IsInvincible):
                    case nameof(NetworkPosition):
                    case nameof(NetworkRotationY):
                        OnStateChanged?.Invoke(this);
                        break;
                }
            }
        }

        /// <summary>
        /// Controller から呼ばれる: プレイヤー状態を [Networked] プロパティに書き込む。
        /// StateAuthority（Server/Host）側でのみ有効。
        /// </summary>
        public void PushState(Vector3 position, float rotationY, float speed,
            int health, int maxHealth, int stamina, int maxStamina, bool isInvincible)
        {
            if (!HasStateAuthority) return;

            NetworkPosition = position;
            NetworkRotationY = rotationY;
            Speed = speed;
            Health = health;
            MaxHealth = maxHealth;
            Stamina = stamina;
            MaxStamina = maxStamina;
            IsInvincible = isInvincible;
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
