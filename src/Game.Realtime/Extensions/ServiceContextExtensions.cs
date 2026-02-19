using Game.Server.Shared.Extensions;
using Grpc.Core;
using MagicOnion.Server;

namespace Game.Realtime.Extensions;

public static class ServiceContextExtensions
{
    public static string GetUserId(this ServiceContext context)
    {
        return context.CallContext.GetHttpContext().User?.GetUserId() ?? "";
    }
}
