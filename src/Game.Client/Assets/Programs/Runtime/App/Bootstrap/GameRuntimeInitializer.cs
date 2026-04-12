using Game.App.Bootstrap;
using Game.MVP.Core.DI;
using Game.Shared.Bootstrap;
using Game.Shared.Playmode;
using Game.Shared.Realtime;
using UnityEngine;
using VContainer.Unity;

namespace Game.App
{
    public class GameRuntimeInitializer : IInitializable
    {
        public void Initialize()
        {
            if (UnityPlaymodeHelper.IsServer())
            {
                // 1. モジュール初期化（SurvivorLifetimeScope 型登録）
                RuntimeInitializerRegistry.ExecuteAll();

                // 2. SurvivorLifetimeScope 生成（MessagePipe + サーバー DI + セッション）
                //    VContainer により Root の子スコープとして自動接続される
                var scope = SurvivorGameLauncher.CreateScope();

                // VContainer の RegisterEntryPoint<UnityServerBootstrap> が
                // IAsyncStartable.StartAsync を自動実行するため、直接呼び出しは不要。
                // Fusion Server セッション開始は SurvivorServerGameLoop が
                // ServerHttpListener からの /session/start リクエストを受信後に行う。
                Debug.Log("[GameRuntimeInitializer] Server scope created, UnityServerBootstrap will initialize via VContainer EntryPoint");

                _ = scope; // スコープ参照保持（GC 対策）
            }
            else
            {
                RuntimeInitializerRegistry.ExecuteAll();
                MessagePackInitializer.Initialize();
                MagicOnionInitializer.Initialize();
                GameBootstrap.Startup();
            }
        }
    }
}
