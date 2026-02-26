using Game.Shared.Bootstrap;
using Game.Shared.Realtime;
using UnityEngine;

namespace Game.App.Bootstrap
{
    public static class GameRuntimeInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
#if UNITY_SERVER
            // Dedicated Server では ServerBootstrap が初期化を担当
            // クライアント専用の UI/シーンロード/gRPC クライアント初期化をスキップ
            return;
#endif

            // 1. モジュール初期化（SubsystemRegistration で登録されたコールバック）
            RuntimeInitializerRegistry.ExecuteAll();

            // 2. シリアライゼーション基盤（MessagePack Resolver 統合）
            MessagePackInitializer.Initialize();

            // 3. gRPC 通信基盤（GrpcChannelProviderHost）
            MagicOnionInitializer.Initialize();

            // 4. アプリケーションブートストラップ
            GameBootstrap.Startup();
        }
    }
}
