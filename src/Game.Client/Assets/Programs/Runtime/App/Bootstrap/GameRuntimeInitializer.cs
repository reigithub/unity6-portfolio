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

                // 3. サーバーインフラ初期化（ヘルスプローブ、コマンドライン引数解析）
                UnityServerBootstrap.Initialize();

                // 4. Fusion Server セッション開始
                var connector = scope.Container.Resolve<ISurvivorNetworkStageConnector>();
                _ = connector.StartServerAsync(stageId: 0);
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
