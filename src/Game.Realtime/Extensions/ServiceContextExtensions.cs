using Grpc.Core;
using MagicOnion.Server;
using Microsoft.AspNetCore.Http;

namespace Game.Realtime.Extensions;

public static class ServiceContextExtensions
{
    public static string GetUserId(this ServiceContext context)
    {
        return context.CallContext.GetHttpContext().User?.FindFirst("sub")?.Value ?? "";
    }
}
