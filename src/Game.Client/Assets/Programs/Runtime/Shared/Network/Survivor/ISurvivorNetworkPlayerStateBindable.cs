using System.Collections.Generic;

namespace Game.Shared.Network.Survivor
{
    /// <summary>
    /// NetworkSurvivorPlayerState とバインド可能なコンポーネントのインターフェース。
    /// Game.Shared → Game.MVP.Survivor のアセンブリ境界を超えるために使用。
    /// </summary>
    public interface ISurvivorNetworkPlayerStateBindable
    {
        void BindNetworkPlayerState(SurvivorNetworkPlayerState playerState);
    }

    /// <summary>
    /// INetworkPlayerStateBindable の静的レジストリ。
    /// SurvivorPlayerController が Awake で登録、OnDestroy で解除。
    /// NetworkSurvivorPlayerState.OnNetworkSpawn でレジストリからバインド対象を取得。
    /// FindObjectsByType を回避し、タイミング問題も解決。
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
