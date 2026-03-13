using System;
using System.Collections.Generic;
using Fusion;
using VContainer;

namespace Game.Shared.Network.Fusion
{
    public class FusionRunnerService : IFusionRunnerService
    {
        /// <summary>
        /// static クラス（UnityPlaymodeHelper 等）からの限定的アクセス用。
        /// DI 可能なクラスでは必ず IFusionRunnerService を inject すること。
        /// </summary>
        internal static FusionRunnerService Current { get; private set; }

        public NetworkRunner Runner { get; private set; }
        public bool IsActive => Runner != null && Runner.IsRunning;
        public bool IsServer => IsActive && Runner.IsServer;
        public bool IsClient => IsActive && Runner.IsClient;
        public GameMode GameMode => Runner != null ? Runner.GameMode : default;
        public bool IsHostMode => IsActive && GameMode == GameMode.Host;
        public bool IsDedicatedServer => IsActive && GameMode == GameMode.Server;
        public PlayerRef LocalPlayer => Runner != null ? Runner.LocalPlayer : PlayerRef.None;
        public IObjectResolver Resolver { get; set; }

        public event Action OnClientDisconnected;

        private readonly Dictionary<Type, SimulationBehaviour> _registry = new();

        public void Initialize(NetworkRunner runner, IObjectResolver resolver)
        {
            Runner = runner;
            Resolver = resolver;
            Current = this;
        }

        public void Clear()
        {
            Runner = null;
            _registry.Clear();
            if (Current == this) Current = null;
        }

        public string GetDebugStatus()
        {
            if (IsActive)
            {
                return $"[Fusion] isServer={Runner.IsServer}, gameMode={GameMode}";
            }
            return "[Offline]";
        }

        public void RaiseClientDisconnected() => OnClientDisconnected?.Invoke();

        public bool TryGetLocalPlayerComponent<T>(out T component) where T : NetworkBehaviour
        {
            component = null;
            if (!IsActive) return false;
            var localPlayer = Runner.LocalPlayer;
            if (!localPlayer.IsRealPlayer) return false;
            return TryGetPlayerComponent(localPlayer, out component);
        }

        public bool TryGetPlayerComponent<T>(PlayerRef player, out T component) where T : NetworkBehaviour
        {
            component = null;
            if (!IsActive) return false;
            if (!Runner.TryGetPlayerObject(player, out var playerObject)) return false;
            if (!playerObject.TryGetComponent(out component)) return false;
            return component != null;
        }

        public void Register<T>(T behaviour) where T : SimulationBehaviour
        {
            _registry[typeof(T)] = behaviour;
        }

        public void Unregister<T>(T behaviour) where T : SimulationBehaviour
        {
            if (_registry.TryGetValue(typeof(T), out var existing) && existing == behaviour)
            {
                _registry.Remove(typeof(T));
            }
        }

        public bool TryGet<T>(out T behaviour) where T : SimulationBehaviour
        {
            if (_registry.TryGetValue(typeof(T), out var value))
            {
                behaviour = (T)value;
                return true;
            }
            behaviour = default;
            return false;
        }
    }
}
