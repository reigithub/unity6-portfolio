using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject(true)]
    public class MessageResponse
    {
        public string Message { get; set; } = string.Empty;
    }

    [MessagePackObject(true)]
    public class SuccessResponse
    {
        public bool Success { get; set; }
    }

    [MessagePackObject(true)]
    public class ApiErrorResponse
    {
        public string Error { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string TraceId { get; set; } = string.Empty;
    }
}
