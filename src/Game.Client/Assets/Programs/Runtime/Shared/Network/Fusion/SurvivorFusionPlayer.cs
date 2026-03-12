using System;
using Fusion;
using Game.Shared.Network.Survivor;
using UnityEngine;

namespace Game.Shared.Network.Fusion
{
    /// <summary>
    /// Fusion 2 プレイヤー NetworkBehaviour。
    /// 旧 SurvivorNetworkPlayerState に相当する役割。
    /// 別 NetworkObject として Spawn され、SurvivorPlayerController とレジストリ経由でバインド。
    /// [Networked] プロパティでプレイヤー状態を自動同期。
    /// </summary>
    public class SurvivorFusionPlayer : NetworkBehaviour
    {
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
        private ISurvivorNetworkPlayerStateBindable _boundController;
        private bool _isBound;

        /// <summary>入力収集デリゲート（InputAuthority 側の Controller が設定）</summary>
        public Func<PlayerNetworkInput> InputGatherer { get; set; }

        /// <summary>クライアント側で状態変更を検知するイベント</summary>
        public event Action<SurvivorFusionPlayer> OnStateChanged;

        public override void Spawned()
        {
            _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
            DontDestroyOnLoad(gameObject);

            TryBindToController();

            if (HasInputAuthority && SurvivorFusionRunner.Instance != null)
            {
                SurvivorFusionRunner.Instance.InputProvider = () => InputGatherer?.Invoke() ?? default;
            }

            Debug.Log($"[SurvivorFusionPlayer] Spawned (InputAuth={HasInputAuthority}, StateAuth={HasStateAuthority})");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (HasInputAuthority && SurvivorFusionRunner.Instance != null)
            {
                SurvivorFusionRunner.Instance.InputProvider = null;
            }
            _boundController = null;
            _isBound = false;
        }

        public override void FixedUpdateNetwork()
        {
            // Controller はシーンロード後に生成されるため、ポーリングでバインドを試行
            if (!_isBound)
            {
                TryBindToController();
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

        /// <summary>Controller が破棄された時に呼ばれる。再バインド待ちに戻す。</summary>
        public void Unbind()
        {
            _boundController = null;
            _isBound = false;
            InputGatherer = null;
        }

        private void TryBindToController()
        {
            if (_isBound) return;

            var bindables = SurvivorNetworkPlayerStateBindableRegistry.Bindables;
            for (int i = 0; i < bindables.Count; i++)
            {
                bindables[i].BindFusionPlayer(this);
                _boundController = bindables[i];
                _isBound = true;
                Debug.Log($"[SurvivorFusionPlayer] Bound to {bindables[i].GetType().Name}");
                break;
            }
        }
    }
}
