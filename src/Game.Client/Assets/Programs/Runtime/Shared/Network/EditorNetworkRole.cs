#if UNITY_EDITOR
using System.Linq;

namespace Game.Shared.Network
{
    /// <summary>
    /// MPPM タグからネットワークロールを判定する。
    /// MPPM 非アクティブ時はタグが空 → Host (SP デフォルト) にフォールスルー。
    ///
    /// Play Mode Scenarios での設定例:
    ///   Main Editor: Tag = "Host"   → StartHost
    ///   Player 2:    Tag = "Client" → StartClient (localhost:7777)
    /// </summary>
    public static class EditorNetworkRole
    {
        public enum Mode { Host, Client, Server }

        public static Mode Resolve()
        {
            // var tags = global::Unity.Multiplayer.PlayMode.CurrentPlayer.ReadOnlyTags();
            var tags = global::Unity.Multiplayer.PlayMode.CurrentPlayer.Tags;

            if (tags.Contains("Server"))
                return Mode.Server;
            if (tags.Contains("Client"))
                return Mode.Client;

            // "Host" タグ or タグなし → Host (SP デフォルト)
            return Mode.Host;
        }
    }
}
#endif
