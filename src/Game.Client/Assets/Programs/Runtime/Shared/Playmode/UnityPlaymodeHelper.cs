using Game.Shared.Network.Fusion;

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
            return FusionRunnerService.Current?.IsDedicatedServer ?? false;
#endif
        }

        public static bool IsClient()
        {
#if UNITY_SERVER
            return false;
#elif UNITY_EDITOR
            return Multiplayer.MppmHelper.IsClient() || !Multiplayer.MppmHelper.IsActive();
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
            return FusionRunnerService.Current?.IsHostMode ?? false;
#endif
        }
    }
}
