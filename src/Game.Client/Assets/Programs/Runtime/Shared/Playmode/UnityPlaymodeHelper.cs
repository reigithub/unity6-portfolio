using Game.Shared.Network.Fusion;

namespace Game.Shared.Playmode
{
    public static class UnityPlaymodeHelper
    {
        private static IFusionRunnerService _runnerService;

        /// <summary>
        /// FusionRunnerService が初期化されたときに呼び出す。
        /// </summary>
        /// <param name="service">登録するサービスインスタンス。</param>
        internal static void SetRunnerService(IFusionRunnerService service) => _runnerService = service;

        /// <summary>
        /// FusionRunnerService がクリアされたときに呼び出す。
        /// </summary>
        /// <param name="service">解除するサービスインスタンス。</param>
        internal static void ClearRunnerService(IFusionRunnerService service)
        {
            if (_runnerService == service)
                _runnerService = null;
        }

        public static bool IsServer()
        {
#if UNITY_SERVER
            return true;
#elif UNITY_EDITOR
            return Multiplayer.MppmHelper.IsServer();
#else
            return _runnerService?.IsDedicatedServer ?? false;
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
            return _runnerService?.IsHostMode ?? false;
#endif
        }
    }
}
