using Cysharp.Net.Http;
using Grpc.Net.Client;
using MagicOnion.Client;
using MagicOnion.Unity;
using Game.Library.Shared.Realtime.Services;
using UnityEngine;

namespace Game.Shared.Realtime
{
    // Source Generator: IL2CPP 向けにクライアントプロキシを事前生成
    // Game.Library.Shared アセンブリ内の全 MagicOnion サービス/Hub を自動スキャン
    [MagicOnionClientGeneration(typeof(IMatchmakingService))]
    partial class MagicOnionGeneratedClientInitializer { }

    public static class MagicOnionInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize()
        {
            // GrpcChannelx: Unity ライフサイクル統合チャンネル管理
            GrpcChannelProviderHost.Initialize(new DefaultGrpcChannelProvider(
                () => new GrpcChannelOptions
                {
                    HttpHandler = new YetAnotherHttpHandler { Http2Only = true },
                    DisposeHttpClient = true,
                }));

            Debug.Log("[MagicOnionInitializer] Initialized GrpcChannelProviderHost");
        }
    }
}
