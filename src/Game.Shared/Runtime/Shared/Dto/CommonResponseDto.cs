using MessagePack;

namespace Game.Library.Shared.Dto
{
    [MessagePackObject]
    public class MessageResponse
    {
        [Key(0)]
        public string Message { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class SuccessResponse
    {
        [Key(0)]
        public bool Success { get; set; }
    }

    [MessagePackObject]
    public class ApiErrorResponse
    {
        [Key(0)]
        public string Error { get; set; } = string.Empty;

        [Key(1)]
        public string Message { get; set; } = string.Empty;

        [Key(2)]
        public string TraceId { get; set; } = string.Empty;
    }

    [MessagePackObject]
    public class EmptyRequest { }
}
