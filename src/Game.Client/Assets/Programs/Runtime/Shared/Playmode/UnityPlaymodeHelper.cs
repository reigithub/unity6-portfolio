using Game.Shared.Network;

namespace Game.Shared.Playmode
{
    public static class UnityPlaymodeHelper
    {
        public static bool IsServer()
        {
#if UNITY_SERVER
            return true;
#elif UNITY_EDITOR
            return Multiplayer.MppmHelper.IsServer();
#else
            return NetworkModeHelper.IsHeadlessServer;
#endif
        }

        public static bool IsClient()
        {
#if UNITY_SERVER
            return false;
#elif UNITY_EDITOR
            return Multiplayer.MppmHelper.IsClient();
#else
            return true;
#endif
        }

        public static bool IsHost()
        {
#if UNITY_SERVER
            return false;
#elif UNITY_EDITOR
            return Multiplayer.MppmHelper.IsHost();
#else
            return NetworkModeHelper.IsNetworkHost;
#endif
        }
    }
}
