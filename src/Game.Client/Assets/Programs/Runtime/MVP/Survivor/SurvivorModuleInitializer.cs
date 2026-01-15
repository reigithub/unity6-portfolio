using Game.MVP.Core.DI;
using UnityEngine;

namespace Game.MVP.Survivor
{
    /// <summary>
    /// Survivorモジュールの初期化
    /// アセンブリロード時にLifetimeScopeを登録
    /// </summary>
    public static class SurvivorModuleInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            // MVPGameLauncherにLifetimeScopeの型を登録
            SurvivorGameLauncher.RegisterLifetimeScopeType<SurvivorLifetimeScope>();
            Debug.Log("[SurvivorModuleInitializer] Registered SurvivorLifetimeScope");
        }
    }
}