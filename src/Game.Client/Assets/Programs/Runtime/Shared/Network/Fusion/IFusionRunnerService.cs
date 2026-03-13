using System;
using Fusion;
using VContainer;

namespace Game.Shared.Network.Fusion
{
    public interface IFusionRunnerService
    {
        NetworkRunner Runner { get; }
        IObjectResolver Resolver { get; }
        bool IsActive { get; }
        bool IsServer { get; }
        bool IsClient { get; }
        GameMode GameMode { get; }

        PlayerRef LocalPlayer { get; }
        bool TryGetLocalPlayerComponent<T>(out T component) where T : NetworkBehaviour;
        bool TryGetPlayerComponent<T>(PlayerRef player, out T component) where T : NetworkBehaviour;

        /// <summary>SimulationBehaviour をレジストリに登録する。Spawned() で呼ぶ。</summary>
        void Register<T>(T behaviour) where T : SimulationBehaviour;
        /// <summary>SimulationBehaviour をレジストリから解除する。Despawned() で呼ぶ。</summary>
        void Unregister<T>(T behaviour) where T : SimulationBehaviour;
        /// <summary>レジストリから型で O(1) 取得する。</summary>
        bool TryGet<T>(out T behaviour) where T : SimulationBehaviour;

        void Initialize(NetworkRunner runner, IObjectResolver resolver);
        void Clear();

        event Action OnClientDisconnected;
    }
}
