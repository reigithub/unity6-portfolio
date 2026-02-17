using Game.Client.MasterData;
using Game.Shared.Realtime;
using MessagePack;
using MessagePack.Resolvers;
using UnityEngine;

namespace Game.Shared.Bootstrap
{
    public static class MessagePackInitializer
    {
        public static void Initialize()
        {
            IFormatterResolver formatterResolver = CompositeResolver.Create(
                MagicOnionGeneratedClientInitializer.Resolver,
                MasterMemoryResolver.Instance,
                GeneratedMessagePackResolver.Instance,
                StandardResolver.Instance
            );
            MessagePackSerializer.DefaultOptions = MessagePackSerializerOptions.Standard.WithResolver(formatterResolver);

            Debug.Log("[MessagePackInitializer] Initialized MessagePack DefaultOptions with all resolvers");
        }
    }
}
