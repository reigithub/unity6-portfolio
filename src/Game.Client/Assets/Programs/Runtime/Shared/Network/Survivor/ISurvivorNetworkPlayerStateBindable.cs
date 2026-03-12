using System.Collections.Generic;
using Game.Shared.Network.Fusion;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// SurvivorFusionPlayer とバインド可能なコンポーネントのインターフェース。
    /// Game.Shared → Game.MVP.Survivor のアセンブリ境界を超えるために使用。
    /// </summary>
    public interface ISurvivorNetworkPlayerStateBindable
    {
        void BindFusionPlayer(SurvivorFusionPlayer fusionPlayer);
    }

    /// <summary>
    /// INetworkPlayerStateBindable の静的レジストリ。
    /// SurvivorPlayerController が Awake で登録、OnDestroy で解除。
    /// SurvivorFusionPlayer.FixedUpdateNetwork でレジストリからバインド対象を取得。
    /// </summary>
    public static class SurvivorNetworkPlayerStateBindableRegistry
    {
        private static readonly List<ISurvivorNetworkPlayerStateBindable> _bindables = new();

        public static IReadOnlyList<ISurvivorNetworkPlayerStateBindable> Bindables => _bindables;

        public static void Register(ISurvivorNetworkPlayerStateBindable bindable)
        {
            if (!_bindables.Contains(bindable))
            {
                _bindables.Add(bindable);
            }
        }

        public static void Unregister(ISurvivorNetworkPlayerStateBindable bindable)
        {
            _bindables.Remove(bindable);
        }
    }
}
