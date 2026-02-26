using System.Collections.Generic;

namespace Game.Shared.Netcode.Survivor
{
    /// <summary>
    /// NetworkSurvivorPlayerState とバインド可能なコンポーネントのインターフェース。
    /// Game.Shared → Game.MVP.Survivor のアセンブリ境界を超えるために使用。
    /// </summary>
    public interface INetworkPlayerStateBindable
    {
        void BindNetworkPlayerState(NetworkSurvivorPlayerState playerState);
    }

    /// <summary>
    /// INetworkPlayerStateBindable の静的レジストリ。
    /// SurvivorPlayerController が Awake で登録、OnDestroy で解除。
    /// NetworkSurvivorPlayerState.OnNetworkSpawn でレジストリからバインド対象を取得。
    /// FindObjectsByType を回避し、タイミング問題も解決。
    /// </summary>
    public static class NetworkPlayerStateBindableRegistry
    {
        private static readonly List<INetworkPlayerStateBindable> _bindables = new();

        public static IReadOnlyList<INetworkPlayerStateBindable> Bindables => _bindables;

        public static void Register(INetworkPlayerStateBindable bindable)
        {
            if (!_bindables.Contains(bindable))
            {
                _bindables.Add(bindable);
            }
        }

        public static void Unregister(INetworkPlayerStateBindable bindable)
        {
            _bindables.Remove(bindable);
        }
    }
}
