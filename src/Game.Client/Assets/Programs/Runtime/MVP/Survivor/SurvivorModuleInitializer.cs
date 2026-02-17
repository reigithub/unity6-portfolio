using Game.MVP.Core.DI;
using Game.Shared.Bootstrap;
using UnityEngine;

namespace Game.MVP.Survivor
{
    /// <summary>
    /// Survivorモジュールの初期化
    /// SubsystemRegistration でレジストリに登録し、BeforeSceneLoad で実行される
    /// </summary>
    public static class SurvivorModuleInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            RuntimeInitializerRegistry.Register(0, Initialize);
        }

        private static void Initialize()
        {
            SurvivorGameLauncher.RegisterLifetimeScopeType<SurvivorLifetimeScope>();
            Debug.Log("[SurvivorModuleInitializer] Registered SurvivorLifetimeScope");
        }
    }
}