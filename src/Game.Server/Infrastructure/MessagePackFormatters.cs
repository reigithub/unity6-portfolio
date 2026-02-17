using MessagePack;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Game.Server.Infrastructure;

public class MessagePackInputFormatter : InputFormatter
{
    private const string MediaType = "application/x-msgpack";

    public MessagePackInputFormatter()
    {
        SupportedMediaTypes.Add(MediaType);
    }

    protected override bool CanReadType(Type type) => true;

    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        using var ms = new MemoryStream();
        await context.HttpContext.Request.Body.CopyToAsync(ms);
        var bytes = ms.ToArray();

        if (bytes.Length == 0)
        {
            return await InputFormatterResult.NoValueAsync();
        }

        var result = MessagePackSerializer.Deserialize(context.ModelType, bytes);
        return await InputFormatterResult.SuccessAsync(result);
    }
}

public class MessagePackOutputFormatter : OutputFormatter
{
    private const string MediaType = "application/x-msgpack";

    public MessagePackOutputFormatter()
    {
        SupportedMediaTypes.Add(MediaType);
    }

    protected override bool CanWriteType(Type? type) => type != null;

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context)
    {
        if (context.Object == null)
        {
            return;
        }

        var bytes = MessagePackSerializer.Serialize(context.ObjectType!, context.Object);
        context.HttpContext.Response.ContentType = MediaType;
        await context.HttpContext.Response.Body.WriteAsync(bytes);
    }
}
