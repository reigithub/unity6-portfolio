using Game.App.Bootstrap;
using Game.MVP.Core.DI;
using Game.Shared.Bootstrap;
using Game.Shared.Network.Survivor;
using Game.Shared.Playmode;
using Game.Shared.Realtime;
using Game.Shared.Unity.Server;
using UnityEngine;
using VContainer;
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

                // 3. サーバーインフラ初期化（ServerHttpListener 起動、自己登録、コマンドライン引数解析）
                var sessionConnector = scope.Container.Resolve<ISurvivorNetworkSessionConnector>();
                UnityServerBootstrap.Initialize(sessionConnector);

                // Fusion Server セッション開始は SurvivorServerGameLoop が
                // ServerHttpListener からの /session/start リクエストを受信後に行う。
                // ここでは即時起動しない。
                Debug.Log("[GameRuntimeInitializer] Server initialized, waiting for session request via HTTP");

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
